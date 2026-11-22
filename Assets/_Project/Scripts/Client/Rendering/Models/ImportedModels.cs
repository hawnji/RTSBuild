using System;
using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;

namespace RTS.Client
{
    /// <summary>
    /// Swap-in models that replace the procedural ones from <see cref="ModelLibrary"/>.
    ///
    /// Lookup order for a model key such as "tank_mbt" (faction suffix "ca" or "id"):
    ///   1. Resources/Models/tank_mbt_ca.bytes  (exported by Tools/BlenderModels/build.py)
    ///   2. Resources/Models/tank_mbt.bytes
    ///   3. Resources/Models/tank_mbt_ca (or tank_mbt) prefab/FBX whose child objects are named Body, Turret, Rotor
    ///      and Extra. Mesh vertex colours are used; enable Read/Write in the import settings for team colours.
    /// If none exists the procedural model is used, so models can be replaced one at a time.
    ///
    /// Team colour: vertices coloured pure magenta (255, 0, 255) are replaced with the owner's colour.
    /// </summary>
    public static class ImportedModels
    {
        public const string Folder = "Models/";

        class Part
        {
            public string Name;
            public AnimParent Parent;
            public AnimKind Kind;
            public float Param, Phase;
            public Vector3 Pivot;            // model space
            public Vector3[] V, N;
            public Color32[] C;
            public int[] T;
            public Mesh Shared;          // prefab mesh used as-is when it cannot be read
        }

        class Source
        {
            public float Height, MuzzleForward, MuzzleHeight;
            public bool RotorVertical, ExtraSpins, Infantry;
            public int FootW, FootH;
            public readonly List<Part> Parts = new List<Part>();
        }

        static readonly Dictionary<string, Source> _sources = new Dictionary<string, Source>();
        static bool? _enabled;

        /// <summary>Lets the settings or a debug key switch back to procedural models.</summary>
        public static bool Enabled
        {
            get { if (_enabled == null) _enabled = PlayerPrefs.GetInt("rts_imported_models", 1) == 1; return _enabled.Value; }
            set { _enabled = value; PlayerPrefs.SetInt("rts_imported_models", value ? 1 : 0); ModelLibrary.ClearCache(); }
        }

        static string Suffix(FactionId f) => f == FactionId.Dominion ? "_id" : f == FactionId.Neutral ? "_ne" : "_ca";

        static Source Find(string key, FactionId f)
        {
            if (string.IsNullOrEmpty(key) || !Enabled) return null;
            string id = key + Suffix(f);
            if (_sources.TryGetValue(id, out var s)) return s;
            s = LoadBytes(key + Suffix(f)) ?? LoadBytes(key) ?? LoadPrefab(key + Suffix(f)) ?? LoadPrefab(key);
            _sources[id] = s;
            return s;
        }

        public static bool TryUnit(UnitDef d, Color team, out ModelParts mp)
        {
            mp = null;
            var s = Find(d.Model ?? "tank_mbt", d.Faction);
            if (s == null) return false;
            mp = Build(s, team, d.Id);
            return true;
        }

        public static bool TryBuilding(BuildingDef d, Color team, int w, int h, out ModelParts mp)
        {
            mp = null;
            var s = Find(d.Model ?? d.Role.ToString(), d.Faction);
            if (s == null) return false;
            float rot = 0f;
            if (s.FootW > 0 && s.FootH > 0)
            {
                if (s.FootW == w && s.FootH == h) rot = 0f;
                else if (s.FootW == h && s.FootH == w) rot = 90f;
                else return false;   // a model made for another footprint would not fit the cells
            }
            mp = Build(s, team, d.Id, rot);
            return true;
        }

