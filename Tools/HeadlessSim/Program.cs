using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using RTS.Sim;

namespace RTS.Headless
{
    /// <summary>
    /// Headless QA harness. Runs the engine-independent simulation without Unity:
    ///   skirmish   AI-vs-AI 1v1 on every map at every difficulty
    ///   teams      2v2 AI matches on every 4-player map
    ///   missions   smoke run of every campaign mission (+ intro), optionally with an autopilot AI for the player
    ///   save       save/load round trip at several points of a match
    ///   determinism  same seed twice must produce identical state
    ///   all        everything above
    /// Exit code is the number of failed checks.
    /// </summary>
    public static class Program
    {
        static int _failures;
        static readonly StringBuilder Report = new StringBuilder();
        static float _minutes = 30f;

        public static int Main(string[] args)
        {
            Catalog.EnsureBuilt();
            string mode = args.Length > 0 ? args[0] : "all";
            for (int i = 1; i < args.Length; i++)
                if (args[i] == "--minutes" && i + 1 < args.Length) _minutes = float.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
            string filter = args.FirstOrDefault(a => a.StartsWith("--only="))?.Substring(7);
            Line($"# Headless QA run ({mode})  units={Catalog.Units.Count} buildings={Catalog.Buildings.Count} weapons={Catalog.Weapons.Count}");
            var sw = Stopwatch.StartNew();
            try
            {
                if (mode == "skirmish" || mode == "all") RunSkirmish(filter);
                if (mode == "teams" || mode == "all") RunTeams(filter);
                if (mode == "missions" || mode == "all") RunMissions(filter, false);
                if (mode == "autopilot" || mode == "all") RunMissions(filter, true);
                if (mode == "save" || mode == "all") RunSaveLoad();
                if (mode == "determinism" || mode == "all") RunDeterminism();
                if (mode == "data" || mode == "all") RunDataChecks();
                if (mode == "maps" || mode == "all") RunMapChecks();
                if (mode == "balance") RunBalance();
                if (mode == "slots") RunSlotBias();
                if (mode == "units") UnitTable();
                if (mode == "probe") Probe(filter);
                if (mode == "profile") Profile(filter ?? "dust_basin", args);
                if (mode == "trace") Trace(filter ?? "twin_rivers", args);
            }
            catch (Exception ex) { Fail("harness crashed: " + ex); }
            Line($"\nTotal failures: {_failures}   wall time {sw.Elapsed.TotalSeconds:F0}s");
            var outPath = Environment.GetEnvironmentVariable("QA_REPORT");
            if (!string.IsNullOrEmpty(outPath)) File.AppendAllText(outPath, Report.ToString());
            return _failures;
        }

        static void Line(string s) { Console.WriteLine(s); Report.AppendLine(s); }
        static void Fail(string s) { _failures++; Line("  FAIL: " + s); }

        // ------------------------------------------------------------------ helpers
        class RunResult
        {
            public int Ticks; public string Error; public List<string> AIErrors = new List<string>();
            public double Ms; public int MaxUnits; public int Invariants;
        }

        /// <summary>Steps the world, clearing events like the client does, and checks invariants periodically.</summary>
        static RunResult Run(World w, int maxTicks, Func<World, bool> stop = null)
        {
            var r = new RunResult();
            var sw = Stopwatch.StartNew();
            try
            {
                for (int t = 0; t < maxTicks; t++)
                {
                    w.Step();
                    foreach (var e in w.Events)
                        if (e.Type == SimEventType.Hint && e.Text != null && e.Text.StartsWith("AI error") && r.AIErrors.Count < 5) r.AIErrors.Add(e.Text);
                    w.Events.Clear();
                    r.Ticks++;
                    r.MaxUnits = Math.Max(r.MaxUnits, w.Units.Count);
                    if (t % 150 == 0) r.Invariants += CheckInvariants(w);
                    if (stop != null && stop(w)) break;
                }
            }
            catch (Exception ex) { r.Error = ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace; }
            r.Ms = sw.Elapsed.TotalMilliseconds;
            return r;
        }

        static readonly HashSet<string> _reported = new HashSet<string>();
        static int Inv(string msg) { if (_reported.Add(msg)) Fail("invariant: " + msg); return 1; }

