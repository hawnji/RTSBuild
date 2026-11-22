using System;
using System.IO;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>
    /// Application entry point and front-end state machine. Created automatically after the first scene loads, so
    /// the Boot scene can stay empty. Owns the camera, the active GameSession (or menu demo battle), the HUD and
    /// the menus, and routes the single IMGUI pass that both draws the UI and collects input.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot I { get; private set; }

        Camera _cam;
        GameSession _session;
        Hud _hud;
        Menus _menus;
        public UiScreen Screen = UiScreen.MainMenu;
        UiScreen _prevScreen;
        Action _pendingLoad; string _loadingText; int _loadingFrames;
        bool _viewingBattlefield;
        bool _isIntro;
        float _demoTimer, _demoCamTimer;
        Vec2 _demoCamTarget;
        MissionDef _lastMission; AIDifficulty _lastDifficulty; SkirmishConfig _lastSkirmish;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (I != null) return;
            var go = new GameObject("GameRoot");
            go.AddComponent<GameRoot>();
        }

        void Awake()
        {
            if (I != null && I != this) { Destroy(gameObject); return; }
            I = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = false;
            Catalog.EnsureBuilt();
            DefinitionOverrides.ApplyFromResources();
            ClientSettings.Load();
            CampaignProgress.Load();
            AudioManager.Init();
            ClientSettings.Apply();
            _cam = Camera.main;
            if (_cam == null)
            {
                var cgo = new GameObject("Main Camera") { tag = "MainCamera" };
                _cam = cgo.AddComponent<Camera>();
                DontDestroyOnLoad(cgo);
            }
            _cam.cullingMask = ~(1 << 31);
            // remove stray listeners (the audio manager owns the listener)
            var l = _cam.GetComponent<AudioListener>(); if (l != null) Destroy(l);
            _menus = new Menus(this);
            StartDemo();
            Screen = UiScreen.MainMenu;
            AudioManager.PlayMusic("menu");
        }

        void OnApplicationFocus(bool focus) { if (!focus) InputState.ReleaseAll(); InputState.MouseInWindow = focus; }

        // ------------------------------------------------------------------ navigation
        public void Go(UiScreen s)
        {
            _prevScreen = Screen;
            Screen = s;
            if (s == UiScreen.Campaign && !CampaignProgress.Current.IntroSeen) { StartIntro(); return; }
            if ((s == UiScreen.MainMenu || s == UiScreen.Campaign || s == UiScreen.Skirmish) && (_session == null || !_session.Demo)) StartDemo();
            if (s == UiScreen.MainMenu || s == UiScreen.Campaign || s == UiScreen.Skirmish || s == UiScreen.Credits || s == UiScreen.Encyclopedia) AudioManager.PlayMusic("menu");
        }

        void BeginLoad(string text, Action a)
        {
            _loadingText = text; _pendingLoad = a; _loadingFrames = 0;
            Screen = UiScreen.Loading;
        }

        void EndSession()
        {
            if (_session != null) { _session.Destroy(); _session = null; }
            _hud = null;
            AudioManager.StopAllSfx();
        }

        void StartDemo()
        {
            EndSession();
            try
            {
                var c = new SkirmishConfig { MapId = "twin_rivers", Spectator = true };
                c.Settings.StartBase = StartingBase.SmallBase; c.Settings.StartUnits = StartingUnits.Heavy; c.Settings.StartingCredits = 15000;
                c.Settings.Seed = UnityEngine.Random.Range(1, 100000); c.Settings.Fog = FogMode.Off;
                c.Slots.Add(new SlotConfig { Human = false, Faction = FactionId.Alliance, Team = 0, Color = 0, Difficulty = AIDifficulty.Hard, Name = "Alliance AI" });
                c.Slots.Add(new SlotConfig { Human = false, Faction = FactionId.Dominion, Team = 1, Color = 1, Difficulty = AIDifficulty.Hard, Name = "Dominion AI" });
                var w = GameSetup.CreateSkirmish(c);
                _session = new GameSession(w, 0, false, null, AIDifficulty.Normal, c, true, _cam);
                _session.Cam.TargetZoom = _session.Cam.Zoom = 9f;
                _session.Speed = 1.25f;
                _demoTimer = 0; _demoCamTimer = 0;
                _demoCamTarget = new Vec2(w.Map.Width * 0.5f, w.Map.Height * 0.5f);
            }
            catch (Exception ex) { Debug.LogError("Demo battle failed to start: " + ex); _session = null; }
        }

        public void StartIntro()
        {
            BeginLoad("The Aurite War", () =>
            {
                EndSession();
                var def = CampaignDatabase.Intro;
                var w = GameSetup.CreateMission(def, AIDifficulty.Normal);
                _session = new GameSession(w, w.Mission.Human.Id, true, def, AIDifficulty.Normal, null, false, _cam);
                _hud = new Hud(_session) { OnMenu = Pause };
                _isIntro = true; _viewingBattlefield = false;
                Screen = UiScreen.InGame;
                AudioManager.PlayMusic(def.Music ?? "tension");
            });
        }

        public void StartMission(MissionDef def, AIDifficulty diff)
        {
            _lastMission = def; _lastDifficulty = diff; _lastSkirmish = null;
            BeginLoad("Mission " + def.Index + ": " + def.Title, () =>
            {
                EndSession();
                var w = GameSetup.CreateMission(def, diff);
                _session = new GameSession(w, w.Mission.Human.Id, true, def, diff, null, false, _cam);
                _hud = new Hud(_session) { OnMenu = Pause };
                _isIntro = false; _viewingBattlefield = false;
                Screen = UiScreen.InGame;
                AudioManager.PlayMusic(def.Music ?? "battle");
            });
        }

        public void StartSkirmish(SkirmishConfig c)
        {
            _lastSkirmish = SimSerializer.Clone(c); _lastMission = null;
            BeginLoad("Skirmish: " + (MapLibrary.Find(c.MapId)?.Name ?? c.MapId), () =>
            {
                EndSession();
                var w = GameSetup.CreateSkirmish(c);
                int local = 0;
                foreach (var p in w.Players) if (p.IsHuman) { local = p.Id; break; }
                _session = new GameSession(w, local, false, null, AIDifficulty.Normal, c, false, _cam);
                _hud = new Hud(_session) { OnMenu = Pause };
                _isIntro = false; _viewingBattlefield = false;
                Screen = UiScreen.InGame;
                var me = w.GetPlayer(local);
                AudioManager.PlayMusic(me != null && me.Faction == FactionId.Dominion ? "dominion" : c.Spectator ? "battle" : "alliance");
            });
        }

        public void Restart()
        {
            if (_lastMission != null) StartMission(_lastMission, _lastDifficulty);
            else if (_lastSkirmish != null) StartSkirmish(SimSerializer.Clone(_lastSkirmish));
            else QuitToMenu();
        }

        public void Pause()
        {
            if (_session == null || _session.Demo) return;
            _session.Paused = true;
            Screen = UiScreen.Paused;
        }

        public void Resume()
        {
            if (_session == null) { Go(UiScreen.MainMenu); return; }
            _session.Paused = false;
            Screen = UiScreen.InGame;
        }

        public void QuitToMenu(bool toMain = true)
        {
            EndSession();
            _isIntro = false;
            StartDemo();
            Screen = toMain ? UiScreen.MainMenu : Screen;
            AudioManager.PlayMusic("menu");
        }

        public void ViewBattlefield() { _viewingBattlefield = true; Screen = UiScreen.InGame; }

        public void Quit()
        {
            ClientSettings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ save / load
        public void SaveTo(string slot, string label)
        {
            if (_session == null || _session.Demo) return;
            try
            {
                var title = _session.Mission != null ? _session.Mission.Title : (MapLibrary.Find(_session.Skirmish?.MapId)?.Name ?? "Skirmish");
                var snap = _session.Snapshot(label ?? title);
                SaveManager.Save(slot, snap, _session.Mission != null ? "Mission " + _session.Mission.Index + " (" + _session.Mission.Faction + ")" : "Skirmish - " + title);
                _menus.ShowStatus("Game saved.");
            }
            catch (Exception ex) { Debug.LogError("Save failed: " + ex); _menus.ShowStatus("Save failed: " + ex.Message); }
        }

        public void LoadSlot(string slot)
        {
            WorldSnapshot snap;
            try { snap = SaveManager.Load(slot); }
            catch (Exception ex) { _menus.ShowStatus("Could not load: " + ex.Message); return; }
            BeginLoad("Loading saved game", () =>
            {
                try
                {
                    EndSession();
                    var w = SaveGame.Restore(snap);
                    MissionDef def = snap.IsMission ? CampaignDatabase.Find(snap.MissionId) : null;
                    _session = new GameSession(w, snap.LocalPlayer, snap.IsMission, def, snap.MissionDifficulty, snap.Skirmish, false, _cam);
                    _session.Cam.LookAt(new Vec2(snap.CameraX, snap.CameraY));
                    _hud = new Hud(_session) { OnMenu = Pause };
                    _lastMission = def; _lastDifficulty = snap.MissionDifficulty; _lastSkirmish = snap.Skirmish;
                    _isIntro = false; _viewingBattlefield = false;
                    Screen = UiScreen.InGame;
                    AudioManager.PlayMusic(def?.Music ?? "battle");
                    _session.Notify("Game loaded.", Color.white, 3f);
                }
                catch (Exception ex)
                {
                    Debug.LogError("Load failed: " + ex);
                    QuitToMenu();
                    _menus.ShowStatus("Load failed: " + ex.Message);
                }
            });
        }

        // ------------------------------------------------------------------ frame
        void Update()
        {
            InputState.BeginFrame();
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            AudioManager.Update(dt);
            IconCache.Process(Screen == UiScreen.Loading ? 0 : 4);

            if (Screen == UiScreen.Loading)
            {
                if (_pendingLoad != null && ++_loadingFrames >= 3)
                {
                    var a = _pendingLoad; _pendingLoad = null;
                    try { a(); }
                    catch (Exception ex) { Debug.LogError("Loading failed: " + ex); QuitToMenu(); _menus.ShowStatus("Failed to start: " + ex.Message); }
                }
                return;
            }

            var keys = ClientSettings.Current.Keys;
            if (keys.Pressed(GameAction.Screenshot))
            {
                var path = Path.Combine(Application.persistentDataPath, "screenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png");
                ScreenCapture.CaptureScreenshot(path);
                _menus.ShowStatus("Screenshot saved to " + path);
            }

            if (_session == null) return;
            bool inGame = Screen == UiScreen.InGame;
            if (_session.Demo) UpdateDemo(dt);

            if (inGame && !_session.Demo)
            {
                if (keys.Pressed(GameAction.Pause))
                {
                    InputState.EatKey(keys.Get(GameAction.Pause).Key);
                    if (_isIntro) { FinishIntro(); return; }
                    if (_session.Controller.Mode != CursorMode.Normal) _session.Controller.Mode = CursorMode.Normal;
                    else if (_viewingBattlefield) Screen = UiScreen.Results;
                    else Pause();
                }
                if (keys.Pressed(GameAction.QuickSave) && !_session.W.Cinematic && !_session.Ended) SaveTo("quicksave", "Quick save");
                if (keys.Pressed(GameAction.QuickLoad) && SaveManager.Exists("quicksave")) { LoadSlot("quicksave"); return; }
                if (keys.Pressed(GameAction.ToggleObjectives)) _session.ShowObjectives = !_session.ShowObjectives;
                if (_session.Mission == null)
                {
                    if (keys.Pressed(GameAction.SpeedUp)) _session.Speed = Mathf.Min(2f, _session.Speed + 0.25f);
                    if (keys.Pressed(GameAction.SpeedDown)) _session.Speed = Mathf.Max(0.5f, _session.Speed - 0.25f);
                }
            }
            else if (Screen == UiScreen.Paused && keys.Pressed(GameAction.Pause)) { InputState.EatKey(keys.Get(GameAction.Pause).Key); Resume(); }

            bool allowInput = inGame && !_session.Demo;
            try { _session.Update(dt, allowInput); }
            catch (Exception ex) { Debug.LogError("Session update error: " + ex); }

            if (inGame && _session.Ended && !_viewingBattlefield && !_session.Demo)
            {
                if (_isIntro) { FinishIntro(); return; }
                if (_session.Mission != null && _session.Result > 0) CampaignProgress.Current.MarkComplete(_session.Mission, _session.Difficulty);
                _session.Paused = false;
                Screen = UiScreen.Results;
            }
        }

        void FinishIntro()
        {
            CampaignProgress.Current.IntroSeen = true; CampaignProgress.Save();
            _isIntro = false;
            EndSession();
            StartDemo();
            Screen = UiScreen.Campaign;
            AudioManager.PlayMusic("menu");
        }

        void UpdateDemo(float dt)
        {
            var s = _session;
            _demoTimer += dt; _demoCamTimer -= dt;
            if (_demoCamTimer <= 0)
            {
                _demoCamTimer = 9f;
                // pick an interesting spot: recent combat or a random unit
                var w = s.W;
                Vec2? best = null;
                foreach (var pr in w.Projectiles) { best = pr.Pos; break; }
                if (best == null && w.Units.Count > 0) best = w.Units[UnityEngine.Random.Range(0, w.Units.Count)].Pos;
                _demoCamTarget = best ?? new Vec2(w.Map.Width * 0.5f, w.Map.Height * 0.5f);
            }
            var cur = s.Cam.FocusSim;
            s.Cam.LookAt(Vec2.Lerp(cur, _demoCamTarget, 1f - Mathf.Exp(-dt * 0.4f)), false);
            s.Cam.TargetYaw += dt * 1.5f;
            if (_demoTimer > 420f || s.W.GameOver) StartDemo();
        }

        void OnGUI()
        {
            var e = Event.current;
            InputState.Collect(e);
            UIKit.Init();
            UIKit.BeginScaled();
            try
            {
                switch (Screen)
                {
                    case UiScreen.MainMenu: _menus.MainMenu(); break;
                    case UiScreen.Campaign: _menus.Campaign(); break;
                    case UiScreen.Briefing: _menus.Briefing(); break;
                    case UiScreen.Skirmish: _menus.SkirmishSetup(); break;
                    case UiScreen.Load: _menus.LoadScreen(); break;
                    case UiScreen.Save: _menus.SaveScreen(); break;
                    case UiScreen.Options: _menus.Options(); break;
                    case UiScreen.Credits: _menus.Credits(); break;
                    case UiScreen.Encyclopedia: _menus.Encyclopedia(); break;
                    case UiScreen.Loading: _menus.Loading(_loadingText); break;
                    case UiScreen.InGame:
                        _hud?.Draw();
                        if (_viewingBattlefield)
                        {
                            var r = new Rect(UIKit.VW * 0.5f - 140, 50, 280, 40);
                            UIKit.Block(r);
                            if (UIKit.Btn(r, "Return to results (Esc)", UIKit.Button)) Screen = UiScreen.Results;
                        }
                        break;
                    case UiScreen.Paused: _hud?.Draw(); if (_session != null) _menus.Pause(_session); break;
                    case UiScreen.Results: if (_session != null) _menus.Results(_session); else Screen = UiScreen.MainMenu; break;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is ExitGUIException)) Debug.LogError("UI error: " + ex);
                else throw;
            }
            finally { GUI.matrix = Matrix4x4.identity; }
        }

        void OnApplicationQuit() => ClientSettings.Save();
    }
}
