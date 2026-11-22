using System;
using System.IO;
using UnityEngine;

namespace RTS.Client
{
    /// <summary>Player preferences persisted as JSON in Application.persistentDataPath/settings.json.</summary>
    [Serializable]
    public class ClientSettings
    {
        // audio (linear 0..1)
        public float MasterVolume = 0.8f, MusicVolume = 0.55f, SfxVolume = 0.8f, VoiceVolume = 0.9f, UiVolume = 0.7f;
        // graphics
        public int QualityLevel = 2;       // 0 low, 1 medium, 2 high
        public bool Fullscreen = true;
        public int ResolutionIndex = -1;   // -1 = current
        public bool VSync = true;
        public int TargetFps = 60;
        public float UiScale = 1f;
        public bool ShowFps;
        // gameplay
        public float ScrollSpeed = 1f;
        public bool EdgeScroll = true;
        public bool InvertZoom;
        public bool AlwaysShowHealth;
        public bool Subtitles = true;
        public bool ClassicCamera = true;  // fixed isometric angle by default
        public string PlayerName = "Commander";
        public int LastDifficulty = 1;
        public Keybinds Keys = Keybinds.Default();

        static string PathOf => System.IO.Path.Combine(Application.persistentDataPath, "settings.json");
        public static ClientSettings Current = new ClientSettings();

        public static void Load()
        {
            try
            {
                if (File.Exists(PathOf))
                {
                    var s = JsonUtility.FromJson<ClientSettings>(File.ReadAllText(PathOf));
                    if (s != null) Current = s;
                }
            }
            catch (Exception ex) { Debug.LogWarning("Settings could not be loaded: " + ex.Message); Current = new ClientSettings(); }
            if (Current.Keys == null || Current.Keys.Bindings.Count == 0) Current.Keys = Keybinds.Default();
            Apply();
        }

        public static void Save()
        {
            try { File.WriteAllText(PathOf, JsonUtility.ToJson(Current, true)); }
            catch (Exception ex) { Debug.LogWarning("Settings could not be saved: " + ex.Message); }
        }

        public static void Apply()
        {
            var s = Current;
            QualitySettings.vSyncCount = s.VSync ? 1 : 0;
            Application.targetFrameRate = s.VSync ? -1 : s.TargetFps;
            int levels = QualitySettings.names.Length;
            if (levels > 0) QualitySettings.SetQualityLevel(Mathf.Clamp(Mathf.RoundToInt(s.QualityLevel / 2f * (levels - 1)), 0, levels - 1), true);
            var res = Screen.resolutions;
            if (s.ResolutionIndex >= 0 && s.ResolutionIndex < res.Length)
                Screen.SetResolution(res[s.ResolutionIndex].width, res[s.ResolutionIndex].height, s.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            else if (Screen.fullScreen != s.Fullscreen)
                Screen.fullScreenMode = s.Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            AudioManager.ApplyVolumes();
        }
    }
}