        static int CheckInvariants(World w)
        {
            int bad = 0;
            foreach (var u in w.Units)
            {
                if (float.IsNaN(u.Pos.X) || float.IsNaN(u.Pos.Y)) bad += Inv($"unit {u.DefId} has NaN position");
                else if (!u.IsInside && !w.Map.InBounds((int)u.Pos.X, (int)u.Pos.Y) && u.Def.Category != UnitCategory.Aircraft) bad += Inv($"unit {u.DefId} out of bounds at {u.Pos.X:F1},{u.Pos.Y:F1}");
                if (float.IsNaN(u.Hp) || u.Hp > u.MaxHp + 0.01f) bad += Inv($"unit {u.DefId} bad health {u.Hp}/{u.MaxHp}");
                if (!w.ById.ContainsKey(u.Id)) bad += Inv($"unit {u.DefId} missing from ById");
            }
            foreach (var b in w.Buildings)
            {
                if (b.Dead) continue;
                if (float.IsNaN(b.Hp) || b.Hp > b.MaxHp + 0.01f) bad += Inv($"building {b.DefId} bad health {b.Hp}/{b.MaxHp}");
                for (int y = 0; y < b.H; y++)
                    for (int x = 0; x < b.W; x++)
                    {
                        int cx = b.Origin.X + x, cy = b.Origin.Y + y;
                        if (!w.Map.InBounds(cx, cy)) { bad += Inv($"building {b.DefId} out of bounds"); continue; }
                        if (w.BuildingGrid[w.Map.Index(cx, cy)] != b.Id) bad += Inv($"building grid mismatch for {b.DefId}");
                    }
            }
            foreach (var p in w.Players)
            {
                if (float.IsNaN(p.Credits) || p.Credits < -0.5f) bad += Inv($"player {p.Name} credits {p.Credits}");
            }
            return bad;
        }

        static string Winner(World w)
        {
            if (!w.GameOver) return "none";
            var names = w.Players.Where(p => !p.IsNeutral && p.Team == w.WinningTeam).Select(p => p.Name + "(" + p.Faction + ")");
            return "team " + w.WinningTeam + " " + string.Join("+", names);
        }

        static string Stats(World w)
        {
            var sb = new StringBuilder();
            foreach (var p in w.Players)
            {
                if (p.IsNeutral) continue;
                var s = p.Stats;
                sb.Append($"\n      {p.Name,-14} {p.Faction,-9} built u{s.UnitsBuilt}/b{s.BuildingsBuilt} lost u{s.UnitsLost}/b{s.BuildingsLost} killed u{s.UnitsKilled}/b{s.BuildingsKilled} harvested {s.CreditsHarvested:F0} spent {s.CreditsSpent:F0} bldgs {w.BuildingsOf(p.Id).Count()} units {w.UnitsOf(p.Id).Count()}");
            }
            return sb.ToString();
        }

        static void Report_(string label, World w, RunResult r, bool expectEnd)
        {
            double simSec = r.Ticks / (double)World.TicksPerSecond;
            Line($"  {label}: {(r.Error != null ? "CRASH" : w.GameOver ? "ended" : "timeout")} at {simSec / 60:F1} min, winner {Winner(w)}, peak units {r.MaxUnits}, {r.Ms / Math.Max(1, r.Ticks):F2} ms/tick{Stats(w)}");
            if (r.Error != null) Fail(label + " crashed: " + r.Error);
            foreach (var e in r.AIErrors) Fail(label + " " + e);
            if (expectEnd && !w.GameOver && r.Error == null) Line("      note: no winner within time limit");
        }

