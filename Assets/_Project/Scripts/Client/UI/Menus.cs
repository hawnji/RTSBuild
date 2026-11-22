using System;
using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    public enum UiScreen { MainMenu, Campaign, Briefing, Skirmish, Load, Save, Options, Credits, InGame, Paused, Results, Loading, Encyclopedia }

    /// <summary>All front-end and overlay menus (IMGUI). Navigation state lives in GameRoot.</summary>
    public class Menus
    {
        readonly GameRoot _root;
        public Menus(GameRoot root) { _root = root; }

        // shared state
        FactionId _campaignFaction = FactionId.Alliance;
        MissionDef _briefing;
        float _briefingStart;
        Texture2D _briefingMap;
        AIDifficulty _difficulty = AIDifficulty.Normal;
        Vector2 _scroll, _scroll2;
        int _optTab;
        string _saveName = "";
        string _status;
        float _statusTime;
        int _encTab; string _encSel;
        public SkirmishConfig Skirmish = SkirmishConfig.Default1v1();
        Texture2D _skirmishPreview; string _previewFor;
        public UiScreen ReturnFromOptions = UiScreen.MainMenu;
        public UiScreen ReturnFromLoad = UiScreen.MainMenu;

        void Status(string s) { _status = s; _statusTime = Time.unscaledTime; }

        static Rect Center(float w, float h) => new Rect((UIKit.VW - w) * 0.5f, (UIKit.VH - h) * 0.5f, w, h);

        void Backdrop(float alpha = 0.55f) => UIKit.Fill(new Rect(0, 0, UIKit.VW, UIKit.VH), new Color(0, 0, 0, alpha));

        void DrawStatus()
        {
            if (_status == null || Time.unscaledTime - _statusTime > 4f) return;
            var r = new Rect(UIKit.VW * 0.5f - 300, UIKit.VH - 60, 600, 36);
            GUI.Box(r, GUIContent.none, UIKit.PanelDark);
            GUI.Label(r, _status, UIKit.CenterLabel);
        }

        // ================================================================== main menu
        public void MainMenu()
        {
            float vw = UIKit.VW, vh = UIKit.VH;
            GUI.DrawTexture(new Rect(0, 0, vw * 0.45f, vh), UIKit.Gradient, ScaleMode.StretchToFill);
            UIKit.Fill(new Rect(0, 0, vw * 0.36f, vh), new Color(0, 0, 0, 0.55f));
            var titleStyle = new GUIStyle(UIKit.Title) { alignment = TextAnchor.MiddleLeft, fontSize = 64 };
            UIKit.Shadowed(new Rect(70, 90, 800, 80), "AURITE FRONT", titleStyle);
            GUI.Label(new Rect(76, 165, 600, 30), "<size=20><color=#c0c4c8>War for Veldmark</color></size>", UIKit.Label);
            float y = 260, w = 380, h = 52;
            if (UIKit.Btn(new Rect(70, y, w, h), "Campaign", UIKit.ButtonBig)) _root.Go(UiScreen.Campaign); y += h + 12;
            if (UIKit.Btn(new Rect(70, y, w, h), "Skirmish", UIKit.ButtonBig)) _root.Go(UiScreen.Skirmish); y += h + 12;
            if (UIKit.Btn(new Rect(70, y, w, h), "Load Game", UIKit.ButtonBig)) { ReturnFromLoad = UiScreen.MainMenu; _root.Go(UiScreen.Load); } y += h + 12;
            if (UIKit.Btn(new Rect(70, y, w, h), "Field Manual", UIKit.ButtonBig)) _root.Go(UiScreen.Encyclopedia); y += h + 12;
            if (UIKit.Btn(new Rect(70, y, w, h), "Options", UIKit.ButtonBig)) { ReturnFromOptions = UiScreen.MainMenu; _root.Go(UiScreen.Options); } y += h + 12;
            if (UIKit.Btn(new Rect(70, y, w, h), "Credits", UIKit.ButtonBig)) _root.Go(UiScreen.Credits); y += h + 12;
            if (UIKit.Btn(new Rect(70, y, w, h), "Quit", UIKit.ButtonBig)) _root.Quit();
            GUI.Label(new Rect(70, vh - 40, 600, 24), "<size=12><color=#808080>Version " + Application.version + "  -  Background: live AI demonstration battle</color></size>", UIKit.LabelSmall);
            DrawStatus();
        }

        // ================================================================== campaign
        public void Campaign()
        {
            Backdrop(0.7f);
            var r = Center(1300, 820);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            GUI.Label(new Rect(r.x + 24, r.y + 16, 600, 40), "CAMPAIGN", UIKit.Header);
            if (UIKit.Btn(new Rect(r.xMax - 360, r.y + 18, 160, 34), "Watch Intro", UIKit.Button)) _root.StartIntro();
            if (UIKit.Btn(new Rect(r.xMax - 190, r.y + 18, 170, 34), "Back", UIKit.Button)) _root.Go(UiScreen.MainMenu);

            // faction tabs
            var fa = new Rect(r.x + 24, r.y + 70, 300, 44); var fd = new Rect(r.x + 334, r.y + 70, 300, 44);
            if (UIKit.Btn(fa, "Continental Alliance", _campaignFaction == FactionId.Alliance ? UIKit.TabOn : UIKit.Tab)) _campaignFaction = FactionId.Alliance;
            if (UIKit.Btn(fd, "Iron Dominion", _campaignFaction == FactionId.Dominion ? UIKit.TabOn : UIKit.Tab)) _campaignFaction = FactionId.Dominion;
            var fdef = Catalog.Faction(_campaignFaction);
            GUI.Label(new Rect(r.x + 660, r.y + 66, r.width - 690, 60), "<i>\"" + fdef.Motto + "\"</i>\n<size=13>" + fdef.Description + "</size>", UIKit.LabelSmall);

            // mission list
            var list = CampaignDatabase.For(_campaignFaction);
            int unlocked = CampaignProgress.Current.Unlocked(_campaignFaction);
            var la = new Rect(r.x + 24, r.y + 140, 610, r.height - 160);
            GUI.Box(la, GUIContent.none, UIKit.PanelDark);
            float y = la.y + 8;
            foreach (var m in list)
            {
                bool open = m.Index <= unlocked;
                bool done = CampaignProgress.Current.IsComplete(m.Id);
                var row = new Rect(la.x + 8, y, la.width - 16, 50);
                string label = $"<b>{m.Index:00}. {m.Title}</b>  <size=12><color=#a0a0a0>{m.Location}</color></size>" + (done ? "  <color=#70ff70>[complete]</color>" : "");
                if (!open) label = $"<color=#606060>{m.Index:00}. Locked</color>";
                if (UIKit.Btn(row, label, _briefing == m ? UIKit.ListItemOn : UIKit.ListItem, open)) { OpenBriefing(m); }
                y += 54;
            }
            // side info
            var info = new Rect(r.x + 654, r.y + 140, r.width - 678, r.height - 160);
            GUI.Box(info, GUIContent.none, UIKit.PanelDark);
            GUI.Label(new Rect(info.x + 16, info.y + 12, info.width - 32, info.height - 24),
                "<b>THE AURITE WAR</b>\n\n<size=14>" + CampaignDatabase.Prologue + "</size>\n\n<size=13><color=#a0a0a0>Select a mission to read its briefing. Completing a mission unlocks the next one. Progress is saved automatically.</color></size>", UIKit.Label);
        }

        public void OpenBriefing(MissionDef m)
        {
            _briefing = m; _briefingStart = Time.unscaledTime;
            _difficulty = (AIDifficulty)Mathf.Clamp(ClientSettings.Current.LastDifficulty, 0, 3);
            if (_briefingMap != null) UnityEngine.Object.Destroy(_briefingMap);
            _briefingMap = MapPreview(m.Map, 256);
            _root.Go(UiScreen.Briefing);
        }

        public static Texture2D MapPreview(MapSpec spec, int size)
        {
            try
            {
                var map = MapGenerator.Generate(spec);
                var t = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                var px = new Color32[map.Width * map.Height];
                for (int i = 0; i < px.Length; i++)
                {
                    var c = Palette.Minimap(map.Cells[i], map.Theme).Mul(1f + map.Level[i] * 0.07f);
                    if (map.Ore[i] > 0) c = map.OreKind[i] == 1 ? new Color(0.35f, 0.8f, 0.9f) : new Color(0.8f, 0.62f, 0.25f);
                    px[i] = c;
                }
                foreach (var s in map.StartPositions)
                    for (int dy = -2; dy <= 2; dy++) for (int dx = -2; dx <= 2; dx++)
                        if (map.InBounds(s.X + dx, s.Y + dy)) px[(s.Y + dy) * map.Width + s.X + dx] = new Color32(255, 255, 255, 255);
                t.SetPixels32(px); t.Apply();
                return t;
            }
            catch (Exception ex) { Debug.LogWarning("Map preview failed: " + ex.Message); return null; }
        }

        public void Briefing()
        {
            var m = _briefing;
            if (m == null) { _root.Go(UiScreen.Campaign); return; }
            Backdrop(0.75f);
            var r = Center(1300, 820);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            var accent = UIKit.FactionColor(m.Faction);
            UIKit.Fill(new Rect(r.x, r.y, r.width, 4), accent);
            GUI.Label(new Rect(r.x + 24, r.y + 16, 900, 40), $"MISSION {m.Index}: {m.Title.ToUpper()}", UIKit.Header);
            GUI.Label(new Rect(r.x + 26, r.y + 56, 900, 24), $"<color=#a0a0a0>{m.Location}  -  {m.Date}</color>", UIKit.LabelSmall);
            // briefing text with teletype reveal
            int chars = Mathf.Min(m.Briefing.Length, (int)((Time.unscaledTime - _briefingStart) * 90f));
            var ta = new Rect(r.x + 24, r.y + 96, 760, r.height - 190);
            GUI.Box(ta, GUIContent.none, UIKit.PanelDark);
            GUI.Label(new Rect(ta.x + 16, ta.y + 14, ta.width - 32, ta.height - 28), "<size=17>" + m.Briefing.Substring(0, chars) + (chars < m.Briefing.Length ? "_" : "") + "</size>", UIKit.Label);
            if (chars < m.Briefing.Length && Event.current.type == EventType.MouseDown && ta.Contains(Event.current.mousePosition)) _briefingStart = -1000;
            // map + tags
            var mr = new Rect(r.x + 806, r.y + 96, 470, 470);
            GUI.Box(mr, GUIContent.none, UIKit.PanelDark);
            if (_briefingMap != null) GUI.DrawTextureWithTexCoords(new Rect(mr.x + 8, mr.y + 8, mr.width - 16, mr.height - 16), _briefingMap, new Rect(0, 1, 1, -1));
            string tags = m.Tags.Count > 0 ? string.Join(", ", m.Tags) : "";
            GUI.Label(new Rect(mr.x, mr.yMax + 8, mr.width, 50), $"<size=13>Operation type: <b>{tags}</b>\nTheatre: {m.Map.Theme}  -  Map {m.Map.Width}x{m.Map.Height}</size>", UIKit.LabelSmall);
            // difficulty
            GUI.Label(new Rect(mr.x, mr.yMax + 64, 200, 24), "<b>Difficulty</b>", UIKit.Label);
            string[] dn = { "Easy", "Normal", "Hard", "Brutal" };
            for (int i = 0; i < 4; i++)
                if (UIKit.Btn(new Rect(mr.x + i * 118, mr.yMax + 92, 112, 34), dn[i], _difficulty == (AIDifficulty)i ? UIKit.TabOn : UIKit.Tab)) _difficulty = (AIDifficulty)i;
            if (UIKit.Btn(new Rect(r.x + 24, r.yMax - 70, 200, 48), "Back", UIKit.ButtonBig)) _root.Go(UiScreen.Campaign);
            if (UIKit.Btn(new Rect(r.xMax - 284, r.yMax - 70, 260, 48), "Begin Mission", UIKit.ButtonBig))
            {
                ClientSettings.Current.LastDifficulty = (int)_difficulty; ClientSettings.Save();
                _root.StartMission(m, _difficulty);
            }
        }

        // ================================================================== skirmish setup
        public void SkirmishSetup()
        {
            Backdrop(0.72f);
            var r = Center(1500, 900);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            GUI.Label(new Rect(r.x + 24, r.y + 16, 600, 40), "SKIRMISH", UIKit.Header);
            var c = Skirmish;
            var spec = MapLibrary.Find(c.MapId) ?? MapLibrary.Skirmish[0];
            if (_previewFor != spec.Id) { if (_skirmishPreview != null) UnityEngine.Object.Destroy(_skirmishPreview); _skirmishPreview = MapPreview(spec, 256); _previewFor = spec.Id; }

            // maps
            var ml = new Rect(r.x + 24, r.y + 70, 330, 520);
            GUI.Box(ml, GUIContent.none, UIKit.PanelDark);
            float y = ml.y + 8;
            foreach (var s in MapLibrary.Skirmish)
            {
                var row = new Rect(ml.x + 8, y, ml.width - 16, 60);
                string label = $"<b>{s.Name}</b>\n<size=11><color=#a0a0a0>{s.MaxPlayers} players  {s.Theme}  {s.Width}x{s.Height}</color></size>";
                if (UIKit.Btn(row, label, s.Id == c.MapId ? UIKit.ListItemOn : UIKit.ListItem)) SelectMap(s);
                y += 64;
            }
            // preview
            var pv = new Rect(r.x + 370, r.y + 70, 380, 380);
            GUI.Box(pv, GUIContent.none, UIKit.PanelDark);
            if (_skirmishPreview != null) GUI.DrawTextureWithTexCoords(new Rect(pv.x + 6, pv.y + 6, pv.width - 12, pv.height - 12), _skirmishPreview, new Rect(0, 1, 1, -1));
            GUI.Label(new Rect(pv.x, pv.yMax + 6, pv.width, 140), "<size=13>" + spec.Description + "</size>", UIKit.LabelSmall);

            // players
            var pl = new Rect(r.x + 770, r.y + 70, r.width - 794, 360);
            GUI.Box(pl, GUIContent.none, UIKit.PanelDark);
            GUI.Label(new Rect(pl.x + 10, pl.y + 6, 600, 24), "<b>Players</b>  <size=12><color=#a0a0a0>(click a field to cycle it)</color></size>", UIKit.Label);
            y = pl.y + 36;
            int maxSlots = Mathf.Max(2, spec.MaxPlayers);
            while (c.Slots.Count < maxSlots) c.Slots.Add(new SlotConfig { Enabled = false, Faction = FactionId.Dominion, Team = c.Slots.Count % 2, Color = c.Slots.Count, Name = "AI " + (c.Slots.Count + 1) });
            string[] dn = { "Easy", "Normal", "Hard", "Brutal" };
            for (int i = 0; i < maxSlots; i++)
            {
                var s = c.Slots[i];
                float x = pl.x + 10;
                string type = !s.Enabled ? "Closed" : s.Human ? "Human" : "AI " + dn[(int)s.Difficulty];
                if (UIKit.Btn(new Rect(x, y, 150, 34), type, UIKit.ButtonSmall, i > 0 || true))
                {
                    if (i == 0 && !c.Spectator) { s.Human = true; s.Enabled = true; }
                    else if (!s.Enabled) { s.Enabled = true; s.Human = false; s.Difficulty = AIDifficulty.Easy; }
                    else if (s.Human) { s.Human = false; s.Difficulty = AIDifficulty.Easy; }
                    else if (s.Difficulty < AIDifficulty.Brutal) s.Difficulty++;
                    else s.Enabled = i < 2 ? true : false;
                }
                x += 156;
                if (UIKit.Btn(new Rect(x, y, 200, 34), s.Faction == FactionId.Alliance ? "Continental Alliance" : "Iron Dominion", UIKit.ButtonSmall, s.Enabled)) s.Faction = s.Faction == FactionId.Alliance ? FactionId.Dominion : FactionId.Alliance;
                x += 206;
                if (UIKit.Btn(new Rect(x, y, 90, 34), "Team " + (s.Team + 1), UIKit.ButtonSmall, s.Enabled)) s.Team = (s.Team + 1) % Mathf.Max(2, maxSlots);
                x += 96;
                UIKit.Fill(new Rect(x, y + 4, 26, 26), Palette.Team[s.Color % 7]);
                if (UIKit.Btn(new Rect(x + 30, y, 80, 34), Palette.TeamNames[s.Color % 7], UIKit.ButtonSmall, s.Enabled)) s.Color = (s.Color + 1) % 7;
                x += 116;
                if (UIKit.Btn(new Rect(x, y, 100, 34), s.StartIndex < 0 ? "Start: Any" : "Start " + (s.StartIndex + 1), UIKit.ButtonSmall, s.Enabled)) s.StartIndex = s.StartIndex + 1 >= spec.Starts.Count ? -1 : s.StartIndex + 1;
                y += 40;
            }
            if (UIKit.Btn(new Rect(pl.x + 10, pl.yMax - 44, 220, 34), c.Spectator ? "<color=#ffd040>Spectator mode ON</color>" : "Spectator mode OFF", UIKit.ButtonSmall))
            {
                c.Spectator = !c.Spectator;
                c.Slots[0].Human = !c.Spectator;
                if (c.Spectator) { c.Slots[0].Difficulty = AIDifficulty.Normal; }
            }

            // settings
            var st = new Rect(r.x + 770, r.y + 446, r.width - 794, 360);
            GUI.Box(st, GUIContent.none, UIKit.PanelDark);
            GUI.Label(new Rect(st.x + 10, st.y + 6, 300, 24), "<b>Game Settings</b>", UIKit.Label);
            var g = c.Settings;
            float sx = st.x + 10, sy = st.y + 38, cw = (st.width - 30) / 2f;
            int row2 = 0;
            Rect Cell() { var cr = new Rect(sx + (row2 % 2) * (cw + 10), sy + (row2 / 2) * 42, cw, 34); row2++; return cr; }
            int[] credits = { 2500, 5000, 10000, 20000, 50000 };
            if (UIKit.Btn(Cell(), "Starting credits: " + UIKit.Money(g.StartingCredits), UIKit.ButtonSmall)) g.StartingCredits = credits[(Array.IndexOf(credits, g.StartingCredits) + 1) % credits.Length];
            if (UIKit.Btn(Cell(), "Fog of war: " + g.Fog, UIKit.ButtonSmall)) g.Fog = (FogMode)(((int)g.Fog + 1) % 3);
            if (UIKit.Btn(Cell(), "Superweapons: " + (g.Superweapons ? "On" : "Off"), UIKit.ButtonSmall)) g.Superweapons = !g.Superweapons;
            if (UIKit.Btn(Cell(), "Victory: " + VictoryName(g.Victory), UIKit.ButtonSmall)) g.Victory = (VictoryCondition)(((int)g.Victory + 1) % 3);
            if (UIKit.Btn(Cell(), "Starting units: " + g.StartUnits, UIKit.ButtonSmall)) g.StartUnits = (StartingUnits)(((int)g.StartUnits + 1) % 3);
            if (UIKit.Btn(Cell(), "Starting base: " + BaseName(g.StartBase), UIKit.ButtonSmall)) g.StartBase = (StartingBase)(((int)g.StartBase + 1) % 3);
            if (UIKit.Btn(Cell(), "Game speed: " + g.GameSpeed.ToString("0.##") + "x", UIKit.ButtonSmall)) g.GameSpeed = g.GameSpeed >= 2f ? 0.5f : g.GameSpeed + 0.25f;
            if (UIKit.Btn(Cell(), "Ore regrowth: " + (g.OreRegrowth ? "On" : "Off"), UIKit.ButtonSmall)) g.OreRegrowth = !g.OreRegrowth;
            if (UIKit.Btn(Cell(), "Random seed: " + g.Seed, UIKit.ButtonSmall)) g.Seed = UnityEngine.Random.Range(1, 99999);

            if (UIKit.Btn(new Rect(r.x + 24, r.yMax - 70, 200, 48), "Back", UIKit.ButtonBig)) _root.Go(UiScreen.MainMenu);
            if (UIKit.Btn(new Rect(r.x + 240, r.yMax - 70, 200, 48), "Quick 2v2", UIKit.ButtonBig)) { Skirmish = SkirmishConfig.Default2v2(); _previewFor = null; }
            int enabled = c.Slots.FindAll(s => s.Enabled).Count;
            int teams = new HashSet<int>(c.Slots.FindAll(s => s.Enabled).ConvertAll(s => s.Team)).Count;
            bool valid = enabled >= 2 && teams >= 2 && enabled <= spec.Starts.Count;
            if (!valid) GUI.Label(new Rect(r.xMax - 700, r.yMax - 60, 400, 30), "<color=#ff8060>Need at least two players on different teams.</color>", UIKit.RightLabel);
            if (UIKit.Btn(new Rect(r.xMax - 284, r.yMax - 70, 260, 48), "Start Battle", UIKit.ButtonBig, valid))
            {
                foreach (var s in c.Slots) if (s.Enabled && string.IsNullOrEmpty(s.Name)) s.Name = s.Human ? ClientSettings.Current.PlayerName : "AI";
                if (!c.Spectator) c.Slots[0].Name = ClientSettings.Current.PlayerName;
                c.TeamGame = enabled > 2;
                _root.StartSkirmish(c);
            }
        }

        void SelectMap(MapSpec s)
        {
            Skirmish.MapId = s.Id;
            var slots = Skirmish.Slots;
            for (int i = 0; i < slots.Count; i++) { if (i >= s.MaxPlayers) slots[i].Enabled = false; if (slots[i].StartIndex >= s.Starts.Count) slots[i].StartIndex = -1; }
            if (s.MaxPlayers >= 4 && slots.FindAll(x => x.Enabled).Count == 2)
            {
                var d = SkirmishConfig.Default2v2(); d.MapId = s.Id; d.Settings = Skirmish.Settings; Skirmish = d;
            }
        }

        static string VictoryName(VictoryCondition v) => v == VictoryCondition.DestroyStructures ? "Destroy structures" : v == VictoryCondition.Annihilation ? "Annihilation" : "Commander (yard/MCV)";
        static string BaseName(StartingBase b) => b == StartingBase.MCV ? "MCV" : b == StartingBase.ConstructionYard ? "Construction yard" : "Small base";

        // ================================================================== load / save
        public void LoadScreen()
        {
            Backdrop(0.72f);
            var r = Center(900, 700);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            GUI.Label(new Rect(r.x + 24, r.y + 16, 600, 40), "LOAD GAME", UIKit.Header);
            var list = SaveManager.List();
            var la = new Rect(r.x + 24, r.y + 70, r.width - 48, r.height - 160);
            GUI.Box(la, GUIContent.none, UIKit.PanelDark);
            _scroll = GUI.BeginScrollView(la, _scroll, new Rect(0, 0, la.width - 20, Mathf.Max(la.height, list.Count * 64 + 10)));
            float y = 6;
            if (list.Count == 0) GUI.Label(new Rect(10, 10, 500, 30), "No saved games.", UIKit.Label);
            foreach (var m in list)
            {
                var row = new Rect(6, y, la.width - 150, 58);
                if (UIKit.Btn(row, $"<b>{m.Label}</b>\n<size=12><color=#a0a0a0>{m.SavedAt}   {m.Description}   game time {UIKit.Time(m.GameTime)}</color></size>", UIKit.ListItem)) _root.LoadSlot(m.Slot);
                if (UIKit.Btn(new Rect(row.xMax + 6, y + 12, 100, 34), "Delete", UIKit.ButtonSmall)) { SaveManager.Delete(m.Slot); Status("Save deleted."); }
                y += 64;
            }
            GUI.EndScrollView();
            if (UIKit.Btn(new Rect(r.x + 24, r.yMax - 70, 200, 48), "Back", UIKit.ButtonBig)) _root.Go(ReturnFromLoad);
            DrawStatus();
        }

        public void SaveScreen()
        {
            Backdrop(0.72f);
            var r = Center(900, 700);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            GUI.Label(new Rect(r.x + 24, r.y + 16, 600, 40), "SAVE GAME", UIKit.Header);
            GUI.Label(new Rect(r.x + 24, r.y + 74, 120, 30), "Name:", UIKit.Label);
            _saveName = GUI.TextField(new Rect(r.x + 100, r.y + 70, 520, 36), _saveName, 40, UIKit.TextField);
            if (UIKit.Btn(new Rect(r.x + 640, r.y + 70, 236, 36), "Save New", UIKit.Button))
            {
                var slot = "save_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                _root.SaveTo(slot, string.IsNullOrWhiteSpace(_saveName) ? null : _saveName);
                Status("Game saved.");
            }
            var list = SaveManager.List();
            var la = new Rect(r.x + 24, r.y + 120, r.width - 48, r.height - 210);
            GUI.Box(la, GUIContent.none, UIKit.PanelDark);
            _scroll = GUI.BeginScrollView(la, _scroll, new Rect(0, 0, la.width - 20, Mathf.Max(la.height, list.Count * 64 + 10)));
            float y = 6;
            foreach (var m in list)
            {
                var row = new Rect(6, y, la.width - 150, 58);
                GUI.Label(new Rect(row.x + 8, row.y + 4, row.width, row.height), $"<b>{m.Label}</b>\n<size=12><color=#a0a0a0>{m.SavedAt}   {m.Description}</color></size>", UIKit.Label);
                if (UIKit.Btn(new Rect(row.xMax + 6, y + 12, 100, 34), "Overwrite", UIKit.ButtonSmall)) { _root.SaveTo(m.Slot, m.Label); Status("Game saved."); }
                y += 64;
            }
            GUI.EndScrollView();
            if (UIKit.Btn(new Rect(r.x + 24, r.yMax - 70, 200, 48), "Back", UIKit.ButtonBig)) _root.Go(UiScreen.Paused);
            DrawStatus();
        }

        // ================================================================== options
        public void Options()
        {
            Backdrop(0.72f);
            var r = Center(1100, 800);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            GUI.Label(new Rect(r.x + 24, r.y + 16, 600, 40), "OPTIONS", UIKit.Header);
            string[] tabs = { "Audio", "Graphics", "Gameplay", "Controls" };
            for (int i = 0; i < tabs.Length; i++) if (UIKit.Btn(new Rect(r.x + 24 + i * 170, r.y + 66, 160, 38), tabs[i], _optTab == i ? UIKit.TabOn : UIKit.Tab)) _optTab = i;
            var a = new Rect(r.x + 24, r.y + 116, r.width - 48, r.height - 200);
            GUI.Box(a, GUIContent.none, UIKit.PanelDark);
            var s = ClientSettings.Current;
            float y = a.y + 20, lx = a.x + 24, cx = a.x + 330, cw = 420;
            bool changed = false;
            float Slider(string label, float v)
            {
                GUI.Label(new Rect(lx, y, 300, 30), label, UIKit.Label);
                float nv = GUI.HorizontalSlider(new Rect(cx, y + 8, cw, 20), v, 0f, 1f);
                GUI.Label(new Rect(cx + cw + 16, y, 80, 30), Mathf.RoundToInt(nv * 100) + "%", UIKit.Label);
                y += 44; if (Mathf.Abs(nv - v) > 0.0001f) changed = true;
                return nv;
            }
            bool Toggle(string label, bool v)
            {
                GUI.Label(new Rect(lx, y, 300, 30), label, UIKit.Label);
                bool nv = UIKit.Btn(new Rect(cx, y - 2, 140, 32), v ? "On" : "Off", v ? UIKit.TabOn : UIKit.Tab) ? !v : v;
                y += 44; if (nv != v) changed = true;
                return nv;
            }
            switch (_optTab)
            {
                case 0:
                    s.MasterVolume = Slider("Master volume", s.MasterVolume);
                    s.MusicVolume = Slider("Music", s.MusicVolume);
                    s.SfxVolume = Slider("Effects", s.SfxVolume);
                    s.VoiceVolume = Slider("Voice and radio", s.VoiceVolume);
                    s.UiVolume = Slider("Interface", s.UiVolume);
                    if (changed) AudioManager.ApplyVolumes();
                    if (UIKit.Btn(new Rect(cx, y, 200, 34), "Test sound", UIKit.ButtonSmall)) AudioManager.Play2D("explosion_medium", AudioBus.Sfx);
                    break;
                case 1:
                {
                    GUI.Label(new Rect(lx, y, 300, 30), "Quality", UIKit.Label);
                    string[] q = { "Low", "Medium", "High" };
                    for (int i = 0; i < 3; i++) if (UIKit.Btn(new Rect(cx + i * 140, y - 2, 134, 32), q[i], s.QualityLevel == i ? UIKit.TabOn : UIKit.Tab)) { s.QualityLevel = i; changed = true; }
                    y += 44;
                    s.Fullscreen = Toggle("Fullscreen", s.Fullscreen);
                    var res = Screen.resolutions;
                    GUI.Label(new Rect(lx, y, 300, 30), "Resolution", UIKit.Label);
                    string cur = s.ResolutionIndex >= 0 && s.ResolutionIndex < res.Length ? res[s.ResolutionIndex].width + "x" + res[s.ResolutionIndex].height : "Current (" + Screen.width + "x" + Screen.height + ")";
                    if (UIKit.Btn(new Rect(cx, y - 2, 300, 32), cur, UIKit.ButtonSmall, res.Length > 0)) { s.ResolutionIndex = s.ResolutionIndex + 1 >= res.Length ? -1 : s.ResolutionIndex + 1; changed = true; }
                    y += 44;
                    s.VSync = Toggle("Vertical sync", s.VSync);
                    GUI.Label(new Rect(lx, y, 300, 30), "Interface scale", UIKit.Label);
                    float us = GUI.HorizontalSlider(new Rect(cx, y + 8, cw, 20), s.UiScale, 0.7f, 1.5f);
                    if (Event.current.type == EventType.MouseUp || Mathf.Abs(us - s.UiScale) > 0.001f) { s.UiScale = Mathf.Round(us * 20f) / 20f; }
                    GUI.Label(new Rect(cx + cw + 16, y, 80, 30), Mathf.RoundToInt(s.UiScale * 100) + "%", UIKit.Label);
                    y += 44;
                    s.ShowFps = Toggle("Show frame rate", s.ShowFps);
                    if (changed) ClientSettings.Apply();
                    break;
                }
                case 2:
                {
                    GUI.Label(new Rect(lx, y, 300, 30), "Scroll speed", UIKit.Label);
                    s.ScrollSpeed = GUI.HorizontalSlider(new Rect(cx, y + 8, cw, 20), s.ScrollSpeed, 0.3f, 2.5f);
                    GUI.Label(new Rect(cx + cw + 16, y, 80, 30), s.ScrollSpeed.ToString("0.0") + "x", UIKit.Label);
                    y += 44;
                    s.EdgeScroll = Toggle("Edge scrolling", s.EdgeScroll);
                    s.InvertZoom = Toggle("Invert mouse wheel zoom", s.InvertZoom);
                    s.AlwaysShowHealth = Toggle("Always show health bars", s.AlwaysShowHealth);
                    s.Subtitles = Toggle("Radio subtitles", s.Subtitles);
                    GUI.Label(new Rect(lx, y, 300, 30), "Commander name", UIKit.Label);
                    s.PlayerName = GUI.TextField(new Rect(cx, y - 2, 300, 32), s.PlayerName ?? "", 24, UIKit.TextField);
                    y += 44;
                    break;
                }
                case 3:
                {
                    _scroll2 = GUI.BeginScrollView(new Rect(a.x + 10, a.y + 10, a.width - 20, a.height - 20), _scroll2, new Rect(0, 0, a.width - 50, s.Keys.Bindings.Count * 36 + 120));
                    float yy = 4;
                    GUI.Label(new Rect(10, yy, 900, 50), "<size=13>Left-click selects, right-click issues orders, middle-drag pans. Ctrl+right-click forces fire, Alt+right-click forces a move, Shift queues waypoints. Ctrl+0-9 assigns a control group, 0-9 recalls it (double-tap to centre).</size>", UIKit.LabelSmall);
                    yy += 56;
                    foreach (GameAction act in Enum.GetValues(typeof(GameAction)))
                    {
                        var b = s.Keys.Get(act);
                        GUI.Label(new Rect(10, yy, 300, 30), Keybinds.Pretty(act), UIKit.Label);
                        bool capturing = _capturing == act && InputState.CaptureNextKey != null;
                        if (UIKit.Btn(new Rect(320, yy - 2, 200, 30), capturing ? "<color=#ffd040>Press a key...</color>" : Keybinds.KeyName(b), UIKit.ButtonSmall))
                        {
                            _capturing = act;
                            var captured = b;
                            InputState.CaptureNextKey = key => { captured.Key = key; captured.Ctrl = InputState.Ctrl; ClientSettings.Save(); };
                        }
                        if (UIKit.Btn(new Rect(530, yy - 2, 90, 30), "Clear", UIKit.ButtonSmall)) b.Key = KeyCode.None;
                        yy += 36;
                    }
                    GUI.EndScrollView();
                    break;
                }
            }
            if (_optTab == 3 && UIKit.Btn(new Rect(r.x + 240, r.yMax - 70, 260, 48), "Reset key bindings", UIKit.ButtonBig)) { s.Keys = Keybinds.Default(); Status("Key bindings reset."); }
            if (UIKit.Btn(new Rect(r.x + 24, r.yMax - 70, 200, 48), "Back", UIKit.ButtonBig)) { ClientSettings.Save(); ClientSettings.Apply(); _root.Go(ReturnFromOptions); }
            DrawStatus();
        }
        GameAction _capturing;

        // ================================================================== credits / field manual
        public void Credits()
        {
            Backdrop(0.75f);
            var r = Center(900, 700);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            GUI.Label(new Rect(r.x + 24, r.y + 16, 600, 40), "CREDITS", UIKit.Header);
            GUI.Label(new Rect(r.x + 30, r.y + 80, r.width - 60, r.height - 170),
                "<b>AURITE FRONT: War for Veldmark</b>\nAn original real-time strategy game.\n\n" +
                "<b>Design, programming, procedural art, synthesized audio and music</b>\nGenerated with Claude Code for Hawnji.\n\n" +
                "<b>Technology</b>\nUnity 6 (Universal Render Pipeline). Engine-independent deterministic simulation in C#.\n" +
                "All meshes, textures, sounds and music are generated procedurally at runtime; the project ships no third-party art or audio.\n\n" +
                "<b>Inspiration</b>\nClassic 1990s base-building real-time strategy games. All factions, units, names, maps, story and assets in this game are original.\n\n" +
                "<size=13><color=#a0a0a0>Thank you for playing.</color></size>", UIKit.Label);
            if (UIKit.Btn(new Rect(r.x + 24, r.yMax - 70, 200, 48), "Back", UIKit.ButtonBig)) _root.Go(UiScreen.MainMenu);
        }

        public void Encyclopedia()
        {
            Backdrop(0.75f);
            var r = Center(1400, 860);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            GUI.Label(new Rect(r.x + 24, r.y + 16, 600, 40), "FIELD MANUAL", UIKit.Header);
            string[] tabs = { "Alliance units", "Alliance structures", "Dominion units", "Dominion structures", "Research", "Support powers", "How to play" };
            for (int i = 0; i < tabs.Length; i++) if (UIKit.Btn(new Rect(r.x + 24 + i * 190, r.y + 66, 184, 36), tabs[i], _encTab == i ? UIKit.TabOn : UIKit.Tab)) { _encTab = i; _encSel = null; _scroll = Vector2.zero; }
            var la = new Rect(r.x + 24, r.y + 114, 360, r.height - 200);
            var da = new Rect(r.x + 400, r.y + 114, r.width - 424, r.height - 200);
            GUI.Box(la, GUIContent.none, UIKit.PanelDark); GUI.Box(da, GUIContent.none, UIKit.PanelDark);
            var ids = new List<string>();
            if (_encTab == 0 || _encTab == 2) ids.AddRange(Catalog.Faction(_encTab == 0 ? FactionId.Alliance : FactionId.Dominion).Units);
            else if (_encTab == 1 || _encTab == 3) ids.AddRange(Catalog.Faction(_encTab == 1 ? FactionId.Alliance : FactionId.Dominion).Buildings);
            else if (_encTab == 4) { ids.AddRange(Catalog.Faction(FactionId.Alliance).Techs); ids.AddRange(Catalog.Faction(FactionId.Dominion).Techs); }
            if (_encTab == 5)
            {
                float yy = da.y + 14;
                foreach (var p in Catalog.Powers.Values)
                {
                    GUI.Label(new Rect(da.x + 16, yy, da.width - 32, 80), $"<b>{p.Name}</b>{(p.IsSuperweapon ? "  <color=#ff8060>[superweapon]</color>" : "")}\n<size=13>{p.Description}  Charge {UIKit.Time(p.ChargeTime)}, radius {p.Radius}, warning {p.WarningTime}s.</size>", UIKit.Label);
                    yy += 84;
                }
                GUI.Label(new Rect(la.x + 12, la.y + 12, la.width - 24, la.height - 24), "<size=13>Superweapons are announced to every player when built and when launched. Countermeasure structures (Aegis / Bastion) reduce the damage of strikes landing within their radius.</size>", UIKit.LabelSmall);
            }
            else if (_encTab == 6)
            {
                GUI.Label(new Rect(da.x + 16, da.y + 14, da.width - 32, da.height - 28),
                    "<b>Economy.</b> Deploy your mobile HQ to found a base. Refineries come with a harvester; harvesters gather ore (gold) and aurite crystals (blue, double value). Credits above your storage cap are lost: build silos.\n\n" +
                    "<b>Power.</b> Most structures consume power. If consumption exceeds production, production slows, radar goes offline and powered defenses stop firing.\n\n" +
                    "<b>Building.</b> Choose a structure on the sidebar; when it is READY click it again and place it near your existing buildings. Press Tab to rotate walls and gates. Right-click an item to put it on hold, again to cancel (full refund of money spent).\n\n" +
                    "<b>Combat.</b> Every weapon has a warhead with different effectiveness against infantry, light, medium and heavy armor, aircraft, ships and structures. Units gain veterancy (veteran, elite) from kills. Splash damage can hurt your own units.\n\n" +
                    "<b>Aircraft</b> carry limited ammunition and return to their hangar pads to rearm.\n\n" +
                    "<b>Engineers</b> capture damaged enemy and neutral structures and repair your own. Tech structures (derricks, hospitals, relays) grant bonuses when captured.\n\n" +
                    "<b>Controls.</b> Left-click select, drag to box-select, right-click to order. A = attack-move, G = guard, S = stop, P = patrol, D = deploy/unload, T = cycle stance, X = scatter, H = home base, Space = last alert, Ctrl+number = assign group, R = repair mode, Z = sell mode, F6 quick save, F9 quick load, Esc = menu. All keys can be rebound in Options.", UIKit.Label);
            }
            else
            {
                _scroll = GUI.BeginScrollView(la, _scroll, new Rect(0, 0, la.width - 20, ids.Count * 40 + 10));
                float yy = 6;
                foreach (var id in ids)
                {
                    string nm = Catalog.Unit(id)?.Name ?? Catalog.Building(id)?.Name ?? Catalog.Tech(id)?.Name ?? id;
                    if (Catalog.Building(id) != null && !Catalog.Building(id).Buildable) continue;
                    if (UIKit.Btn(new Rect(6, yy, la.width - 32, 36), nm, _encSel == id ? UIKit.ListItemOn : UIKit.ListItem)) _encSel = id;
                    yy += 40;
                }
                GUI.EndScrollView();
                if (_encSel != null) DrawEntry(da, _encSel);
            }
            if (UIKit.Btn(new Rect(r.x + 24, r.yMax - 70, 200, 48), "Back", UIKit.ButtonBig)) _root.Go(UiScreen.MainMenu);
        }

        void DrawEntry(Rect da, string id)
        {
            var icon = IconCache.Get(id, Catalog.Unit(id)?.Faction == FactionId.Dominion || Catalog.Building(id)?.Faction == FactionId.Dominion ? 1 : 0);
            var ir = new Rect(da.x + 16, da.y + 16, 220, 220);
            GUI.Box(ir, GUIContent.none, UIKit.Cameo);
            if (icon != null) GUI.DrawTexture(new Rect(ir.x + 2, ir.y + 2, ir.width - 4, ir.height - 4), icon, ScaleMode.ScaleAndCrop);
            string text;
            var u = Catalog.Unit(id); var b = Catalog.Building(id); var t = Catalog.Tech(id);
            if (u != null)
            {
                text = $"<b>{u.Name}</b>  ({u.Category}, tier {u.Tier})\n<size=14>{u.Description}</size>\n\nCost {UIKit.Money(u.Cost)}   Build time {u.BuildTime:0}s\nHealth {u.Health}   Armor {u.Armor}   Speed {u.Speed:0.0}   Sight {u.Sight:0}\nProduced at: {u.ProducedAt}" +
                       (u.Prerequisites.Count > 0 ? "\nRequires: " + string.Join(", ", u.Prerequisites) : "") + (u.RequiredTech.Count > 0 ? "\nResearch: " + string.Join(", ", u.RequiredTech.ConvertAll(x => Catalog.Tech(x)?.Name ?? x)) : "");
                foreach (var w in u.Weapons)
                {
                    var wd = Catalog.Weapon(w); if (wd == null) continue;
                    text += $"\n\n<b>{wd.Name}</b>: {wd.Damage} dmg ({wd.Warhead}), range {wd.Range}, {wd.Burst}x every {wd.Cooldown:0.##}s{(wd.Splash > 0 ? ", splash " + wd.Splash : "")}. Targets: {wd.Targets}";
                }
                if (u.Ammo > 0) text += $"\nAmmunition: {u.Ammo} (rearms at hangar)";
                if (u.TransportCapacity > 0) text += $"\nTransport capacity: {u.TransportCapacity}";
                if (u.Abilities.Count > 0) text += "\nAbilities: " + string.Join(", ", u.Abilities);
            }
            else if (b != null)
            {
                text = $"<b>{b.Name}</b>  (tier {b.Tier})\n<size=14>{b.Description}</size>\n\nCost {UIKit.Money(b.Cost)}   Build time {b.BuildTime:0}s\nHealth {b.Health}   Size {b.Width}x{b.Height}   Power {(b.Power >= 0 ? "+" : "")}{b.Power}" +
                       (b.Storage > 0 ? "\nStorage +" + b.Storage : "") + (b.Prerequisites.Count > 0 ? "\nRequires: " + string.Join(", ", b.Prerequisites) : "");
                foreach (var w in b.Weapons) { var wd = Catalog.Weapon(w); if (wd != null) text += $"\n\n<b>{wd.Name}</b>: {wd.Damage} dmg ({wd.Warhead}), range {wd.Range}"; }
                if (b.Power_Kind.HasValue) text += "\n\nSupport power: " + SupportPowerSystem.Def(b.Power_Kind.Value).Name;
            }
            else if (t != null)
            {
                text = $"<b>{t.Name}</b>  ({t.Faction})\n<size=14>{t.Description}</size>\n\nCost {UIKit.Money(t.Cost)}   Research time {t.ResearchTime:0}s\nResearched at: {t.ResearchedAt}";
                foreach (var fx in t.Effects) text += $"\n- {fx.Kind} x{fx.Value:0.##}" + (fx.Category.HasValue ? " (" + fx.Category + ")" : "") + (fx.Target != null ? " [" + fx.Target + "]" : "");
            }
            else text = id;
            GUI.Label(new Rect(da.x + 256, da.y + 16, da.width - 272, da.height - 32), text, UIKit.Label);
        }

        // ================================================================== in-game overlays
        public void Pause(GameSession s)
        {
            Backdrop(0.6f);
            var r = Center(420, 520);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            GUI.Label(new Rect(r.x, r.y + 16, r.width, 40), "PAUSED", new GUIStyle(UIKit.Header) { alignment = TextAnchor.MiddleCenter });
            float y = r.y + 76, w = r.width - 60, x = r.x + 30;
            if (UIKit.Btn(new Rect(x, y, w, 48), "Resume", UIKit.ButtonBig)) _root.Resume(); y += 58;
            bool canSave = !s.W.Cinematic && !s.Ended;
            if (UIKit.Btn(new Rect(x, y, w, 48), "Save Game", UIKit.ButtonBig, canSave)) { _saveName = s.Mission != null ? s.Mission.Title : "Skirmish"; _root.Go(UiScreen.Save); } y += 58;
            if (UIKit.Btn(new Rect(x, y, w, 48), "Load Game", UIKit.ButtonBig)) { ReturnFromLoad = UiScreen.Paused; _root.Go(UiScreen.Load); } y += 58;
            if (UIKit.Btn(new Rect(x, y, w, 48), "Options", UIKit.ButtonBig)) { ReturnFromOptions = UiScreen.Paused; _root.Go(UiScreen.Options); } y += 58;
            if (UIKit.Btn(new Rect(x, y, w, 48), "Restart", UIKit.ButtonBig)) _root.Restart(); y += 58;
            if (UIKit.Btn(new Rect(x, y, w, 48), "Quit to Main Menu", UIKit.ButtonBig)) _root.QuitToMenu(); y += 58;
            if (UIKit.Btn(new Rect(x, y, w, 48), "Exit Game", UIKit.ButtonBig)) _root.Quit();
            DrawStatus();
        }

        public void Results(GameSession s)
        {
            Backdrop(0.7f);
            var r = Center(1100, 760);
            GUI.Box(r, GUIContent.none, UIKit.Panel);
            bool win = s.Result > 0;
            string title = s.Spectator ? "BATTLE OVER" : win ? "VICTORY" : "DEFEAT";
            GUI.Label(new Rect(r.x, r.y + 20, r.width, 70), title, new GUIStyle(UIKit.Title) { normal = { textColor = win ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.3f) } });
            string text = s.Mission != null ? (win ? s.Mission.VictoryText : (s.ResultText ?? s.Mission.DefeatText)) : (win ? "The enemy has been defeated." : "Your forces have been destroyed.");
            GUI.Label(new Rect(r.x + 40, r.y + 100, r.width - 80, 80), "<size=17>" + text + "</size>", UIKit.CenterLabel);
            // stats table
            var t = new Rect(r.x + 40, r.y + 200, r.width - 80, 360);
            GUI.Box(t, GUIContent.none, UIKit.PanelDark);
            string[] cols = { "Player", "Units built", "Units lost", "Units killed", "Structures built", "Lost", "Destroyed", "Harvested" };
            float[] cx = { 16, 300, 410, 520, 640, 760, 840, 940 };
            for (int i = 0; i < cols.Length; i++) GUI.Label(new Rect(t.x + cx[i], t.y + 10, 140, 24), "<b><size=13>" + cols[i] + "</size></b>", UIKit.LabelSmall);
            float y = t.y + 40;
            foreach (var p in s.W.Players)
            {
                if (p.IsNeutral) continue;
                var st = p.Stats;
                UIKit.Fill(new Rect(t.x + 4, y + 6, 8, 14), Palette.ForPlayer(p));
                string[] vals = { p.Name + (p.Defeated ? " <color=#ff6050>(defeated)</color>" : ""), st.UnitsBuilt.ToString(), st.UnitsLost.ToString(), st.UnitsKilled.ToString(), st.BuildingsBuilt.ToString(), st.BuildingsLost.ToString(), st.BuildingsKilled.ToString(), UIKit.Money(st.CreditsHarvested) };
                for (int i = 0; i < vals.Length; i++) GUI.Label(new Rect(t.x + cx[i], y, 280, 26), "<size=14>" + vals[i] + "</size>", UIKit.LabelSmall);
                y += 30;
            }
            GUI.Label(new Rect(t.x + 16, t.yMax - 34, 400, 24), "<size=13>Battle time " + UIKit.Time(s.W.Time) + "</size>", UIKit.LabelSmall);

            float by = r.yMax - 74;
            if (s.Mission != null)
            {
                if (win)
                {
                    var list = CampaignDatabase.For(s.Mission.Faction);
                    var next = list.Find(m => m.Index == s.Mission.Index + 1);
                    if (next != null && UIKit.Btn(new Rect(r.xMax - 300, by, 260, 50), "Next Mission", UIKit.ButtonBig)) { _root.QuitToMenu(false); OpenBriefing(next); }
                    if (next == null && UIKit.Btn(new Rect(r.xMax - 300, by, 260, 50), "Campaign Complete", UIKit.ButtonBig)) { _root.QuitToMenu(false); _root.Go(UiScreen.Campaign); }
                }
                else if (UIKit.Btn(new Rect(r.xMax - 300, by, 260, 50), "Retry", UIKit.ButtonBig)) _root.Restart();
                if (UIKit.Btn(new Rect(r.x + 40, by, 240, 50), "Campaign Menu", UIKit.ButtonBig)) { _root.QuitToMenu(false); _root.Go(UiScreen.Campaign); }
            }
            else
            {
                if (UIKit.Btn(new Rect(r.xMax - 300, by, 260, 50), "Play Again", UIKit.ButtonBig)) _root.Restart();
                if (UIKit.Btn(new Rect(r.x + 40, by, 240, 50), "Main Menu", UIKit.ButtonBig)) _root.QuitToMenu();
            }
            if (UIKit.Btn(new Rect(r.x + 300, by, 240, 50), "View Battlefield", UIKit.ButtonBig)) _root.ViewBattlefield();
        }

        public void Loading(string what)
        {
            UIKit.Fill(new Rect(0, 0, UIKit.VW, UIKit.VH), new Color(0.03f, 0.03f, 0.04f, 1f));
            GUI.Label(Center(800, 100), "<size=28><b>" + (what ?? "Loading") + "</b></size>\n<size=14><color=#a0a0a0>Generating terrain and deploying forces...</color></size>", UIKit.CenterLabel);
        }

        public void ShowStatus(string s) => Status(s);
    }
}
