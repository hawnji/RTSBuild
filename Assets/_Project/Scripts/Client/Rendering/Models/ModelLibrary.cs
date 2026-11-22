using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>Meshes making up one procedural model. Parts are animated independently by EntityView.</summary>
    public class ModelParts
    {
        public Mesh Body, Turret, Rotor, Extra;
        public Vector3 TurretPos, RotorPos, ExtraPos;
        public bool RotorVertical = true;     // spins around Y (helicopter) vs Z (propeller)
        public bool ExtraSpins;               // radar dishes etc.
        public float MuzzleForward = 0.5f;    // distance from turret pivot to muzzle
        public float MuzzleHeight = 0.3f;
        public float Height = 0.5f;           // overall height (health bar placement)
        public bool Infantry;
    }

    /// <summary>
    /// Builds every unit and structure from code. Faction styles: the Continental Alliance uses pale steel with sleek
    /// sloped panels; the Iron Dominion uses dark olive and rust with boxy, riveted shapes. Team colour is baked into
    /// vertex colours (one cached mesh set per model × faction × team colour).
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, ModelParts> _cache = new Dictionary<string, ModelParts>();
        static readonly MeshBuilder B = new MeshBuilder();

        // Style context for the model currently being built.
        static Color Hull, Dark, Team, Glass, Metal, Accent;
        static bool CA;

        static void Style(FactionId f, Color team)
        {
            CA = f != FactionId.Dominion;
            Team = team;
            if (f == FactionId.Alliance) { Hull = new Color(0.66f, 0.69f, 0.72f); Dark = new Color(0.36f, 0.39f, 0.43f); Accent = new Color(0.85f, 0.87f, 0.9f); }
            else if (f == FactionId.Dominion) { Hull = new Color(0.40f, 0.39f, 0.29f); Dark = new Color(0.24f, 0.22f, 0.18f); Accent = new Color(0.55f, 0.32f, 0.18f); }
            else { Hull = new Color(0.70f, 0.64f, 0.55f); Dark = new Color(0.40f, 0.33f, 0.27f); Accent = new Color(0.62f, 0.25f, 0.2f); }
            Glass = new Color(0.25f, 0.45f, 0.6f);
            Metal = new Color(0.28f, 0.28f, 0.30f);
        }

        static Mesh Take(string name) { var m = B.ToMesh(name); B.Clear(); return m; }

        public static ModelParts ForUnit(UnitDef d, Color team, int colorKey)
        {
            string key = "u|" + d.Id + "|" + colorKey;
            if (_cache.TryGetValue(key, out var mp)) return mp;
            Style(d.Faction, team);
            mp = new ModelParts();
            B.Clear();
            BuildUnit(d, mp);
            _cache[key] = mp;
            return mp;
        }

        public static ModelParts ForBuilding(BuildingDef d, Color team, int colorKey, int w, int h)
        {
            string key = "b|" + d.Id + "|" + colorKey + "|" + w + "x" + h;
            if (_cache.TryGetValue(key, out var mp)) return mp;
            Style(d.Faction, team);
            mp = new ModelParts();
            B.Clear();
            BuildBuilding(d, mp, w, h);
            _cache[key] = mp;
            return mp;
        }

        public static void ClearCache() => _cache.Clear();

        // =====================================================================================================
        // Units
        // =====================================================================================================
        static void BuildUnit(UnitDef d, ModelParts mp)
        {
            string m = d.Model ?? "tank_mbt";
            if (m.StartsWith("inf_")) { Infantry(d, m, mp); return; }
            switch (m)
            {
                case "scout": Wheeled(mp, 0.75f, 0.45f, 0.22f, 4); TurretSmall(mp, 0.16f, 0.35f, 0.04f, 0.28f); break;
                case "apc": Wheeled(mp, 0.95f, 0.6f, 0.32f, 6); B.Box(new Vector3(0, 0.42f, -0.1f), new Vector3(0.5f, 0.12f, 0.5f), Dark); mp.Body = Merge(mp.Body, Take("apc")); TurretSmall(mp, 0.14f, 0.3f, 0.035f, 0.52f); break;
                case "convoy_truck": Truck(mp, false); break;
                case "eng_carrier": Truck(mp, true); break;
                case "repair_truck": RepairTruck(mp); break;
                case "tank_mbt": Tracked(mp, 0.95f, 0.62f, 0.26f); TurretTank(mp, 0.42f, 0.38f, 0.16f, 0.055f, 0.55f, 1, 0.34f); break;
                case "tank_heavy": Tracked(mp, 1.15f, 0.78f, 0.30f); TurretTank(mp, 0.55f, 0.5f, 0.2f, 0.065f, 0.62f, 2, 0.4f); break;
                case "td": TankDestroyer(mp); break;
                case "flame_tank": Tracked(mp, 0.95f, 0.64f, 0.26f); FlameTurret(mp); break;
                case "aa_track": Tracked(mp, 0.95f, 0.62f, 0.24f); AATurret(mp); break;
                case "howitzer": Tracked(mp, 1.05f, 0.62f, 0.24f); HowitzerTurret(mp); break;
                case "mlrs": Wheeled(mp, 1.05f, 0.6f, 0.26f, 6); RocketPod(mp); break;
                case "siege": Siege(mp); break;
                case "harvester": Harvester(mp); break;
                case "mcv": Mcv(mp); break;
                case "fighter": Jet(mp, 0.9f, 0.9f, false); break;
                case "bomber": Jet(mp, 1.35f, 1.5f, true); break;
                case "stealth_air": Stealth(mp); break;
                case "recon_air": Jet(mp, 0.8f, 1.0f, false, true); break;
                case "heli_attack": Heli(mp, 0.9f, false); break;
                case "adv_heli": Heli(mp, 1.05f, true); break;
                case "transport_air": TransportHeli(mp); break;
                case "patrol": Ship(mp, 1.1f, 0.42f, 0); break;
                case "destroyer": Ship(mp, 1.7f, 0.55f, 1); break;
                case "cruiser": Ship(mp, 2.1f, 0.65f, 2); break;
                case "battleship": Ship(mp, 2.6f, 0.8f, 3); break;
                case "lst": Lst(mp); break;
                case "tender": Ship(mp, 1.5f, 0.6f, 4); break;
                case "sub": Sub(mp); break;
                default: Tracked(mp, 0.9f, 0.6f, 0.25f); TurretTank(mp, 0.4f, 0.36f, 0.15f, 0.05f, 0.5f, 1, 0.34f); break;
            }
        }

        static void Infantry(UnitDef d, string m, ModelParts mp)
        {
            mp.Infantry = true;
            var uniform = CA ? new Color(0.42f, 0.48f, 0.42f) : new Color(0.38f, 0.33f, 0.24f);
            if (d.Faction == FactionId.Neutral) uniform = new Color(0.45f, 0.38f, 0.3f);
            var skin = new Color(0.85f, 0.68f, 0.52f);
            float s = m == "inf_heavy" || m == "inf_elite" ? 1.12f : 1f;
            B.Push(Vector3.zero, Quaternion.identity, Vector3.one * s);
            // legs
            B.Box(new Vector3(-0.045f, 0.09f, 0), new Vector3(0.06f, 0.18f, 0.07f), uniform.Mul(0.8f));
            B.Box(new Vector3(0.045f, 0.09f, 0), new Vector3(0.06f, 0.18f, 0.07f), uniform.Mul(0.8f));
            // torso: team colour vest
            B.Box(new Vector3(0, 0.27f, 0), new Vector3(0.17f, 0.18f, 0.1f), uniform, Team);
            B.Box(new Vector3(0, 0.28f, 0.01f), new Vector3(0.18f, 0.08f, 0.11f), Team);
            if (m == "inf_heavy") B.Box(new Vector3(0, 0.3f, 0), new Vector3(0.22f, 0.14f, 0.13f), Hull, Team);
            // head + helmet
            B.Box(new Vector3(0, 0.41f, 0), new Vector3(0.08f, 0.08f, 0.08f), skin);
            var helmet = m == "inf_medic" ? Color.white : m == "inf_engineer" ? new Color(0.9f, 0.75f, 0.2f) : uniform.Mul(0.75f);
            B.Box(new Vector3(0, 0.46f, 0), new Vector3(0.1f, 0.04f, 0.1f), helmet);
            // gear
            switch (m)
            {
                case "inf_rifle": B.Box(new Vector3(0.07f, 0.28f, 0.1f), new Vector3(0.025f, 0.03f, 0.2f), Metal); break;
                case "inf_heavy": B.Box(new Vector3(0.09f, 0.28f, 0.1f), new Vector3(0.04f, 0.05f, 0.24f), Metal); break;
                case "inf_at":
                case "inf_aa":
                    B.Barrel(new Vector3(0.08f, 0.38f, -0.12f), 0.03f, 0.32f, m == "inf_aa" ? Dark : Hull); break;
                case "inf_engineer": B.Box(new Vector3(0, 0.27f, -0.08f), new Vector3(0.12f, 0.14f, 0.07f), new Color(0.5f, 0.4f, 0.2f)); break;
                case "inf_medic": B.Box(new Vector3(0, 0.27f, -0.08f), new Vector3(0.12f, 0.12f, 0.06f), Color.white); B.Box(new Vector3(0, 0.28f, -0.115f), new Vector3(0.06f, 0.02f, 0.005f), Color.red); break;
                case "inf_flame":
                    B.Cylinder(new Vector3(0, 0.18f, -0.09f), 0.04f, 0.18f, 6, Accent);
                    B.Box(new Vector3(0.07f, 0.26f, 0.1f), new Vector3(0.035f, 0.035f, 0.18f), Metal); break;
                case "inf_sniper": B.Box(new Vector3(0.07f, 0.29f, 0.14f), new Vector3(0.02f, 0.025f, 0.32f), Dark); break;
                case "inf_elite": B.Box(new Vector3(0.07f, 0.28f, 0.1f), new Vector3(0.03f, 0.035f, 0.22f), Metal); B.Box(new Vector3(0, 0.27f, -0.08f), new Vector3(0.14f, 0.16f, 0.06f), Dark); break;
                case "inf_specialist": B.Box(new Vector3(0.08f, 0.3f, 0.1f), new Vector3(0.05f, 0.05f, 0.18f), new Color(0.3f, 0.6f, 0.9f)); B.Cylinder(new Vector3(0, 0.36f, -0.08f), 0.01f, 0.25f, 4, Metal); break;
            }
            B.Pop();
            mp.Body = Take(m);
            mp.Height = 0.55f * s;
            mp.MuzzleForward = 0.22f; mp.MuzzleHeight = 0.3f;
        }

        // ---------------------------------------------------------------- vehicle chassis
        static void Tracked(ModelParts mp, float len, float wid, float h)
        {
            float tw = wid * 0.24f;
            // tracks
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                float x = sgn * (wid * 0.5f - tw * 0.5f);
                B.Box(new Vector3(x, h * 0.32f, 0), new Vector3(tw, h * 0.64f, len), Metal, Metal.Mul(1.15f));
                for (int i = 0; i < 4; i++) B.Box(new Vector3(x + sgn * tw * 0.52f, h * 0.3f, -len * 0.36f + i * len * 0.24f), new Vector3(0.02f, h * 0.36f, h * 0.36f), Metal.Mul(0.7f));
            }
            // hull
            if (CA)
            {
                B.Taper(new Vector3(0, h * 0.45f, -0.02f), new Vector3(wid * 0.78f, 0, len * 0.92f), new Vector3(wid * 0.62f, 0, len * 0.66f), h * 0.55f, Hull, Hull.Mul(1.08f));
                B.Wedge(new Vector3(0, h * 0.68f, len * 0.4f), new Vector3(wid * 0.62f, h * 0.45f, len * 0.16f), Hull.Mul(0.95f), -1);
            }
            else
            {
                B.Box(new Vector3(0, h * 0.72f, 0), new Vector3(wid * 0.8f, h * 0.55f, len * 0.95f), Hull, Hull.Mul(1.05f));
                B.Box(new Vector3(0, h * 0.6f, len * 0.5f), new Vector3(wid * 0.7f, h * 0.35f, 0.06f), Dark);
                for (int i = 0; i < 3; i++) B.Box(new Vector3(-wid * 0.3f + i * wid * 0.3f, h * 1.0f, -len * 0.35f), new Vector3(0.05f, 0.03f, 0.05f), Accent);
            }
            B.Box(new Vector3(0, h * 1.0f + 0.005f, -len * 0.42f), new Vector3(wid * 0.5f, 0.02f, 0.08f), Team);
            mp.Body = Take("tracked");
            mp.Height = h + 0.25f;
        }

        static void Wheeled(ModelParts mp, float len, float wid, float h, int wheels)
        {
            int per = wheels / 2;
            for (int i = 0; i < per; i++)
                for (int sgn = -1; sgn <= 1; sgn += 2)
                {
                    float z = -len * 0.35f + i * (len * 0.7f / Mathf.Max(1, per - 1));
                    B.Push(new Vector3(sgn * wid * 0.42f, h * 0.38f, z), Quaternion.Euler(0, 0, 90), Vector3.one);
                    B.Cylinder(new Vector3(0, -0.05f, 0), h * 0.38f, 0.1f, 8, Metal.Mul(0.8f), Dark);
                    B.Pop();
                }
            if (CA)
            {
                B.Taper(new Vector3(0, h * 0.5f, 0), new Vector3(wid * 0.78f, 0, len), new Vector3(wid * 0.6f, 0, len * 0.7f), h * 0.6f, Hull);
                B.Box(new Vector3(0, h * 1.12f, len * 0.18f), new Vector3(wid * 0.4f, 0.05f, len * 0.2f), Glass);
            }
            else
            {
                B.Box(new Vector3(0, h * 0.82f, 0), new Vector3(wid * 0.8f, h * 0.6f, len), Hull);
                B.Box(new Vector3(0, h * 1.14f, len * 0.3f), new Vector3(wid * 0.55f, 0.05f, len * 0.15f), Glass.Mul(0.7f));
            }
            B.Box(new Vector3(0, h * 1.13f, -len * 0.3f), new Vector3(wid * 0.45f, 0.02f, 0.1f), Team);
            mp.Body = Take("wheeled");
            mp.Height = h + 0.25f;
        }

        // ---------------------------------------------------------------- turrets
        static void TurretTank(ModelParts mp, float w, float l, float h, float barrelR, float barrelLen, int barrels, float pivotY)
        {
            if (CA) B.Taper(Vector3.zero, new Vector3(w, 0, l), new Vector3(w * 0.75f, 0, l * 0.7f), h, Hull.Mul(1.05f), Hull.Mul(1.12f));
            else { B.Box(new Vector3(0, h * 0.5f, -0.02f), new Vector3(w, h, l), Hull.Mul(1.02f)); B.Box(new Vector3(0, h + 0.02f, -l * 0.15f), new Vector3(w * 0.35f, 0.04f, w * 0.35f), Dark); }
            B.Box(new Vector3(0, h + 0.005f, -l * 0.3f), new Vector3(w * 0.4f, 0.01f, l * 0.2f), Team);
            for (int i = 0; i < barrels; i++)
            {
                float x = barrels == 1 ? 0 : (i == 0 ? -1 : 1) * barrelR * 1.6f;
                B.Barrel(new Vector3(x, h * 0.5f, l * 0.35f), barrelR, barrelLen, Metal);
            }
            mp.Turret = Take("turret"); mp.TurretPos = new Vector3(0, pivotY, -0.05f);
            mp.MuzzleForward = l * 0.35f + barrelLen; mp.MuzzleHeight = pivotY + h * 0.5f;
        }

        static void TurretSmall(ModelParts mp, float s, float barrelLen, float barrelR, float pivotY)
        {
            B.Cylinder(Vector3.zero, s, s * 0.6f, 6, Dark);
            B.Barrel(new Vector3(0, s * 0.35f, s * 0.6f), barrelR, barrelLen, Metal);
            mp.Turret = Take("turret_s"); mp.TurretPos = new Vector3(0, pivotY, 0);
            mp.MuzzleForward = s * 0.6f + barrelLen; mp.MuzzleHeight = pivotY + s * 0.35f;
        }

        static void TankDestroyer(ModelParts mp)
        {
            Tracked(mp, 1.0f, 0.62f, 0.24f);
            // casemate style: the "turret" is a low fixed superstructure that still rotates for gameplay readability
            B.Wedge(new Vector3(0, 0.1f, 0.05f), new Vector3(0.48f, 0.2f, 0.55f), Hull.Mul(1.05f), -0.3f);
            if (CA) { B.Box(new Vector3(0, 0.12f, 0.3f), new Vector3(0.12f, 0.08f, 0.2f), Dark); B.Barrel(new Vector3(0, 0.12f, 0.35f), 0.035f, 0.7f, new Color(0.3f, 0.5f, 0.8f)); }
            else B.Barrel(new Vector3(0, 0.1f, 0.3f), 0.06f, 0.6f, Metal);
            mp.Turret = Take("td"); mp.TurretPos = new Vector3(0, 0.3f, -0.05f); mp.MuzzleForward = 1.0f; mp.MuzzleHeight = 0.42f;
        }

        static void FlameTurret(ModelParts mp)
        {
            B.Box(new Vector3(0, 0.08f, 0), new Vector3(0.36f, 0.16f, 0.36f), Hull);
            B.Cylinder(new Vector3(-0.1f, 0.0f, -0.3f), 0.08f, 0.2f, 6, Accent);
            B.Cylinder(new Vector3(0.1f, 0.0f, -0.3f), 0.08f, 0.2f, 6, Accent);
            B.Barrel(new Vector3(0, 0.08f, 0.15f), 0.05f, 0.35f, Metal);
            mp.Turret = Take("flame"); mp.TurretPos = new Vector3(0, 0.3f, 0); mp.MuzzleForward = 0.5f; mp.MuzzleHeight = 0.38f;
        }

        static void AATurret(ModelParts mp)
        {
            B.Cylinder(Vector3.zero, 0.2f, 0.12f, 8, Hull.Mul(1.05f));
            B.Push(new Vector3(0, 0.15f, 0), Quaternion.Euler(-35, 0, 0), Vector3.one);
            if (CA) { B.Box(new Vector3(-0.14f, 0, 0.05f), new Vector3(0.1f, 0.12f, 0.3f), Dark); B.Box(new Vector3(0.14f, 0, 0.05f), new Vector3(0.1f, 0.12f, 0.3f), Dark); }
            else { for (int i = 0; i < 4; i++) B.Barrel(new Vector3(-0.12f + i * 0.08f, 0, 0), 0.025f, 0.45f, Metal); }
            B.Pop();
            mp.Turret = Take("aa"); mp.TurretPos = new Vector3(0, 0.28f, 0); mp.MuzzleForward = 0.35f; mp.MuzzleHeight = 0.55f;
        }

        static void HowitzerTurret(ModelParts mp)
        {
            B.Box(new Vector3(0, 0.1f, -0.1f), new Vector3(0.48f, 0.2f, 0.5f), Hull.Mul(1.03f), Hull.Mul(1.1f));
            B.Box(new Vector3(0, 0.205f, -0.2f), new Vector3(0.2f, 0.01f, 0.15f), Team);
            B.Push(new Vector3(0, 0.12f, 0.12f), Quaternion.Euler(-18, 0, 0), Vector3.one);
            B.Barrel(Vector3.zero, 0.06f, 0.85f, Metal);
            B.Pop();
            mp.Turret = Take("howitzer"); mp.TurretPos = new Vector3(0, 0.28f, -0.05f); mp.MuzzleForward = 0.95f; mp.MuzzleHeight = 0.65f;
        }

        static void RocketPod(ModelParts mp)
        {
            B.Push(Vector3.zero, Quaternion.Euler(-25, 0, 0), Vector3.one);
            B.Box(new Vector3(0, 0.12f, 0), new Vector3(0.45f, 0.22f, 0.5f), Dark);
            for (int r = 0; r < 2; r++) for (int c = 0; c < 4; c++) B.Box(new Vector3(-0.15f + c * 0.1f, 0.06f + r * 0.11f, 0.255f), new Vector3(0.06f, 0.06f, 0.01f), Team.Mul(0.7f));
            B.Pop();
            mp.Turret = Take("mlrs"); mp.TurretPos = new Vector3(0, 0.36f, -0.15f); mp.MuzzleForward = 0.3f; mp.MuzzleHeight = 0.55f;
        }

        static void Siege(ModelParts mp)
        {
            Tracked(mp, 1.45f, 0.95f, 0.34f);
            if (CA)
            {
                B.Box(new Vector3(0, 0.15f, -0.1f), new Vector3(0.65f, 0.3f, 0.7f), Hull);
                B.Box(new Vector3(-0.12f, 0.2f, 0.45f), new Vector3(0.1f, 0.1f, 0.9f), new Color(0.35f, 0.55f, 0.85f));
                B.Box(new Vector3(0.12f, 0.2f, 0.45f), new Vector3(0.1f, 0.1f, 0.9f), new Color(0.35f, 0.55f, 0.85f));
            }
            else
            {
                B.Box(new Vector3(0, 0.18f, -0.1f), new Vector3(0.75f, 0.36f, 0.75f), Hull);
                B.Push(new Vector3(0, 0.22f, 0.2f), Quaternion.Euler(-15, 0, 0), Vector3.one);
                B.Barrel(Vector3.zero, 0.11f, 1.1f, Metal, 8);
                B.Pop();
            }
            B.Box(new Vector3(0, 0.31f, -0.3f), new Vector3(0.3f, 0.01f, 0.2f), Team);
            mp.Turret = Take("siege"); mp.TurretPos = new Vector3(0, 0.42f, -0.1f); mp.MuzzleForward = 1.2f; mp.MuzzleHeight = 0.75f;
        }

        // ---------------------------------------------------------------- utility vehicles
        static void Truck(ModelParts mp, bool engineer)
        {
            Wheeled(mp, 1.0f, 0.56f, 0.26f, 6);
            B.Box(new Vector3(0, 0.42f, -0.12f), new Vector3(0.52f, 0.34f, 0.6f), engineer ? Accent : new Color(0.45f, 0.42f, 0.32f));
            B.Box(new Vector3(0, 0.6f, -0.12f), new Vector3(0.54f, 0.02f, 0.62f), Team);
            if (engineer) { B.Box(new Vector3(0, 0.66f, -0.3f), new Vector3(0.08f, 0.1f, 0.4f), Metal); }
            mp.Body = Merge(mp.Body, Take("truck"));
        }

        static void RepairTruck(ModelParts mp)
        {
            Wheeled(mp, 0.95f, 0.56f, 0.26f, 4);
            B.Box(new Vector3(0, 0.4f, -0.15f), new Vector3(0.48f, 0.26f, 0.45f), Hull);
            B.Push(new Vector3(0, 0.55f, -0.2f), Quaternion.Euler(-40, 0, 0), Vector3.one);
            B.Box(new Vector3(0, 0, 0.2f), new Vector3(0.06f, 0.06f, 0.45f), new Color(0.9f, 0.75f, 0.2f));
            B.Pop();
            mp.Body = Merge(mp.Body, Take("repair"));
        }

        static void Harvester(ModelParts mp)
        {
            Tracked(mp, 1.15f, 0.8f, 0.3f);
            B.Box(new Vector3(0, 0.5f, -0.15f), new Vector3(0.7f, 0.4f, 0.7f), CA ? Hull : new Color(0.55f, 0.45f, 0.25f), Team);
            B.Box(new Vector3(0, 0.48f, 0.38f), new Vector3(0.4f, 0.3f, 0.25f), Dark);
            B.Box(new Vector3(0, 0.6f, 0.38f), new Vector3(0.3f, 0.06f, 0.2f), Glass);
            // front scoop
            B.Wedge(new Vector3(0, 0.15f, 0.6f), new Vector3(0.75f, 0.18f, 0.2f), Metal, -1);
            mp.Body = Merge(mp.Body, Take("harv"));
            mp.Height = 0.8f;
        }

        static void Mcv(ModelParts mp)
        {
            Tracked(mp, 1.5f, 0.9f, 0.32f);
            B.Box(new Vector3(0, 0.58f, -0.15f), new Vector3(0.8f, 0.42f, 0.95f), Hull, Hull.Mul(1.1f));
            B.Box(new Vector3(0, 0.8f, -0.15f), new Vector3(0.6f, 0.02f, 0.4f), Team);
            B.Box(new Vector3(0, 0.6f, 0.5f), new Vector3(0.6f, 0.32f, 0.3f), Dark);
            B.Box(new Vector3(0, 0.7f, 0.66f), new Vector3(0.45f, 0.1f, 0.02f), Glass);
            B.Cylinder(new Vector3(0.25f, 0.8f, -0.4f), 0.03f, 0.5f, 4, Metal);
            B.Push(new Vector3(-0.2f, 0.82f, -0.1f), 30f);
            B.Box(new Vector3(0, 0.05f, 0.25f), new Vector3(0.08f, 0.08f, 0.6f), new Color(0.9f, 0.72f, 0.15f));
            B.Pop();
            mp.Body = Merge(mp.Body, Take("mcv"));
            mp.Height = 1.0f;
        }

        // ---------------------------------------------------------------- aircraft
        static void Jet(ModelParts mp, float len, float span, bool bomber, bool recon = false)
        {
            float fw = bomber ? 0.22f : 0.14f;
            B.Taper(new Vector3(0, -fw * 0.5f, 0), new Vector3(fw * 1.2f, 0, len), new Vector3(fw, 0, len * 0.8f), fw, Hull);
            B.Cone(new Vector3(0, 0, len * 0.5f), fw * 0.55f, 0.01f, 6, Hull);
            B.Push(new Vector3(0, 0, len * 0.5f), Quaternion.Euler(90, 0, 0), Vector3.one);
            B.Cone(Vector3.zero, fw * 0.55f, len * 0.25f, 6, Hull.Mul(1.05f));
            B.Pop();
            B.Box(new Vector3(0, fw * 0.55f, len * 0.22f), new Vector3(fw * 0.6f, fw * 0.4f, len * 0.22f), Glass);
            // wings
            float sweep = bomber ? 0.1f : 0.25f;
            var wingCol = CA ? Hull.Mul(0.95f) : Hull.Mul(0.9f);
            B.Quad(new Vector3(0, 0, len * 0.15f), new Vector3(-span * 0.5f, 0, -len * sweep), new Vector3(-span * 0.5f, 0, -len * (sweep + 0.12f)), new Vector3(0, 0, -len * 0.2f), wingCol);
            B.Quad(new Vector3(0, 0, len * 0.15f), new Vector3(0, 0, -len * 0.2f), new Vector3(span * 0.5f, 0, -len * (sweep + 0.12f)), new Vector3(span * 0.5f, 0, -len * sweep), wingCol);
            B.Quad(new Vector3(0, -0.01f, len * 0.15f), new Vector3(0, -0.01f, -len * 0.2f), new Vector3(-span * 0.5f, -0.01f, -len * (sweep + 0.12f)), new Vector3(-span * 0.5f, -0.01f, -len * sweep), wingCol.Mul(0.7f));
            B.Quad(new Vector3(0, -0.01f, len * 0.15f), new Vector3(span * 0.5f, -0.01f, -len * sweep), new Vector3(span * 0.5f, -0.01f, -len * (sweep + 0.12f)), new Vector3(0, -0.01f, -len * 0.2f), wingCol.Mul(0.7f));
            // wingtip team colour
            B.Box(new Vector3(-span * 0.47f, 0.01f, -len * (sweep + 0.06f)), new Vector3(span * 0.06f, 0.02f, len * 0.1f), Team);
            B.Box(new Vector3(span * 0.47f, 0.01f, -len * (sweep + 0.06f)), new Vector3(span * 0.06f, 0.02f, len * 0.1f), Team);
            // tail
            B.Wedge(new Vector3(0, fw * 0.9f, -len * 0.38f), new Vector3(0.02f, fw * 1.4f, len * 0.2f), Hull.Mul(0.9f), -0.6f);
            if (bomber)
                for (int sgn = -1; sgn <= 1; sgn += 2) B.Barrel(new Vector3(sgn * span * 0.22f, -0.06f, -len * 0.1f), 0.06f, 0.35f, Dark);
            if (recon) B.Sphere(new Vector3(0, -fw * 0.6f, 0), fw * 0.5f, Dark, 2, 6);
            mp.Body = Take("jet");
            mp.Height = 0.3f;
            mp.MuzzleForward = len * 0.5f; mp.MuzzleHeight = 0;
        }

        static void Stealth(ModelParts mp)
        {
            var c = new Color(0.18f, 0.19f, 0.22f);
            B.Tri(new Vector3(0, 0.08f, 0.6f), new Vector3(0.75f, 0, -0.45f), new Vector3(-0.75f, 0, -0.45f), c);
            B.Tri(new Vector3(0, 0.08f, 0.6f), new Vector3(-0.75f, 0, -0.45f), new Vector3(0, 0.12f, -0.3f), c.Mul(0.9f));
            B.Tri(new Vector3(0, 0.08f, 0.6f), new Vector3(0, 0.12f, -0.3f), new Vector3(0.75f, 0, -0.45f), c.Mul(1.1f));
            B.Tri(new Vector3(0, 0.08f, 0.6f), new Vector3(-0.75f, 0, -0.45f), new Vector3(0.75f, 0, -0.45f), c.Mul(0.6f));
            B.Box(new Vector3(0, 0.1f, 0.25f), new Vector3(0.12f, 0.04f, 0.18f), Glass.Mul(0.6f));
            B.Box(new Vector3(0, 0.05f, -0.42f), new Vector3(0.3f, 0.02f, 0.05f), Team);
            mp.Body = Take("stealth"); mp.Height = 0.3f; mp.MuzzleForward = 0.5f; mp.MuzzleHeight = 0;
        }

        static void Heli(ModelParts mp, float len, bool advanced)
        {
            B.Taper(new Vector3(0, -0.12f, 0.05f), new Vector3(0.28f, 0, len * 0.55f), new Vector3(0.22f, 0, len * 0.4f), 0.26f, Hull);
            B.Box(new Vector3(0, 0.02f, len * 0.3f), new Vector3(0.18f, 0.16f, 0.2f), Glass);
            B.Box(new Vector3(0, 0f, -len * 0.4f), new Vector3(0.07f, 0.07f, len * 0.55f), Hull.Mul(0.95f));
            B.Box(new Vector3(0.05f, 0.08f, -len * 0.68f), new Vector3(0.02f, 0.22f, 0.12f), Team);
            // stub wings with pods
            B.Box(new Vector3(0, -0.04f, 0), new Vector3(0.6f, 0.03f, 0.12f), Dark);
            for (int sgn = -1; sgn <= 1; sgn += 2) B.Barrel(new Vector3(sgn * 0.26f, -0.08f, -0.1f), 0.04f, 0.28f, advanced ? Team.Mul(0.6f) : Metal);
            B.Box(new Vector3(0, -0.2f, 0.32f), new Vector3(0.04f, 0.04f, 0.14f), Metal);
            mp.Body = Take("heli");
            // main rotor
            for (int i = 0; i < (advanced ? 4 : 2); i++)
            {
                B.Push(Vector3.zero, i * (advanced ? 45f : 90f));
                B.Box(Vector3.zero, new Vector3(0.05f, 0.01f, len * 1.3f), Dark);
                B.Pop();
            }
            B.Cylinder(new Vector3(0, -0.05f, 0), 0.03f, 0.08f, 5, Metal);
            mp.Rotor = Take("rotor"); mp.RotorPos = new Vector3(0, 0.2f, 0.02f);
            mp.Height = 0.35f; mp.MuzzleForward = len * 0.45f; mp.MuzzleHeight = -0.15f;
        }

        static void TransportHeli(ModelParts mp)
        {
            B.Box(new Vector3(0, 0, 0), new Vector3(0.38f, 0.32f, 1.2f), Hull, Hull.Mul(1.08f));
            B.Box(new Vector3(0, 0.05f, 0.62f), new Vector3(0.3f, 0.2f, 0.06f), Glass);
            B.Box(new Vector3(0, 0.2f, 0.45f), new Vector3(0.18f, 0.12f, 0.2f), Dark);
            B.Box(new Vector3(0, 0.2f, -0.45f), new Vector3(0.18f, 0.18f, 0.2f), Dark);
            B.Box(new Vector3(0, 0.165f, 0), new Vector3(0.2f, 0.01f, 0.4f), Team);
            mp.Body = Take("transport");
            for (int k = 0; k < 2; k++)
            {
                float z = k == 0 ? 0.45f : -0.45f;
                for (int i = 0; i < 3; i++) { B.Push(new Vector3(0, 0, z), i * 60f + k * 30f); B.Box(Vector3.zero, new Vector3(0.04f, 0.01f, 1.0f), Dark); B.Pop(); }
            }
            mp.Rotor = Take("rotor2"); mp.RotorPos = new Vector3(0, 0.3f, 0);
            mp.Height = 0.5f; mp.MuzzleForward = 0.6f; mp.MuzzleHeight = 0;
        }

        // ---------------------------------------------------------------- naval
        static void Hull3(float len, float wid, float h, Color c)
        {
            // pointed bow hull
            float hl = len * 0.5f, hw = wid * 0.5f;
            Vector3 bow = new Vector3(0, h, hl), bowLow = new Vector3(0, 0, hl * 0.9f);
            Vector3 lf = new Vector3(-hw, h, hl * 0.35f), rf = new Vector3(hw, h, hl * 0.35f), lb = new Vector3(-hw * 0.9f, h, -hl), rb = new Vector3(hw * 0.9f, h, -hl);
            Vector3 lfl = new Vector3(-hw * 0.8f, 0, hl * 0.3f), rfl = new Vector3(hw * 0.8f, 0, hl * 0.3f), lbl = new Vector3(-hw * 0.75f, 0, -hl), rbl = new Vector3(hw * 0.75f, 0, -hl);
            B.Quad(lb, lf, rf, rb, Hull.Mul(0.9f)); // deck
            B.Tri(lf, bow, rf, Hull.Mul(0.9f));
            B.Quad(lbl, lb, lf, lfl, c.Mul(0.85f));
            B.Quad(rfl, rf, rb, rbl, c.Mul(0.85f));
            B.Quad(lfl, lf, bow, bowLow, c);
            B.Quad(bowLow, bow, rf, rfl, c);
            B.Quad(rbl, rb, lb, lbl, c.Mul(0.8f));
        }

        static void Ship(ModelParts mp, float len, float wid, int cls)
        {
            Hull3(len, wid, 0.18f, CA ? new Color(0.45f, 0.5f, 0.55f) : new Color(0.32f, 0.32f, 0.3f));
            B.Box(new Vector3(0, 0.181f, -len * 0.3f), new Vector3(wid * 0.5f, 0.01f, len * 0.12f), Team);
            float bridgeH = 0.18f + cls * 0.05f;
            B.Box(new Vector3(0, 0.18f + bridgeH * 0.5f, -len * 0.08f), new Vector3(wid * 0.5f, bridgeH, len * 0.22f), Hull);
            B.Box(new Vector3(0, 0.18f + bridgeH + 0.04f, -len * 0.04f), new Vector3(wid * 0.35f, 0.08f, len * 0.1f), Glass.Mul(0.8f));
            B.Cylinder(new Vector3(0, 0.18f + bridgeH, -len * 0.15f), 0.015f, 0.3f + cls * 0.06f, 4, Metal);
            if (cls >= 2) B.Cylinder(new Vector3(0, 0.18f + bridgeH, -len * 0.25f), 0.07f, 0.15f, 6, Dark);
            if (cls == 4) { B.Box(new Vector3(0, 0.26f, len * 0.22f), new Vector3(wid * 0.6f, 0.15f, len * 0.25f), new Color(0.9f, 0.9f, 0.9f)); B.Box(new Vector3(0, 0.34f, len * 0.22f), new Vector3(0.18f, 0.01f, 0.06f), Color.red); }
            mp.Body = Take("ship");
            // main turret (forward)
            float ts = 0.12f + cls * 0.03f;
            B.Box(new Vector3(0, ts * 0.4f, 0), new Vector3(ts * 1.6f, ts * 0.8f, ts * 1.8f), Hull.Mul(1.05f));
            int barrels = cls == 0 ? 1 : cls >= 3 ? 3 : 2;
            if (cls == 4) barrels = 1;
            for (int i = 0; i < barrels; i++) B.Barrel(new Vector3((i - (barrels - 1) * 0.5f) * ts * 0.45f, ts * 0.45f, ts * 0.8f), ts * 0.14f, ts * 2.4f, Metal);
            mp.Turret = Take("shipturret"); mp.TurretPos = new Vector3(0, 0.19f, len * 0.22f);
            mp.MuzzleForward = ts * 3.2f; mp.MuzzleHeight = 0.19f + ts * 0.45f;
            if (cls >= 2)
            {
                B.Box(new Vector3(0, ts * 0.35f, 0), new Vector3(ts * 1.4f, ts * 0.7f, ts * 1.6f), Hull.Mul(1.05f));
                for (int i = 0; i < 2; i++) B.Barrel(new Vector3((i - 0.5f) * ts * 0.45f, ts * 0.4f, ts * 0.7f), ts * 0.12f, ts * 2.0f, Metal);
                mp.Extra = Take("shipturret2"); mp.ExtraPos = new Vector3(0, 0.19f, -len * 0.36f);
            }
            mp.Height = 0.5f + cls * 0.08f;
        }

        static void Lst(ModelParts mp)
        {
            B.Box(new Vector3(0, 0.1f, 0), new Vector3(0.75f, 0.2f, 1.6f), CA ? new Color(0.45f, 0.5f, 0.55f) : new Color(0.32f, 0.32f, 0.3f), Hull.Mul(0.8f));
            B.Box(new Vector3(0, 0.25f, 0.82f), new Vector3(0.7f, 0.3f, 0.05f), Dark);
            B.Box(new Vector3(0, 0.32f, -0.6f), new Vector3(0.5f, 0.25f, 0.3f), Hull);
            B.Box(new Vector3(0, 0.201f, 0.1f), new Vector3(0.4f, 0.01f, 0.4f), Team);
            mp.Body = Take("lst"); mp.Height = 0.6f;
        }

        static void Sub(ModelParts mp)
        {
            var c = new Color(0.16f, 0.18f, 0.2f);
            B.Push(Vector3.zero, Quaternion.Euler(90, 0, 0), new Vector3(1, 1, 0.55f));
            B.Cylinder(new Vector3(0, -0.7f, 0), 0.2f, 1.4f, 8, c, c, -1, true);
            B.Pop();
            B.Push(new Vector3(0, 0, 0.7f), Quaternion.Euler(90, 0, 0), new Vector3(1, 1, 0.55f));
            B.Cone(Vector3.zero, 0.2f, 0.25f, 8, c);
            B.Pop();
            B.Box(new Vector3(0, 0.2f, 0.15f), new Vector3(0.1f, 0.22f, 0.3f), c.Mul(1.2f));
            B.Box(new Vector3(0, 0.315f, 0.15f), new Vector3(0.11f, 0.01f, 0.2f), Team);
            mp.Body = Take("sub"); mp.Height = 0.4f; mp.MuzzleForward = 0.8f; mp.MuzzleHeight = 0;
        }

        static Mesh Merge(Mesh a, Mesh b)
        {
            var mb = new MeshBuilder();
            foreach (var m in new[] { a, b })
            {
                if (m == null) continue;
                int off = mb.V.Count;
                var v = m.vertices; var n = m.normals; var c = m.colors32; var t = m.triangles;
                mb.V.AddRange(v); mb.N.AddRange(n); mb.C.AddRange(c);
                foreach (var i in t) mb.T.Add(i + off);
            }
            return mb.ToMesh("merged");
        }

        // =====================================================================================================
        // Buildings (pivot at footprint centre, ground level; +Z = map "south" (increasing y))
        // =====================================================================================================
        static void BuildBuilding(BuildingDef d, ModelParts mp, int w, int h)
        {
            float W = w, H = h;
            string m = d.Model ?? d.Role.ToString();
            bool rotated = d.Width != d.Height && w == d.Height && h == d.Width;
            if (!m.StartsWith("civ_") && !m.StartsWith("tech_") && m != "wall" && m != "gate")
                Foundation(W, H);
            mp.Height = 1.2f;
            switch (m)
            {
                case "ConstructionYard": ConYard(W, H, mp); break;
                case "Power": PowerPlant(W, H, false); break;
                case "AdvancedPower": PowerPlant(W, H, true); break;
                case "Refinery": Refinery(W, H); break;
                case "Silo": Silo(W, H); break;
                case "Barracks": Barracks(W, H, false); break;
                case "AdvancedBarracks": Barracks(W, H, true); break;
                case "Factory": Factory(W, H, false); break;
                case "AdvancedFactory": Factory(W, H, true); break;
                case "RepairDepot": RepairDepot(W, H, mp); break;
                case "Hangar": Hangar(W, H, d.Docks, false); break;
                case "AdvancedHangar": Hangar(W, H, d.Docks, true); break;
                case "Shipyard": Shipyard(W, H, false); break;
                case "AdvancedShipyard": Shipyard(W, H, true); break;
                case "TechCenter": TechCenter(W, H); break;
                case "ResearchLab": Lab(W, H); break;
                case "Radar": Radar(W, H, mp); break;
                case "CommCenter": Comm(W, H, mp); break;
                case "Superweapon": Superweapon(W, H, mp); break;
                case "SpecialPower": SpecialPower(W, H, mp); break;
                case "Countermeasure": Countermeasure(W, H, mp); break;
                case "mgbunker": Bunker(W, H, mp); break;
                case "aiturret": Turret(mp, 0); break;
                case "avturret": Turret(mp, 1); break;
                case "aadefense": Turret(mp, 2); break;
                case "artillery": Turret(mp, 3); break;
                case "wall": Wall(W, H); break;
                case "gate": Gate(W, H, mp, rotated); break;
                case "civ_house": House(W, H, new Color(0.85f, 0.8f, 0.68f), new Color(0.55f, 0.25f, 0.2f)); break;
                case "civ_barn": House(W, H, new Color(0.6f, 0.22f, 0.18f), new Color(0.35f, 0.3f, 0.28f)); break;
                case "civ_chapel": Chapel(W, H); break;
                case "civ_watertower": WaterTower(); break;
                case "tech_derrick": Derrick(W, H, mp); break;
                case "tech_hospital": Hospital(W, H); break;
                case "tech_relay": Relay(W, H, mp); break;
                case "tech_depot": Depot(W, H); break;
                default: B.Box(new Vector3(0, 0.4f, 0), new Vector3(W * 0.8f, 0.8f, H * 0.8f), Hull, Team); break;
            }
            if (B.Count > 0) mp.Body = Take(m);
            if (mp.Body == null) mp.Body = new Mesh();
        }

        static void Foundation(float W, float H)
        {
            var c = CA ? new Color(0.5f, 0.52f, 0.54f) : new Color(0.36f, 0.34f, 0.3f);
            B.Box(new Vector3(0, 0.04f, 0), new Vector3(W * 0.96f, 0.1f, H * 0.96f), c, c.Mul(1.05f));
        }

        static void TeamStripe(Vector3 c, float w, float d) => B.Box(c, new Vector3(w, 0.03f, d), Team);

        static void ConYard(float W, float H, ModelParts mp)
        {
            B.Box(new Vector3(-W * 0.12f, 0.45f, H * 0.05f), new Vector3(W * 0.6f, 0.7f, H * 0.55f), Hull, Hull.Mul(1.1f));
            B.Box(new Vector3(W * 0.25f, 0.75f, -H * 0.2f), new Vector3(W * 0.28f, 1.3f, H * 0.28f), Dark);
            B.Box(new Vector3(W * 0.25f, 1.25f, -H * 0.06f), new Vector3(W * 0.24f, 0.2f, 0.02f), Glass);
            TeamStripe(new Vector3(-W * 0.12f, 0.81f, H * 0.05f), W * 0.5f, H * 0.1f);
            B.Box(new Vector3(-W * 0.12f, 0.4f, H * 0.33f), new Vector3(W * 0.35f, 0.5f, 0.02f), Metal);
            mp.Body = Take("cy");
            // crane on Extra (slow swing)
            B.Cylinder(Vector3.zero, 0.08f, 1.4f, 6, new Color(0.9f, 0.72f, 0.15f));
            B.Box(new Vector3(0, 1.42f, 0.5f), new Vector3(0.1f, 0.1f, 1.4f), new Color(0.9f, 0.72f, 0.15f));
            B.Box(new Vector3(0, 1.42f, -0.3f), new Vector3(0.22f, 0.2f, 0.25f), Dark);
            B.Cylinder(new Vector3(0, 0.8f, 1.1f), 0.01f, 0.6f, 3, Metal);
            mp.Extra = Take("crane"); mp.ExtraPos = new Vector3(W * 0.32f, 0.1f, H * 0.3f); mp.ExtraSpins = true;
            mp.Height = 1.6f;
        }

        static void PowerPlant(float W, float H, bool adv)
        {
            if (CA)
            {
                B.Box(new Vector3(0, 0.3f, H * 0.2f), new Vector3(W * 0.75f, 0.45f, H * 0.4f), Hull);
                int n = adv ? 2 : 1;
                for (int i = 0; i < n; i++)
                {
                    float x = n == 1 ? 0 : (i == 0 ? -W * 0.22f : W * 0.22f);
                    B.Cylinder(new Vector3(x, 0.1f, -H * 0.15f), W * 0.2f, 0.7f, 10, Accent, null, W * 0.14f);
                    B.Sphere(new Vector3(x, 0.8f, -H * 0.15f), W * 0.14f, new Color(0.4f, 0.75f, 0.95f), 3, 8);
                }
                TeamStripe(new Vector3(0, 0.54f, H * 0.2f), W * 0.6f, 0.12f);
            }
            else
            {
                B.Box(new Vector3(0, 0.35f, 0), new Vector3(W * 0.8f, 0.55f, H * 0.7f), Hull);
                int n = adv ? 3 : 2;
                for (int i = 0; i < n; i++)
                {
                    float x = -W * 0.25f + i * (W * 0.5f / Mathf.Max(1, n - 1));
                    B.Cylinder(new Vector3(x, 0.6f, -H * 0.15f), 0.13f, adv ? 1.2f : 0.9f, 8, Dark, Metal, 0.11f);
                    B.Cylinder(new Vector3(x, (adv ? 1.8f : 1.5f) - 0.12f, -H * 0.15f), 0.135f, 0.08f, 8, Accent);
                }
                TeamStripe(new Vector3(0, 0.64f, H * 0.25f), W * 0.6f, 0.12f);
            }
        }

        static void Refinery(float W, float H)
        {
            B.Box(new Vector3(W * 0.1f, 0.4f, H * 0.15f), new Vector3(W * 0.6f, 0.65f, H * 0.5f), Hull, Hull.Mul(1.08f));
            for (int i = 0; i < 2; i++) B.Cylinder(new Vector3(-W * 0.3f, 0.1f, -H * 0.05f + i * H * 0.35f), 0.28f, 0.9f, 8, CA ? Accent : Accent.Mul(1.1f), Dark);
            // dock pad (north side, y-)
            B.Box(new Vector3(0, 0.1f, -H * 0.38f), new Vector3(W * 0.45f, 0.06f, H * 0.2f), Metal);
            B.Box(new Vector3(W * 0.1f, 0.5f, -H * 0.2f), new Vector3(0.12f, 0.4f, H * 0.2f), Dark);
            TeamStripe(new Vector3(W * 0.1f, 0.74f, H * 0.15f), W * 0.5f, 0.15f);
            B.Cylinder(new Vector3(W * 0.3f, 0.7f, H * 0.3f), 0.06f, 0.6f, 6, Metal);
        }

        static void Silo(float W, float H)
        {
            for (int i = 0; i < 2; i++) B.Cylinder(new Vector3((i - 0.5f) * W * 0.42f, 0.1f, 0), W * 0.2f, 0.65f, 10, Accent, Hull, W * 0.18f);
            TeamStripe(new Vector3(0, 0.12f, H * 0.35f), W * 0.6f, 0.08f);
        }

        static void Barracks(float W, float H, bool adv)
        {
            if (CA)
            {
                B.Box(new Vector3(0, 0.3f, 0), new Vector3(W * 0.75f, 0.45f, H * 0.65f), Hull);
                B.Wedge(new Vector3(0, 0.65f, 0), new Vector3(W * 0.8f, 0.25f, H * 0.7f), Dark);
            }
            else
            {
                B.Box(new Vector3(0, 0.35f, 0), new Vector3(W * 0.8f, 0.55f, H * 0.7f), Hull, Dark);
                for (int i = 0; i < 3; i++) B.Box(new Vector3(-W * 0.3f + i * W * 0.3f, 0.66f, 0), new Vector3(0.08f, 0.08f, H * 0.7f), Accent);
            }
            B.Box(new Vector3(0, 0.25f, -H * 0.34f), new Vector3(W * 0.25f, 0.35f, 0.02f), Metal);
            B.Cylinder(new Vector3(W * 0.33f, 0.1f, H * 0.3f), 0.025f, 1.1f, 4, Metal);
            B.Box(new Vector3(W * 0.33f + 0.14f, 1.05f, H * 0.3f), new Vector3(0.28f, 0.16f, 0.01f), Team);
            if (adv) { B.Box(new Vector3(-W * 0.25f, 0.85f, H * 0.1f), new Vector3(0.3f, 0.5f, 0.3f), Dark); B.Cylinder(new Vector3(-W * 0.25f, 1.1f, H * 0.1f), 0.015f, 0.5f, 4, Metal); }
        }

        static void Factory(float W, float H, bool adv)
        {
            float hh = adv ? 1.0f : 0.85f;
            B.Box(new Vector3(0, hh * 0.5f + 0.05f, H * 0.05f), new Vector3(W * 0.85f, hh, H * 0.7f), Hull, Hull.Mul(1.08f));
            if (CA) for (int i = 0; i < 3; i++) B.Wedge(new Vector3(-W * 0.28f + i * W * 0.28f, hh + 0.2f, H * 0.05f), new Vector3(W * 0.26f, 0.3f, H * 0.68f), Dark, -0.8f);
            else { B.Box(new Vector3(0, hh + 0.12f, H * 0.05f), new Vector3(W * 0.6f, 0.2f, H * 0.4f), Dark); B.Cylinder(new Vector3(W * 0.3f, hh, H * 0.25f), 0.1f, 0.6f, 6, Metal); }
            // big door facing the exit (y-)
            B.Box(new Vector3(0, hh * 0.38f + 0.05f, -H * 0.31f), new Vector3(W * 0.42f, hh * 0.72f, 0.03f), Metal);
            for (int i = 0; i < 4; i++) B.Box(new Vector3(0, 0.15f + i * hh * 0.17f, -H * 0.33f), new Vector3(W * 0.42f, 0.02f, 0.01f), Dark);
            TeamStripe(new Vector3(0, hh + 0.06f, H * 0.38f), W * 0.7f, 0.1f);
            if (adv) { B.Box(new Vector3(-W * 0.32f, hh + 0.3f, -H * 0.2f), new Vector3(0.25f, 0.6f, 0.25f), Accent); }
        }

        static void RepairDepot(float W, float H, ModelParts mp)
        {
            B.Box(new Vector3(0, 0.12f, 0), new Vector3(W * 0.8f, 0.06f, H * 0.8f), Metal, new Color(0.35f, 0.35f, 0.33f));
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var p = Quaternion.Euler(0, a, 0) * new Vector3(0, 0, W * 0.36f);
                B.Box(p + new Vector3(0, 0.4f, 0), new Vector3(0.12f, 0.6f, 0.12f), Hull);
            }
            B.Ring(new Vector3(0, 0.16f, 0), W * 0.25f, W * 0.3f, 16, Team);
            mp.Body = Take("depot");
            B.Box(new Vector3(0, 0, 0), new Vector3(W * 0.7f, 0.06f, 0.1f), new Color(0.9f, 0.72f, 0.15f));
            B.Box(new Vector3(0, 0, 0), new Vector3(0.1f, 0.06f, W * 0.7f), new Color(0.9f, 0.72f, 0.15f));
            mp.Extra = Take("depotarm"); mp.ExtraPos = new Vector3(0, 0.75f, 0); mp.ExtraSpins = true;
        }

        static void Hangar(float W, float H, int docks, bool adv)
        {
            B.Box(new Vector3(0, 0.08f, 0), new Vector3(W * 0.95f, 0.04f, H * 0.95f), new Color(0.3f, 0.3f, 0.3f));
            int n = Mathf.Max(1, docks);
            for (int i = 0; i < n; i++)
            {
                // Same layout as UnitBehaviorSystem.PadPosition, relative to the footprint centre.
                float x = 0.75f - W * 0.5f + (i % 2) * (W - 1.5f);
                float z = 0.6f - H * 0.5f + (i / 2) * Mathf.Max(0.8f, H - 1.2f);
                B.Ring(new Vector3(x, 0.11f, z), 0.3f, 0.36f, 12, new Color(0.9f, 0.85f, 0.3f));
            }
            B.Box(new Vector3(0, 0.3f, H * 0.42f), new Vector3(W * 0.5f, 0.45f, H * 0.14f), Hull);
            B.Box(new Vector3(0, 0.75f, H * 0.42f), new Vector3(0.3f, 1.0f, 0.25f), Dark);
            B.Box(new Vector3(0, 1.3f, H * 0.42f), new Vector3(0.36f, 0.16f, 0.3f), Glass);
            TeamStripe(new Vector3(0, 0.54f, H * 0.42f), W * 0.4f, 0.08f);
            if (adv) B.Cylinder(new Vector3(-W * 0.36f, 0.1f, H * 0.42f), 0.12f, 0.9f, 6, Accent);
        }

        static void Shipyard(float W, float H, bool adv)
        {
            B.Box(new Vector3(-W * 0.38f, 0.25f, 0), new Vector3(W * 0.2f, 0.4f, H * 0.9f), Hull);
            B.Box(new Vector3(W * 0.38f, 0.25f, 0), new Vector3(W * 0.2f, 0.4f, H * 0.9f), Hull);
            B.Box(new Vector3(0, 0.25f, H * 0.4f), new Vector3(W * 0.95f, 0.4f, H * 0.15f), Hull.Mul(0.95f));
            B.Box(new Vector3(-W * 0.38f, 1.0f, 0), new Vector3(0.12f, 1.2f, 0.12f), new Color(0.9f, 0.72f, 0.15f));
            B.Box(new Vector3(-W * 0.1f, 1.55f, 0), new Vector3(W * 0.6f, 0.1f, 0.1f), new Color(0.9f, 0.72f, 0.15f));
            TeamStripe(new Vector3(0, 0.46f, H * 0.4f), W * 0.6f, 0.08f);
            if (adv) B.Box(new Vector3(W * 0.38f, 0.75f, H * 0.2f), new Vector3(0.4f, 0.6f, 0.4f), Dark);
        }

        static void TechCenter(float W, float H)
        {
            B.Box(new Vector3(0, 0.35f, 0), new Vector3(W * 0.75f, 0.55f, H * 0.75f), Hull);
            B.Sphere(new Vector3(0, 0.65f, 0), W * 0.25f, CA ? Accent : Dark, 3, 10);
            B.Cylinder(new Vector3(W * 0.28f, 0.6f, H * 0.28f), 0.02f, 0.9f, 4, Metal);
            B.Cylinder(new Vector3(-W * 0.28f, 0.6f, H * 0.28f), 0.02f, 0.7f, 4, Metal);
            TeamStripe(new Vector3(0, 0.63f, -H * 0.3f), W * 0.6f, 0.1f);
        }

        static void Lab(float W, float H)
        {
            B.Box(new Vector3(0, 0.3f, 0), new Vector3(W * 0.8f, 0.45f, H * 0.8f), Hull);
            B.Box(new Vector3(-W * 0.18f, 0.7f, 0), new Vector3(W * 0.3f, 0.4f, H * 0.5f), Glass.Mul(1.2f));
            B.Cylinder(new Vector3(W * 0.2f, 0.5f, 0), 0.22f, 0.5f, 10, Accent, new Color(0.5f, 0.9f, 0.6f));
            TeamStripe(new Vector3(0, 0.54f, H * 0.35f), W * 0.6f, 0.08f);
        }

        static void Radar(float W, float H, ModelParts mp)
        {
            B.Box(new Vector3(0, 0.3f, 0), new Vector3(W * 0.6f, 0.45f, H * 0.6f), Hull);
            B.Cylinder(new Vector3(0, 0.5f, 0), 0.08f, 0.6f, 6, Metal);
            TeamStripe(new Vector3(0, 0.54f, H * 0.25f), W * 0.5f, 0.08f);
            mp.Body = Take("radar");
            B.Push(Vector3.zero, Quaternion.Euler(-25, 0, 0), Vector3.one);
            B.Cylinder(new Vector3(0, 0, 0), 0.5f, 0.12f, 10, Accent, Accent.Mul(0.8f), 0.55f);
            B.Pop();
            B.Box(new Vector3(0, 0.2f, 0.1f), new Vector3(0.04f, 0.3f, 0.04f), Metal);
            mp.Extra = Take("dish"); mp.ExtraPos = new Vector3(0, 1.1f, 0); mp.ExtraSpins = true;
            mp.Height = 1.6f;
        }

        static void Comm(float W, float H, ModelParts mp)
        {
            B.Box(new Vector3(0, 0.35f, 0), new Vector3(W * 0.7f, 0.55f, H * 0.7f), Hull);
            B.Taper(new Vector3(0, 0.6f, 0), new Vector3(0.4f, 0, 0.4f), new Vector3(0.08f, 0, 0.08f), 1.6f, Dark);
            B.Sphere(new Vector3(0, 2.25f, 0), 0.08f, Color.red, 2, 6);
            TeamStripe(new Vector3(0, 0.64f, H * 0.3f), W * 0.5f, 0.08f);
            mp.Height = 2.4f;
        }

        static void Superweapon(float W, float H, ModelParts mp)
        {
            if (CA)
            {
                // Helios uplink: ring of pylons around a skyward emitter
                B.Box(new Vector3(0, 0.25f, 0), new Vector3(W * 0.8f, 0.35f, H * 0.8f), Hull);
                for (int i = 0; i < 4; i++)
                {
                    var p = Quaternion.Euler(0, i * 90 + 45, 0) * new Vector3(0, 0, W * 0.36f);
                    B.Taper(p + Vector3.up * 0.4f, new Vector3(0.3f, 0, 0.3f), new Vector3(0.1f, 0, 0.1f), 1.4f, Accent);
                }
                B.Cylinder(new Vector3(0, 0.4f, 0), 0.4f, 0.6f, 12, Dark, new Color(0.5f, 0.85f, 1f));
                TeamStripe(new Vector3(0, 0.44f, H * 0.36f), W * 0.6f, 0.1f);
                mp.Body = Take("helios");
                B.Ring(Vector3.zero, 0.55f, 0.7f, 20, new Color(0.5f, 0.85f, 1f));
                B.Ring(new Vector3(0, 0.02f, 0), 0.7f, 0.72f, 20, Team);
                mp.Extra = Take("heliosring"); mp.ExtraPos = new Vector3(0, 1.4f, 0); mp.ExtraSpins = true;
            }
            else
            {
                // Hammerfall silo: armoured silo with split doors and a visible warhead
                B.Box(new Vector3(0, 0.3f, 0), new Vector3(W * 0.85f, 0.5f, H * 0.85f), Hull, Dark);
                B.Box(new Vector3(-W * 0.2f, 0.58f, 0), new Vector3(W * 0.36f, 0.06f, H * 0.6f), Accent);
                B.Box(new Vector3(W * 0.2f, 0.58f, 0), new Vector3(W * 0.36f, 0.06f, H * 0.6f), Accent);
                B.Cylinder(new Vector3(W * 0.36f, 0.5f, H * 0.36f), 0.15f, 0.8f, 6, Dark);
                TeamStripe(new Vector3(0, 0.56f, H * 0.4f), W * 0.7f, 0.08f);
                mp.Body = Take("hammerfall");
                B.Cylinder(Vector3.zero, 0.18f, 1.1f, 8, new Color(0.6f, 0.6f, 0.55f));
                B.Cone(new Vector3(0, 1.1f, 0), 0.18f, 0.4f, 8, new Color(0.7f, 0.15f, 0.1f));
                mp.Extra = Take("missile"); mp.ExtraPos = new Vector3(0, -0.6f, 0); mp.ExtraSpins = false;
            }
            mp.Height = 2.2f;
        }

        static void SpecialPower(float W, float H, ModelParts mp)
        {
            B.Box(new Vector3(0, 0.3f, 0), new Vector3(W * 0.75f, 0.45f, H * 0.75f), Hull);
            if (CA) { B.Taper(new Vector3(0, 0.5f, 0), new Vector3(0.6f, 0, 0.6f), new Vector3(0.2f, 0, 0.2f), 0.8f, Accent); B.Sphere(new Vector3(0, 1.35f, 0), 0.15f, new Color(0.5f, 0.8f, 1f), 2, 6); }
            else for (int i = 0; i < 3; i++) { B.Push(new Vector3(-0.4f + i * 0.4f, 0.55f, 0), Quaternion.Euler(-50, 0, 0), Vector3.one); B.Barrel(Vector3.zero, 0.09f, 0.8f, Metal); B.Pop(); }
            TeamStripe(new Vector3(0, 0.54f, H * 0.32f), W * 0.5f, 0.08f);
            mp.Height = 1.5f;
        }

        static void Countermeasure(float W, float H, ModelParts mp)
        {
            B.Box(new Vector3(0, 0.25f, 0), new Vector3(W * 0.7f, 0.35f, H * 0.7f), Hull);
            B.Sphere(new Vector3(0, 0.45f, 0), W * 0.28f, CA ? new Color(0.45f, 0.7f, 0.95f) : new Color(0.75f, 0.5f, 0.25f), 3, 10);
            TeamStripe(new Vector3(0, 0.44f, H * 0.32f), W * 0.5f, 0.06f);
            mp.Height = 1.3f;
        }

        static void Bunker(float W, float H, ModelParts mp)
        {
            B.Taper(new Vector3(0, 0.05f, 0), new Vector3(W * 0.9f, 0, H * 0.9f), new Vector3(W * 0.6f, 0, H * 0.6f), 0.45f, CA ? Hull : new Color(0.45f, 0.43f, 0.36f));
            B.Box(new Vector3(0, 0.35f, H * 0.3f), new Vector3(W * 0.5f, 0.06f, 0.05f), Color.black);
            TeamStripe(new Vector3(0, 0.51f, 0), W * 0.4f, 0.08f);
            mp.Height = 0.7f;
        }

        static void Turret(ModelParts mp, int kind)
        {
            B.Cylinder(new Vector3(0, 0.05f, 0), 0.42f, 0.35f, 8, Hull.Mul(0.9f), Hull, 0.36f);
            TeamStripe(new Vector3(0, 0.41f, 0.3f), 0.25f, 0.05f);
            mp.Body = Take("tbase");
            switch (kind)
            {
                case 0: // anti-infantry
                    B.Box(new Vector3(0, 0.1f, 0), new Vector3(0.32f, 0.2f, 0.32f), Dark);
                    if (CA) { B.Barrel(new Vector3(-0.06f, 0.12f, 0.15f), 0.03f, 0.3f, Metal); B.Barrel(new Vector3(0.06f, 0.12f, 0.15f), 0.03f, 0.3f, Metal); }
                    else { B.Barrel(new Vector3(0, 0.12f, 0.15f), 0.06f, 0.25f, Accent); }
                    mp.MuzzleForward = 0.45f; break;
                case 1: // anti-vehicle
                    if (CA) { B.Taper(Vector3.zero, new Vector3(0.42f, 0, 0.5f), new Vector3(0.3f, 0, 0.36f), 0.24f, Hull); B.Box(new Vector3(0, 0.12f, 0.45f), new Vector3(0.08f, 0.08f, 0.6f), new Color(0.35f, 0.55f, 0.85f)); }
                    else { B.Box(new Vector3(0, 0.13f, 0), new Vector3(0.46f, 0.26f, 0.5f), Hull); B.Barrel(new Vector3(0, 0.13f, 0.22f), 0.07f, 0.6f, Metal); }
                    mp.MuzzleForward = 0.8f; break;
                case 2: // anti-air
                    B.Cylinder(Vector3.zero, 0.2f, 0.15f, 8, Dark);
                    B.Push(new Vector3(0, 0.2f, 0), Quaternion.Euler(-40, 0, 0), Vector3.one);
                    if (CA) { for (int i = 0; i < 2; i++) B.Box(new Vector3(-0.15f + i * 0.3f, 0, 0.05f), new Vector3(0.14f, 0.16f, 0.36f), Hull); }
                    else for (int i = 0; i < 4; i++) B.Barrel(new Vector3(-0.15f + i * 0.1f, 0, 0), 0.03f, 0.5f, Metal);
                    B.Pop(); mp.MuzzleForward = 0.4f; break;
                case 3: // artillery
                    B.Box(new Vector3(0, 0.15f, -0.05f), new Vector3(0.5f, 0.3f, 0.5f), Hull);
                    B.Push(new Vector3(0, 0.2f, 0.15f), Quaternion.Euler(-30, 0, 0), Vector3.one);
                    B.Barrel(Vector3.zero, 0.08f, 0.9f, Metal);
                    B.Pop(); mp.MuzzleForward = 0.95f; break;
            }
            mp.Turret = Take("tgun"); mp.TurretPos = new Vector3(0, 0.4f, 0); mp.MuzzleHeight = 0.55f;
            mp.Height = 0.9f;
        }

        static void Wall(float W, float H)
        {
            var c = CA ? new Color(0.62f, 0.62f, 0.6f) : new Color(0.42f, 0.4f, 0.35f);
            B.Box(new Vector3(0, 0.3f, 0), new Vector3(W * 0.92f, 0.6f, H * 0.92f), c, c.Mul(1.1f));
            B.Box(new Vector3(0, 0.62f, 0), new Vector3(W * 0.5f, 0.06f, H * 0.5f), c.Mul(0.8f));
        }

        static void Gate(float W, float H, ModelParts mp, bool rotated)
        {
            var c = CA ? new Color(0.62f, 0.62f, 0.6f) : new Color(0.42f, 0.4f, 0.35f);
            bool alongX = W >= H;
            if (alongX)
            {
                B.Box(new Vector3(-W * 0.5f + 0.25f, 0.4f, 0), new Vector3(0.5f, 0.8f, H * 0.95f), c);
                B.Box(new Vector3(W * 0.5f - 0.25f, 0.4f, 0), new Vector3(0.5f, 0.8f, H * 0.95f), c);
            }
            else
            {
                B.Box(new Vector3(0, 0.4f, -H * 0.5f + 0.25f), new Vector3(W * 0.95f, 0.8f, 0.5f), c);
                B.Box(new Vector3(0, 0.4f, H * 0.5f - 0.25f), new Vector3(W * 0.95f, 0.8f, 0.5f), c);
            }
            mp.Body = Take("gate");
            if (alongX) B.Box(Vector3.zero, new Vector3(W - 1f, 0.6f, 0.15f), Team.Mul(0.8f));
            else B.Box(Vector3.zero, new Vector3(0.15f, 0.6f, H - 1f), Team.Mul(0.8f));
            mp.Extra = Take("gatedoor"); mp.ExtraPos = new Vector3(0, 0.35f, 0);
            mp.Height = 0.9f;
        }

        static void House(float W, float H, Color wall, Color roof)
        {
            B.Box(new Vector3(0, 0.3f, 0), new Vector3(W * 0.7f, 0.55f, H * 0.6f), wall);
            B.Wedge(new Vector3(0, 0.72f, 0), new Vector3(W * 0.78f, 0.3f, H * 0.7f), roof);
            B.Box(new Vector3(0, 0.2f, -H * 0.305f), new Vector3(0.16f, 0.3f, 0.02f), new Color(0.35f, 0.25f, 0.15f));
            B.Box(new Vector3(W * 0.2f, 0.85f, H * 0.1f), new Vector3(0.1f, 0.3f, 0.1f), new Color(0.5f, 0.3f, 0.25f));
        }

        static void Chapel(float W, float H)
        {
            var wall = new Color(0.82f, 0.8f, 0.74f);
            B.Box(new Vector3(0, 0.4f, H * 0.1f), new Vector3(W * 0.55f, 0.75f, H * 0.65f), wall);
            B.Wedge(new Vector3(0, 0.95f, H * 0.1f), new Vector3(W * 0.6f, 0.35f, H * 0.7f), new Color(0.4f, 0.35f, 0.32f));
            B.Box(new Vector3(0, 0.8f, -H * 0.3f), new Vector3(0.4f, 1.5f, 0.4f), wall);
            B.Taper(new Vector3(0, 1.55f, -H * 0.3f), new Vector3(0.45f, 0, 0.45f), new Vector3(0.02f, 0, 0.02f), 0.6f, new Color(0.4f, 0.35f, 0.32f));
        }

        static void WaterTower()
        {
            for (int i = 0; i < 4; i++) { var p = Quaternion.Euler(0, i * 90 + 45, 0) * new Vector3(0, 0, 0.25f); B.Box(p + Vector3.up * 0.5f, new Vector3(0.05f, 1f, 0.05f), new Color(0.45f, 0.35f, 0.25f)); }
            B.Cylinder(new Vector3(0, 0.95f, 0), 0.32f, 0.4f, 10, new Color(0.6f, 0.55f, 0.5f), new Color(0.45f, 0.4f, 0.35f), 0.3f);
        }

        static void Derrick(float W, float H, ModelParts mp)
        {
            B.Box(new Vector3(0, 0.1f, 0), new Vector3(W * 0.8f, 0.12f, H * 0.8f), new Color(0.35f, 0.33f, 0.3f));
            B.Taper(new Vector3(-W * 0.15f, 0.15f, 0), new Vector3(0.6f, 0, 0.6f), new Vector3(0.12f, 0, 0.12f), 1.5f, new Color(0.45f, 0.4f, 0.3f));
            B.Cylinder(new Vector3(W * 0.25f, 0.15f, H * 0.2f), 0.2f, 0.5f, 8, new Color(0.6f, 0.5f, 0.2f));
            B.Box(new Vector3(W * 0.25f, 0.2f, -H * 0.25f), new Vector3(0.3f, 0.06f, 0.3f), Team.Mul(0.8f));
            mp.Height = 1.7f;
        }

        static void Hospital(float W, float H)
        {
            B.Box(new Vector3(0, 0.3f, 0), new Vector3(W * 0.8f, 0.5f, H * 0.7f), new Color(0.88f, 0.88f, 0.85f));
            B.Box(new Vector3(0, 0.56f, 0), new Vector3(0.5f, 0.02f, 0.14f), Color.red);
            B.Box(new Vector3(0, 0.56f, 0), new Vector3(0.14f, 0.02f, 0.5f), Color.red);
            B.Box(new Vector3(W * 0.35f, 0.6f, H * 0.3f), new Vector3(0.12f, 0.12f, 0.12f), Team);
        }

        static void Relay(float W, float H, ModelParts mp)
        {
            B.Box(new Vector3(0, 0.25f, 0), new Vector3(W * 0.6f, 0.4f, H * 0.6f), new Color(0.55f, 0.55f, 0.5f));
            B.Taper(new Vector3(0, 0.45f, 0), new Vector3(0.3f, 0, 0.3f), new Vector3(0.05f, 0, 0.05f), 1.5f, Metal);
            B.Box(new Vector3(W * 0.25f, 0.5f, H * 0.25f), new Vector3(0.12f, 0.12f, 0.12f), Team);
            mp.Height = 2f;
        }

        static void Depot(float W, float H)
        {
            for (int i = 0; i < 3; i++) B.Box(new Vector3(-W * 0.3f + i * W * 0.3f, 0.3f, 0), new Vector3(W * 0.26f, 0.5f, H * 0.7f), new Color(0.5f, 0.45f, 0.35f), new Color(0.4f, 0.36f, 0.3f));
            B.Box(new Vector3(0, 0.58f, H * 0.3f), new Vector3(W * 0.6f, 0.03f, 0.1f), Team);
        }
    }
}