        // ------------------------------------------------------------------ skirmish
        static void RunSkirmish(string filter)
        {
            Line("\n## Skirmish 1v1 AI vs AI");
            int ticks = (int)(_minutes * 60 * World.TicksPerSecond);
            var diffs = new[] { AIDifficulty.Easy, AIDifficulty.Normal, AIDifficulty.Hard, AIDifficulty.Brutal };
            int n = 0;
            foreach (var map in MapLibrary.Skirmish)
            {
                if (filter != null && !map.Id.Contains(filter)) continue;
                var d = diffs[n % diffs.Length];
                var d2 = diffs[(n + 1) % diffs.Length];
                n++;
                var c = new SkirmishConfig { MapId = map.Id, Spectator = true };
                c.Settings.Seed = 1000 + n;
                c.Slots.Add(new SlotConfig { Faction = FactionId.Alliance, Team = 0, Color = 0, Difficulty = d, Name = "Alliance " + d });
                c.Slots.Add(new SlotConfig { Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = d2, Name = "Dominion " + d2 });
                World w;
                try { w = GameSetup.CreateSkirmish(c); }
                catch (Exception ex) { Fail($"{map.Id}: setup crashed {ex}"); continue; }
                var r = Run(w, ticks, x => x.GameOver);
                Report_($"{map.Id} ({map.Width}x{map.Height}, {d} vs {d2})", w, r, true);
            }
            // mirror matches at equal difficulty on the first map: the stronger difficulty should win more often
            Line("\n## Difficulty ladder (twin_rivers, Alliance vs Dominion)");
            foreach (var pair in new[] { (AIDifficulty.Easy, AIDifficulty.Hard), (AIDifficulty.Hard, AIDifficulty.Easy), (AIDifficulty.Normal, AIDifficulty.Brutal), (AIDifficulty.Brutal, AIDifficulty.Normal) })
            {
                var c = new SkirmishConfig { MapId = "twin_rivers", Spectator = true };
                c.Settings.Seed = 77;
                c.Slots.Add(new SlotConfig { Faction = FactionId.Alliance, Team = 0, Color = 0, Difficulty = pair.Item1, Name = "A-" + pair.Item1 });
                c.Slots.Add(new SlotConfig { Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = pair.Item2, Name = "D-" + pair.Item2 });
                var w = GameSetup.CreateSkirmish(c);
                var r = Run(w, ticks, x => x.GameOver);
                Report_($"{pair.Item1} vs {pair.Item2}", w, r, true);
            }
        }

        static void RunTeams(string filter)
        {
            Line("\n## Skirmish 2v2 AI");
            int ticks = (int)(_minutes * 60 * World.TicksPerSecond);
            int n = 0;
            foreach (var map in MapLibrary.Skirmish)
            {
                if (map.MaxPlayers < 4) continue;
                if (filter != null && !map.Id.Contains(filter)) continue;
                n++;
                var c = new SkirmishConfig { MapId = map.Id, Spectator = true, TeamGame = true };
                c.Settings.Seed = 500 + n;
                c.Slots.Add(new SlotConfig { Faction = FactionId.Alliance, Team = 0, Color = 0, Difficulty = AIDifficulty.Hard, Name = "Alliance A" });
                c.Slots.Add(new SlotConfig { Faction = FactionId.Dominion, Team = 0, Color = 2, Difficulty = AIDifficulty.Normal, Name = "Dominion A" });
                c.Slots.Add(new SlotConfig { Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = AIDifficulty.Hard, Name = "Dominion B" });
                c.Slots.Add(new SlotConfig { Faction = FactionId.Alliance, Team = 1, Color = 3, Difficulty = AIDifficulty.Normal, Name = "Alliance B" });
                World w;
                try { w = GameSetup.CreateSkirmish(c); }
                catch (Exception ex) { Fail($"{map.Id} 2v2: setup crashed {ex}"); continue; }
                var r = Run(w, ticks, x => x.GameOver);
                Report_($"{map.Id} 2v2", w, r, true);
            }
            if (n == 0) Fail("no 4-player maps found");
        }

        // ------------------------------------------------------------------ missions
        static void RunMissions(string filter, bool autopilot)
        {
            Line(autopilot ? "\n## Campaign missions with autopilot AI for the player (12 min each)" : "\n## Campaign mission smoke runs (idle player, 8 min each)");
            var list = new List<MissionDef> { CampaignDatabase.Intro };
            list.AddRange(CampaignDatabase.Alliance);
            list.AddRange(CampaignDatabase.Dominion);
            int ticks = (int)((autopilot ? 12 : 8) * 60 * World.TicksPerSecond);
            foreach (var def in list)
            {
                if (filter != null && !def.Id.Contains(filter)) continue;
                if (autopilot && def.IsCinematic) continue;
                World w;
                try { w = GameSetup.CreateMission(def, AIDifficulty.Normal); }
                catch (Exception ex) { Fail($"{def.Id}: setup crashed {ex}"); continue; }
                if (w.Mission == null || w.Mission.Human == null) { Fail($"{def.Id}: no mission runner or human player"); continue; }
                if (w.Mission.Objectives.Count == 0 && !def.IsCinematic) Fail($"{def.Id}: no objectives");
                if (autopilot)
                {
                    w.Mission.Human.IsAI = true;
                    w.AIs.Add(new SkirmishAI(w, w.Mission.Human.Id));
                }
                var r = Run(w, ticks, x => x.Mission.Result != 0);
                string res = w.Mission.Result > 0 ? "VICTORY" : w.Mission.Result < 0 ? "defeat" : "running";
                var obj = string.Join(", ", w.Mission.Objectives.Select(o => $"{o.Id}:{o.State}"));
                Line($"  {def.Id,-6} {Trim(def.Title, 28),-28} {res,-8} {r.Ticks / 900f:F1} min  fired {w.Mission.Triggers.Count(t => t.Fired)}/{w.Mission.Triggers.Count}  [{obj}]  {r.Ms / Math.Max(1, r.Ticks):F2} ms/tick");
                if (!autopilot && def.IsCinematic && w.Mission.Result == 0) Line("      note: intro did not finish within the window");
                if (!autopilot && !def.IsCinematic && w.Mission.Result < 0 && r.Ticks < 2 * 60 * 15 && !def.Tags.Contains("timed"))
                    Line($"      note: idle player lost after {r.Ticks / 900f:F1} min ({w.Mission.ResultReason})");
                if (r.Error != null) Fail($"{def.Id} crashed: {r.Error}");
                foreach (var e in r.AIErrors) Fail($"{def.Id} {e}");
                // save/load mid mission
                if (!autopilot && r.Error == null) SaveRoundTrip(w, def.Id, 0, null, AIDifficulty.Normal);
            }
        }

