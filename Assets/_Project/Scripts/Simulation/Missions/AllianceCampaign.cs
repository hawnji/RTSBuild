using System;
using System.Collections.Generic;
using static RTS.Sim.MissionKit;

namespace RTS.Sim
{
    /// <summary>The Continental Alliance campaign: twelve operations from the defence of Halvorsen to the fall of Ironhold.</summary>
    public static class AllianceCampaign
    {
        static Vec2 V(float x, float y) => new Vec2(x, y);
        const FactionId CA = FactionId.Alliance, ID = FactionId.Dominion;

        public static List<MissionDef> Build()
        {
            var list = new List<MissionDef>();

            // ============================================================ 1. First Light — establish a forward base
            {
                var map = Map("ca01", "Halvorsen Ridge", 80, 96, MapTheme.Temperate, 1101, 0.09f);
                map.Add(River(3, new[] { 2 }, V(0, 52), V(30, 56), V(50, 54), V(80, 60)));
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(26, 26))).Add(Feat(FeatureKind.OreField, 4, V(56, 38))).Add(Feat(FeatureKind.OreField, 4, V(50, 76)));
                map.Add(Road(V(18, 12), V(40, 40), V(40, 70), V(52, 84))).Add(Feat(FeatureKind.Forest, 6, V(62, 18), V(12, 66)));
                map.Starts.Add(new Int2(18, 14)); map.Starts.Add(new Int2(52, 84));
                map.Neutrals.Add(new NeutralPlacement("tech_derrick", 64, 46, "derrick"));
                map.Neutrals.Add(new NeutralPlacement("civ_house", 30, 44)); map.Neutrals.Add(new NeutralPlacement("civ_chapel", 46, 46));
                list.Add(new MissionDef
                {
                    Id = "ca01", Faction = CA, Index = 1, Title = "First Light", Location = "Halvorsen Ridge, southern border", Date = "9 March 1994",
                    Map = map, StartingCredits = 6000, EnemyFaction = ID, Music = "alliance",
                    Briefing = "Commander, Dominion armour crossed the Kesh border at dawn. Halvorsen Ridge is the only defensible ground between their spearhead and the capital road.\n\n" +
                               "Deploy your Mobile HQ, establish an ore economy and build a Training Camp and Vehicle Assembly. Once your base is running, destroy the Dominion forward outpost north of the river.\n\n" +
                               "The ore derrick east of the river is still running. An engineer could put it to work for us.",
                    VictoryText = "The outpost is ours. Halvorsen Ridge holds — for now.",
                    Tags = { "establish base", "destroy outpost" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Dominion Vanguard", ID, 1, builds: false, attacks: false); m.AddNeutral(); m.Human.MaxTechTier = 2; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.Spawn(m.Human, "ca_mhq", 18, 14);
                        m.SpawnGroup(m.Human, 22, 17, null, "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_engineer", "ca_wasp");
                        m.BuildBase(e, 52, 82, BasePreset.Outpost, "outpost");
                        m.Build(e, "id_power", 56, 86, "outpost_extra");
                        m.Build(e, "id_mgbunker", 44, 74); m.Build(e, "id_mgbunker", 58, 74);
                        m.SpawnGroup(e, 50, 76, "garrison", "id_levy", "id_levy", "id_levy", "id_levy", "id_breacher", "id_ravager");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("base", "Deploy the Mobile HQ and build an Ore Processing Plant, Training Camp and Vehicle Assembly.");
                        m.AddObjective("outpost", "Destroy the Dominion outpost north of the river.");
                        m.AddObjective("derrick", "Capture the ore derrick with an engineer.", primary: false);
                        m.At(2, "intro", () => { m.Say("Col. Vance", "Commander, deploy the HQ on the ridge. The Dominion will not wait for us to settle in."); m.CameraTo(18, 14); });
                        m.When("base_done", () => m.HasBuilt(m.Human, BuildingRole.Refinery) && m.HasBuilt(m.Human, BuildingRole.Barracks) && m.HasBuilt(m.Human, BuildingRole.Factory), () =>
                        {
                            m.Complete("base");
                            m.Say("Col. Vance", "Good work. Now push across the bridge and take that outpost apart.");
                            m.Reveal(52, 82, 10, 20);
                        });
                        m.When("derrick", () => m.OwnedBy("derrick", m.Human), () => { m.Complete("derrick"); m.Say("Col. Vance", "Derrick secured. Every credit counts."); });
                        m.When("outpost_dead", () => m.CountBuildings(e) == 0, () => m.Complete("outpost"));
                        m.At(240, "raid1", () => m.Attack(e, 40, 66, 20, 18, "raid", Scale(m, "id_levy", "id_levy", "id_levy", "id_breacher")));
                        m.At(480, "raid2", () => m.Attack(e, 50, 66, 20, 18, "raid", Scale(m, "id_levy", "id_levy", "id_jackal", "id_breacher", "id_ravager")));
                        m.At(500, "hint_sell", () => m.Hint("Tip: right-click a damaged structure's repair button to restore it. Low power slows production."));
                    }
                });
            }