        static ModelParts Build(Source s, Color team, string name, float yaw = 0f)
        {
            var mp = new ModelParts
            {
                Height = s.Height, MuzzleForward = s.MuzzleForward, MuzzleHeight = s.MuzzleHeight,
                RotorVertical = s.RotorVertical, ExtraSpins = s.ExtraSpins, Infantry = s.Infantry,
            };
            var rot = Quaternion.Euler(0, yaw, 0);
            Color32 tc = team;
            foreach (var p in s.Parts)
            {
                Mesh m;
                if (p.Shared != null) m = p.Shared;
                else
                {
                    m = new Mesh { name = name + "_" + p.Name };
                    if (p.V.Length > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    var v = p.V; var n = p.N;
                    if (yaw != 0f)
                    {
                        v = new Vector3[p.V.Length]; n = new Vector3[p.N.Length];
                        for (int i = 0; i < v.Length; i++) { v[i] = rot * p.V[i]; n[i] = rot * p.N[i]; }
                    }
                    var c = new Color32[p.C.Length];
                    for (int i = 0; i < c.Length; i++)
                    {
                        var k = p.C[i];
                        c[i] = IsTeam(k) ? new Color32(tc.r, tc.g, tc.b, 255) : k;
                    }
                    m.vertices = v; m.normals = n; m.colors32 = c; m.triangles = p.T;
                    m.RecalculateBounds();
                }
                var pivot = rot * p.Pivot;
                if (p.Kind != AnimKind.None)
                {
                    // Stored pivots are in model space; animated pieces are placed relative to their parent's pivot.
                    var parentPivot = p.Parent == AnimParent.Body ? Vector3.zero : rot * PivotOf(s, p.Parent);
                    mp.Anim.Add(new AnimPart { Mesh = m, Parent = p.Parent, LocalPos = pivot - parentPivot, Kind = p.Kind, Param = p.Param, Phase = p.Phase });
                    continue;
                }
                switch (p.Name)
                {
                    case "Turret": mp.Turret = m; mp.TurretPos = pivot; break;
                    case "Rotor": mp.Rotor = m; mp.RotorPos = pivot; break;
                    case "Extra": mp.Extra = m; mp.ExtraPos = pivot; break;
                    default: mp.Body = m; break;
                }
            }
            if (mp.Body == null) mp.Body = new Mesh();
            return mp;
        }

        static Vector3 PivotOf(Source s, AnimParent parent)
        {
            string n = parent.ToString();
            foreach (var p in s.Parts) if (p.Kind == AnimKind.None && p.Name == n) return p.Pivot;
            return Vector3.zero;
        }

        static bool IsTeam(Color32 c) => c.r > 250 && c.g < 5 && c.b > 250;

        // ------------------------------------------------------------------------------------------------ .bytes

        static Source LoadBytes(string res)
        {
            var ta = Resources.Load<TextAsset>(Folder + res);
            if (ta == null) return null;
            try { return Parse(ta.bytes); }
            catch (Exception ex) { Debug.LogWarning("[RTS] Could not read model " + res + ": " + ex.Message); return null; }
        }

        static Source Parse(byte[] data)
        {
            var f = RtsmFile.Read(data);
            var s = new Source
            {
                Height = f.Height, MuzzleForward = f.MuzzleForward, MuzzleHeight = f.MuzzleHeight,
                RotorVertical = f.RotorVertical, ExtraSpins = f.ExtraSpins, Infantry = f.Infantry, FootW = f.FootW, FootH = f.FootH,
            };
            foreach (var fp in f.Parts)
            {
                int vc = fp.VertexCount;
                var p = new Part { Name = fp.Name, Parent = (AnimParent)fp.Parent, Kind = (AnimKind)fp.Kind, Param = fp.Param, Phase = fp.Phase, Pivot = new Vector3(fp.Pivot[0], fp.Pivot[1], fp.Pivot[2]), V = new Vector3[vc], N = new Vector3[vc], C = new Color32[vc], T = fp.Indices };
                for (int i = 0; i < vc; i++)
                {
                    p.V[i] = new Vector3(fp.Positions[i * 3], fp.Positions[i * 3 + 1], fp.Positions[i * 3 + 2]);
                    p.N[i] = new Vector3(fp.Normals[i * 3], fp.Normals[i * 3 + 1], fp.Normals[i * 3 + 2]);
                    p.C[i] = new Color32(fp.Colors[i * 4], fp.Colors[i * 4 + 1], fp.Colors[i * 4 + 2], fp.Colors[i * 4 + 3]);
                }
                s.Parts.Add(p);
            }
            return s;
        }

        // ------------------------------------------------------------------------------------------------ prefabs / FBX

        static Source LoadPrefab(string res)
        {
            var go = Resources.Load<GameObject>(Folder + res);
            if (go == null) return null;
            var s = new Source { Height = 0.5f, MuzzleForward = 0.5f, MuzzleHeight = 0.3f, RotorVertical = true };
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                string n = mf.gameObject.name;
                string partName = n.StartsWith("Turret") ? "Turret" : n.StartsWith("Rotor") ? "Rotor" : n.StartsWith("Extra") ? "Extra" : "Body";
                if (s.Parts.Exists(x => x.Name == partName)) continue;
                var t = mf.transform;
                var p = new Part { Name = partName, Pivot = partName == "Body" ? Vector3.zero : go.transform.InverseTransformPoint(t.position) };
                var mesh = mf.sharedMesh;
                if (mesh.isReadable)
                {
                    // Bake the child's rotation/scale so the part pivot keeps only its position.
                    var m = Matrix4x4.TRS(Vector3.zero, t.rotation, t.lossyScale);
                    if (partName == "Body") m = go.transform.worldToLocalMatrix * t.localToWorldMatrix;
                    var v = mesh.vertices; var nn = mesh.normals; var c = mesh.colors32;
                    p.V = new Vector3[v.Length]; p.N = new Vector3[v.Length]; p.C = new Color32[v.Length];
                    for (int i = 0; i < v.Length; i++)
                    {
                        p.V[i] = m.MultiplyPoint3x4(v[i]);
                        p.N[i] = nn.Length == v.Length ? m.MultiplyVector(nn[i]).normalized : Vector3.up;
                        p.C[i] = c.Length == v.Length ? c[i] : new Color32(200, 200, 200, 255);
                    }
                    p.T = mesh.triangles;
                }
                else p.Shared = mesh;
                s.Parts.Add(p);
                var b = mesh.bounds;
                s.Height = Mathf.Max(s.Height, b.max.y * t.lossyScale.y + p.Pivot.y);
            }
            if (s.Parts.Count == 0) return null;
            s.Parts.Sort((a, b) => a.Name == "Body" ? -1 : b.Name == "Body" ? 1 : 0);
            return s;
        }
    }
}
