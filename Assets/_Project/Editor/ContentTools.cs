using System;
using System.Text;
using RTS.Client;
using RTS.Sim;
using UnityEditor;
using UnityEngine;

namespace RTS.EditorTools
{
    /// <summary>Data export and in-editor validation helpers.</summary>
    public static class ContentTools
    {
        const string DbPath = "Assets/_Project/Resources/RTSDefinitions.asset";

        [MenuItem("RTS/Data/Export Definitions Asset (ScriptableObject)", priority = 30)]
        public static void ExportDefinitions()
        {
            var db = AssetDatabase.LoadAssetAtPath<DefinitionDatabase>(DbPath);
            bool created = db == null;
            if (created) db = ScriptableObject.CreateInstance<DefinitionDatabase>();
            DefinitionOverrides.Fill(db);
            if (created) AssetDatabase.CreateAsset(db, DbPath);
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Selection.activeObject = db;
            Debug.Log($"[RTS] Definitions exported to {DbPath}: {db.Units.Count} units, {db.Buildings.Count} buildings, {db.Weapons.Count} weapons. Edit values in the Inspector; they override the code catalog at startup.");
        }

        [MenuItem("RTS/Data/Validate Maps and Missions", priority = 31)]
        public static void Validate()
        {
            var sb = new StringBuilder();
            int errors = 0;
            Catalog.EnsureBuilt();
            foreach (var spec in MapLibrary.Skirmish)
            {
                try
                {
                    var c = new SkirmishConfig { MapId = spec.Id, Spectator = true };
                    for (int i = 0; i < Math.Min(spec.MaxPlayers, spec.Starts.Count); i++)
                        c.Slots.Add(new SlotConfig { Human = false, Faction = i % 2 == 0 ? FactionId.Alliance : FactionId.Dominion, Team = i % 2, Color = i, Name = "AI" + i });
                    var w = GameSetup.CreateSkirmish(c);
                    for (int t = 0; t < 300; t++) { w.Step(); w.Events.Clear(); }
                    sb.AppendLine($"OK   map {spec.Id}: {w.Units.Count} units, {w.Buildings.Count} buildings after 20s");
                }
                catch (Exception ex) { errors++; sb.AppendLine($"FAIL map {spec.Id}: {ex.Message}"); }
            }
            foreach (var m in CampaignDatabase.All())
            {
                try
                {
                    var w = GameSetup.CreateMission(m, AIDifficulty.Normal);
                    for (int t = 0; t < 150; t++) { w.Step(); w.Events.Clear(); }
                    sb.AppendLine($"OK   mission {m.Id} {m.Title}");
                }
                catch (Exception ex) { errors++; sb.AppendLine($"FAIL mission {m.Id}: {ex.Message}"); }
            }
            if (errors == 0) Debug.Log("[RTS] Validation passed\n" + sb);
            else Debug.LogError($"[RTS] Validation found {errors} problem(s)\n" + sb);
        }

        [MenuItem("RTS/Data/Clear Saved Progress and Settings", priority = 40)]
        public static void ClearProgress()
        {
            if (!EditorUtility.DisplayDialog("Clear progress", "Delete campaign progress, settings and saved games from persistentDataPath?", "Delete", "Cancel")) return;
            var dir = Application.persistentDataPath;
            foreach (var f in new[] { "campaign.json", "settings.json" }) { var p = System.IO.Path.Combine(dir, f); if (System.IO.File.Exists(p)) System.IO.File.Delete(p); }
            var saves = System.IO.Path.Combine(dir, "Saves");
            if (System.IO.Directory.Exists(saves)) System.IO.Directory.Delete(saves, true);
            Debug.Log("[RTS] Cleared " + dir);
        }
    }
}
