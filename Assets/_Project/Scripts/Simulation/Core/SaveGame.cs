using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace RTS.Sim
{
    /// <summary>
    /// Reflection-driven binary serializer for simulation state. Serializes public instance fields that are not marked
    /// [NonSerialized]; supports primitives, enums, strings, nullable values, arrays, lists, dictionaries and nested
    /// classes/structs. Delegates are skipped. Fields are written in name order so layouts are stable between builds.
    /// </summary>
    public static class SimSerializer
    {
        static readonly Dictionary<Type, FieldInfo[]> _fields = new Dictionary<Type, FieldInfo[]>();

        static FieldInfo[] Fields(Type t)
        {
            if (_fields.TryGetValue(t, out var f)) return f;
            var list = new List<FieldInfo>();
            foreach (var fi in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (fi.IsNotSerialized || fi.IsLiteral) continue;
                if (typeof(Delegate).IsAssignableFrom(fi.FieldType)) continue;
                list.Add(fi);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            f = list.ToArray();
            _fields[t] = f;
            return f;
        }

        public static void Write(BinaryWriter w, object o, Type t)
        {
            if (t == typeof(int)) { w.Write((int)o); return; }
            if (t == typeof(float)) { w.Write((float)o); return; }
            if (t == typeof(bool)) { w.Write((bool)o); return; }
            if (t == typeof(uint)) { w.Write((uint)o); return; }
            if (t == typeof(byte)) { w.Write((byte)o); return; }
            if (t == typeof(long)) { w.Write((long)o); return; }
            if (t == typeof(double)) { w.Write((double)o); return; }
            if (t == typeof(short)) { w.Write((short)o); return; }
            if (t.IsEnum) { w.Write(Convert.ToInt32(o)); return; }
            if (t == typeof(string)) { w.Write(o != null); if (o != null) w.Write((string)o); return; }
            var nullable = Nullable.GetUnderlyingType(t);
            if (nullable != null) { w.Write(o != null); if (o != null) Write(w, o, nullable); return; }
            if (!t.IsValueType) { w.Write(o != null); if (o == null) return; }
            if (t.IsArray)
            {
                var a = (Array)o; var et = t.GetElementType();
                w.Write(a.Length);
                if (et == typeof(byte)) { w.Write((byte[])a); return; }
                for (int i = 0; i < a.Length; i++) Write(w, a.GetValue(i), et);
                return;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                var l = (IList)o; var et = t.GetGenericArguments()[0];
                w.Write(l.Count);
                for (int i = 0; i < l.Count; i++) Write(w, l[i], et);
                return;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var d = (IDictionary)o; var ga = t.GetGenericArguments();
                w.Write(d.Count);
                foreach (DictionaryEntry kv in d) { Write(w, kv.Key, ga[0]); Write(w, kv.Value, ga[1]); }
                return;
            }
            foreach (var f in Fields(t)) Write(w, f.GetValue(o), f.FieldType);
        }

        public static object Read(BinaryReader r, Type t, object existing = null)
        {
            if (t == typeof(int)) return r.ReadInt32();
            if (t == typeof(float)) return r.ReadSingle();
            if (t == typeof(bool)) return r.ReadBoolean();
            if (t == typeof(uint)) return r.ReadUInt32();
            if (t == typeof(byte)) return r.ReadByte();
            if (t == typeof(long)) return r.ReadInt64();
            if (t == typeof(double)) return r.ReadDouble();
            if (t == typeof(short)) return r.ReadInt16();
            if (t.IsEnum) return Enum.ToObject(t, r.ReadInt32());
            if (t == typeof(string)) return r.ReadBoolean() ? r.ReadString() : null;
            var nullable = Nullable.GetUnderlyingType(t);
            if (nullable != null) return r.ReadBoolean() ? Read(r, nullable) : null;
            if (!t.IsValueType && !r.ReadBoolean()) return null;
            if (t.IsArray)
            {
                int n = r.ReadInt32(); var et = t.GetElementType();
                if (et == typeof(byte)) return r.ReadBytes(n);
                var a = Array.CreateInstance(et, n);
                for (int i = 0; i < n; i++) a.SetValue(Read(r, et), i);
                return a;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                int n = r.ReadInt32(); var et = t.GetGenericArguments()[0];
                var l = (IList)Activator.CreateInstance(t);
                for (int i = 0; i < n; i++) l.Add(Read(r, et));
                return l;
            }
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                int n = r.ReadInt32(); var ga = t.GetGenericArguments();
                var d = (IDictionary)Activator.CreateInstance(t);
                for (int i = 0; i < n; i++) { var k = Read(r, ga[0]); d[k] = Read(r, ga[1]); }
                return d;
            }
            object o = existing ?? Activator.CreateInstance(t);
            foreach (var f in Fields(t)) f.SetValue(o, Read(r, f.FieldType));
            return o;
        }

        public static T Clone<T>(T o)
        {
            using (var ms = new MemoryStream())
            {
                var w = new BinaryWriter(ms);
                Write(w, o, typeof(T));
                ms.Position = 0;
                return (T)Read(new BinaryReader(ms), typeof(T));
            }
        }

        /// <summary>Copies serializable fields from src into dst (keeps object identity for script closures).</summary>
        public static void CopyInto<T>(T src, T dst) where T : class
        {
            foreach (var f in Fields(typeof(T))) f.SetValue(dst, f.GetValue(src));
        }
    }

    /// <summary>A complete, versioned snapshot of a running match.</summary>
    [Serializable]
    public class WorldSnapshot
    {
        public const int CurrentVersion = 3;
        public int Version = CurrentVersion;
        public string Label;
        public string SavedAt;
        public bool IsMission;
        public string MissionId;
        public AIDifficulty MissionDifficulty;
        public SkirmishConfig Skirmish;
        public GameSettings Settings;
        public int LocalPlayer;
        public int Tick;
        public float Time;
        public int NextId;
        public uint Rng;
        public bool GameOver;
        public int WinningTeam;
        public bool NeutralHostile;
        public List<Player> Players = new List<Player>();
        public List<Unit> Units = new List<Unit>();
        public List<Building> Buildings = new List<Building>();
        public List<Projectile> Projectiles = new List<Projectile>();
        public List<FireZone> Fires = new List<FireZone>();
        public Terrain[] Cells;
        public byte[] Level;
        public int[] Ore;
        public byte[] OreKind;
        public List<int> FogTeams = new List<int>();
        public List<bool[]> FogExplored = new List<bool[]>();
        public List<bool> FogRevealAll = new List<bool>();
        public List<PendingStrike> Strikes = new List<PendingStrike>();
        public List<RevealZone> Reveals = new List<RevealZone>();
        public MissionSaveState Mission;
        public float CameraX, CameraY;
    }

    public static class SaveGame
    {
        const uint Magic = 0x52545331; // "RTS1"

        public static WorldSnapshot Capture(World w, int localPlayer, SkirmishConfig skirmish, AIDifficulty missionDifficulty)
        {
            var s = new WorldSnapshot
            {
                IsMission = w.Mission != null, MissionId = w.Mission?.Def.Id, MissionDifficulty = missionDifficulty,
                Skirmish = skirmish, Settings = w.Settings, LocalPlayer = localPlayer, Tick = w.Tick, Time = w.Time,
                NextId = w.NextId, Rng = w.Rng.State, GameOver = w.GameOver, WinningTeam = w.WinningTeam, NeutralHostile = w.NeutralHostile,
                Cells = w.Map.Cells, Level = w.Map.Level, Ore = w.Map.Ore, OreKind = w.Map.OreKind,
                SavedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            };
            s.Players.AddRange(w.Players);
            foreach (var u in w.Units) if (!u.Dead) s.Units.Add(u);
            foreach (var b in w.Buildings) if (!b.Dead) s.Buildings.Add(b);
            foreach (var p in w.Projectiles) if (!p.Done) s.Projectiles.Add(p);
            s.Fires.AddRange(w.Fires);
            var teams = new HashSet<int>();
            foreach (var p in w.Players) teams.Add(p.Team);
            foreach (var t in teams)
            {
                var f = w.Fog.GetTeam(t);
                s.FogTeams.Add(t); s.FogExplored.Add(f.Explored); s.FogRevealAll.Add(f.RevealAll);
            }
            s.Strikes.AddRange(w.Powers.Pending);
            s.Reveals.AddRange(w.Fog.RevealZones);
            if (w.Mission != null) s.Mission = w.Mission.Capture();
            return s;
        }

        public static byte[] Serialize(WorldSnapshot s)
        {
            using (var ms = new MemoryStream())
            {
                var bw = new BinaryWriter(ms);
                bw.Write(Magic); bw.Write(s.Version);
                SimSerializer.Write(bw, s, typeof(WorldSnapshot));
                return ms.ToArray();
            }
        }

        public static WorldSnapshot Deserialize(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                var br = new BinaryReader(ms);
                if (br.ReadUInt32() != Magic) throw new InvalidDataException("Not a save file.");
                int v = br.ReadInt32();
                if (v != WorldSnapshot.CurrentVersion) throw new InvalidDataException("Save file version " + v + " is not supported (expected " + WorldSnapshot.CurrentVersion + ").");
                return (WorldSnapshot)SimSerializer.Read(br, typeof(WorldSnapshot));
            }
        }

        /// <summary>Rebuilds a playable world from a snapshot.</summary>
        public static World Restore(WorldSnapshot s)
        {
            Catalog.EnsureBuilt();
            World w;
            if (s.IsMission)
            {
                var def = CampaignDatabase.Find(s.MissionId) ?? throw new InvalidDataException("Unknown mission " + s.MissionId);
                w = GameSetup.CreateMission(def, s.MissionDifficulty, true);
            }
            else
            {
                var spec = MapLibrary.Find(s.Skirmish?.MapId) ?? MapLibrary.Skirmish[0];
                w = new World(MapGenerator.Generate(spec), s.Settings);
            }
            w.Settings = s.Settings ?? w.Settings;
            w.Tick = s.Tick; w.Time = s.Time; w.NextId = s.NextId; w.Rng.State = s.Rng;
            w.GameOver = s.GameOver; w.WinningTeam = s.WinningTeam; w.NeutralHostile = s.NeutralHostile;
            if (s.Cells != null && s.Cells.Length == w.Map.Cells.Length)
            {
                Array.Copy(s.Cells, w.Map.Cells, s.Cells.Length);
                Array.Copy(s.Level, w.Map.Level, s.Level.Length);
                Array.Copy(s.Ore, w.Map.Ore, s.Ore.Length);
                Array.Copy(s.OreKind, w.Map.OreKind, s.OreKind.Length);
            }

            // Players: keep instances created by mission scripts so their closures stay valid.
            for (int i = 0; i < s.Players.Count; i++)
            {
                if (i < w.Players.Count) SimSerializer.CopyInto(s.Players[i], w.Players[i]);
                else w.Players.Add(s.Players[i]);
                var p = w.Players[i];
                if (p.Mods == null) p.Mods = new TechModifiers();
                p.Mods.Recompute(p);
                for (int q = 0; q < p.Queues.Length; q++) if (p.Queues[q] == null) p.Queues[q] = new List<ProductionItem>();
            }

            // Clear anything the restoring mission setup spawned.
            foreach (var b in w.Buildings) w.Spatial.RemoveBuilding(b);
            w.Units.Clear(); w.Buildings.Clear(); w.ById.Clear(); w.Projectiles.Clear(); w.Fires.Clear();
            Array.Clear(w.BuildingGrid, 0, w.BuildingGrid.Length);

            foreach (var b in s.Buildings)
            {
                b.Def = Catalog.Building(b.DefId);
                if (b.Def == null) continue;
                foreach (var ws in b.Weapons) ws.Def = Catalog.Weapons[ws.WeaponId];
                w.Buildings.Add(b); w.ById[b.Id] = b;
                for (int y = 0; y < b.H; y++)
                    for (int x = 0; x < b.W; x++)
                        if (w.Map.InBounds(b.Origin.X + x, b.Origin.Y + y)) w.BuildingGrid[w.Map.Index(b.Origin.X + x, b.Origin.Y + y)] = b.Id;
                w.Spatial.AddBuilding(b);
            }
            foreach (var u in s.Units)
            {
                u.Def = Catalog.Unit(u.DefId);
                if (u.Def == null) continue;
                foreach (var ws in u.Weapons) ws.Def = Catalog.Weapons[ws.WeaponId];
                u.Path.Clear(); u.PathPending = false;
                if (u.Moving) u.RepathTimer = 0;
                w.Units.Add(u); w.ById[u.Id] = u;
            }
            foreach (var p in s.Projectiles) { p.Weapon = Catalog.Weapons[p.WeaponId]; w.Projectiles.Add(p); }
            w.Fires.AddRange(s.Fires);
            w.Powers.Pending.Clear(); w.Powers.Pending.AddRange(s.Strikes);
            w.Fog.RevealZones.Clear(); w.Fog.RevealZones.AddRange(s.Reveals);
            for (int i = 0; i < s.FogTeams.Count; i++)
            {
                var f = w.Fog.EnsureTeam(s.FogTeams[i]);
                if (s.FogExplored[i] != null && s.FogExplored[i].Length == f.Explored.Length) Array.Copy(s.FogExplored[i], f.Explored, f.Explored.Length);
                f.RevealAll = s.FogRevealAll[i];
            }
            w.Pathfinder.InvalidateArea(new Int2(0, 0), w.Map.Width, w.Map.Height);
            foreach (var p in w.Players) { w.Economy.RecalculatePlayer(p.Id); w.Powers.RefreshOwnership(p.Id); }

            w.AIs.Clear();
            foreach (var p in w.Players) if (p.IsAI && !p.Defeated) w.AIs.Add(new SkirmishAI(w, p.Id));
            if (s.Mission != null && w.Mission != null) w.Mission.Restore(s.Mission);
            w.Events.Clear();
            w.Spatial.Rebuild(w.Units);
            w.Fog.Update();
            return w;
        }
    }
}
