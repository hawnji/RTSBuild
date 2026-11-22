using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace RTS.Client
{
    /// <summary>
    /// Reader for the RTSM model format written by Tools/BlenderModels/kit.py (write_rtsm). It has no Unity
    /// dependency so the format can be checked outside the editor. Positions are already in Unity space.
    /// </summary>
    public sealed class RtsmFile
    {
        public sealed class Part
        {
            public string Name;
            public float[] Pivot = new float[3];
            public float[] Positions;   // xyz per vertex
            public float[] Normals;     // xyz per vertex
            public byte[] Colors;       // rgba per vertex
            public int[] Indices;
            public int VertexCount => Positions.Length / 3;
        }

        public float Height, MuzzleForward, MuzzleHeight;
        public bool RotorVertical, ExtraSpins, Infantry;
        public int FootW, FootH;
        public readonly List<Part> Parts = new List<Part>();

        public static RtsmFile Read(byte[] data)
        {
            using (var br = new BinaryReader(new MemoryStream(data)))
            {
                if (data.Length < 20 || br.ReadByte() != 'R' || br.ReadByte() != 'T' || br.ReadByte() != 'S' || br.ReadByte() != 'M')
                    throw new InvalidDataException("not an RTSM file");
                int ver = br.ReadByte();
                if (ver != 1) throw new InvalidDataException("unsupported version " + ver);
                var f = new RtsmFile { Height = br.ReadSingle(), MuzzleForward = br.ReadSingle(), MuzzleHeight = br.ReadSingle() };
                int flags = br.ReadByte();
                f.RotorVertical = (flags & 1) != 0; f.ExtraSpins = (flags & 2) != 0; f.Infantry = (flags & 4) != 0;
                f.FootW = br.ReadByte(); f.FootH = br.ReadByte();
                int parts = br.ReadByte();
                for (int pi = 0; pi < parts; pi++)
                {
                    var p = new Part();
                    p.Name = Encoding.ASCII.GetString(br.ReadBytes(br.ReadByte()));
                    for (int k = 0; k < 3; k++) p.Pivot[k] = br.ReadSingle();
                    int vc = br.ReadInt32();
                    if (vc <= 0 || vc > 4_000_000) throw new InvalidDataException("bad vertex count " + vc);
                    p.Positions = new float[vc * 3]; p.Normals = new float[vc * 3]; p.Colors = new byte[vc * 4];
                    for (int i = 0; i < vc; i++)
                    {
                        for (int k = 0; k < 3; k++) p.Positions[i * 3 + k] = br.ReadSingle();
                        for (int k = 0; k < 3; k++) p.Normals[i * 3 + k] = br.ReadSingle();
                        for (int k = 0; k < 4; k++) p.Colors[i * 4 + k] = br.ReadByte();
                    }
                    int ic = br.ReadInt32();
                    if (ic <= 0 || ic % 3 != 0) throw new InvalidDataException("bad index count " + ic);
                    p.Indices = new int[ic];
                    bool wide = vc > 65535;
                    for (int i = 0; i < ic; i++)
                    {
                        int t = wide ? br.ReadInt32() : br.ReadUInt16();
                        if (t >= vc) throw new InvalidDataException("index out of range");
                        p.Indices[i] = t;
                    }
                    f.Parts.Add(p);
                }
                if (f.Parts.Count == 0) throw new InvalidDataException("no parts");
                if (br.BaseStream.Position != data.Length) throw new InvalidDataException("trailing data");
                return f;
            }
        }
    }
}
