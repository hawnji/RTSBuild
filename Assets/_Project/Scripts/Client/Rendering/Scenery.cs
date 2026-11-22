using System.Collections.Generic;
using UnityEngine;

namespace RTS.Client
{
    /// <summary>
    /// Map scenery pieces built in Blender (Tools/BlenderModels/build_scenery.py) and stored in
    /// Resources/Models/Scenery/&lt;name&gt;_&lt;variant&gt;.bytes. Pieces are copied into the terrain's chunk meshes.
    ///
    /// Marker colours: pure green (r = 0, b = 0) is foliage and pure blue (r = 0, g = 0) is stone; both are replaced
    /// with the map theme's colours and the marker's channel value is kept as shading. Vertex alpha from 0.5 to 1
    /// is a wind-sway weight, and alpha below 0.5 makes the vertex glow (see RTSDecor.shader).
    /// </summary>
    public static class Scenery
    {
        class Piece
        {
            public Vector3[] V, N;
            public Color32[] C;
            public int[] T;
        }

        static readonly Dictionary<string, List<Piece>> _pieces = new Dictionary<string, List<Piece>>();

        static List<Piece> Load(string name)
        {
            if (_pieces.TryGetValue(name, out var list)) return list;
            list = new List<Piece>();
            if (ImportedModels.Enabled)
            {
                for (int v = 0; v < 8; v++)
                {
                    var ta = Resources.Load<TextAsset>(ImportedModels.Folder + "Scenery/" + name + "_" + v);
                    if (ta == null) break;
                    try
                    {
                        var f = RtsmFile.Read(ta.bytes);
                        var p = f.Parts[0];
                        int n = p.VertexCount;
                        var pc = new Piece { V = new Vector3[n], N = new Vector3[n], C = new Color32[n], T = p.Indices };
                        for (int i = 0; i < n; i++)
                        {
                            pc.V[i] = new Vector3(p.Positions[i * 3], p.Positions[i * 3 + 1], p.Positions[i * 3 + 2]);
                            pc.N[i] = new Vector3(p.Normals[i * 3], p.Normals[i * 3 + 1], p.Normals[i * 3 + 2]);
                            pc.C[i] = new Color32(p.Colors[i * 4], p.Colors[i * 4 + 1], p.Colors[i * 4 + 2], p.Colors[i * 4 + 3]);
                        }
                        list.Add(pc);
                    }
                    catch (System.Exception ex) { Debug.LogWarning("[RTS] Bad scenery piece " + name + "_" + v + ": " + ex.Message); break; }
                }
            }
            _pieces[name] = list;
            return list;
        }

        public static bool Has(string name) => Load(name).Count > 0;

        public static void ClearCache() => _pieces.Clear();

        /// <summary>Copies a scenery piece into the builder. Returns false when the piece is missing.</summary>
        public static bool Append(MeshBuilder mb, string name, float pick, Vector3 pos, float yawDeg, Vector3 scale, Color leaf, Color rock, float shade = 1f)
        {
            var list = Load(name);
            if (list.Count == 0) return false;
            var p = list[Mathf.Clamp((int)(pick * list.Count), 0, list.Count - 1)];
            var m = Matrix4x4.TRS(pos, Quaternion.Euler(0, yawDeg, 0), scale);
            var nm = m.inverse.transpose;
            int off = mb.V.Count;
            for (int i = 0; i < p.V.Length; i++)
            {
                mb.V.Add(m.MultiplyPoint3x4(p.V[i]));
                mb.N.Add(nm.MultiplyVector(p.N[i]).normalized);
                var c = p.C[i];
                Color32 o;
                if (c.r == 0 && c.b == 0 && c.g > 0) o = Shade(leaf, c.g / 204f * shade, c.a);
                else if (c.r == 0 && c.g == 0 && c.b > 0) o = Shade(rock, c.b / 204f * shade, c.a);
                else o = c;
                mb.C.Add(o);
            }
            foreach (var t in p.T) mb.T.Add(t + off);
            return true;
        }

        static Color32 Shade(Color c, float k, byte a)
        {
            return new Color32((byte)Mathf.Clamp(c.r * k * 255f, 0, 255), (byte)Mathf.Clamp(c.g * k * 255f, 0, 255), (byte)Mathf.Clamp(c.b * k * 255f, 0, 255), a);
        }
    }
}
