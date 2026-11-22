using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    public class UiMessage
    {
        public string Speaker, Text;
        public float Time, Duration;
        public Color Color = Color.white;
        public bool Subtitle;
    }

    /// <summary>
    /// One running match (skirmish, campaign mission or menu demo battle): owns the World, drives the fixed-rate
    /// simulation with interpolation, and wires rendering, input, effects, audio and messages to it.
    /// </summary>
    public class GameSession
    {
        public readonly World W;
        public readonly bool IsMission;
        public readonly MissionDef Mission;
        public readonly AIDifficulty Difficulty;
        public readonly SkirmishConfig Skirmish;
        public readonly bool Demo;
        public int LocalPlayer;
        public bool Spectator;

        public readonly GameObject Root;
        public readonly TerrainRenderer TerrainView;
        public readonly ViewManager Views;
        public readonly FxRenderer Fx;
        public readonly OverlayRenderer Overlay;
        public readonly FogTexture Fog;
        public readonly RTSCamera Cam;
        public readonly PlayerController Controller;
        public readonly Minimap Minimap;

        public bool Paused;
        public float Speed = 1f;
        float _acc;
        public float Alpha;
        public readonly List<UiMessage> Messages = new List<UiMessage>();
        public UiMessage Subtitle;
        readonly Queue<UiMessage> _subtitleQueue = new Queue<UiMessage>();
        public float EndTimer = -1;
        public bool Ended;
        public int Result;         // 1 victory, -1 defeat
        public string ResultText;
        public bool ShowObjectives = true;
        float _attackCooldown, _harvCooldown, _lowPowerCooldown;
        float _camPanTime, _camPanDur; Vec2 _camPanFrom, _camPanTo;
        public float SessionTime;
        public System.Action<string> OnNotice;

        public Player Me => W.GetPlayer(LocalPlayer);

        public GameSession(World w, int localPlayer, bool isMission, MissionDef mission, AIDifficulty diff, SkirmishConfig sk, bool demo, Camera cam)
        {
            W = w; LocalPlayer = localPlayer; IsMission = isMission; Mission = mission; Difficulty = diff; Skirmish = sk; Demo = demo;
            Spectator = demo || (sk != null && sk.Spectator);
            Root = new GameObject(demo ? "DemoSession" : "GameSession");
            SceneLighting.Apply(w.Map.Theme);
            TerrainView = new TerrainRenderer(w.Map, Root.transform);
            Views = new ViewManager(w, Root.transform, localPlayer) { RevealAll = Spectator };
            Fog = new FogTexture(w, w.TeamOf(localPlayer)) { RevealAll = Spectator };
            Fx = new FxRenderer(w, Root.transform) { Quality = ClientSettings.Current.QualityLevel == 0 ? 0.5f : 1f };
            Cam = new RTSCamera(cam, w.Map);
            Controller = new PlayerController(w, Cam, Views, localPlayer) { Enabled = !Spectator };
            Controller.OnOrderSound = OrderSound;
            Overlay = new OverlayRenderer(w, Views, Controller, Root.transform);
            Minimap = new Minimap(w, this);
            Speed = Mathf.Clamp(w.Settings.GameSpeed, 0.5f, 2f);
            var me = Me;
            if (me != null)
            {
                Vec2 start = me.StartCell.Center;
                foreach (var b in w.BuildingsOf(localPlayer)) { start = b.Center; break; }
                if (me.StartCell == default(Int2)) foreach (var u in w.UnitsOf(localPlayer)) { start = u.Pos; break; }
                Cam.LookAt(start);
            }
            W.Events.Clear();
            Fog.Update(1f, true);
        }

        public void Destroy()
        {
            Views.Clear();
            Fog.Destroy();
            Minimap.Destroy();
            Object.Destroy(Root);
            if (TerrainHeights.Current != null) TerrainHeights.Current = null;
        }

        // ------------------------------------------------------------------ frame
        public void Update(float dt, bool allowInput)
        {
            SessionTime += dt;
            bool cinematic = W.Cinematic;
            Cam.Locked = cinematic || _camPanDur > 0;
            if (_camPanDur > 0)
            {
                _camPanTime += dt;
                float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(_camPanTime / _camPanDur));
                Cam.LookAt(Vec2.Lerp(_camPanFrom, _camPanTo, t), false);
                if (_camPanTime >= _camPanDur) _camPanDur = 0;
            }
            if (allowInput && !cinematic && !Paused) Controller.Update();
            Cam.Update(dt, ClientSettings.Current.Keys, allowInput);

            if (!Paused)
            {
                _acc += dt * Speed;
                int steps = 0;
                while (_acc >= World.Dt && steps < 8)
                {
                    W.Step();
                    ProcessEvents();
                    _acc -= World.Dt;
                    steps++;
                }
                if (steps == 8) _acc = 0;   // falling behind: drop time rather than spiral
            }
            ProcessEvents();   // events raised by commands between ticks
            Alpha = Mathf.Clamp01(_acc / World.Dt);

            TerrainView.Update();
            Views.Sync(Alpha, dt, Time.time);
            Fog.Update(dt);
            Fx.Update(Paused ? 0 : dt, Cam.Cam, Alpha, Views);
            Overlay.Update(dt, Cam.Cam, Time.time);
            Minimap.Update(dt);
            AudioManager.SetListener(Cam.Focus, Cam.Zoom);
            UpdateMessages(dt);
            AmbientEffects(dt);
            CheckEnd(dt);
        }

        void UpdateMessages(float dt)
        {
            float now = SessionTime;
            Messages.RemoveAll(m => now - m.Time > m.Duration);
            if (Subtitle != null && now - Subtitle.Time > Subtitle.Duration) Subtitle = null;
            if (Subtitle == null && _subtitleQueue.Count > 0) { Subtitle = _subtitleQueue.Dequeue(); Subtitle.Time = now; AudioManager.Play2D(Subtitle.Text.Length > 60 ? "radio_long" : "radio", AudioBus.Voice, 0.8f); }
        }

        public void Notify(string text, Color c, float duration = 5f)
        {
            if (Demo) return;
            Messages.Add(new UiMessage { Text = text, Time = SessionTime, Duration = duration, Color = c });
            if (Messages.Count > 7) Messages.RemoveAt(0);
            OnNotice?.Invoke(text);
        }

        void Say(string speaker, string text)
        {
            if (Demo) return;
            var m = new UiMessage { Speaker = speaker, Text = text, Duration = Mathf.Clamp(2.5f + text.Length * 0.055f, 3f, 9f), Subtitle = true, Color = Color.white };
            _subtitleQueue.Enqueue(m);
            Messages.Add(new UiMessage { Speaker = speaker, Text = text, Time = SessionTime, Duration = 12f, Color = new Color(0.85f, 0.9f, 1f) });
            if (Messages.Count > 7) Messages.RemoveAt(0);
        }

        void OrderSound(string key, Vector3? pos)
        {
            switch (key)
            {
                case "select": AudioManager.Play2D("click_soft", AudioBus.Ui, 0.6f); break;
                case "ack": AudioManager.Play2D("ack", AudioBus.Voice, 0.5f, Random.Range(0.95f, 1.1f)); break;
                case "ack_attack": AudioManager.Play2D("ack", AudioBus.Voice, 0.55f, 0.85f); break;
                default:
                    if (pos.HasValue) AudioManager.PlayAt(key, pos.Value, 1f, 1f, 6); else AudioManager.Play2D(key, AudioBus.Ui);
                    break;
            }
        }

        // smoke from damaged buildings and burning vehicles
        float _ambientTimer;
        void AmbientEffects(float dt)
        {
            if (Paused) return;
            _ambientTimer += dt;
            if (_ambientTimer < 0.12f) return;
            _ambientTimer = 0;
            foreach (var kv in Views.Views)
            {
                var v = kv.Value;
                if (!v.Visible) continue;
                var e = W.Get(kv.Key);
                if (e == null || e.HpRatio > 0.5f) continue;
                if (Random.value > (e.IsBuilding ? 0.6f : 0.3f)) continue;
                var p = v.Go.transform.position + Vector3.up * v.Parts.Height * 0.7f + Random.insideUnitSphere * (e.IsBuilding ? e.Radius * 0.5f : 0.1f);
                Fx.Smoke(p, e.IsBuilding ? 0.3f : 0.15f, new Color(0.15f, 0.14f, 0.13f, 0.55f));
                if (e.HpRatio < 0.25f) Fx.Fire(p, e.IsBuilding ? 0.25f : 0.12f);
            }
        }

        void CheckEnd(float dt)
        {
            if (Ended || Demo) return;
            bool over = W.GameOver || (W.Mission != null && W.Mission.Result != 0);
            if (!over) return;
            if (EndTimer < 0)
            {
                EndTimer = 3.5f;
                var me = Me;
                if (W.Mission != null && W.Mission.Result != 0) Result = W.Mission.Result;
                else if (Spectator) Result = 0;
                else Result = me != null && W.WinningTeam == me.Team ? 1 : -1;
                ResultText = W.Mission?.ResultReason;
                AudioManager.PlayMusic(Result > 0 ? "victory" : Result < 0 ? "defeat" : "menu");
                Notify(Result > 0 ? "Mission accomplished." : Result < 0 ? "Mission failed." : "The battle is over.", Result > 0 ? Color.green : Color.red, 6f);
            }
            EndTimer -= dt;
            if (EndTimer <= 0) Ended = true;
        }

        // ------------------------------------------------------------------ events → feedback
        bool Mine(SimEvent e) => e.Player == LocalPlayer || e.Player == -1;
        Vector3 WP(Vec2 p, float h = 0.3f) => new Vector3(p.X, Conv.Ground(W.Map, p) + h, p.Y);
        bool Seen(Vec2 p) => Spectator || W.Fog.IsVisible(W.TeamOf(LocalPlayer), p);

        void ProcessEvents()
        {
            if (W.Events.Count == 0) return;
            foreach (var e in W.Events) Handle(e);
            W.Events.Clear();
        }

        void Handle(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Fire:
                {
                    if (!Seen(e.Pos) && !Seen(e.Pos2)) break;
                    var wd = Catalog.Weapon(e.Text);
                    if (wd == null) break;
                    var v = Views.Get(e.EntityId);
                    Vector3 muzzle = v != null && v.Visible ? v.Muzzle() : WP(e.Pos, 0.4f + e.Value);
                    var dir = (WP(e.Pos2) - muzzle); dir.y = 0; dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
                    float size = Mathf.Clamp(wd.Damage / 120f, 0.12f, 0.6f);
                    switch (wd.Projectile)
                    {
                        case ProjectileKind.Hitscan:
                            Fx.Muzzle(muzzle, dir, size * 0.6f, new Color(1f, 0.85f, 0.4f));
                            Fx.LineFx(muzzle, WP(e.Pos2, 0.25f), 0.06f, 0.025f, new Color(1f, 0.9f, 0.6f, 0.7f));
                            break;
                        case ProjectileKind.Beam:
                        case ProjectileKind.Rail:
                            var bc = wd.Projectile == ProjectileKind.Rail ? new Color(0.5f, 0.75f, 1f, 1f) : new Color(1f, 0.5f, 0.9f, 1f);
                            if (e.Text.Contains("arc")) bc = new Color(0.6f, 0.8f, 1f);
                            Fx.LineFx(muzzle, WP(e.Pos2, 0.3f), 0.25f, 0.08f, bc);
                            Fx.LineFx(muzzle, WP(e.Pos2, 0.3f), 0.15f, 0.25f, bc.WithA(0.35f));
                            Fx.Muzzle(muzzle, dir, 0.25f, bc);
                            break;
                        case ProjectileKind.Flame:
                            for (int i = 0; i < 4; i++) Fx.Emit(muzzle, (dir * 4f + Random.insideUnitSphere) * (0.8f + i * 0.2f), 0.35f, 0.12f, new Color(1f, 0.75f, 0.3f), new Color(0.9f, 0.2f, 0.05f, 0f), true, 0.6f, -1f);
                            break;
                        case ProjectileKind.Torpedo:
                            break;
                        default:
                            Fx.Muzzle(muzzle, dir, size, new Color(1f, 0.8f, 0.35f));
                            if (wd.Projectile == ProjectileKind.Shell || wd.Projectile == ProjectileKind.Ballistic) Fx.Smoke(muzzle, size * 0.8f, new Color(0.6f, 0.6f, 0.6f, 0.4f));
                            break;
                    }
                    AudioManager.PlayAt(wd.Sound, muzzle, Mathf.Clamp(0.4f + wd.Damage / 200f, 0.4f, 1f), 1f, wd.Damage > 80 ? 4 : 2);
                    break;
                }
                case SimEventType.Impact:
                {
                    if (!Seen(e.Pos)) break;
                    var wd = Catalog.Weapon(e.Text);
                    var p = WP(e.Pos, 0.1f);
                    var t = W.Map.Get((int)e.Pos.X, (int)e.Pos.Y);
                    bool water = t == Terrain.Water;
                    float splash = Mathf.Max(e.Value, wd != null ? wd.Splash : 0);
                    if (water && (wd == null || wd.Projectile != ProjectileKind.Hitscan)) { Fx.Splash(p, 0.5f + splash * 0.5f); AudioManager.PlayAt("splash", p, 0.6f); break; }
                    if (wd != null && (wd.Projectile == ProjectileKind.Hitscan || wd.Projectile == ProjectileKind.Bullet) && splash < 0.4f)
                    {
                        Fx.Sparks(p, 2, new Color(1f, 0.85f, 0.5f));
                        if (Random.value < 0.3f) Fx.Smoke(p, 0.06f, new Color(0.5f, 0.45f, 0.4f, 0.5f));
                        break;
                    }
                    if (wd != null && wd.EmpSeconds > 0) { Fx.Ring(p, 1.5f, new Color(0.5f, 0.7f, 1f, 0.8f), 0.5f); AudioManager.PlayAt("emp", p, 0.5f); break; }
                    if (wd != null && wd.Projectile == ProjectileKind.Flame) { Fx.Fire(p, 0.3f); break; }
                    Fx.Explosion(p, 0.35f + splash * 0.6f);
                    AudioManager.PlayAt(splash > 1.5f ? "explosion_medium" : "explosion_small", p, 0.7f, 1f, 3);
                    if (splash > 1.5f) Cam.Shake(0.05f * splash);
                    break;
                }
                case SimEventType.Explosion:
                {
                    var p = WP(e.Pos, 0.2f);
                    if (!Seen(e.Pos)) break;
                    Fx.Explosion(p, Mathf.Max(0.8f, e.Value));
                    AudioManager.PlayAt(e.Value > 2 ? "explosion_large" : "explosion_medium", p, 0.9f, 1f, 5);
                    Cam.Shake(0.15f);
                    break;
                }
                case SimEventType.UnitDied:
                {
                    if (!Seen(e.Pos)) break;
                    var ud = Catalog.Unit(e.Text);
                    var p = WP(e.Pos, 0.2f + e.Value);
                    if (ud != null && ud.Category == UnitCategory.Infantry) { Fx.Emit(p, Vector3.up * 0.2f, 0.4f, 0.1f, new Color(0.5f, 0.1f, 0.08f, 0.6f), new Color(0.3f, 0.05f, 0.05f, 0f), false, 0.1f); break; }
                    float s = ud == null ? 1f : ud.Category == UnitCategory.Naval ? 1.8f : ud.Category == UnitCategory.Aircraft ? 1.2f : 1f;
                    Fx.Explosion(p, s);
                    AudioManager.PlayAt(s > 1.5f ? "explosion_large" : "explosion_medium", p, 1f, 1f, 6);
                    Cam.Shake(0.08f * s);
                    if (e.Player == LocalPlayer && ud != null && ud.Cost >= 700) Notify(ud.Name + " lost.", new Color(1f, 0.6f, 0.5f), 3f);
                    break;
                }
                case SimEventType.BuildingDestroyed:
                {
                    var p = WP(e.Pos, 0.3f);
                    if (Seen(e.Pos) || e.Player == LocalPlayer)
                    {
                        for (int i = 0; i < 3 + (int)e.Value; i++) Fx.Explosion(p + Random.insideUnitSphere * e.Value * 0.4f, 0.8f + e.Value * 0.4f);
                        AudioManager.PlayAt("building_collapse", p, 1f, 1f, 8);
                        Cam.Shake(0.25f);
                    }
                    if (e.Player == LocalPlayer) { var bd = Catalog.Building(e.Text); Notify((bd?.Name ?? "Structure") + " destroyed.", new Color(1f, 0.5f, 0.4f)); Controller.AddAlert(e.Pos); }
                    break;
                }
                case SimEventType.ConstructionComplete:
                    if (e.Player == LocalPlayer) { Notify("Construction complete.", new Color(0.6f, 1f, 0.6f), 3f); AudioManager.Play2D("ready", AudioBus.Voice); }
                    break;
                case SimEventType.UnitReady:
                    if (e.Player == LocalPlayer) { Notify((Catalog.Unit(e.Text)?.Name ?? "Unit") + " ready.", new Color(0.6f, 1f, 0.6f), 2.5f); AudioManager.Play2D("ready", AudioBus.Voice, 0.7f); }
                    break;
                case SimEventType.ResearchComplete:
                    if (e.Player == LocalPlayer) { Notify("Research complete: " + (Catalog.Tech(e.Text)?.Name ?? e.Text) + ".", new Color(0.6f, 0.9f, 1f)); AudioManager.Play2D("promote", AudioBus.Voice); }
                    break;
                case SimEventType.InsufficientFunds:
                    if (e.Player == LocalPlayer) { Notify("Insufficient funds.", new Color(1f, 0.8f, 0.3f), 3f); AudioManager.Play2D("denied", AudioBus.Voice); }
                    break;
                case SimEventType.SilosNeeded:
                    if (e.Player == LocalPlayer) Notify("Storage full: build silos or refineries.", new Color(1f, 0.8f, 0.3f), 4f);
                    break;
                case SimEventType.LowPower:
                    if (e.Player == LocalPlayer && SessionTime - _lowPowerCooldown > 10f) { _lowPowerCooldown = SessionTime; Notify("Low power. Production slowed and defenses offline.", new Color(1f, 0.7f, 0.3f)); AudioManager.Play2D("power_down", AudioBus.Voice); }
                    break;
                case SimEventType.PowerRestored:
                    if (e.Player == LocalPlayer) { Notify("Power restored.", new Color(0.6f, 1f, 0.6f), 3f); AudioManager.Play2D("power_up", AudioBus.Voice); }
                    break;
                case SimEventType.UnderAttack:
                    if (e.Player == LocalPlayer && SessionTime - _attackCooldown > 15f)
                    {
                        _attackCooldown = SessionTime;
                        Notify("Our base is under attack!", new Color(1f, 0.4f, 0.3f));
                        AudioManager.Play2D("alarm", AudioBus.Voice, 0.6f);
                        Controller.AddAlert(e.Pos);
                        Minimap.Ping(e.Pos, new Color(1f, 0.3f, 0.2f));
                    }
                    break;
                case SimEventType.HarvesterUnderAttack:
                    if (e.Player == LocalPlayer && SessionTime - _harvCooldown > 15f)
                    {
                        _harvCooldown = SessionTime;
                        Notify("Harvester under attack!", new Color(1f, 0.6f, 0.3f));
                        Controller.AddAlert(e.Pos);
                        Minimap.Ping(e.Pos, new Color(1f, 0.6f, 0.2f));
                    }
                    break;
                case SimEventType.SupportPowerReady:
                    if (e.Player == LocalPlayer) { Notify(SupportPowerName(e.Text) + " ready.", new Color(1f, 0.8f, 0.4f)); AudioManager.Play2D("ready", AudioBus.Voice); }
                    break;
                case SimEventType.SupportPowerLaunched:
                {
                    var name = SupportPowerName(e.Text);
                    bool super = e.Player == -1;
                    if (e.OtherId == LocalPlayer) Notify(name + " launched.", new Color(1f, 0.8f, 0.4f));
                    else if (super) { Notify("Warning: enemy " + name + " launched!", new Color(1f, 0.3f, 0.2f), 6f); AudioManager.Play2D("alarm", AudioBus.Voice); Minimap.Ping(e.Pos, Color.red); Controller.AddAlert(e.Pos); }
                    if (e.Text == "HammerfallMissile" || e.Text == "OrbitalLance") AudioManager.Play2D("superweapon_charge", AudioBus.Sfx, 0.6f);
                    break;
                }
                case SimEventType.SupportPowerImpact:
                {
                    var p = WP(e.Pos, 0.2f);
                    if (e.Text == "OrbitalLance")
                    {
                        Fx.LineFx(p + Vector3.up * 40f, p, 1.2f, 1.2f, new Color(0.6f, 0.85f, 1f));
                        Fx.LineFx(p + Vector3.up * 40f, p, 0.8f, 3f, new Color(0.6f, 0.85f, 1f, 0.4f));
                        for (int i = 0; i < 6; i++) Fx.Explosion(p + Random.insideUnitSphere * e.Value * 0.5f, 2f);
                        Fx.Ring(p, e.Value * 1.5f, new Color(0.6f, 0.85f, 1f), 0.8f);
                        AudioManager.PlayAt("lance", p, 1f, 1f, 10);
                    }
                    else if (e.Text == "HammerfallMissile")
                    {
                        for (int i = 0; i < 10; i++) Fx.Explosion(p + Random.insideUnitSphere * e.Value * 0.6f, 2.6f);
                        Fx.Ring(p, e.Value * 1.6f, new Color(1f, 0.55f, 0.2f), 1f);
                    }
                    else if (e.Text == "EmpPulse") { Fx.Ring(p, e.Value, new Color(0.5f, 0.7f, 1f), 0.6f); Fx.Ring(p, e.Value * 0.6f, new Color(0.7f, 0.85f, 1f), 0.4f); AudioManager.PlayAt("emp", p, 1f, 1f, 9); break; }
                    else if (e.Text == "SkyeyeReveal") { Fx.Ring(p, e.Value, new Color(0.4f, 1f, 0.6f, 0.6f), 1.2f); break; }
                    AudioManager.PlayAt("explosion_huge", p, 1f, 1f, 10);
                    Cam.Shake(1.0f);
                    break;
                }
                case SimEventType.Intercepted:
                    Notify("Superweapon strike partially intercepted by countermeasures.", new Color(0.6f, 0.9f, 1f));
                    Fx.Ring(WP(e.Pos, 2f), 3f, new Color(0.5f, 0.8f, 1f), 0.6f);
                    break;
                case SimEventType.SuperweaponDetected:
                    if (W.GetPlayer(e.OtherId) is Player op && W.IsEnemy(LocalPlayer, op.Id)) { Notify("Enemy superweapon detected.", new Color(1f, 0.35f, 0.25f), 6f); AudioManager.Play2D("alarm", AudioBus.Voice, 0.7f); }
                    break;
                case SimEventType.Promoted:
                    if (e.Player == LocalPlayer) { Fx.Sparks(WP(e.Pos, 0.6f), 8, new Color(1f, 0.9f, 0.3f)); AudioManager.PlayAt("promote", WP(e.Pos), 0.6f); }
                    break;
                case SimEventType.Captured:
                    if (e.Player == LocalPlayer) Notify((Catalog.Building(e.Text)?.Name ?? "Structure") + " captured.", new Color(0.5f, 0.9f, 1f));
                    else if (e.OtherId == LocalPlayer) { Notify("Structure captured by the enemy!", new Color(1f, 0.4f, 0.3f)); Controller.AddAlert(e.Pos); }
                    break;
                case SimEventType.Sold:
                    if (e.Player == LocalPlayer) AudioManager.PlayAt("sell", WP(e.Pos), 0.8f);
                    break;
                case SimEventType.BuildingPlaced:
                    if (Seen(e.Pos)) { Fx.Smoke(WP(e.Pos, 0.2f), 0.6f, new Color(0.55f, 0.5f, 0.45f, 0.5f)); }
                    break;
                case SimEventType.Deployed:
                    if (e.Player == LocalPlayer) AudioManager.PlayAt("place", WP(e.Pos), 1f);
                    break;
                case SimEventType.OreDelivered:
                    if (e.Player == LocalPlayer && Seen(e.Pos)) AudioManager.PlayAt("money", WP(e.Pos), 0.4f, 1f, 1);
                    break;
                case SimEventType.Crushed:
                    if (Seen(e.Pos)) { AudioManager.PlayAt("crush", WP(e.Pos), 0.7f); Fx.Emit(WP(e.Pos, 0.05f), Vector3.up * 0.2f, 0.5f, 0.12f, new Color(0.45f, 0.1f, 0.08f, 0.7f), new Color(0.3f, 0.05f, 0.05f, 0f), false); }
                    break;
                case SimEventType.Healed:
                    if (Seen(e.Pos)) Fx.Emit(WP(e.Pos, 0.5f), Vector3.up * 0.6f, 0.6f, 0.08f, new Color(0.4f, 1f, 0.4f), new Color(0.4f, 1f, 0.4f, 0f), true);
                    break;
                case SimEventType.Repairing:
                    if (Seen(e.Pos) && Random.value < 0.4f) Fx.Sparks(WP(e.Pos, 0.6f) + Random.insideUnitSphere * 0.5f, 3, new Color(1f, 0.9f, 0.4f));
                    break;
                case SimEventType.EmpHit:
                    Fx.Sparks(WP(e.Pos, 0.4f), 6, new Color(0.5f, 0.7f, 1f));
                    break;
                case SimEventType.Paradrop:
                    if (e.Player == LocalPlayer) Notify("Airborne troops deployed.", new Color(0.6f, 1f, 0.6f));
                    break;
                case SimEventType.Reinforcements:
                    if (e.Player == LocalPlayer) { Notify("Reinforcements have arrived.", new Color(0.6f, 1f, 0.6f)); Minimap.Ping(e.Pos, new Color(0.4f, 1f, 0.4f)); Controller.AddAlert(e.Pos); }
                    break;
                case SimEventType.MissionMessage:
                    if (e.Player == -1 || e.Player == LocalPlayer) Say(string.IsNullOrEmpty(e.Text2) ? "" : e.Text2, e.Text);
                    break;
                case SimEventType.Hint:
                    if (e.Text != null && e.Text.StartsWith("AI error")) { Debug.LogWarning(e.Text); break; }
                    if (e.Text != null && e.Text.StartsWith("Trigger ")) { Debug.LogWarning(e.Text); break; }
                    Notify("Tip: " + e.Text, new Color(0.8f, 0.9f, 1f), 8f);
                    break;
                case SimEventType.ObjectiveUpdated:
                {
                    var st = (ObjectiveState)(int)e.Value;
                    if (st == ObjectiveState.Completed) { Notify("Objective complete: " + e.Text2, new Color(0.5f, 1f, 0.5f), 6f); AudioManager.Play2D("promote", AudioBus.Ui, 0.7f); }
                    else if (st == ObjectiveState.Failed) Notify("Objective failed: " + e.Text2, new Color(1f, 0.4f, 0.3f), 6f);
                    else if (st == ObjectiveState.Active) { Notify("New objective: " + e.Text2, new Color(1f, 0.9f, 0.5f), 6f); ShowObjectives = true; AudioManager.Play2D("build_start", AudioBus.Ui, 0.6f); }
                    break;
                }
                case SimEventType.TimerStarted:
                    Notify("Timer: " + e.Text, new Color(1f, 0.85f, 0.4f), 4f);
                    break;
                case SimEventType.CameraPan:
                    if (Demo) break;
                    if (e.Value <= 0.01f) Cam.LookAt(e.Pos);
                    else { _camPanFrom = Cam.FocusSim; _camPanTo = e.Pos; _camPanDur = e.Value; _camPanTime = 0; }
                    break;
                case SimEventType.CinematicStart:
                    Controller.Selection.Clear(); Controller.Mode = CursorMode.Normal;
                    break;
                case SimEventType.PlayerDefeated:
                    if (!Demo) Notify(e.Text + " has been defeated.", new Color(1f, 0.85f, 0.5f), 6f);
                    break;
                case SimEventType.BridgeDestroyed:
                    if (Seen(e.Pos)) { Fx.Explosion(WP(e.Pos), 1.5f); Notify("A bridge has been destroyed.", new Color(1f, 0.8f, 0.5f)); }
                    break;
                case SimEventType.TreeDestroyed:
                    if (Seen(e.Pos)) Fx.Smoke(WP(e.Pos, 0.4f), 0.3f, new Color(0.3f, 0.28f, 0.2f, 0.5f));
                    break;
                case SimEventType.CannotPlace:
                    if (e.Player == LocalPlayer) AudioManager.Play2D("denied", AudioBus.Ui);
                    break;
                case SimEventType.Loaded:
                case SimEventType.Unloaded:
                    if (e.Player == LocalPlayer) AudioManager.Play2D("click_soft", AudioBus.Ui, 0.5f);
                    break;
            }
        }

        public static string SupportPowerName(string kind)
        {
            if (System.Enum.TryParse<SupportPowerKind>(kind, out var k) && Catalog.Powers.TryGetValue(k, out var d)) return d.Name;
            return kind;
        }

        public WorldSnapshot Snapshot(string label)
        {
            var s = SaveGame.Capture(W, LocalPlayer, Skirmish, Difficulty);
            s.Label = label;
            s.CameraX = Cam.Focus.x; s.CameraY = Cam.Focus.z;
            return s;
        }
    }
}
