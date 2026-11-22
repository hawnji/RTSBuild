using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>Registry of all campaign missions plus shared helpers used by mission scripts.</summary>
    public static class CampaignDatabase
    {
        static List<MissionDef> _alliance, _dominion;
        static MissionDef _intro;

        public static List<MissionDef> Alliance { get { if (_alliance == null) _alliance = AllianceCampaign.Build(); return _alliance; } }
        public static List<MissionDef> Dominion { get { if (_dominion == null) _dominion = DominionCampaign.Build(); return _dominion; } }
        public static MissionDef Intro { get { if (_intro == null) _intro = IntroCinematic.Build(); return _intro; } }

        public static List<MissionDef> For(FactionId f) => f == FactionId.Alliance ? Alliance : Dominion;

        public static MissionDef Find(string id)
        {
            if (id == Intro.Id) return Intro;
            foreach (var m in Alliance) if (m.Id == id) return m;
            foreach (var m in Dominion) if (m.Id == id) return m;
            return null;
        }

        public static IEnumerable<MissionDef> All()
        {
            foreach (var m in Alliance) yield return m;
            foreach (var m in Dominion) yield return m;
        }

        public const string Prologue =
            "1994. The continent of Veldmark.\n\n" +
            "For thirty years the Iron Dominion has fed its foundries with the coal and iron of the northern Kesh plateau. " +
            "When surveyors discover aurite — a crystal that stores energy like no fuel before it — beneath the southern republics, " +
            "Marshal-Chancellor Viktor Strand declares the deposits 'a birthright of industry'.\n\n" +
            "The republics answer by forming the Continental Alliance. Their scientists race to finish Helios, an orbital weapon platform. " +
            "The Dominion's engineers race to finish Hammerfall.\n\nOn the morning of the ninth of March, the first shells fall on Halvorsen.";
    }

    /// <summary>Utility functions shared by mission scripts (scaling waves by difficulty, etc.).</summary>
    public static class MissionKit
    {
        static Vec2 V(float x, float y) => new Vec2(x, y);

        public static MapFeature Feat(FeatureKind k, float r, params Vec2[] pts) => MapFeature.Make(k, r, pts);

        public static MapFeature River(float width, int[] bridges, params Vec2[] pts)
        {
            var f = new MapFeature { Kind = FeatureKind.River, Width = width };
            f.Points.AddRange(pts);
            if (bridges != null) f.BridgeAt.AddRange(bridges);
            return f;
        }

        public static MapFeature Road(params Vec2[] pts)
        {
            var f = new MapFeature { Kind = FeatureKind.Road, Width = 1.6f };
            f.Points.AddRange(pts);
            return f;
        }

        public static MapFeature Ridge(float width, params Vec2[] pts)
        {
            var f = new MapFeature { Kind = FeatureKind.Ridge, Width = width };
            f.Points.AddRange(pts);
            return f;
        }

        public static MapFeature Ocean(float x, float y, float dx, float dy) => new MapFeature { Kind = FeatureKind.Ocean, Points = { V(x, y), V(dx, dy) } };

        public static MapSpec Map(string id, string name, int w, int h, MapTheme theme, int seed, float forest = 0.07f, float rough = 0.03f)
            => new MapSpec { Id = id, Name = name, Width = w, Height = h, Theme = theme, Seed = seed, ForestDensity = forest, Roughness = rough, Skirmish = false };

        /// <summary>Scales a wave by difficulty: Easy drops units, Hard/Brutal add extra copies of the first entries.</summary>
        public static string[] Scale(MissionRunner m, params string[] units)
        {
            var l = new List<string>(units);
            switch (m.Difficulty)
            {
                case AIDifficulty.Easy: if (l.Count > 2) l.RemoveRange(l.Count - Math.Max(1, l.Count / 3), Math.Max(1, l.Count / 3)); break;
                case AIDifficulty.Hard: l.Add(units[0]); if (units.Length > 1) l.Add(units[1]); break;
                case AIDifficulty.Brutal: l.Add(units[0]); if (units.Length > 1) l.Add(units[1]); if (units.Length > 2) l.Add(units[2]); l.Add(units[0]); break;
            }
            return l.ToArray();
        }

        /// <summary>Recurring scripted assault waves from a set of spawn points toward a target.</summary>
        public static void Waves(MissionRunner m, string id, Player enemy, float firstAt, float interval, int count, Vec2[] from, Vec2 target, Func<int, string[]> composition, Func<bool> active = null)
        {
            for (int i = 0; i < count; i++)
            {
                int wave = i;
                float t = firstAt + interval * i;
                m.When(id + "_" + i, () => m.W.Time >= t && (active == null || active()), () =>
                {
                    var p = from[wave % from.Length];
                    m.Attack(enemy, p.X, p.Y, target.X, target.Y, "wave", Scale(m, composition(wave)));
                });
            }
        }

        public static string[] Inf(FactionId f, int n)
        {
            var l = new List<string>();
            for (int i = 0; i < n; i++) l.Add(f == FactionId.Alliance ? (i % 4 == 3 ? "ca_lancer" : "ca_rifleman") : (i % 4 == 3 ? "id_breacher" : "id_levy"));
            return l.ToArray();
        }

        public static string[] Mix(params string[][] parts)
        {
            var l = new List<string>();
            foreach (var p in parts) l.AddRange(p);
            return l.ToArray();
        }
    }
}