        static string Trim(string s, int n) => s == null ? "" : s.Length <= n ? s : s.Substring(0, n - 1) + "…";

        // ------------------------------------------------------------------ save/load
        static string Fingerprint(World w)
        {
            var sb = new StringBuilder();
            sb.Append($"t{w.Tick} id{w.NextId} u{w.Units.Count} b{w.Buildings.Count} p{w.Projectiles.Count};");
            foreach (var p in w.Players) sb.Append($"{p.Id}:{p.Credits:F1}:{p.PowerProduced}/{p.PowerConsumed}:{p.Researched.Count};");
            foreach (var u in w.Units.OrderBy(u => u.Id)) sb.Append($"{u.Id}:{u.DefId}:{u.Pos.X:F2},{u.Pos.Y:F2}:{u.Hp:F1};");
            foreach (var b in w.Buildings.OrderBy(b => b.Id)) sb.Append($"{b.Id}:{b.DefId}:{b.Hp:F1};");
            int ore = 0; for (int i = 0; i < w.Map.Ore.Length; i++) ore += w.Map.Ore[i];
            sb.Append("ore" + ore);
            return sb.ToString();
        }

        static bool SaveRoundTrip(World w, string label, int localPlayer, SkirmishConfig sk, AIDifficulty diff)
        {
            try
            {
                var snap = SaveGame.Capture(w, localPlayer, sk, diff);
                var bytes = SaveGame.Serialize(snap);
                var snap2 = SaveGame.Deserialize(bytes);
                var w2 = SaveGame.Restore(snap2);
                string a = Fingerprint(w), b = Fingerprint(w2);
                if (a != b)
                {
                    int i = 0; while (i < a.Length && i < b.Length && a[i] == b[i]) i++;
                    Fail($"{label}: restored world differs at char {i}: '{Excerpt(a, i)}' vs '{Excerpt(b, i)}'");
                    return false;
                }
                // the restored world must keep running
                var r = Run(w2, 900);
                if (r.Error != null) { Fail($"{label}: restored world crashed: {r.Error}"); return false; }
                foreach (var e in r.AIErrors) Fail($"{label} (restored) {e}");
                Line($"      save/load ok ({bytes.Length / 1024} KB), restored world ran 60 s");
                return true;
            }
            catch (Exception ex) { Fail($"{label}: save/load threw {ex}"); return false; }
        }

        static string Excerpt(string s, int i) => s.Substring(Math.Max(0, i - 30), Math.Min(60, s.Length - Math.Max(0, i - 30)));

        static void RunSaveLoad()
        {
            Line("\n## Save/load round trips");
            foreach (var mapId in new[] { "twin_rivers", "crossroads" })
            {
                var c = mapId == "crossroads" ? SkirmishConfig.Default2v2() : SkirmishConfig.Default1v1();
                c.MapId = mapId; c.Settings.Seed = 4242;
                foreach (var s in c.Slots) if (!s.Human) s.Difficulty = AIDifficulty.Hard;
                var w = GameSetup.CreateSkirmish(c);
                int local = w.Players.First(p => p.IsHuman).Id;
                foreach (var minute in new[] { 0, 3, 10 })
                {
                    var r = Run(w, minute * 900 - w.Tick);
                    if (r.Error != null) { Fail($"{mapId}: crashed before save: {r.Error}"); break; }
                    Line($"  {mapId} at {minute} min:");
                    SaveRoundTrip(w, $"{mapId}@{minute}", local, c, AIDifficulty.Normal);
                }
            }
        }

