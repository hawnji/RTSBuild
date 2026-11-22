// Usage: dotnet run -- [folder]   (default: Assets/_Project/Resources/Models). Exit code = number of bad files.
using System;
using System.IO;
using System.Linq;
using RTS.Client;

string dir = args.Length > 0 ? args[0] : Path.Combine("..", "..", "Assets", "_Project", "Resources", "Models");
int bad = 0, files = 0;
foreach (var path in Directory.GetFiles(dir, "*.bytes").OrderBy(p => p))
{
    files++;
    string name = Path.GetFileNameWithoutExtension(path);
    try
    {
        var f = RtsmFile.Read(File.ReadAllBytes(path));
        var issues = new System.Collections.Generic.List<string>();
        int tris = 0; bool team = false;
        float minY = float.MaxValue, maxY = float.MinValue, maxXZ = 0;
        foreach (var p in f.Parts)
        {
            tris += p.Indices.Length / 3;
            for (int i = 0; i < p.VertexCount; i++)
            {
                float nx = p.Normals[i * 3], ny = p.Normals[i * 3 + 1], nz = p.Normals[i * 3 + 2];
                float len = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
                if (float.IsNaN(len) || MathF.Abs(len - 1) > 0.02f) { issues.Add($"{p.Name}: normal {i} has length {len}"); break; }
                float y = p.Positions[i * 3 + 1] + p.Pivot[1];
                minY = MathF.Min(minY, y); maxY = MathF.Max(maxY, y);
                maxXZ = MathF.Max(maxXZ, MathF.Max(MathF.Abs(p.Positions[i * 3] + p.Pivot[0]), MathF.Abs(p.Positions[i * 3 + 2] + p.Pivot[2])));
                if (p.Colors[i * 4] == 255 && p.Colors[i * 4 + 1] == 0 && p.Colors[i * 4 + 2] == 255) team = true;
            }
        }
        if (f.Parts[0].Name != "Body") issues.Add("first part is not Body");
        if (!team) issues.Add("no team-colour vertices");
        if (minY < -0.05f) issues.Add($"geometry below ground ({minY:0.00})");
        float limit = f.FootW > 0 ? MathF.Max(f.FootW, f.FootH) * 0.5f + 0.05f : 1.6f;
        if (maxXZ > limit) issues.Add($"wider than expected ({maxXZ:0.00} > {limit:0.00})");
        if (tris > 40000) issues.Add($"too many triangles ({tris})");
        string parts = string.Join(" ", f.Parts.Select(p => $"{p.Name}:{p.Indices.Length / 3}"));
        Console.WriteLine($"{(issues.Count == 0 ? "OK  " : "BAD ")} {name,-26} {tris,6} tris  h={maxY:0.00}  {parts}");
        foreach (var s in issues) Console.WriteLine("       " + s);
        if (issues.Count > 0) bad++;
    }
    catch (Exception ex) { bad++; Console.WriteLine($"BAD  {name}: {ex.Message}"); }
}
Console.WriteLine($"{files} models, {bad} with problems");
return bad;
