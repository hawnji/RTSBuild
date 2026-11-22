using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>
    /// In-game interface: top bar (credits, power, clock), right sidebar (minimap, production tabs and cameos,
    /// repair/sell), support power buttons, selection/command panel, objectives, timers, messages, subtitles,
    /// tooltips and cursor feedback.
    /// </summary>
    public class Hud
    {
        readonly GameSession _s;
        ProductionQueueType _tab = ProductionQueueType.Structures;
        float _shownCredits;
        string _tooltip;
        public bool Visible = true;
        public System.Action OnMenu;
        const float SideW = 320f, TopH = 40f;
        static readonly string[] TabNames = { "Build", "Defense", "Infantry", "Vehicles", "Aircraft", "Naval", "Research" };

        public Hud(GameSession s) { _s = s; _shownCredits = s.Me?.Credits ?? 0; }

        World W => _s.W;
        Player Me => _s.Me;

        public void Draw()
        {
            if (!Visible) return;
            _tooltip = null;
            var me = Me;
            float vw = UIKit.VW, vh = UIKit.VH;
            bool cinematic = W.Cinematic;
            if (cinematic) { DrawCinematicBars(vw, vh); DrawSubtitle(vw, vh); return; }

            DrawTopBar(vw, me);
            if (!_s.Spectator && me != null)
            {
                DrawSidebar(vw, vh, me);
                DrawPowers(me, vh);
                DrawSelectionPanel(vw, vh, me);
            }
            else DrawSpectatorPanel(vw, vh);
            DrawObjectives();
            DrawTimers(vw);
            DrawMessages();
            DrawSubtitle(vw, vh);
            DrawBoxSelect();
            DrawCursor();
            if (_tooltip != null) DrawTooltip(_tooltip);
        }

        // ------------------------------------------------------------------ top bar
        void DrawTopBar(float vw, Player me)
        {
            var r = new Rect(0, 0, vw, TopH);
            GUI.Box(r, GUIContent.none, UIKit.Panel); UIKit.Block(r);
            if (me != null && !_s.Spectator)
            {
                _shownCredits = Mathf.MoveTowards(_shownCredits, me.Credits, Mathf.Max(20f, Mathf.Abs(me.Credits - _shownCredits) * 4f) * Time.unscaledDeltaTime);
                var creditStyle = new GUIStyle(UIKit.LabelBold) { fontSize = 22, normal = { textColor = UIKit.Accent } };
                GUI.Label(new Rect(14, 6, 200, 30), UIKit.Money(_shownCredits), creditStyle);
                bool full = me.StorageCap > 0 && me.Credits >= me.StorageCap - 1;
                GUI.Label(new Rect(170, 11, 160, 24), "<size=12>Storage " + UIKit.Money(me.StorageCap) + (full ? " <color=#ff8040>FULL</color>" : "") + "</size>", UIKit.LabelSmall);
                // power meter
                var pr = new Rect(330, 12, 220, 16);
                float max = Mathf.Max(1, Mathf.Max(me.PowerProduced, me.PowerConsumed));
                UIKit.Fill(pr, new Color(0, 0, 0, 0.6f));
                UIKit.Fill(new Rect(pr.x + 1, pr.y + 1, (pr.width - 2) * me.PowerProduced / max, pr.height - 2), me.LowPower ? new Color(0.9f, 0.3f, 0.2f) : new Color(0.3f, 0.85f, 0.4f));
                float cx = pr.x + pr.width * me.PowerConsumed / max;
                UIKit.Fill(new Rect(cx - 1, pr.y - 3, 2, pr.height + 6), Color.white);
                GUI.Label(new Rect(560, 9, 220, 24), "<size=13>Power " + me.PowerProduced + " / " + me.PowerConsumed + (me.LowPower ? "  <color=#ff6040>LOW</color>" : "") + "</size>", UIKit.LabelSmall);
                if (pr.Contains(Event.current.mousePosition)) _tooltip = "<b>Power</b>\nProduced " + me.PowerProduced + ", consumed " + me.PowerConsumed + ".\nLow power slows production, disables radar and powered defenses.";
            }
            else GUI.Label(new Rect(14, 8, 400, 26), "<b>Spectating</b>", UIKit.Label);

            string title = _s.Mission != null ? _s.Mission.Title : (_s.Skirmish != null ? (MapLibrary.Find(_s.Skirmish.MapId)?.Name ?? "Skirmish") : "");
            GUI.Label(new Rect(vw * 0.5f - 200, 8, 400, 26), "<b>" + title + "</b>   " + UIKit.Time(W.Time), UIKit.CenterLabel);
            float x = vw - SideW - 10;
            if (UIKit.Btn(new Rect(vw - 100, 5, 92, 30), "Menu", UIKit.ButtonSmall)) OnMenu?.Invoke();
            if (_s.Mission != null && UIKit.Btn(new Rect(vw - 210, 5, 104, 30), "Objectives", UIKit.ButtonSmall)) _s.ShowObjectives = !_s.ShowObjectives;
            if (_s.Mission == null && !_s.Demo)
            {
                GUI.Label(new Rect(vw - 340, 9, 70, 24), "<size=12>Speed</size>", UIKit.RightLabel);
                if (UIKit.Btn(new Rect(vw - 264, 7, 24, 26), "-", UIKit.ButtonSmall)) _s.Speed = Mathf.Max(0.5f, _s.Speed - 0.25f);
                GUI.Label(new Rect(vw - 240, 9, 40, 24), "<size=12>" + _s.Speed.ToString("0.##") + "x</size>", UIKit.CenterLabel);
                if (UIKit.Btn(new Rect(vw - 200, 7, 24, 26), "+", UIKit.ButtonSmall)) _s.Speed = Mathf.Min(2f, _s.Speed + 0.25f);
            }
            if (ClientSettings.Current.ShowFps) GUI.Label(new Rect(vw * 0.5f + 210, 9, 120, 24), "<size=12>" + Mathf.RoundToInt(1f / Mathf.Max(0.001f, Time.smoothDeltaTime)) + " fps</size>", UIKit.LabelSmall);
        }

        // ------------------------------------------------------------------ sidebar
        void DrawSidebar(float vw, float vh, Player me)
        {
            var side = new Rect(vw - SideW, TopH, SideW, vh - TopH);
            GUI.Box(side, GUIContent.none, UIKit.Panel); UIKit.Block(side);
            var accent = UIKit.FactionColor(me.Faction);
            UIKit.Fill(new Rect(side.x, side.y, 3, side.height), accent);
            var mm = new Rect(side.x + 10, side.y + 10, SideW - 20, SideW - 40);
            _s.Minimap.Draw(mm, UIKit.Scale);

            // repair / sell / tabs
            float y = mm.yMax + 8;
            var pc = _s.Controller;
            if (UIKit.Btn(new Rect(side.x + 10, y, 96, 28), (pc.Mode == CursorMode.Repair ? "<color=#ffd040>" : "") + "Repair" + (pc.Mode == CursorMode.Repair ? "</color>" : ""), UIKit.ButtonSmall)) pc.Mode = pc.Mode == CursorMode.Repair ? CursorMode.Normal : CursorMode.Repair;
            if (UIKit.Btn(new Rect(side.x + 112, y, 96, 28), (pc.Mode == CursorMode.Sell ? "<color=#ffd040>" : "") + "Sell" + (pc.Mode == CursorMode.Sell ? "</color>" : ""), UIKit.ButtonSmall)) pc.Mode = pc.Mode == CursorMode.Sell ? CursorMode.Normal : CursorMode.Sell;
            if (UIKit.Btn(new Rect(side.x + 214, y, 96, 28), "Base (H)", UIKit.ButtonSmall)) { foreach (var b in W.BuildingsOf(me.Id)) { _s.Cam.LookAt(b.Center); break; } }
            y += 34;

            var avail = new List<string>[7];
            for (int q = 0; q < 7; q++) avail[q] = Available(me, (ProductionQueueType)q);
            if (avail[(int)_tab].Count == 0 && me.Ready[(int)_tab] == null && me.Queues[(int)_tab].Count == 0)
                for (int q = 0; q < 7; q++) if (avail[q].Count > 0) { _tab = (ProductionQueueType)q; break; }

            float tw = (SideW - 20) / 4f;
            for (int q = 0; q < 7; q++)
            {
                var tr = new Rect(side.x + 10 + (q % 4) * tw, y + (q / 4) * 28, tw - 2, 26);
                bool has = avail[q].Count > 0 || me.Queues[q].Count > 0 || me.Ready[q] != null;
                var label = TabNames[q];
                if (me.Ready[q] != null) label = "<color=#80ff80>" + label + "*</color>";
                else if (me.Queues[q].Count > 0) label = "<color=#ffd080>" + label + "</color>";
                if (UIKit.Btn(tr, label, _tab == (ProductionQueueType)q ? UIKit.TabOn : UIKit.Tab, has)) _tab = (ProductionQueueType)q;
            }
            y += 60;

            // production queue status for the tab
            var queue = me.Queues[(int)_tab];
            if (queue.Count > 0 && queue[0].OnHold) GUI.Label(new Rect(side.x + 10, y, SideW - 20, 20), "<size=12><color=#ffc040>On hold — right-click the item to cancel</color></size>", UIKit.LabelSmall);
            y += 4;

            // cameo grid
            var items = avail[(int)_tab];
            float cw = (SideW - 20) / 3f, ch = cw * 0.82f;
            var gridArea = new Rect(side.x + 10, y, SideW - 20, side.yMax - y - 10);
            int cols = 3;
            for (int i = 0; i < items.Count; i++)
            {
                var cr = new Rect(gridArea.x + (i % cols) * cw, gridArea.y + (i / cols) * ch, cw - 4, ch - 4);
                if (cr.yMax > gridArea.yMax) break;
                DrawCameo(cr, items[i], me);
            }
            if (items.Count == 0)
                GUI.Label(new Rect(gridArea.x, gridArea.y + 10, gridArea.width, 60), _tab == ProductionQueueType.Research ? "<size=13>No research available. Build a Tech Center or Research Lab.</size>" : "<size=13>Nothing available. Build the required structures first.</size>", UIKit.LabelSmall);
        }

        List<string> Available(Player me, ProductionQueueType q)
        {
            var l = new List<string>();
            var f = Catalog.Faction(me.Faction);
            if (f == null) return l;
            var prod = W.Production;
            if (q == ProductionQueueType.Research)
            {
                foreach (var t in f.Techs) { var td = Catalog.Tech(t); if (td != null && (prod.CanResearch(me, td) || InQueue(me, q, t))) l.Add(t); }
                return l;
            }
            if (q == ProductionQueueType.Structures || q == ProductionQueueType.Defenses)
            {
                foreach (var b in f.Buildings) { var bd = Catalog.Building(b); if (bd != null && bd.Queue == q && (prod.CanBuildStructure(me, bd) || me.Ready[(int)q] == b || InQueue(me, q, b))) l.Add(b); }
                return l;
            }
            foreach (var u in f.Units) { var ud = Catalog.Unit(u); if (ud != null && Catalog.QueueFor(ud) == q && (prod.CanBuildUnit(me, ud) || InQueue(me, q, u)) && HasProducer(me, ud)) l.Add(u); }
            return l;
        }

        bool HasProducer(Player me, UnitDef u)
        {
            foreach (var r in ProductionSystem.Family(u.ProducedAt)) if (W.HasRole(me.Id, r, false)) return true;
            return false;
        }

        static bool InQueue(Player me, ProductionQueueType q, string id)
        {
            foreach (var it in me.Queues[(int)q]) if (it.DefId == id) return true;
            return false;
        }

        void DrawCameo(Rect r, string id, Player me)
        {
            bool tech = _tab == ProductionQueueType.Research;
            var ud = tech ? null : Catalog.Unit(id);
            var bd = tech || ud != null ? null : Catalog.Building(id);
            var td = tech ? Catalog.Tech(id) : null;
            string name = ud?.Name ?? bd?.Name ?? td?.Name ?? id;
            int cost = W.Production.CostOf(me, id, tech);
            GUI.Box(r, GUIContent.none, UIKit.Cameo);
            var icon = IconCache.Get(id, me.ColorIndex % 7);
            if (icon != null) GUI.DrawTexture(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), icon, ScaleMode.ScaleAndCrop);
            GUI.DrawTexture(new Rect(r.x + 2, r.yMax - 22, r.width - 4, 20), UIKit.Gradient);
            GUI.Label(new Rect(r.x + 4, r.yMax - 22, r.width - 8, 20), "<size=11><b>" + name + "</b></size>", UIKit.LabelSmall);
            GUI.Label(new Rect(r.x + 4, r.y + 2, r.width - 8, 16), "<size=11>" + UIKit.Money(cost) + "</size>", UIKit.LabelSmall);

            var q = me.Queues[(int)_tab];
            int count = 0; ProductionItem active = null;
            foreach (var it in q) if (it.DefId == id) { count += it.Count; if (active == null) active = it; }
            bool isFirst = q.Count > 0 && q[0].DefId == id;
            bool ready = me.Ready[(int)_tab] == id;
            if (isFirst && active != null && !ready)
            {
                // clock-wipe style progress overlay
                float p = Mathf.Clamp01(active.Progress);
                UIKit.Fill(new Rect(r.x + 2, r.y + 2 + (r.height - 4) * p, r.width - 4, (r.height - 4) * (1 - p)), new Color(0, 0, 0, 0.55f));
                UIKit.ProgressBar(new Rect(r.x + 4, r.yMax - 28, r.width - 8, 5), p, active.OnHold ? new Color(1f, 0.7f, 0.2f) : new Color(0.4f, 0.9f, 0.4f));
                if (active.OnHold) GUI.Label(r, "<b>ON HOLD</b>", UIKit.CenterLabel);
            }
            else if (count > 0) UIKit.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), new Color(0, 0, 0, 0.35f));
            if (ready) { UIKit.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), new Color(0.2f, 0.6f, 0.2f, 0.25f + 0.15f * Mathf.Sin(Time.unscaledTime * 6))); GUI.Label(r, "<b><color=#a0ffa0>READY</color></b>", UIKit.CenterLabel); }
            if (count > 1 || (count == 1 && !isFirst)) GUI.Label(new Rect(r.x, r.y + 2, r.width - 6, 18), count.ToString(), UIKit.Badge);
            if (me.Credits < cost && count == 0 && !ready) UIKit.Fill(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), new Color(0.3f, 0, 0, 0.25f));

            var e = Event.current;
            if (r.Contains(e.mousePosition))
            {
                var desc = ud?.Description ?? bd?.Description ?? td?.Description ?? "";
                string extra = "";
                if (ud != null) extra = $"\nHealth {ud.Health}  Speed {ud.Speed:0.0}  Armor {ud.Armor}";
                if (bd != null) extra = $"\nPower {(bd.Power >= 0 ? "+" : "")}{bd.Power}  Size {bd.Width}x{bd.Height}";
                float time = ud?.BuildTime ?? bd?.BuildTime ?? td?.ResearchTime ?? 0;
                _tooltip = $"<b>{name}</b>  {UIKit.Money(cost)}  ({time:0}s)\n{desc}{extra}\n<size=11><color=#a0a0a0>Left: build/queue (Shift: x5)   Right: hold / cancel</color></size>";
                if (e.type == EventType.MouseDown)
                {
                    if (e.button == 0) LeftClickItem(id, tech, ready, me);
                    else if (e.button == 1)
                    {
                        if (ready) W.Commands.Cancel(me.Id, _tab, id);
                        else if (isFirst && active != null && !active.OnHold && active.Count <= 1) W.Commands.Hold(me.Id, _tab);
                        else W.Commands.Cancel(me.Id, _tab, id);
                        AudioManager.Play2D("click", AudioBus.Ui, 0.6f);
                    }
                    e.Use();
                }
            }
        }

        void LeftClickItem(string id, bool tech, bool ready, Player me)
        {
            if (ready) { _s.Controller.BeginPlacement(_tab); AudioManager.Play2D("click", AudioBus.Ui); return; }
            var q = me.Queues[(int)_tab];
            if (q.Count > 0 && q[0].DefId == id && q[0].OnHold) { W.Commands.Hold(me.Id, _tab); return; }
            bool structure = _tab == ProductionQueueType.Structures || _tab == ProductionQueueType.Defenses;
            if (structure && (q.Count > 0 || me.Ready[(int)_tab] != null)) { AudioManager.Play2D("denied", AudioBus.Ui); _s.Notify("Construction already in progress.", new Color(1f, 0.8f, 0.4f), 2f); return; }
            int n = InputState.Shift && !structure && !tech ? 5 : 1;
            bool ok = false;
            for (int i = 0; i < n; i++) ok |= W.Commands.Enqueue(me.Id, id, tech);
            AudioManager.Play2D(ok ? "build_start" : "denied", AudioBus.Ui, 0.7f);
        }

        // ------------------------------------------------------------------ support powers
        void DrawPowers(Player me, float vh)
        {
            float y = TopH + 60;
            for (int k = 0; k < me.PowerOwned.Length; k++)
            {
                if (!me.PowerOwned[k]) continue;
                var kind = (SupportPowerKind)k;
                var def = SupportPowerSystem.Def(kind);
                var r = new Rect(10, y, 150, 44);
                UIKit.Block(r);
                float frac = W.Powers.ChargeFraction(me, kind);
                bool ready = me.PowerReady[k];
                GUI.Box(r, GUIContent.none, UIKit.Panel);
                UIKit.ProgressBar(new Rect(r.x + 6, r.yMax - 10, r.width - 12, 6), frac, ready ? new Color(1f, 0.75f, 0.25f) : new Color(0.4f, 0.6f, 0.9f));
                string label = "<size=12><b>" + def.Name + "</b></size>\n<size=11>" + (ready ? "<color=#ffd060>READY - click to target</color>" : UIKit.Time(def.ChargeTime * (1 - frac))) + "</size>";
                if (GUI.Button(new Rect(r.x, r.y, r.width, r.height - 8), label, new GUIStyle(UIKit.LabelSmall) { padding = new RectOffset(6, 4, 3, 0) }) && ready)
                {
                    _s.Controller.BeginPower(kind);
                    AudioManager.Play2D("click", AudioBus.Ui);
                }
                if (r.Contains(Event.current.mousePosition)) _tooltip = "<b>" + def.Name + "</b>\n" + def.Description;
                y += 50;
            }
        }

        // ------------------------------------------------------------------ selection panel
        void DrawSelectionPanel(float vw, float vh, Player me)
        {
            var pc = _s.Controller;
            if (pc.Selection.Count == 0) return;
            var r = new Rect(10, vh - 170, Mathf.Min(620, vw - SideW - 30), 160);
            GUI.Box(r, GUIContent.none, UIKit.Panel); UIKit.Block(r);
            var units = pc.SelectedUnits();
            var e0 = W.Get(pc.Selection[0]);
            if (e0 == null) return;

            if (units.Count > 1)
            {
                // group summary
                var groups = new Dictionary<string, List<Unit>>();
                foreach (var u in units) { if (!groups.TryGetValue(u.DefId, out var l)) groups[u.DefId] = l = new List<Unit>(); l.Add(u); }
                float x = r.x + 10, y = r.y + 10; float cs = 56;
                foreach (var kv in groups)
                {
                    var cr = new Rect(x, y, cs, cs);
                    GUI.Box(cr, GUIContent.none, UIKit.Cameo);
                    var icon = IconCache.Get(kv.Key, me.ColorIndex % 7);
                    if (icon != null) GUI.DrawTexture(new Rect(cr.x + 2, cr.y + 2, cs - 4, cs - 4), icon, ScaleMode.ScaleAndCrop);
                    GUI.Label(new Rect(cr.x, cr.y, cs - 3, 16), kv.Value.Count.ToString(), UIKit.Badge);
                    float hp = 0; foreach (var u in kv.Value) hp += u.HpRatio; hp /= kv.Value.Count;
                    UIKit.ProgressBar(new Rect(cr.x + 3, cr.yMax - 7, cs - 6, 4), hp, hp > 0.6f ? Color.green : hp > 0.3f ? Color.yellow : Color.red);
                    if (Event.current.type == EventType.MouseDown && cr.Contains(Event.current.mousePosition))
                    {
                        pc.Selection.Clear(); foreach (var u in kv.Value) pc.Selection.Add(u.Id);
                        Event.current.Use();
                    }
                    if (cr.Contains(Event.current.mousePosition)) _tooltip = Catalog.Unit(kv.Key)?.Name + " x" + kv.Value.Count + "\n<size=11>Click to select only these</size>";
                    x += cs + 4;
                    if (x + cs > r.x + r.width - 210) { x = r.x + 10; y += cs + 4; if (y + cs > r.yMax) break; }
                }
                DrawUnitCommands(new Rect(r.xMax - 200, r.y + 8, 192, r.height - 16), units);
                return;
            }

            // single entity
            var icon1 = IconCache.Get(e0.DefId, (W.GetPlayer(e0.Owner)?.ColorIndex ?? 7) % 7);
            var ir = new Rect(r.x + 10, r.y + 10, 100, 100);
            GUI.Box(ir, GUIContent.none, UIKit.Cameo);
            if (icon1 != null) GUI.DrawTexture(new Rect(ir.x + 2, ir.y + 2, 96, 96), icon1, ScaleMode.ScaleAndCrop);
            var owner = W.GetPlayer(e0.Owner);
            string ownerName = owner == null ? "" : owner.Id == me.Id ? "" : " <size=12>(" + owner.Name + ")</size>";
            GUI.Label(new Rect(r.x + 120, r.y + 8, 300, 26), "<b>" + e0.Name + "</b>" + ownerName, UIKit.Label);
            UIKit.ProgressBar(new Rect(r.x + 120, r.y + 36, 220, 12), e0.HpRatio, e0.HpRatio > 0.6f ? new Color(0.3f, 0.9f, 0.3f) : e0.HpRatio > 0.3f ? Color.yellow : Color.red);
            GUI.Label(new Rect(r.x + 346, r.y + 32, 120, 20), "<size=12>" + Mathf.CeilToInt(e0.Hp) + " / " + Mathf.CeilToInt(e0.MaxHp) + "</size>", UIKit.LabelSmall);
            string info = "";
            if (e0 is Unit u1)
            {
                info = (e0.Rank == 2 ? "<color=#ffd040>Elite</color>" : e0.Rank == 1 ? "<color=#d0d0e0>Veteran</color>" : "Rookie") + "   Stance: " + u1.Stance;
                if (u1.Def.Ammo > 0) info += "   Ammo " + u1.Ammo + "/" + u1.Def.Ammo;
                if (u1.Def.Has(UnitAbility.Harvester)) info += "   Cargo " + Mathf.RoundToInt(u1.Cargo) + "/" + u1.Def.HarvestCapacity;
                if (u1.Def.TransportCapacity > 0) info += "   Passengers " + u1.Passengers.Count + "/" + u1.Def.TransportCapacity;
                if (e0.Disabled) info += "   <color=#80a0ff>EMP</color>";
                GUI.Label(new Rect(r.x + 120, r.y + 54, r.width - 330, 60), "<size=12>" + info + "\n<color=#a0a0a0>" + u1.Def.Description + "</color></size>", UIKit.LabelSmall);
                if (u1.Owner == me.Id) DrawUnitCommands(new Rect(r.xMax - 200, r.y + 8, 192, r.height - 16), units);
            }
            else if (e0 is Building b)
            {
                info = (b.Def.Power != 0 ? "Power " + (b.Def.Power > 0 ? "+" : "") + b.Def.Power + "   " : "") + (b.Operational ? "" : b.BuildUp < 1 ? "Under construction   " : b.Selling ? "Selling   " : "Offline   ") + (!b.Powered && b.Def.Power < 0 ? "<color=#ff8060>Unpowered</color>" : "");
                GUI.Label(new Rect(r.x + 120, r.y + 54, r.width - 330, 60), "<size=12>" + info + "\n<color=#a0a0a0>" + b.Def.Description + "</color></size>", UIKit.LabelSmall);
                if (b.Owner == me.Id)
                {
                    var cr = new Rect(r.xMax - 200, r.y + 10, 192, 30);
                    if (UIKit.Btn(cr, b.Repairing ? "<color=#ffd040>Repairing</color>" : "Repair", UIKit.ButtonSmall, b.Hp < b.MaxHp || b.Repairing)) W.Commands.ToggleRepair(b.Id, me.Id);
                    cr.y += 34;
                    if (UIKit.Btn(cr, "Sell (+" + UIKit.Money(b.Def.Cost * 0.5f * b.HpRatio) + ")", UIKit.ButtonSmall)) W.Commands.Sell(b.Id, me.Id);
                    cr.y += 34;
                    if (b.HasRally) GUI.Label(cr, "<size=11>Right-click the map to move the rally point</size>", UIKit.LabelSmall);
                    else if (b.Def.Role == BuildingRole.Barracks || b.Def.Role == BuildingRole.Factory || b.Def.Role == BuildingRole.Hangar || b.Def.Role == BuildingRole.Shipyard) GUI.Label(cr, "<size=11>Right-click the map to set a rally point</size>", UIKit.LabelSmall);
                }
            }
        }

        void DrawUnitCommands(Rect r, List<Unit> units)
        {
            if (units.Count == 0) return;
            var me = Me; var ids = _s.Controller.Selection;
            float bw = (r.width - 4) / 2f, bh = 26;
            int i = 0;
            Rect Cell() { var c = new Rect(r.x + (i % 2) * (bw + 4), r.y + (i / 2) * (bh + 4), bw, bh); i++; return c; }
            var keys = ClientSettings.Current.Keys;
            string K(GameAction a) => " <size=10>(" + Keybinds.KeyName(keys.Get(a)) + ")</size>";
            if (UIKit.Btn(Cell(), "Stop" + K(GameAction.Stop), UIKit.ButtonSmall)) W.Commands.Stop(ids, me.Id);
            if (UIKit.Btn(Cell(), "Guard" + K(GameAction.Guard), UIKit.ButtonSmall)) foreach (var u in units) W.Commands.Guard(new[] { u.Id }, me.Id, u.Pos);
            if (UIKit.Btn(Cell(), "Attack-Move" + K(GameAction.AttackMove), UIKit.ButtonSmall)) _s.Controller.Mode = CursorMode.AttackMove;
            if (UIKit.Btn(Cell(), "Patrol" + K(GameAction.Patrol), UIKit.ButtonSmall)) _s.Controller.Mode = CursorMode.Patrol;
            bool canDeploy = units.Exists(u => u.Def.Has(UnitAbility.DeployToHQ) || u.Passengers.Count > 0 || u.Def.Locomotor == Locomotor.Submarine);
            string deployLabel = units.Exists(u => u.Def.Has(UnitAbility.DeployToHQ)) ? "Deploy" : units.Exists(u => u.Passengers.Count > 0) ? "Unload" : units.Exists(u => u.Def.Locomotor == Locomotor.Submarine) ? "Dive/Surface" : "Deploy";
            if (UIKit.Btn(Cell(), deployLabel + K(GameAction.Deploy), UIKit.ButtonSmall, canDeploy)) W.Commands.Deploy(ids, me.Id);
            if (UIKit.Btn(Cell(), "Scatter" + K(GameAction.Scatter), UIKit.ButtonSmall)) foreach (var u in units) W.Commands.Move(new[] { u.Id }, me.Id, u.Pos + W.Rng.InsideCircle(1f).Normalized * 2.5f, false, false);
            // stances
            var st = units[0].Stance;
            string[] names = { "Aggr", "Guard", "Hold", "Cease" };
            float sw = (r.width - 6) / 4f;
            float sy = r.y + 3 * (bh + 4);
            for (int s = 0; s < 4; s++)
            {
                var sr = new Rect(r.x + s * (sw + 2), sy, sw, bh);
                if (UIKit.Btn(sr, (st == (Stance)s ? "<color=#ffd040>" : "") + names[s] + (st == (Stance)s ? "</color>" : ""), UIKit.ButtonSmall)) W.Commands.SetStance(ids, me.Id, (Stance)s);
                if (sr.Contains(Event.current.mousePosition)) _tooltip = s == 0 ? "Aggressive: pursue targets freely." : s == 1 ? "Guard: engage nearby targets, then return." : s == 2 ? "Hold Position: fire without moving." : "Hold Fire: never fire unless ordered.";
            }
        }

        void DrawSpectatorPanel(float vw, float vh)
        {
            if (_s.Demo) return;
            var r = new Rect(vw - 330, TopH + 10, 320, 320 + W.Players.Count * 26);
            GUI.Box(r, GUIContent.none, UIKit.Panel); UIKit.Block(r);
            _s.Minimap.Draw(new Rect(r.x + 10, r.y + 10, 300, 300), UIKit.Scale);
            float y = r.y + 316;
            foreach (var p in W.Players)
            {
                if (p.IsNeutral) continue;
                var c = Palette.ForPlayer(p);
                UIKit.Fill(new Rect(r.x + 10, y + 5, 12, 12), c);
                GUI.Label(new Rect(r.x + 28, y, 290, 24), $"<size=13>{p.Name} ({p.Faction}) {UIKit.Money(p.Credits)}  K:{p.Stats.UnitsKilled + p.Stats.BuildingsKilled}{(p.Defeated ? " <color=#ff6060>DEFEATED</color>" : "")}</size>", UIKit.LabelSmall);
                y += 26;
            }
        }

        // ------------------------------------------------------------------ mission info, messages
        void DrawObjectives()
        {
            var m = W.Mission;
            if (m == null || !_s.ShowObjectives) return;
            var lines = new List<string>();
            foreach (var o in m.Objectives)
            {
                if (o.State == ObjectiveState.Hidden) continue;
                string mark = o.State == ObjectiveState.Completed ? "<color=#70ff70>[done]</color>" : o.State == ObjectiveState.Failed ? "<color=#ff6050>[failed]</color>" : "<color=#ffd060>[  ]</color>";
                lines.Add(mark + " " + (o.Primary ? "" : "<i>(Optional)</i> ") + o.Text);
            }
            if (lines.Count == 0) return;
            float h = 34 + lines.Count * 22;
            var r = new Rect(170, TopH + 10, 460, h);
            GUI.Box(r, GUIContent.none, UIKit.PanelDark); UIKit.Block(r);
            GUI.Label(new Rect(r.x + 10, r.y + 6, 300, 22), "<b>OBJECTIVES</b> <size=11>(O to toggle)</size>", UIKit.LabelSmall);
            for (int i = 0; i < lines.Count; i++) GUI.Label(new Rect(r.x + 10, r.y + 28 + i * 22, r.width - 20, 22), "<size=13>" + lines[i] + "</size>", UIKit.LabelSmall);
        }

        void DrawTimers(float vw)
        {
            var m = W.Mission;
            if (m == null) return;
            float y = TopH + 8;
            foreach (var c in m.Countdowns)
            {
                if (!c.Visible) continue;
                float left = c.EndTime - W.Time;
                if (left < -2) continue;
                var r = new Rect(vw * 0.5f - 150, y, 300, 34);
                GUI.Box(r, GUIContent.none, UIKit.PanelDark);
                var col = left < 30 ? "#ff6050" : "#ffd060";
                GUI.Label(r, $"{c.Label}  <b><color={col}>{UIKit.Time(left)}</color></b>", UIKit.CenterLabel);
                y += 38;
            }
        }

        void DrawMessages()
        {
            float y = TopH + 260;
            float now = _s.SessionTime;
            foreach (var m in _s.Messages)
            {
                float age = now - m.Time;
                float a = Mathf.Clamp01((m.Duration - age) / 1f);
                var style = new GUIStyle(UIKit.LabelSmall) { fontSize = 15, wordWrap = true, normal = { textColor = m.Color.WithA(a) } };
                string text = string.IsNullOrEmpty(m.Speaker) ? m.Text : "<b>" + m.Speaker + ":</b> " + m.Text;
                var r = new Rect(14, y, 560, 40);
                float h = style.CalcHeight(new GUIContent(text), 560);
                r.height = h;
                UIKit.Shadowed(r, text, style);
                y += h + 4;
            }
        }

        void DrawSubtitle(float vw, float vh)
        {
            var s = _s.Subtitle;
            if (s == null || !ClientSettings.Current.Subtitles) return;
            float w = Mathf.Min(900, vw - SideW - 60);
            var text = (string.IsNullOrEmpty(s.Speaker) ? "" : "<color=#ffd060><b>" + s.Speaker.ToUpper() + "</b></color>\n") + s.Text;
            float h = UIKit.Subtitle.CalcHeight(new GUIContent(text), w - 30) + 20;
            var r = new Rect((vw - SideW - w) * 0.5f, vh - 190 - h, w, h);
            if (W.Cinematic) r = new Rect((vw - w) * 0.5f, vh - 100 - h, w, h);
            GUI.Box(r, GUIContent.none, UIKit.PanelDark);
            GUI.Label(new Rect(r.x + 15, r.y + 10, r.width - 30, r.height - 20), text, UIKit.Subtitle);
        }

        void DrawCinematicBars(float vw, float vh)
        {
            UIKit.Fill(new Rect(0, 0, vw, vh * 0.1f), Color.black);
            UIKit.Fill(new Rect(0, vh * 0.9f, vw, vh * 0.1f), Color.black);
            GUI.Label(new Rect(vw - 260, vh - 40, 250, 30), "<size=12><color=#808080>Esc to skip</color></size>", UIKit.RightLabel);
        }

        void DrawBoxSelect()
        {
            var pc = _s.Controller;
            if (!pc.Dragging) return;
            var a = pc.DragStart / UIKit.Scale; var b = InputState.MouseGui / UIKit.Scale;
            if (Vector2.Distance(a, b) < 6f / UIKit.Scale) return;
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            UIKit.Fill(r, new Color(0.3f, 1f, 0.3f, 0.08f));
            GUI.color = new Color(0.4f, 1f, 0.4f, 0.9f); UIKit.Box(r, 1.5f); GUI.color = Color.white;
        }

        void DrawCursor()
        {
            var pc = _s.Controller;
            if (string.IsNullOrEmpty(pc.CursorLabel) || InputState.OverUI) return;
            var m = InputState.MouseGui / UIKit.Scale;
            var style = new GUIStyle(UIKit.LabelSmall) { fontSize = 13, fontStyle = FontStyle.Bold, normal = { textColor = pc.CursorColor }, wordWrap = false };
            UIKit.Shadowed(new Rect(m.x + 18, m.y + 14, 300, 20), pc.CursorLabel, style);
        }

        void DrawTooltip(string text)
        {
            var m = Event.current.mousePosition;
            float w = 300;
            float h = UIKit.Tooltip.CalcHeight(new GUIContent(text), w);
            var r = new Rect(m.x - w - 16, m.y + 10, w, h);
            if (r.x < 4) r.x = m.x + 16;
            if (r.yMax > UIKit.VH - 4) r.y = UIKit.VH - 4 - h;
            GUI.Box(r, text, UIKit.Tooltip);
        }
    }
}