        static void RunDeterminism()
        {
            Line("\n## Determinism (same seed twice)");
            string Play()
            {
                var c = new SkirmishConfig { MapId = "twin_rivers", Spectator = true };
                c.Settings.Seed = 99;
                c.Slots.Add(new SlotConfig { Faction = FactionId.Alliance, Team = 0, Color = 0, Difficulty = AIDifficulty.Hard, Name = "A" });
                c.Slots.Add(new SlotConfig { Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = AIDifficulty.Hard, Name = "B" });
                var w = GameSetup.CreateSkirmish(c);
                Run(w, 15 * 900);
                return Fingerprint(w);
            }
            var a = Play(); var b = Play();
            if (a == b) Line("  identical after 15 min");
            else Fail("two runs with the same seed diverged");
        }

        static void Trace(string mapId, string[] args)
        {
            var d1 = AIDifficulty.Hard; var d2 = AIDifficulty.Easy;
            foreach (var a in args) { if (a.StartsWith("--a=")) d1 = (AIDifficulty)Enum.Parse(typeof(AIDifficulty), a.Substring(4)); if (a.StartsWith("--b=")) d2 = (AIDifficulty)Enum.Parse(typeof(AIDifficulty), a.Substring(4)); }
            var c = new SkirmishConfig { MapId = mapId, Spectator = true };
            c.Settings.Seed = 77;
            foreach (var a in args) if (a.StartsWith("--seed=")) c.Settings.Seed = int.Parse(a.Substring(7));
            c.Slots.Add(new SlotConfig { Faction = FactionId.Alliance, Team = 0, Color = 0, Difficulty = d1, Name = "A-" + d1 });
            c.Slots.Add(new SlotConfig { Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = d2, Name = "D-" + d2 });
            var w = GameSetup.CreateSkirmish(c);
            for (int m = 1; m <= _minutes && !w.GameOver; m++)
            {
                var r = Run(w, 900);
                if (r.Error != null) { Fail(r.Error); return; }
                Line($"-- minute {m}");
                foreach (var ai in w.AIs.OfType<SkirmishAI>())
                {
                    var p = w.GetPlayer(ai.PlayerId);
                    var comp = w.UnitsOf(p.Id).GroupBy(u => u.DefId).Select(g => g.Key + "x" + g.Count());
                    Line($"   {p.Name}: {ai.DebugSummary()} | bldgs {w.BuildingsOf(p.Id).Count()} power {p.PowerProduced}/{p.PowerConsumed} | {string.Join(" ", comp)}");
                    if (args.Contains("--harv"))
                        foreach (var h in w.UnitsOf(p.Id).Where(u => u.Def.Has(UnitAbility.Harvester)))
                            Line($"      harvester {h.Id} {h.HState} pos {h.Pos.X:F0},{h.Pos.Y:F0} cargo {h.Cargo:F0} order {h.CurrentOrder.Type} moving {h.Moving} stuck {h.StuckTimer:F1} last {h.LastHarvestPos.X:F0},{h.LastHarvestPos.Y:F0}");
                }
            }
            Line(Stats(w));
        }

        static void RunBalance()
        {
            Line("\n## Faction balance (equal difficulty, sides swapped)");
            int ticks = (int)(_minutes * 60 * World.TicksPerSecond);
            var wins = new Dictionary<string, int> { ["Alliance"] = 0, ["Dominion"] = 0, ["none"] = 0 };
            foreach (var mapId in new[] { "twin_rivers", "dust_basin", "iron_strait", "frostbite_pass" })
                foreach (var diff in new[] { AIDifficulty.Normal, AIDifficulty.Hard })
                    for (int rep = 0; rep < 4; rep++)
                    {
                        int swap = rep % 2;
                        var c = new SkirmishConfig { MapId = mapId, Spectator = true };
                        c.Settings.Seed = 300 + rep * 17 + (int)diff * 7;
                        var fa = swap == 0 ? FactionId.Alliance : FactionId.Dominion; var fb = swap == 0 ? FactionId.Dominion : FactionId.Alliance;
                        c.Slots.Add(new SlotConfig { Faction = fa, Team = 0, Color = 0, Difficulty = diff, Name = fa.ToString() });
                        c.Slots.Add(new SlotConfig { Faction = fb, Team = 1, Color = 1, Difficulty = diff, Name = fb.ToString() });
                        var w = GameSetup.CreateSkirmish(c);
                        var r = Run(w, ticks, x => x.GameOver);
                        string win = w.GameOver ? w.Players.First(p => !p.IsNeutral && p.Team == w.WinningTeam).Faction.ToString() : "none";
                        wins[win]++;
                        Line($"  {mapId,-15} {diff,-6} {fa} first: winner {win} at {r.Ticks / 900f:F1} min{Stats(w)}");
                        if (r.Error != null) Fail(r.Error);
                    }
            Line($"  totals: Alliance {wins["Alliance"]}, Dominion {wins["Dominion"]}, no result {wins["none"]}");
        }

