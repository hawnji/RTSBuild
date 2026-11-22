using System;
using System.Collections.Generic;
using System.IO;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>Campaign unlocks and results, persisted as JSON.</summary>
    [Serializable]
    public class CampaignProgress
    {
        public int AllianceUnlocked = 1;      // highest playable mission index (1-based)
        public int DominionUnlocked = 1;
        public bool IntroSeen;
        public List<string> Completed = new List<string>();
        public List<int> CompletedDifficulty = new List<int>();

        static string PathOf => Path.Combine(Application.persistentDataPath, "campaign.json");
        public static CampaignProgress Current = new CampaignProgress();

        public static void Load()
        {
            try { if (File.Exists(PathOf)) Current = JsonUtility.FromJson<CampaignProgress>(File.ReadAllText(PathOf)) ?? new CampaignProgress(); }
            catch (Exception ex) { Debug.LogWarning("Campaign progress unreadable: " + ex.Message); Current = new CampaignProgress(); }
        }

        public static void Save()
        {
            try { File.WriteAllText(PathOf, JsonUtility.ToJson(Current, true)); }
            catch (Exception ex) { Debug.LogWarning("Campaign progress not saved: " + ex.Message); }
        }

        public int Unlocked(FactionId f) => f == FactionId.Alliance ? AllianceUnlocked : DominionUnlocked;

        public void MarkComplete(MissionDef m, AIDifficulty d)
        {
            int i = Completed.IndexOf(m.Id);
            if (i < 0) { Completed.Add(m.Id); CompletedDifficulty.Add((int)d); }
            else CompletedDifficulty[i] = Math.Max(CompletedDifficulty[i], (int)d);
            if (m.Faction == FactionId.Alliance) AllianceUnlocked = Math.Max(AllianceUnlocked, m.Index + 1);
            else DominionUnlocked = Math.Max(DominionUnlocked, m.Index + 1);
            Save();
        }

        public bool IsComplete(string id) => Completed.Contains(id);
    }

    [Serializable]
    public class SaveMeta
    {
        public string Slot;
        public string Label;
        public string SavedAt;
        public string Description;
        public float GameTime;
    }

    /// <summary>Save slots on disk: binary world snapshot (.sav) plus a small JSON descriptor (.json).</summary>
    public static class SaveManager
    {
        public static string Dir
        {
            get
            {
                var d = Path.Combine(Application.persistentDataPath, "Saves");
                if (!Directory.Exists(d)) Directory.CreateDirectory(d);
                return d;
            }
        }

        public static string Save(string slot, WorldSnapshot snap, string description)
        {
            var bytes = SaveGame.Serialize(snap);
            File.WriteAllBytes(Path.Combine(Dir, slot + ".sav"), bytes);
            var meta = new SaveMeta { Slot = slot, Label = snap.Label, SavedAt = snap.SavedAt, Description = description, GameTime = snap.Time };
            File.WriteAllText(Path.Combine(Dir, slot + ".json"), JsonUtility.ToJson(meta, true));
            return slot;
        }

        public static WorldSnapshot Load(string slot)
        {
            var p = Path.Combine(Dir, slot + ".sav");
            if (!File.Exists(p)) throw new FileNotFoundException("No save in slot " + slot);
            return SaveGame.Deserialize(File.ReadAllBytes(p));
        }

        public static List<SaveMeta> List()
        {
            var l = new List<SaveMeta>();
            foreach (var f in Directory.GetFiles(Dir, "*.json"))
            {
                try { var m = JsonUtility.FromJson<SaveMeta>(File.ReadAllText(f)); if (m != null && File.Exists(Path.Combine(Dir, m.Slot + ".sav"))) l.Add(m); }
                catch { /* skip unreadable */ }
            }
            l.Sort((a, b) => string.CompareOrdinal(b.SavedAt, a.SavedAt));
            return l;
        }

        public static void Delete(string slot)
        {
            foreach (var ext in new[] { ".sav", ".json" })
            {
                var p = Path.Combine(Dir, slot + ext);
                if (File.Exists(p)) File.Delete(p);
            }
        }

        public static bool Exists(string slot) => File.Exists(Path.Combine(Dir, slot + ".sav"));
    }
}