            // ============================================================ 2. Holding the Line — defend an installation
            {
                var map = Map("ca02", "Halvorsen Radar Station", 96, 96, MapTheme.Temperate, 1202, 0.08f);
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(30, 30))).Add(Feat(FeatureKind.OreField, 4, V(62, 26)));
                map.Add(Ridge(3, V(0, 58), V(30, 60))).Add(Ridge(3, V(66, 60), V(96, 58)));
                map.Add(Road(V(48, 30), V(48, 60), V(30, 90))).Add(Road(V(48, 60), V(70, 90)));
                map.Add(Feat(FeatureKind.Forest, 6, V(16, 76), V(80, 78), V(14, 40)));
                map.Starts.Add(new Int2(48, 26)); map.Starts.Add(new Int2(48, 86));
                list.Add(new MissionDef
                {
                    Id = "ca02", Faction = CA, Index = 2, Title = "Holding the Line", Location = "Halvorsen Radar Station", Date = "14 March 1994",
                    Map = map, StartingCredits = 7000, EnemyFaction = ID, Music = "battle",
                    Briefing = "Our early-warning radar at Halvorsen sees forty kilometres into Dominion territory. Without it, their bombers reach the capital unannounced.\n\n" +
                               "Three Dominion battle groups are converging on the station. Hold for twelve minutes until the 2nd Armoured Brigade arrives. The radar array must survive.\n\n" +
                               "A Dominion artillery battery has been spotted on the northern hills. Silencing it would make the defence considerably easier.",
                    VictoryText = "The 2nd Armoured has arrived. Halvorsen still sees.",
                    Tags = { "defend installation", "survive waves" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Dominion 4th Army", ID, 1, builds: false, attacks: false); m.Human.MaxTechTier = 2; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 48, 26, BasePreset.Small, "base");
                        m.Build(m.Human, "ca_radar", 52, 34, "radar");
                        m.Build(m.Human, "ca_factory", 41, 31);
                        m.Build(m.Human, "ca_avturret", 46, 40); m.Build(m.Human, "ca_aiturret", 52, 40);
                        m.SpawnGroup(m.Human, 48, 38, null, "ca_sabre", "ca_sabre", "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_lancer", "ca_bulwark");
                        m.Build(e, "id_artillery", 30, 84, "battery"); m.Build(e, "id_artillery", 66, 84, "battery");
                        m.Build(e, "id_power", 47, 88, "battery_power");
                        m.SpawnGroup(e, 30, 80, "battery_guard", "id_levy", "id_levy", "id_ironclad", "id_breacher");
                        m.SpawnGroup(e, 66, 80, "battery_guard", "id_levy", "id_levy", "id_ironclad", "id_breacher");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("radar", "Protect the Radar Array.");
                        m.AddObjective("hold", "Hold out for 12 minutes.");
                        m.AddObjective("battery", "Destroy the Dominion artillery battery.", primary: false);
                        m.At(1, "cd", () => { m.Countdown("Reinforcements", 720); m.Say("Gen. Marsh", "Twelve minutes, Commander. Give me twelve minutes."); });
                        m.When("radar_lost", () => m.AllDead("radar"), () => m.Fail("radar"));
                        m.When("battery", () => m.AllDead("battery"), () => { m.Complete("battery"); m.Say("Col. Vance", "Battery silenced. Those guns won't trouble us again."); });
                        m.When("time", () => m.CountdownExpired("Reinforcements"), () =>
                        {
                            m.Complete("hold");
                            m.Reinforce(m.Human, 48, 3, 48, 40, null, "ca_warden", "ca_warden", "ca_sabre", "ca_sabre", "ca_sabre");
                            if (!m.AllDead("radar")) m.Complete("radar");
                        });
                        var spawn = new[] { V(30, 92), V(70, 92), V(48, 92), V(6, 70), V(90, 70) };
                        Waves(m, "w", e, 50, 70, 10, spawn, V(50, 34), i => i < 3 ? Mix(Inf(ID, 5), new[] { "id_jackal" }) : i < 6 ? Mix(Inf(ID, 4), new[] { "id_ravager", "id_ravager" }) : Mix(Inf(ID, 4), new[] { "id_ravager", "id_hailstorm", "id_ravager", "id_ironclad" }));
                        m.At(400, "bombers", () => { m.Say("Radar Op", "Dominion gunships inbound from the north!"); m.Attack(e, 48, 92, 50, 34, "air", Scale(m, "id_harrow", "id_harrow")); });
                        m.At(420, "aahint", () => m.Hint("Build Skyshield Batteries or Skyguard infantry to counter aircraft."));
                    }
                });
            }

            // ============================================================ 3. Broken Column — rescue trapped forces
            {
                var map = Map("ca03", "Orlen Forest", 112, 88, MapTheme.Temperate, 1303, 0.18f);
                map.Add(Feat(FeatureKind.Clearing, 6, V(80, 60))).Add(Feat(FeatureKind.Clearing, 5, V(14, 14)));
                map.Add(Road(V(10, 10), V(40, 30), V(70, 40), V(80, 60), V(100, 80)));
                map.Add(River(3, new[] { 1 }, V(54, 0), V(56, 40), V(58, 88)));
                map.Add(Feat(FeatureKind.OreField, 3.5f, V(30, 16)));
                map.Starts.Add(new Int2(14, 14)); map.Starts.Add(new Int2(96, 76));
                list.Add(new MissionDef
                {
                    Id = "ca03", Faction = CA, Index = 3, Title = "Broken Column", Location = "Orlen Forest", Date = "21 March 1994", NoBase = true,
                    Map = map, StartingCredits = 0, EnemyFaction = ID, Music = "tension",
                    Briefing = "The 3rd Armoured column was ambushed in Orlen Forest. Survivors under Captain Ilse Brandt are dug in at a clearing east of the river, surrounded and low on ammunition.\n\n" +
                               "Take a strike group through the forest, break the encirclement and bring Brandt's people back to the extraction point in the southwest. At least four of her units must survive.\n\n" +
                               "Your infantry can move through the woods. Your vehicles cannot.",
                    VictoryText = "Captain Brandt and her crews are safe. The 3rd Armoured will ride again.",
                    Tags = { "rescue", "escort" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Dominion Hunter Groups", ID, 1, builds: false, attacks: false); m.AddAI("trapped", "3rd Armoured (Trapped)", CA, 0, builds: false, attacks: false); },
                    Populate = m =>
                    {
                        var e = m.P("enemy"); var t = m.P("trapped");
                        m.SpawnGroup(m.Human, 16, 16, null, "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_lancer", "ca_corpsman", "ca_marksman", "ca_sabre", "ca_sabre", "ca_stallion");
                        foreach (var u in m.SpawnGroup(t, 80, 60, "brandt", "ca_sabre", "ca_sabre", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_bulwark", "ca_mender")) u.Hp = u.MaxHp * 0.45f;
                        m.SpawnGroup(e, 72, 52, "ring", "id_levy", "id_levy", "id_levy", "id_breacher", "id_ravager");
                        m.SpawnGroup(e, 88, 68, "ring", "id_levy", "id_levy", "id_ironclad", "id_breacher", "id_ravager");
                        m.SpawnGroup(e, 70, 70, "ring", "id_levy", "id_levy", "id_hunter", "id_jackal");
                        m.SpawnGroup(e, 40, 34, "ambush", "id_levy", "id_levy", "id_levy", "id_breacher");
                        m.Build(e, "id_mgbunker", 56, 42); m.Build(e, "id_mgbunker", 60, 38);
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy"); var t = m.P("trapped");
                        m.Zone("brandt", 80, 60, 7); m.Zone("extract", 12, 12, 6);
                        m.AddObjective("reach", "Reach Captain Brandt's position east of the river.");
                        m.AddObjective("extract", "Bring at least 4 of Brandt's units to the extraction point.");
                        m.AddObjective("keep", "Keep at least 4 of Brandt's units alive.");
                        m.At(2, "intro", () => { m.Say("Capt. Brandt", "...anyone on this net... we can't hold much longer..."); m.CameraTo(80, 60, 2); });
                        m.At(6, "back", () => m.CameraTo(16, 16, 1.5f));
                        m.When("reach", () => m.CountInZone("brandt", m.Human) > 0, () =>
                        {
                            m.Complete("reach");
                            m.GiveTagged("brandt", m.Human);
                            m.Say("Capt. Brandt", "Alliance colours! Never been so glad to see them. We're with you, Commander.");
                            m.Attack(e, 104, 80, 80, 60, "pursuit", Scale(m, "id_ravager", "id_ravager", "id_levy", "id_levy", "id_breacher", "id_jackal"));
                        });
                        m.When("lost", () => m.CountTagged("brandt") < 4, () => m.Fail("keep"));
                        m.When("extracted", () => m.IsComplete("reach") && m.CountInZone("extract", m.Human, u => u.Tag == "brandt") >= 4, () => { m.Complete("extract"); m.Complete("keep"); });
                        m.Every(45, "hunters", () => m.IsComplete("reach") && !m.IsComplete("extract"), () => m.Attack(e, 104, 40, 50, 40, "hunt", Scale(m, "id_levy", "id_levy", "id_breacher")));
                    }
                });
            }

            // ============================================================ 4. Supply Lines — destroy supply facilities
            {
                var map = Map("ca04", "Kessel Rail Junction", 104, 104, MapTheme.Wasteland, 1404, 0.06f, 0.05f);
                map.Add(Road(V(6, 50), V(30, 54), V(52, 52), V(76, 60), V(98, 70))).Add(Road(V(52, 52), V(52, 98)));
                map.Add(Feat(FeatureKind.Forest, 7, V(20, 80), V(80, 24), V(30, 30)));
                map.Add(Feat(FeatureKind.Lake, 5, V(70, 84)));
                map.Starts.Add(new Int2(10, 10)); map.Starts.Add(new Int2(80, 80));
                list.Add(new MissionDef
                {
                    Id = "ca04", Faction = CA, Index = 4, Title = "Supply Lines", Location = "Kessel Rail Junction", Date = "2 April 1994", NoBase = true,
                    Map = map, StartingCredits = 0, EnemyFaction = ID, Music = "tension",
                    Briefing = "The Dominion offensive runs on fuel and shells moved through the Kessel rail junction. Three supply depots feed the entire southern front.\n\n" +
                               "A Phantom Operative and a small strike team will infiltrate the junction. Destroy all three depots. Phantoms are cloaked unless firing; detector units and sentry posts can reveal them.\n\n" +
                               "A fuel convoy is due to leave the junction. If you can intercept it, do so.",
                    VictoryText = "The junction burns. The southern front will starve for weeks.",
                    Tags = { "destroy supply facilities", "infiltration" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Dominion Logistics Corps", ID, 1, builds: false, attacks: false); },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.SpawnGroup(m.Human, 10, 10, null, "ca_phantom", "ca_marksman", "ca_rifleman", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_engineer", "ca_corpsman");
                        m.Build(e, "tech_depot", 50, 56, "depot"); m.Build(e, "tech_depot", 76, 64, "depot"); m.Build(e, "tech_depot", 54, 86, "depot");
                        m.Build(e, "id_mgbunker", 47, 53); m.Build(e, "id_aiturret", 55, 59); m.Build(e, "id_mgbunker", 73, 61); m.Build(e, "id_avturret", 80, 66);
                        m.Build(e, "id_mgbunker", 51, 83); m.Build(e, "id_power", 60, 70, "power"); m.Build(e, "id_power", 64, 70, "power");
                        m.Build(e, "id_barracks", 58, 90);
                        m.SpawnGroup(e, 52, 60, "guard", "id_levy", "id_levy", "id_levy", "id_ironclad");
                        m.SpawnGroup(e, 76, 58, "guard", "id_levy", "id_levy", "id_breacher", "id_jackal");
                        m.SpawnGroup(e, 56, 80, "guard", "id_levy", "id_levy", "id_ironclad", "id_ravager");
                        m.SpawnGroup(e, 30, 54, "patrol", "id_jackal", "id_levy", "id_levy");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.Zone("exit", 98, 70, 4);
                        m.AddObjective("depots", "Destroy all three supply depots.");
                        m.AddObjective("convoy", "Intercept the fuel convoy before it leaves the junction.", primary: false, hidden: true);
                        foreach (var u in m.Tagged("patrol")) m.W.Commands.Patrol(new[] { u.Id }, u.Owner, new Vec2(80, 30));
                        m.At(3, "intro", () => m.Say("Maj. Lind", "Phantom team, you are cleared. Charges on the depots; leave nothing they can use."));
                        m.When("depots", () => m.AllDead("depot"), () => m.Complete("depots"));
                        m.At(180, "convoy", () =>
                        {
                            m.Reveal("convoy");
                            m.Say("Maj. Lind", "Fuel convoy leaving the northern depot, heading east. Stop it!");
                            foreach (var tr in new[] { 0, 1, 2 }) { var u = m.Spawn(e, "civ_truck", 54, 92 - tr * 2, "convoy"); m.W.Commands.Move(new[] { u.Id }, e.Id, new Vec2(98, 70), false, false); }
                        });
                        m.When("convoy_escaped", () => m.Fired("convoy") && m.CountInZone("exit", e, u => u.Tag == "convoy") > 0, () => { m.Fail("convoy"); foreach (var x in m.Tagged("convoy")) if (x is Unit cu && m.Zones["exit"].Contains(cu.Pos)) cu.Dead = true; });
                        m.When("convoy_dead", () => m.Fired("convoy") && m.AllDead("convoy") && m.IsActive("convoy"), () => m.Complete("convoy"));
                        m.Every(90, "react", () => m.W.Time > 120, () => m.Attack(e, 98, 98, 30, 30, "react", Scale(m, "id_levy", "id_levy", "id_breacher")));
                    }
                });
            }

            // ============================================================ 5. Iron Convoy — escort armored convoy
            {
                var map = Map("ca05", "Merrow Highway", 128, 72, MapTheme.Temperate, 1505, 0.1f);
                map.Add(Road(V(6, 36), V(30, 30), V(52, 40), V(74, 32), V(96, 40), V(122, 36)));
                map.Add(Ridge(3, V(40, 0), V(46, 22))).Add(Ridge(3, V(84, 72), V(80, 50)));
                map.Add(River(3, new[] { 1 }, V(60, 0), V(62, 72)));
                map.Add(Feat(FeatureKind.Forest, 6, V(30, 54), V(100, 16), V(70, 58)));
                map.Starts.Add(new Int2(8, 36)); map.Starts.Add(new Int2(120, 36));
                list.Add(new MissionDef
                {
                    Id = "ca05", Faction = CA, Index = 5, Title = "Iron Convoy", Location = "Merrow Highway", Date = "11 April 1994", NoBase = true,
                    Map = map, StartingCredits = 0, EnemyFaction = ID, Music = "battle",
                    Briefing = "Merrow is cut off. Five supply trucks carrying aurite capacitors and medical supplies must reach the city along the highway.\n\n" +
                               "Escort the convoy east. At least three trucks must reach Merrow. Expect ambushes at the river bridge and the ridge cuts. The trucks follow the highway and will stop when threatened; clear the road ahead of them.",
                    VictoryText = "The convoy rolls into Merrow to cheering crowds. The city will hold.",
                    Tags = { "escort convoy" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Dominion Raiders", ID, 1, builds: false, attacks: false); },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        for (int i = 0; i < 5; i++) m.Spawn(m.Human, "civ_truck", 6 + i * 1.2f, 36, "convoy", 0);
                        m.SpawnGroup(m.Human, 10, 40, null, "ca_sabre", "ca_sabre", "ca_sabre", "ca_wasp", "ca_wasp", "ca_sentinel", "ca_stallion", "ca_mender");
                        m.SpawnGroup(m.Human, 10, 32, null, "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_lancer", "ca_corpsman");
                        m.SpawnGroup(e, 44, 30, "ambush1", "id_levy", "id_levy", "id_breacher", "id_breacher", "id_ravager");
                        m.SpawnGroup(e, 62, 46, "ambush2", "id_levy", "id_levy", "id_ironclad", "id_ravager", "id_ravager");
                        m.Build(e, "id_avturret", 66, 44); m.Build(e, "id_mgbunker", 66, 34);
                        m.SpawnGroup(e, 84, 44, "ambush3", "id_hailstorm", "id_levy", "id_levy", "id_breacher");
                        m.SpawnGroup(e, 104, 30, "ambush4", "id_ravager", "id_ravager", "id_levy", "id_levy", "id_hunter");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.Zone("merrow", 122, 36, 5);
                        var route = new[] { V(30, 30), V(52, 40), V(74, 32), V(96, 40), V(122, 36) };
                        m.AddObjective("escort", "Escort at least 3 supply trucks to Merrow.");
                        m.AddObjective("ambush", "Destroy the raider force at the ridge (northeast).", primary: false);
                        m.At(2, "intro", () => m.Say("Convoy Lead", "Convoy rolling. We're counting on you to keep the road clear."));
                        // Trucks advance waypoint by waypoint, pausing when enemies are near.
                        m.Every(2, "drive", () => true, () =>
                        {
                            foreach (var ent in m.Tagged("convoy"))
                            {
                                if (!(ent is Unit u) || u.Owner != m.Human.Id) continue;
                                bool danger = false;
                                foreach (var o in m.W.Units) if (!o.Dead && m.W.IsEnemy(u.Owner, o.Owner) && o.IsArmed && Vec2.Distance(o.Pos, u.Pos) < 8 && m.W.Combat.VisibleTo(o, m.Human.Team)) { danger = true; break; }
                                if (danger) { if (u.Moving) m.W.Movement.Stop(u); continue; }
                                int next = 0;
                                for (int i = 0; i < route.Length; i++) if (u.Pos.X < route[i].X - 1.5f) { next = i; break; } else next = route.Length - 1;
                                if (!u.Moving) m.W.Movement.RequestMove(u, route[next] + new Vec2(0, (u.Id % 3) * 0.8f - 0.8f));
                            }
                        });
                        m.When("ambush", () => m.AllDead("ambush4"), () => m.Complete("ambush"));
                        m.When("arrive", () => m.CountInZone("merrow", m.Human, u => u.Tag == "convoy") >= 3, () => m.Complete("escort"));
                        m.When("fail", () => m.CountTagged("convoy") < 3, () => m.Fail("escort"));
                        m.At(150, "flank", () => m.Attack(e, 60, 70, 40, 36, "flank", Scale(m, "id_jackal", "id_jackal", "id_levy", "id_breacher")));
                        m.At(300, "gunships", () => { m.Say("Convoy Lead", "Gunships! Get those AA tracks up front!"); m.Attack(e, 126, 70, 70, 36, "air", Scale(m, "id_harrow")); });
                    }
                });
            }

            // ============================================================ 6. Stolen Fire — capture enemy technology
            {
                var map = Map("ca06", "Velda Proving Grounds", 104, 104, MapTheme.Desert, 1606, 0.0f, 0.06f);
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(24, 24))).Add(Feat(FeatureKind.OreField, 4.5f, V(52, 40))).Add(Feat(FeatureKind.CrystalField, 4, V(80, 34))).Add(Feat(FeatureKind.OreField, 4, V(30, 70)));
                var plat = Feat(FeatureKind.Plateau, 13, V(78, 80)); plat.Value = 1; plat.Ramps.Add(V(64, 76)); plat.Ramps.Add(V(78, 66)); map.Add(plat);
                map.Add(Road(V(16, 16), V(50, 50), V(78, 80)));
                map.Starts.Add(new Int2(16, 16)); map.Starts.Add(new Int2(80, 82));
                map.Neutrals.Add(new NeutralPlacement("tech_derrick", 50, 20, "derrick")); map.Neutrals.Add(new NeutralPlacement("tech_derrick", 20, 50, "derrick"));
                list.Add(new MissionDef
                {
                    Id = "ca06", Faction = CA, Index = 6, Title = "Stolen Fire", Location = "Velda Proving Grounds", Date = "25 April 1994",
                    Map = map, StartingCredits = 8000, EnemyFaction = ID, Music = "alliance",
                    Briefing = "Intelligence confirms the Dominion is testing a new warhead at the Velda proving grounds. The plans are kept in the Research Bureau on the plateau.\n\n" +
                               "Build a base, break through the plateau defences and capture the Research Bureau with an engineer. Do NOT destroy it — we need those files intact.\n\n" +
                               "Engineers can only capture military structures that have been damaged below half strength.",
                    VictoryText = "The Hammerfall files are in our hands. Now we know what we are racing against.",
                    Tags = { "capture technology", "base assault" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Dominion Weapons Directorate", ID, 1, builds: true, attacks: true, aggression: 0.8f); m.AddNeutral(); m.Human.MaxTechTier = 3; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.Spawn(m.Human, "ca_mhq", 16, 16);
                        m.SpawnGroup(m.Human, 20, 20, null, "ca_sabre", "ca_sabre", "ca_rifleman", "ca_rifleman", "ca_engineer", "ca_engineer");
                        m.BuildBase(e, 80, 82, BasePreset.Medium, "base");
                        m.Build(e, "id_techcenter", 86, 88, "bureau");
                        m.Build(e, "id_avturret", 66, 72); m.Build(e, "id_aiturret", 70, 70); m.Build(e, "id_mgbunker", 78, 68);
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("capture", "Capture the Research Bureau with an engineer.");
                        m.AddObjective("keep", "Do not destroy the Research Bureau.");
                        m.AddObjective("derricks", "Capture both ore derricks.", primary: false);
                        m.At(2, "intro", () => { m.Say("Maj. Lind", "The Bureau is the small domed structure at the top of the plateau. Damage it, don't destroy it."); m.CameraTo(86, 88, 2); });
                        m.At(6, "back", () => m.CameraTo(16, 16));
                        m.When("captured", () => m.OwnedBy("bureau", m.Human), () => { m.Complete("capture"); m.Complete("keep"); });
                        m.When("destroyed", () => m.AllDead("bureau"), () => m.Fail("keep"));
                        m.When("derricks", () => m.OwnedBy("derrick", m.Human), () => m.Complete("derricks"));
                    }
                });
            }

            // ============================================================ 7. Bridgehead — hold territory
            {
                var map = Map("ca07", "Varn River Crossing", 104, 104, MapTheme.Temperate, 1707, 0.08f);
                map.Add(River(5, new[] { 2, 4 }, V(0, 56), V(26, 58), V(46, 56), V(62, 58), V(80, 54), V(104, 56)));
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(26, 24))).Add(Feat(FeatureKind.OreField, 4, V(70, 30))).Add(Feat(FeatureKind.OreField, 4, V(54, 76)));
                map.Add(Road(V(50, 12), V(54, 56), V(52, 96)));
                map.Add(Feat(FeatureKind.Forest, 6, V(14, 80), V(90, 82)));
                map.Starts.Add(new Int2(50, 14)); map.Starts.Add(new Int2(52, 92));
                list.Add(new MissionDef
                {
                    Id = "ca07", Faction = CA, Index = 7, Title = "Bridgehead", Location = "Varn River", Date = "6 May 1994",
                    Map = map, StartingCredits = 9000, EnemyFaction = ID, Music = "battle",
                    Briefing = "The counteroffensive begins. Engineers have repaired the Varn bridges; the Dominion will throw everything at them.\n\n" +
                               "Seize the bridgehead on the north bank and hold it for ten minutes so the main army can cross. You must keep forces in the bridgehead zone and clear it of enemies when the timer ends.",
                    VictoryText = "The army crosses the Varn. The road north is open.",
                    Tags = { "hold territory" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Dominion Varn Group", ID, 1, builds: true, attacks: false); m.Human.MaxTechTier = 3; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 50, 14, BasePreset.Medium, "base");
                        m.SpawnGroup(m.Human, 52, 26, null, "ca_sabre", "ca_sabre", "ca_sabre", "ca_lancer", "ca_lancer", "ca_rifleman", "ca_rifleman", "ca_bulwark", "ca_arbiter");
                        m.BuildBase(e, 52, 90, BasePreset.Medium, "ebase");
                        m.SpawnGroup(e, 54, 64, "bridge_guard", "id_levy", "id_levy", "id_levy", "id_ironclad", "id_ravager", "id_breacher");
                        m.Build(e, "id_mgbunker", 50, 63); m.Build(e, "id_avturret", 58, 64);
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.Zone("bridgehead", 54, 64, 7);
                        m.AddObjective("seize", "Seize the bridgehead on the north bank.");
                        m.AddObjective("hold", "Hold the bridgehead for 10 minutes.", hidden: true);
                        m.AddObjective("base", "Destroy the Dominion Varn base.", primary: false);
                        m.When("seized", () => m.CountInZone("bridgehead", m.Human, u => u.IsArmed) >= 3 && m.EnemiesInZone("bridgehead") == 0, () =>
                        {
                            m.Complete("seize"); m.Reveal("hold"); m.Countdown("Hold the bridgehead", 600);
                            m.Say("Gen. Marsh", "Bridgehead secured. Hold it, Commander. Ten minutes.");
                        });
                        m.When("held", () => m.IsComplete("seize") && m.CountdownExpired("Hold the bridgehead") && m.CountInZone("bridgehead", m.Human, u => u.IsArmed) > 0 && m.EnemiesInZone("bridgehead") == 0, () => m.Complete("hold"));
                        m.When("lost", () => m.IsComplete("seize") && m.CountInZone("bridgehead", m.Human) == 0 && m.EnemiesInZone("bridgehead") > 2, () => m.Fail("hold"));
                        m.When("ebase", () => m.CountBuildings(e) == 0, () => m.Complete("base"));
                        var spawns = new[] { V(20, 98), V(84, 98), V(52, 98) };
                        Waves(m, "w", e, 30, 55, 12, spawns, V(54, 64), i => i % 3 == 2 ? new[] { "id_ravager", "id_ravager", "id_behemoth", "id_levy", "id_levy" } : Mix(Inf(ID, 5), new[] { "id_ravager" }), () => m.IsComplete("seize"));
                    }
                });
            }

            // ============================================================ 8. Silent Coast — naval assault
            {
                var map = Map("ca08", "Gray Coast", 112, 96, MapTheme.Temperate, 1808, 0.07f);
                map.Add(Ocean(56, 50, -0.2f, -1f));
                map.Add(Feat(FeatureKind.OreField, 4, V(30, 80))).Add(Feat(FeatureKind.OreField, 4, V(80, 84))).Add(Feat(FeatureKind.OreField, 3.5f, V(20, 12)));
                map.Add(Feat(FeatureKind.Clearing, 6, V(16, 14)));
                map.Starts.Add(new Int2(16, 14)); map.Starts.Add(new Int2(56, 82));
                list.Add(new MissionDef
                {
                    Id = "ca08", Faction = CA, Index = 8, Title = "Silent Coast", Location = "Gray Coast", Date = "19 May 1994",
                    Map = map, StartingCredits = 9000, EnemyFaction = ID, Music = "naval",
                    Briefing = "Dominion submarines operating from the Gray Coast are sinking our supply ships. Their dockyards must go.\n\n" +
                               "You have a forward naval yard on a southern islet. Build a fleet, destroy both Dominion dockyards and their coastal guns, then land troops on the northern beach.\n\n" +
                               "Destroyers carry sonar and can detect submerged submarines.",
                    VictoryText = "The Gray Coast is silent. Our convoys sail again.",
                    Tags = { "naval", "amphibious assault" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Dominion Northern Fleet", ID, 1, builds: true, attacks: true, aggression: 0.7f); m.Human.MaxTechTier = 3; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 16, 14, BasePreset.Small, "base");
                        m.Build(m.Human, "ca_shipyard", 24, 26);
                        m.Build(m.Human, "ca_radar", 10, 18);
                        m.Spawn(m.Human, "ca_vigilant", 30, 30); m.Spawn(m.Human, "ca_swift", 32, 28); m.Spawn(m.Human, "ca_swift", 34, 30);
                        m.BuildBase(e, 56, 84, BasePreset.Medium, "ebase");
                        m.Build(e, "id_shipyard", 40, 64, "dockyard"); m.Build(e, "id_shipyard", 74, 64, "dockyard");
                        m.Build(e, "id_avturret", 46, 70, "coastgun"); m.Build(e, "id_avturret", 66, 70, "coastgun"); m.Build(e, "id_artillery", 56, 72, "coastgun");
                        m.Spawn(e, "id_hydra", 50, 56); m.Spawn(e, "id_hydra", 64, 54); m.Spawn(e, "id_pike", 56, 58);
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.Zone("beach", 56, 74, 6);
                        m.AddObjective("docks", "Destroy both Dominion dockyards.");
                        m.AddObjective("guns", "Destroy the coastal guns.");
                        m.AddObjective("land", "Land infantry on the northern beach.", hidden: true);
                        m.AddObjective("ebase", "Destroy the Dominion coastal base.", primary: false);
                        m.When("docks", () => m.AllDead("dockyard"), () => m.Complete("docks"));
                        m.When("guns", () => m.AllDead("coastgun"), () => { m.Complete("guns"); m.Reveal("land"); m.Say("Gen. Marsh", "Guns are down. Get boots on that beach!"); });
                        m.When("land", () => m.IsComplete("guns") && m.CountInZone("beach", m.Human, u => u.Def.Category == UnitCategory.Infantry) >= 3, () => m.Complete("land"));
                        m.When("ebase", () => m.CountBuildings(e) == 0, () => m.Complete("ebase"));
                        m.Every(100, "subs", () => !m.AllDead("dockyard"), () => m.Attack(e, 56, 50, 26, 30, "subs", Scale(m, "id_hydra", "id_pike")));
                    }
                });
            }

            // ============================================================ 9. Iron Marshal — eliminate enemy commander
            {
                var map = Map("ca09", "Drava Valley", 112, 96, MapTheme.Winter, 1909, 0.08f, 0.04f);
                map.Add(Road(V(90, 20), V(70, 40), V(56, 60), V(40, 80), V(14, 90)));
                map.Add(Ridge(3, V(40, 0), V(52, 30))).Add(Ridge(3, V(80, 96), V(76, 64)));
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(20, 20))).Add(Feat(FeatureKind.OreField, 4, V(40, 40)));
                map.Add(Feat(FeatureKind.Lake, 6, V(80, 50)));
                map.Starts.Add(new Int2(16, 14)); map.Starts.Add(new Int2(92, 18));
                list.Add(new MissionDef
                {
                    Id = "ca09", Faction = CA, Index = 9, Title = "Iron Marshal", Location = "Drava Valley", Date = "2 June 1994",
                    Map = map, StartingCredits = 8000, EnemyFaction = ID, Music = "tension",
                    Briefing = "Colonel Ruslan Barr — the butcher of Orlen — is withdrawing his headquarters column up the Drava valley road. If he reaches the extraction airfield in the northwest, he escapes.\n\n" +
                               "Build up quickly and destroy Barr's command tank before he reaches the airfield. He is escorted by heavy armour and will move once he knows we are coming.",
                    VictoryText = "Barr's command tank burns in the snow. The Dominion has lost one of its finest — and cruellest — officers.",
                    Tags = { "eliminate commander", "prevent escape" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Barr's Headquarters", ID, 1, builds: true, attacks: false); m.Human.MaxTechTier = 3; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 16, 14, BasePreset.Small, "base");
                        m.SpawnGroup(m.Human, 20, 22, null, "ca_sabre", "ca_sabre", "ca_lancet", "ca_lancer", "ca_lancer", "ca_rifleman");
                        m.BuildBase(e, 92, 18, BasePreset.Medium, "ebase");
                        m.Spawn(e, "id_behemoth", 88, 24, "barr");
                        m.SpawnGroup(e, 86, 28, "escort", "id_behemoth", "id_ravager", "id_ravager", "id_thresher", "id_ironclad", "id_ironclad");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.Zone("airfield", 14, 90, 6);
                        m.AddObjective("barr", "Destroy Colonel Barr's command tank.");
                        m.AddObjective("escape", "Do not let Barr reach the northwest airfield.");
                        m.AddObjective("base", "Destroy Barr's headquarters base.", primary: false);
                        m.At(2, "intro", () => { m.Say("Maj. Lind", "That's Barr's tank, marked on your display. Don't let him slip away."); m.CameraTo(88, 24, 2); m.Reveal(88, 24, 6, 15); });
                        m.At(6, "back", () => m.CameraTo(16, 14));
                        m.At(150, "depart", () =>
                        {
                            m.Say("Radio Intercept", "(Dominion) ...Headquarters column moving out. Airfield in twelve minutes...");
                            var route = new[] { V(70, 40), V(56, 60), V(40, 80), V(14, 90) };
                            foreach (var ent in m.Tagged("barr"))
                                if (ent is Unit u) { m.W.Commands.Move(new[] { u.Id }, e.Id, route[0]); for (int i = 1; i < route.Length; i++) m.W.Commands.Move(new[] { u.Id }, e.Id, route[i], true); }
                            foreach (var ent in m.Tagged("escort"))
                                if (ent is Unit u) m.W.Commands.Follow(new[] { u.Id }, e.Id, m.Tagged("barr").Count > 0 ? m.Tagged("barr")[0].Id : u.Id);
                        });
                        m.When("dead", () => m.AllDead("barr"), () => { m.Complete("barr"); m.Complete("escape"); });
                        m.When("escaped", () => m.CountInZone("airfield", e, u => u.Tag == "barr") > 0, () => m.Fail("escape"));
                        m.When("base", () => m.CountBuildings(e) == 0, () => m.Complete("base"));
                        m.Every(120, "delay", () => m.Fired("depart"), () => m.Attack(e, 92, 40, 20, 20, "delay", Scale(m, "id_ravager", "id_levy", "id_levy", "id_breacher")));
                    }
                });
            }

            // ============================================================ 10. Thunder Over Kesh — destroy multiple bases
            {
                var map = Map("ca10", "Kesh Foothills", 128, 112, MapTheme.Wasteland, 2010, 0.05f, 0.06f);
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(30, 20))).Add(Feat(FeatureKind.OreField, 4.5f, V(64, 50))).Add(Feat(FeatureKind.CrystalField, 4, V(100, 40))).Add(Feat(FeatureKind.OreField, 4, V(30, 80)));
                map.Add(Ridge(4, V(50, 112), V(60, 74))).Add(Feat(FeatureKind.Mountain, 4, V(80, 60)));
                map.Add(River(3, new[] { 1, 3 }, V(0, 46), V(40, 50), V(80, 44), V(128, 48)));
                map.Starts.Add(new Int2(18, 14)); map.Starts.Add(new Int2(30, 96)); map.Starts.Add(new Int2(106, 92));
                map.Neutrals.Add(new NeutralPlacement("tech_derrick", 64, 30)); map.Neutrals.Add(new NeutralPlacement("tech_hospital", 70, 60));
                list.Add(new MissionDef
                {
                    Id = "ca10", Faction = CA, Index = 10, Title = "Thunder Over Kesh", Location = "Kesh Foothills", Date = "20 June 1994",
                    Map = map, StartingCredits = 10000, EnemyFaction = ID, Music = "battle",
                    Briefing = "We stand at the edge of the Kesh plateau. Two Dominion army bases guard the passes to the heartland: the Western Foundry Group and the Eastern Armour Group.\n\n" +
                               "Destroy both bases. They will support each other — strike one hard enough and the other will send help.",
                    VictoryText = "Both army groups are broken. The passes to the Kesh heartland are open.",
                    Tags = { "destroy multiple bases" },
                    Players = m =>
                    {
                        m.AddHuman();
                        m.AddAI("west", "Western Foundry Group", ID, 1, builds: true, attacks: true);
                        m.AddAI("east", "Eastern Armour Group", ID, 1, builds: true, attacks: true);
                        m.AddNeutral();
                        m.Human.MaxTechTier = 4;
                    },
                    Populate = m =>
                    {
                        m.Spawn(m.Human, "ca_mhq", 18, 14);
                        m.SpawnGroup(m.Human, 22, 18, null, "ca_sabre", "ca_sabre", "ca_sabre", "ca_sentinel", "ca_rifleman", "ca_rifleman", "ca_lancer", "ca_lancer", "ca_engineer");
                        m.BuildBase(m.P("west"), 30, 96, BasePreset.Large, "west");
                        m.BuildBase(m.P("east"), 106, 92, BasePreset.Large, "east");
                    },
                    Script = m =>
                    {
                        m.AddObjective("west", "Destroy the Western Foundry Group base.");
                        m.AddObjective("east", "Destroy the Eastern Armour Group base.");
                        m.When("w", () => m.IsDestroyed(m.P("west")), () => { m.Complete("west"); m.Say("Gen. Marsh", "Western group destroyed. One more."); });
                        m.When("e", () => m.IsDestroyed(m.P("east")), () => { m.Complete("east"); m.Say("Gen. Marsh", "Eastern group destroyed."); });
                    }
                });
            }

            // ============================================================ 11. Hammer and Anvil — destroy superweapon
            {
                var map = Map("ca11", "Hammerfall Site", 120, 120, MapTheme.Winter, 2111, 0.07f, 0.05f);
                var plat = Feat(FeatureKind.Plateau, 16, V(90, 92)); plat.Value = 1; plat.Ramps.Add(V(72, 88)); plat.Ramps.Add(V(90, 74)); plat.Ramps.Add(V(76, 78)); map.Add(plat);
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(24, 24))).Add(Feat(FeatureKind.OreField, 4.5f, V(50, 30))).Add(Feat(FeatureKind.OreField, 4, V(28, 60))).Add(Feat(FeatureKind.CrystalField, 4, V(62, 62)));
                map.Add(Feat(FeatureKind.Lake, 7, V(40, 90))).Add(Feat(FeatureKind.Forest, 6, V(90, 30), V(16, 90)));
                map.Starts.Add(new Int2(18, 16)); map.Starts.Add(new Int2(90, 92));
                list.Add(new MissionDef
                {
                    Id = "ca11", Faction = CA, Index = 11, Title = "Hammer and Anvil", Location = "Hammerfall Launch Site, Kesh plateau", Date = "8 July 1994",
                    Map = map, StartingCredits = 12000, EnemyFaction = ID, Music = "tension",
                    Briefing = "The first Hammerfall missile is fuelled. Satellite imagery shows it aimed at the capital.\n\n" +
                               "You have thirty minutes before launch. Destroy the Hammerfall Launch Complex on the plateau. Be warned: a Scatter Field Generator protects the site from our Orbital Lance, and the complex will fire on your base if it is ready before you reach it.",
                    VictoryText = "The Hammerfall complex is a crater. The capital will never know how close it came.",
                    Tags = { "destroy superweapon", "time limit" },
                    Players = m => { m.AddHuman(); m.AddAI("enemy", "Hammerfall Garrison", ID, 1, builds: true, attacks: true, aggression: 1.2f); m.Human.MaxTechTier = 4; },
                    Populate = m =>
                    {
                        var e = m.P("enemy");
                        m.BuildBase(m.Human, 18, 16, BasePreset.Medium, "base");
                        m.SpawnGroup(m.Human, 22, 26, null, "ca_warden", "ca_sabre", "ca_sabre", "ca_lancet", "ca_sentinel", "ca_arbiter", "ca_lancer", "ca_lancer", "ca_engineer");
                        m.BuildBase(e, 90, 92, BasePreset.Fortress, "fort");
                        m.Build(e, "id_superweapon", 98, 100, "hammerfall");
                    },
                    Script = m =>
                    {
                        var e = m.P("enemy");
                        m.AddObjective("destroy", "Destroy the Hammerfall Launch Complex before launch.");
                        m.AddObjective("fort", "Destroy the entire Hammerfall garrison.", primary: false);
                        m.At(1, "cd", () => { m.Countdown("Hammerfall launch", 1800); m.CameraTo(98, 100, 2); m.Reveal(98, 100, 6, 10); });
                        m.At(5, "back", () => { m.CameraTo(18, 16); m.Say("Gen. Marsh", "Thirty minutes, Commander. There will be no second chance."); });
                        m.When("dead", () => m.AllDead("hammerfall"), () => { m.Complete("destroy"); m.ClearCountdown("Hammerfall launch"); });
                        m.When("launch", () => m.CountdownExpired("Hammerfall launch") && !m.AllDead("hammerfall"), () =>
                        {
                            m.Say("Gen. Marsh", "Launch detected... God help us.");
                            m.W.Powers.AreaDamage(new Vec2(18, 16), 9, 5000, "Super", e.Id, 0, true);
                            m.Fail("destroy");
                        });
                        m.When("fort", () => m.CountBuildings(e) == 0, () => m.Complete("fort"));
                        // Partial strikes: the site's support battery shells the player's base periodically.
                        m.Every(240, "bombard", () => !m.AllDead("hammerfall"), () =>
                        {
                            m.Say("Radar Op", "Incoming bombardment!");
                            for (int i = 0; i < 6; i++) { var p = new Vec2(18, 18) + m.W.Rng.InsideCircle(8); m.W.Powers.AreaDamage(p, 1.6f, 140, "HE", e.Id, 0, false); m.W.Emit(new SimEvent { Type = SimEventType.Explosion, Player = -1, Pos = p, Value = 1.4f }); }
                        });
                    }
                });
            }

            // ============================================================ 12. Ironhold — final offensive
            {
                var map = Map("ca12", "Ironhold", 144, 136, MapTheme.Wasteland, 2212, 0.05f, 0.05f);
                map.Add(Feat(FeatureKind.OreField, 4.5f, V(26, 22))).Add(Feat(FeatureKind.OreField, 4.5f, V(118, 22))).Add(Feat(FeatureKind.OreField, 4.5f, V(72, 50)));
                map.Add(Feat(FeatureKind.CrystalField, 4, V(40, 76))).Add(Feat(FeatureKind.CrystalField, 4, V(104, 76))).Add(Feat(FeatureKind.OreField, 4, V(72, 96)));
                map.Add(River(4, new[] { 1, 2, 3 }, V(0, 64), V(40, 62), V(72, 66), V(104, 62), V(144, 64)));
                map.Add(Road(V(26, 16), V(72, 66), V(72, 118))).Add(Road(V(118, 16), V(72, 66)));
                var plat = Feat(FeatureKind.Plateau, 18, V(72, 116)); plat.Value = 1; plat.Ramps.Add(V(72, 97)); plat.Ramps.Add(V(54, 110)); plat.Ramps.Add(V(90, 110)); map.Add(plat);
                map.Starts.Add(new Int2(24, 16)); map.Starts.Add(new Int2(118, 16)); map.Starts.Add(new Int2(72, 118)); map.Starts.Add(new Int2(30, 110)); map.Starts.Add(new Int2(116, 110));
                list.Add(new MissionDef
                {
                    Id = "ca12", Faction = CA, Index = 12, Title = "Ironhold", Location = "Ironhold, capital of the Iron Dominion", Date = "1 August 1994",
                    Map = map, StartingCredits = 15000, EnemyFaction = ID, Music = "finale",
                    Briefing = "This is it. Ironhold — the foundry city that built every tank that crossed our border.\n\n" +
                               "Together with General Marsh's 1st Army to your east, destroy every Dominion structure in Ironhold. Marshal-Chancellor Strand commands from the fortress on the central plateau. He still has a Hammerfall silo.\n\n" +
                               "End this war, Commander.",
                    VictoryText = "Ironhold has fallen. Marshal-Chancellor Strand has surrendered. The war is over.\n\nVeldmark will rebuild — together.",
                    Tags = { "large-scale offensive", "allied operation", "destroy superweapon" },
                    Players = m =>
                    {
                        m.AddHuman();
                        m.AddAI("ally", "1st Army (Gen. Marsh)", CA, 0, builds: true, attacks: true);
                        m.AddAI("strand", "Ironhold Fortress", ID, 1, builds: true, attacks: true, aggression: 1.2f);
                        m.AddAI("west", "Western Guard", ID, 1, builds: true, attacks: true);
                        m.AddAI("east", "Eastern Guard", ID, 1, builds: true, attacks: true);
                    },
                    Populate = m =>
                    {
                        m.BuildBase(m.Human, 24, 16, BasePreset.Medium, "base");
                        m.SpawnGroup(m.Human, 28, 26, null, "ca_warden", "ca_warden", "ca_sabre", "ca_sabre", "ca_lancet", "ca_sentinel", "ca_thunderhead", "ca_lancer", "ca_lancer", "ca_engineer");
                        m.BuildBase(m.P("ally"), 118, 16, BasePreset.Large, "ally");
                        m.BuildBase(m.P("strand"), 72, 118, BasePreset.Fortress, "strand");
                        m.Build(m.P("strand"), "id_superweapon", 80, 126, "silo");
                        m.BuildBase(m.P("west"), 30, 110, BasePreset.Medium, "west");
                        m.BuildBase(m.P("east"), 116, 110, BasePreset.Medium, "east");
                    },
                    Script = m =>
                    {
                        m.AddObjective("all", "Destroy all Dominion structures in Ironhold.");
                        m.AddObjective("silo", "Destroy the Hammerfall silo.", primary: false);
                        m.AddObjective("ally", "Keep General Marsh's 1st Army in the fight.", primary: false);
                        m.At(3, "intro", () => m.Say("Gen. Marsh", "1st Army is in position. We go together, Commander. For Halvorsen. For everyone."));
                        m.When("silo", () => m.AllDead("silo"), () => m.Complete("silo"));
                        m.When("allylost", () => m.IsDestroyed(m.P("ally")), () => m.Fail("ally"));
                        m.When("all", () => m.IsDestroyed(m.P("strand")) && m.IsDestroyed(m.P("west")) && m.IsDestroyed(m.P("east")), () => { m.Complete("all"); if (!m.IsDestroyed(m.P("ally"))) m.Complete("ally"); });
                    }
                });
            }
            return list;
        }
    }
}