        static float Dps(WeaponDef wd, ArmorType a)
        {
            var wh = Catalog.Warhead(wd.Warhead);
            float cycle = wd.Clip > 0 ? (wd.Cooldown * wd.Clip + wd.ReloadTime) / wd.Clip : wd.Cooldown;
            return wd.Damage * wd.Burst * wh.Get(a) / Math.Max(0.05f, cycle);
        }

        static void UnitTable()
        {
            var armors = new[] { ArmorType.Infantry, ArmorType.Light, ArmorType.Medium, ArmorType.Heavy, ArmorType.Aircraft, ArmorType.Building };
            Line("id                 tier cost   hp   armor     spd  | dps inf light med heavy air bld | hp*dps(mix)/cost^2*1e3");
            foreach (var f in new[] { FactionId.Alliance, FactionId.Dominion })
            {
                foreach (var u in Catalog.Units.Values.Where(x => x.Faction == f).OrderBy(x => x.Category).ThenBy(x => x.Cost))
                {
                    var d = new float[armors.Length];
                    foreach (var wid in u.Weapons) { var wd = Catalog.Weapon(wid); if (wd == null) continue; for (int i = 0; i < armors.Length; i++) d[i] = Math.Max(d[i], Dps(wd, armors[i])); }
                    float mix = (d[0] + d[1] + d[2] + d[3]) / 4f;
                    float eff = u.Cost > 0 ? u.Health * mix / (u.Cost * (float)u.Cost) * 1000f : 0;
                    Line($"{u.Id,-18} {u.Tier,4} {u.Cost,5} {u.Health,5} {u.Armor,-9} {u.Speed,4:F1} | {string.Join(" ", d.Select(x => x.ToString("F0").PadLeft(4)))} | {eff:F2}");
                }
                Line("");
            }
        }

        static void RunSlotBias()
        {
            Line("\n## Start-slot bias (mirror factions)");
            int ticks = (int)(_minutes * 60 * World.TicksPerSecond);
            int s0 = 0, s1 = 0, none = 0;
            foreach (var mapId in new[] { "twin_rivers", "dust_basin", "iron_strait", "frostbite_pass" })
                foreach (var f in new[] { FactionId.Alliance, FactionId.Dominion })
                    for (int seed = 0; seed < 2; seed++)
                    {
                        var c = new SkirmishConfig { MapId = mapId, Spectator = true };
                        c.Settings.Seed = 900 + seed * 13;
                        c.Slots.Add(new SlotConfig { Faction = f, Team = 0, Color = 0, Difficulty = AIDifficulty.Hard, Name = "slot0" });
                        c.Slots.Add(new SlotConfig { Faction = f, Team = 1, Color = 1, Difficulty = AIDifficulty.Hard, Name = "slot1" });
                        var w = GameSetup.CreateSkirmish(c);
                        var r = Run(w, ticks, x => x.GameOver);
                        if (!w.GameOver) none++; else if (w.WinningTeam == 0) s0++; else s1++;
                        Line($"  {mapId,-15} {f,-9} seed {c.Settings.Seed}: winner {(w.GameOver ? "slot" + w.WinningTeam : "none")} at {r.Ticks / 900f:F1} min");
                    }
            Line($"  totals: slot0 {s0}, slot1 {s1}, none {none}");
        }

        static void Probe(string id)
        {
            var def = CampaignDatabase.Find(id);
            var w = GameSetup.CreateMission(def, AIDifficulty.Normal);
            Line($"{def.Id} map {w.Map.Width}x{w.Map.Height}");
            void Dump()
            {
                var tags = w.Units.Where(u => u.Tag != null).GroupBy(u => u.Tag).Select(g => $"{g.Key}:{g.Count()}u(owner {g.First().Owner}, {g.First().Pos.X:F0},{g.First().Pos.Y:F0}, hp {g.First().Hp:F0})");
                var btags = w.Buildings.Where(b => b.Tag != null).GroupBy(b => b.Tag).Select(g => $"{g.Key}:{g.Count()}b");
                Line($"  t={w.Time:F1}s result {w.Mission.Result} {string.Join(" ", tags)} | {string.Join(" ", btags)}");
            }
            Dump();
            for (int i = 0; i < 40; i++)
            {
                for (int t = 0; t < 15; t++)
                {
                    w.Step();
                    foreach (var e in w.Events) if (e.Type == SimEventType.UnitDied || e.Type == SimEventType.BuildingDestroyed) { var x = e; Line($"    died: {e.Type} {e.Text} at {e.Pos.X:F0},{e.Pos.Y:F0}"); }
                    w.Events.Clear();
                }
                Dump();
            }
        }

        static void Profile(string mapId, string[] args)
        {
            var c = new SkirmishConfig { MapId = mapId, Spectator = true };
            c.Settings.Seed = 1002;
            c.Slots.Add(new SlotConfig { Faction = FactionId.Alliance, Team = 0, Color = 0, Difficulty = AIDifficulty.Normal, Name = "A" });
            c.Slots.Add(new SlotConfig { Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = AIDifficulty.Hard, Name = "B" });
            var w = GameSetup.CreateSkirmish(c);
            var names = new[] { "spatial", "ai", "economy", "production", "behavior", "movement", "combatB", "projectiles", "fires", "powers", "fog", "victory" };
            var t = new double[names.Length]; var sw = new Stopwatch();
            int logged = 0;
            w.Pathfinder.OnFailed = (a, b, mc, ex) => { if (logged++ < 25 && w.Tick > 3000) Line($"    fail {a.X},{a.Y} -> {b.X},{b.Y} {mc} expanded {ex} startTerrain {w.Map.Get(a.X, a.Y)} lvl {w.Map.LevelAt(a.X, a.Y)} goalTerrain {w.Map.Get(b.X, b.Y)} lvl {w.Map.LevelAt(b.X, b.Y)} bld {(w.BuildingAtCell(b.X, b.Y)?.DefId ?? "-")}"); };
            void M(int i, Action a) { sw.Restart(); a(); t[i] += sw.Elapsed.TotalMilliseconds; }
            for (int k = 0; k < 12 * 900 && !w.GameOver; k++)
            {
                w.Tick++; w.Time += World.Dt;
                M(0, () => w.Spatial.Rebuild(w.Units));
                M(1, () => { foreach (var ai in w.AIs) ai.Update(); });
                M(2, w.Economy.Update); M(3, w.Production.Update); M(4, w.Behavior.UpdateAll); M(5, w.Movement.UpdateAll);
                M(6, w.Combat.UpdateBuildings); M(7, w.Combat.UpdateProjectiles); M(8, w.Combat.UpdateFires); M(9, w.Powers.Update);
                M(10, () => { if (w.Tick % 3 == 0) w.Fog.Update(); });
                for (int i = w.Units.Count - 1; i >= 0; i--) if (w.Units[i].Dead) { w.ById.Remove(w.Units[i].Id); w.Units.RemoveAt(i); }
                for (int i = w.Buildings.Count - 1; i >= 0; i--) if (w.Buildings[i].Dead) { w.ById.Remove(w.Buildings[i].Id); w.Buildings.RemoveAt(i); }
                M(11, w.Victory.Update);
                w.Events.Clear();
                if (k % 900 == 0)
                {
                    var p0 = w.Players[0].StartCell; var p1 = w.Players[1].StartCell;
                    var a0 = w.Pathfinder.NearestPassable(p0, MoveClass.Vehicle, 0, 10); var a1 = w.Pathfinder.NearestPassable(p1, MoveClass.Vehicle, 1, 10);
                    var mid = w.Pathfinder.NearestPassable(new Int2(w.Map.Width / 2, w.Map.Height / 2), MoveClass.Vehicle, 0, 20);
                    Line($"   min {k / 900}: base0->mid {w.Production.KeepsBaseOpen(0, Catalog.Building("ca_wall"), new Int2(0, 0))} reach0 {w.Pathfinder.Reachable(a0, mid, MoveClass.Vehicle, 0)} reach1 {w.Pathfinder.Reachable(a1, mid, MoveClass.Vehicle, 1)} starts {p0.X},{p0.Y} {p1.X},{p1.Y}");
                }
            }
            for (int i = 0; i < names.Length; i++) Line($"  {names[i],-12} {t[i] / Math.Max(1, w.Tick):F3} ms/tick");
            var pf = w.Pathfinder;
            Line($"  paths: {pf.StatSearches / (double)w.Tick:F2}/tick, failed {pf.StatFailed * 100.0 / Math.Max(1, pf.StatSearches):F0}%, avg expanded {pf.StatExpanded / (double)Math.Max(1, pf.StatSearches):F0}");
        }

        static void RunMapChecks()
        {
            Line("\n## Map checks (ore near each start, start reachability)");
            foreach (var spec in MapLibrary.Skirmish)
            {
                var m = MapGenerator.Generate(spec);
                var sb = new StringBuilder();
                int total = 0; foreach (var o in m.Ore) total += o;
                for (int si = 0; si < m.StartPositions.Count; si++)
                {
                    var st = m.StartPositions[si];
                    int r12 = 0, r20 = 0;
                    for (int dy = -20; dy <= 20; dy++) for (int dx = -20; dx <= 20; dx++)
                    {
                        int x = st.X + dx, y = st.Y + dy; if (!m.InBounds(x, y)) continue;
                        int o = m.Ore[m.Index(x, y)]; float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d <= 12) r12 += o; if (d <= 20) r20 += o;
                    }
                    // ore reachable by ground from the start within 30 cells (flood fill over passable land)
                    var seen = new bool[m.Width * m.Height]; var q = new Queue<Int2>(); q.Enqueue(st); seen[m.Index(st.X, st.Y)] = true; int reach = 0;
                    while (q.Count > 0)
                    {
                        var cc = q.Dequeue(); int ci = m.Index(cc.X, cc.Y); reach += m.Ore[ci];
                        foreach (var dd in new[] { new Int2(1, 0), new Int2(-1, 0), new Int2(0, 1), new Int2(0, -1) })
                        {
                            int nx = cc.X + dd.X, ny = cc.Y + dd.Y;
                            if (!m.InBounds(nx, ny) || seen[m.Index(nx, ny)]) continue;
                            if (Math.Abs(nx - st.X) > 30 || Math.Abs(ny - st.Y) > 30) continue;
                            if (!MapData.IsLandPassable(m.Cells[m.Index(nx, ny)])) continue;
                            seen[m.Index(nx, ny)] = true; q.Enqueue(new Int2(nx, ny));
                        }
                    }
                    int box = 0; for (int dy = -30; dy <= 30; dy++) for (int dx = -30; dx <= 30; dx++) { int x = st.X + dx, y = st.Y + dy; if (m.InBounds(x, y)) box += m.Ore[m.Index(x, y)]; }
                    sb.Append($" s{si}@{st.X},{st.Y}:{r12}/{r20} reach30 {reach}/{box}");
                    if (r20 < 4000) Fail($"{spec.Id} start {si} has little ore within 20 cells ({r20})");
                }
                Line($"  {spec.Id,-16} {m.Width}x{m.Height} players {spec.MaxPlayers} ore {total}{sb}");
            }
        }

        // ------------------------------------------------------------------ static data
        static void RunDataChecks()
        {
            Line("\n## Data checks");
            int issues = 0;
            foreach (var u in Catalog.Units.Values)
            {
                if (u.Cost <= 0 && !u.Id.StartsWith("civ")) { Line($"  note: unit {u.Id} has cost {u.Cost}"); }
                if (u.Health <= 0) { Fail($"unit {u.Id} health {u.Health}"); issues++; }
                foreach (var wid in u.Weapons ?? new List<string>())
                    if (Catalog.Weapon(wid) == null) { Fail($"unit {u.Id} references missing weapon {wid}"); issues++; }
            }
            foreach (var b in Catalog.Buildings.Values)
            {
                if (b.Health <= 0) { Fail($"building {b.Id} health {b.Health}"); issues++; }
                foreach (var wid in b.Weapons ?? new List<string>()) if (Catalog.Weapon(wid) == null) { Fail($"building {b.Id} references missing weapon {wid}"); issues++; }
            }
            foreach (var f in new[] { FactionId.Alliance, FactionId.Dominion })
            {
                int units = Catalog.Units.Values.Count(u => u.Faction == f);
                int blds = Catalog.Buildings.Values.Count(b => b.Faction == f);
                Line($"  {f}: {units} units, {blds} buildings");
            }
            foreach (var m in CampaignDatabase.All())
                if (m.Map == null) { Fail($"mission {m.Id} has no map"); issues++; }
            Line(issues == 0 ? "  all references resolve" : $"  {issues} data issues");
        }
    }
}
