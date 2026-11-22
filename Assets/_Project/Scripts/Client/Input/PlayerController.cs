using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    public enum CursorMode { Normal, AttackMove, Patrol, Place, Power, Repair, Sell, ForceMove, Unload }

    /// <summary>
    /// Translates mouse and keyboard input into simulation commands for the local player: selection (click, box,
    /// shift-add, double-click same type), control groups, context orders, attack-move, patrol, guard, stances,
    /// building placement, repair/sell modes and support power targeting. All orders go through World.Commands.
    /// </summary>
    public class PlayerController
    {
        readonly World _w;
        readonly RTSCamera _cam;
        readonly ViewManager _views;
        public int Player;
        public readonly List<int> Selection = new List<int>();
        readonly List<int>[] _groups = new List<int>[10];
        readonly float[] _groupTapTime = new float[10];
        public CursorMode Mode;
        public ProductionQueueType PlaceQueue;
        public bool PlaceRotated;
        public SupportPowerKind PowerKind;
        public bool Dragging;
        public Vector2 DragStart;
        public Entity Hover;
        public Vector3? HoverGround;
        public string CursorLabel;
        public Color CursorColor = Color.white;
        public bool ShowAllHealth;
        public readonly List<Vec2> Alerts = new List<Vec2>();
        int _alertIndex;
        public System.Action<string, Vector3?> OnOrderSound;   // voice acknowledgement hook
        public bool Enabled = true;
        int _downClicks;

        public PlayerController(World w, RTSCamera cam, ViewManager views, int player)
        {
            _w = w; _cam = cam; _views = views; Player = player;
            for (int i = 0; i < 10; i++) _groups[i] = new List<int>();
        }

        Player Me => _w.GetPlayer(Player);
        Keybinds Keys => ClientSettings.Current.Keys;

        public List<Unit> SelectedUnits()
        {
            var l = new List<Unit>();
            foreach (var id in Selection) { var u = _w.GetUnit(id); if (u != null && u.Owner == Player && !u.IsInside) l.Add(u); }
            return l;
        }

        public Building SelectedBuilding()
        {
            if (Selection.Count != 1) return null;
            return _w.GetBuilding(Selection[0]);
        }

        public void PruneSelection()
        {
            Selection.RemoveAll(id => { var e = _w.Get(id); return e == null || (e is Unit u && u.IsInside) || (e.Owner != Player && !_views.IsVisible(e)); });
            for (int i = 0; i < 10; i++) _groups[i].RemoveAll(id => { var e = _w.Get(id); return e == null || e.Owner != Player; });
        }

        public void AddAlert(Vec2 p)
        {
            Alerts.Add(p);
            if (Alerts.Count > 8) Alerts.RemoveAt(0);
            _alertIndex = Alerts.Count - 1;
        }

        // ------------------------------------------------------------------ picking
        public Entity PickAt(Vector2 gui)
        {
            Entity best = null; float bestD = 22f * Mathf.Max(1f, 11f / _cam.Zoom);
            foreach (var kv in _views.Views)
            {
                var v = kv.Value;
                if (!v.Visible || v.IsBuilding) continue;
                var e = _w.Get(kv.Key);
                if (e == null) continue;
                var head = v.Go.transform.position + Vector3.up * v.Parts.Height * 0.5f;
                float d = Vector2.Distance(_cam.WorldToGui(head), gui);
                float r = bestD * (((Unit)e).Def.Category == UnitCategory.Infantry ? 0.7f : 1f) + e.Radius * 30f / _cam.Zoom;
                if (d < r && (best == null || d < bestD)) { best = e; bestD = d; }
            }
            if (best != null) return best;
            var g = _cam.ScreenToGround(new Vector3(gui.x, Screen.height - gui.y));
            if (g.HasValue)
            {
                var b = _w.BuildingAtCell(Mathf.FloorToInt(g.Value.x), Mathf.FloorToInt(g.Value.z));
                if (b != null && _views.Get(b.Id) is EntityView bv && bv.Visible) return b;
            }
            return null;
        }

        // ------------------------------------------------------------------ per-frame
        public void Update()
        {
            CursorLabel = null; CursorColor = Color.white;
            var me = Me;
            if (me == null || !Enabled) return;
            PruneSelection();
            var mouse = InputState.MouseGui;
            bool overUI = InputState.OverUI;
            HoverGround = overUI ? null : _cam.ScreenToGround(InputState.MouseScreen);
            Hover = overUI ? null : PickAt(mouse);
            var k = Keys;

            // ----- keyboard
            HandleControlGroups();
            if (k.Pressed(GameAction.Stop)) { _w.Commands.Stop(Selection, Player); Ack("ack"); }
            if (k.Pressed(GameAction.Deploy)) _w.Commands.Deploy(Selection, Player);
            if (k.Pressed(GameAction.Guard)) { foreach (var u in SelectedUnits()) _w.Commands.Guard(new[] { u.Id }, Player, u.Pos); Ack("ack"); }
            if (k.Pressed(GameAction.Scatter)) Scatter();
            if (k.Pressed(GameAction.CycleStance)) CycleStance();
            if (k.Pressed(GameAction.AttackMove) && SelectedUnits().Count > 0) Mode = CursorMode.AttackMove;
            if (k.Pressed(GameAction.Patrol) && SelectedUnits().Count > 0) Mode = CursorMode.Patrol;
            if (k.Pressed(GameAction.RepairMode)) Mode = Mode == CursorMode.Repair ? CursorMode.Normal : CursorMode.Repair;
            if (k.Pressed(GameAction.SellMode)) Mode = Mode == CursorMode.Sell ? CursorMode.Normal : CursorMode.Sell;
            if (k.Pressed(GameAction.SelectAll)) SelectAll(false);
            if (k.Pressed(GameAction.SelectAllCombat)) SelectAll(true);
            if (k.Pressed(GameAction.SelectSameType)) SelectSameTypeOnScreen();
            if (k.Pressed(GameAction.GoToBase)) GoToBase();
            if (k.Pressed(GameAction.JumpToAlert) && Alerts.Count > 0) { _cam.LookAt(Alerts[_alertIndex]); _alertIndex = (_alertIndex - 1 + Alerts.Count) % Alerts.Count; }
            if (k.Pressed(GameAction.ToggleHealthBars)) ShowAllHealth = !ShowAllHealth;
            if (Mode == CursorMode.Place && k.Pressed(GameAction.RotateStructure)) PlaceRotated = !PlaceRotated;

            // ----- validate modes
            if (Mode == CursorMode.Place && me.Ready[(int)PlaceQueue] == null) Mode = CursorMode.Normal;
            if (Mode == CursorMode.Power && !me.PowerReady[(int)PowerKind]) Mode = CursorMode.Normal;

            // ----- mouse
            if (overUI) { if (!InputState.MouseHeld(0)) Dragging = false; return; }
            switch (Mode)
            {
                case CursorMode.Place: UpdatePlacement(); return;
                case CursorMode.Power: UpdatePowerTargeting(); return;
                case CursorMode.Repair:
                case CursorMode.Sell: UpdateRepairSell(); return;
            }
            UpdateSelectionAndOrders();
        }

        void Ack(string key) => OnOrderSound?.Invoke(key, null);

        void HandleControlGroups()
        {
            for (int i = 0; i <= 9; i++)
            {
                var key = KeyCode.Alpha0 + i;
                if (!InputState.Down(key)) continue;
                if (InputState.Ctrl)
                {
                    _groups[i].Clear();
                    foreach (var id in Selection) { var e = _w.Get(id); if (e != null && e.Owner == Player) _groups[i].Add(id); }
                    foreach (var u in SelectedUnits()) u.GroupId = i;
                }
                else if (InputState.Shift)
                {
                    foreach (var id in _groups[i]) if (!Selection.Contains(id)) Selection.Add(id);
                }
                else
                {
                    if (_groups[i].Count == 0) continue;
                    bool same = Selection.Count == _groups[i].Count && Selection.TrueForAll(_groups[i].Contains);
                    if (same && Time.unscaledTime - _groupTapTime[i] < 0.4f) CenterOnSelection();
                    Selection.Clear(); Selection.AddRange(_groups[i]);
                    _groupTapTime[i] = Time.unscaledTime;
                }
            }
        }

        public void CenterOnSelection()
        {
            if (Selection.Count == 0) return;
            var sum = Vec2.Zero; int n = 0;
            foreach (var id in Selection) { var e = _w.Get(id); if (e != null) { sum += e.Pos; n++; } }
            if (n > 0) _cam.LookAt(sum / n);
        }

        void GoToBase()
        {
            foreach (var b in _w.BuildingsOf(Player)) if (b.Def.Role == BuildingRole.ConstructionYard) { _cam.LookAt(b.Center); return; }
            foreach (var b in _w.BuildingsOf(Player)) { _cam.LookAt(b.Center); return; }
            foreach (var u in _w.UnitsOf(Player)) { _cam.LookAt(u.Pos); return; }
        }

        void Scatter()
        {
            foreach (var u in SelectedUnits())
            {
                var d = _w.Rng.InsideCircle(1f);
                var p = u.Pos + d.Normalized * 2.5f;
                _w.Commands.Move(new[] { u.Id }, Player, p, false, false);
            }
        }

        void CycleStance()
        {
            var units = SelectedUnits();
            if (units.Count == 0) return;
            var next = (Stance)(((int)units[0].Stance + 1) % 4);
            _w.Commands.SetStance(Selection, Player, next);
            _w.Message("", "Stance: " + next, Player);
        }

        void SelectAll(bool combatOnly)
        {
            Selection.Clear();
            foreach (var u in _w.UnitsOf(Player))
            {
                if (u.IsInside) continue;
                if (combatOnly && (!u.IsArmed || u.Def.Has(UnitAbility.Harvester))) continue;
                Selection.Add(u.Id);
            }
        }

        void SelectSameTypeOnScreen()
        {
            var types = new HashSet<string>();
            foreach (var u in SelectedUnits()) types.Add(u.DefId);
            if (types.Count == 0) return;
            Selection.Clear();
            foreach (var u in _w.UnitsOf(Player))
            {
                if (!types.Contains(u.DefId) || u.IsInside) continue;
                var g = _cam.WorldToGui(Conv.ToWorld(u.Pos));
                if (g.x >= 0 && g.y >= 0 && g.x <= Screen.width && g.y <= Screen.height) Selection.Add(u.Id);
            }
        }

        // ------------------------------------------------------------------ selection + orders
        void UpdateSelectionAndOrders()
        {
            var mouse = InputState.MouseGui;
            var units = SelectedUnits();
            var bsel = SelectedBuilding();
            bool ownBuilding = bsel != null && bsel.Owner == Player;

            // cursor feedback
            if (Mode == CursorMode.AttackMove) { CursorLabel = "Attack-Move"; CursorColor = new Color(1f, 0.5f, 0.3f); }
            else if (Mode == CursorMode.Patrol) { CursorLabel = "Patrol"; CursorColor = new Color(0.6f, 0.8f, 1f); }
            else if (units.Count > 0) DescribeContext(units);
            else if (ownBuilding && bsel.HasRally || ownBuilding && IsProducer(bsel)) { CursorLabel = "Set Rally"; CursorColor = new Color(0.7f, 1f, 0.7f); }

            // left button: select / modes
            if (InputState.MouseDown(0))
            {
                if (Mode == CursorMode.AttackMove || Mode == CursorMode.Patrol)
                {
                    if (HoverGround.HasValue)
                    {
                        var p = Conv.ToSim(HoverGround.Value);
                        if (Mode == CursorMode.AttackMove) { _w.Commands.AttackMove(Selection, Player, p, InputState.Shift); Ack("ack"); }
                        else { _w.Commands.Patrol(Selection, Player, p); Ack("ack"); }
                    }
                    if (!InputState.Shift) Mode = CursorMode.Normal;
                    InputState.EatMouse(0);
                    return;
                }
                Dragging = true; DragStart = mouse; _downClicks = InputState.ClickCount(0);
            }
            if (Dragging && InputState.MouseUp(0))
            {
                Dragging = false;
                if (Vector2.Distance(DragStart, mouse) > 6f) BoxSelect(DragStart, mouse, InputState.Shift);
                else ClickSelect(_downClicks >= 2);
            }
            if (!InputState.MouseHeld(0) && !InputState.MouseUp(0)) Dragging = false;

            // right button: context order / cancel modes
            if (InputState.MouseDown(1))
            {
                if (Mode != CursorMode.Normal) { Mode = CursorMode.Normal; return; }
                if (units.Count > 0) IssueContextOrder(units);
                else if (ownBuilding && HoverGround.HasValue && IsProducer(bsel)) { _w.Commands.SetRally(bsel.Id, Player, Conv.ToSim(HoverGround.Value)); Ack("click_soft"); }
                else Selection.Clear();
            }
        }

        static bool IsProducer(Building b)
        {
            var r = b.Def.Role;
            return r == BuildingRole.Barracks || r == BuildingRole.AdvancedBarracks || r == BuildingRole.Factory || r == BuildingRole.AdvancedFactory ||
                   r == BuildingRole.Hangar || r == BuildingRole.AdvancedHangar || r == BuildingRole.Shipyard || r == BuildingRole.AdvancedShipyard;
        }

        void ClickSelect(bool doubleClick)
        {
            var e = Hover;
            if (e == null) { if (!InputState.Shift) Selection.Clear(); return; }
            if (e is Building b && !b.Def.Selectable) return;
            if (doubleClick && e is Unit && e.Owner == Player)
            {
                Selection.Clear();
                var screen = new Rect(0, 0, Screen.width, Screen.height);
                foreach (var u in _w.UnitsOf(Player))
                    if (u.DefId == e.DefId && !u.IsInside && screen.Contains(_cam.WorldToGui(Conv.ToWorld(u.Pos)))) Selection.Add(u.Id);
                return;
            }
            if (InputState.Shift && e.Owner == Player)
            {
                if (Selection.Contains(e.Id)) Selection.Remove(e.Id);
                else
                {
                    // never mix own buildings into a unit selection
                    if (e is Building) Selection.Clear();
                    else Selection.RemoveAll(id => _w.Get(id) is Building || _w.Get(id)?.Owner != Player);
                    Selection.Add(e.Id);
                }
                return;
            }
            Selection.Clear();
            Selection.Add(e.Id);
            if (e.Owner == Player && e is Unit) OnOrderSound?.Invoke("select", null);
        }

        void BoxSelect(Vector2 a, Vector2 b, bool add)
        {
            var r = Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
            var found = new List<Unit>();
            foreach (var u in _w.UnitsOf(Player))
            {
                if (u.IsInside) continue;
                var v = _views.Get(u.Id);
                var wp = v != null ? v.Go.transform.position : Conv.ToWorld(u.Pos);
                if (r.Contains(_cam.WorldToGui(wp))) found.Add(u);
            }
            // Prefer combat units: drop harvesters/MCVs if any armed unit is in the box.
            bool anyCombat = found.Exists(u => u.IsArmed && !u.Def.Has(UnitAbility.Harvester));
            if (!add) Selection.Clear();
            else Selection.RemoveAll(id => !(_w.Get(id) is Unit));
            foreach (var u in found)
            {
                if (anyCombat && (u.Def.Has(UnitAbility.Harvester) || u.Def.Has(UnitAbility.DeployToHQ))) continue;
                if (!Selection.Contains(u.Id)) Selection.Add(u.Id);
            }
            if (Selection.Count > 0) OnOrderSound?.Invoke("select", null);
        }

        // Determine what a right-click would do and show it on the cursor.
        void DescribeContext(List<Unit> units)
        {
            var t = Hover;
            bool ctrl = InputState.Ctrl, alt = InputState.Alt;
            if (ctrl) { CursorLabel = "Force Fire"; CursorColor = new Color(1f, 0.3f, 0.2f); return; }
            if (alt) { CursorLabel = "Force Move"; CursorColor = new Color(0.6f, 1f, 0.6f); return; }
            if (t != null)
            {
                if (_w.IsEnemy(Player, t.Owner) || (t.Owner >= 0 && _w.GetPlayer(t.Owner)?.IsNeutral == true && t is Unit))
                {
                    if (units.Exists(u => u.Def.Has(UnitAbility.CaptureRepair)) && t is Building tb && tb.Def.Capturable) { CursorLabel = "Capture"; CursorColor = new Color(0.4f, 0.9f, 1f); return; }
                    if (units.Exists(u => u.IsArmed)) { CursorLabel = "Attack"; CursorColor = new Color(1f, 0.25f, 0.2f); return; }
                    CursorLabel = "Move"; return;
                }
                if (t is Building b)
                {
                    if (b.Owner != Player && _w.GetPlayer(b.Owner)?.IsNeutral == true && b.Def.Capturable && units.Exists(u => u.Def.Has(UnitAbility.CaptureRepair))) { CursorLabel = "Capture"; CursorColor = new Color(0.4f, 0.9f, 1f); return; }
                    if (b.Owner == Player)
                    {
                        if (b.Def.Role == BuildingRole.Refinery && units.Exists(u => u.Def.Has(UnitAbility.Harvester))) { CursorLabel = "Dock"; return; }
                        if (b.Def.Role == BuildingRole.RepairDepot && units.Exists(u => u.Def.Category == UnitCategory.Vehicle)) { CursorLabel = "Repair"; CursorColor = new Color(1f, 0.9f, 0.3f); return; }
                        if ((b.Def.Role == BuildingRole.Hangar || b.Def.Role == BuildingRole.AdvancedHangar) && units.Exists(u => u.IsAir)) { CursorLabel = "Rearm"; return; }
                        if (b.Hp < b.MaxHp && units.Exists(u => u.Def.Has(UnitAbility.CaptureRepair))) { CursorLabel = "Repair"; CursorColor = new Color(1f, 0.9f, 0.3f); return; }
                    }
                }
                if (t is Unit tu && tu.Owner == Player)
                {
                    if (units.Exists(u => _w.Behavior.CanBoard(tu, u))) { CursorLabel = "Enter"; CursorColor = new Color(0.6f, 1f, 0.6f); return; }
                    if (units.Count == 1 && units[0].Id == tu.Id && tu.Def.Has(UnitAbility.DeployToHQ)) { CursorLabel = "Deploy"; return; }
                    if (tu.Id != units[0].Id) { CursorLabel = "Guard"; CursorColor = new Color(0.6f, 0.8f, 1f); return; }
                }
            }
            if (HoverGround.HasValue)
            {
                var c = Int2.Floor(Conv.ToSim(HoverGround.Value));
                if (_w.Map.HasOre(c.X, c.Y) && units.Exists(u => u.Def.Has(UnitAbility.Harvester))) { CursorLabel = "Harvest"; CursorColor = new Color(1f, 0.85f, 0.3f); return; }
                if (!_w.Fog.IsExplored(_w.TeamOf(Player), c) || !units.Exists(u => u.IsAir || _w.Map.PassableStatic(c.X, c.Y, u.MoveClass))) { CursorLabel = "Move?"; CursorColor = new Color(0.8f, 0.8f, 0.8f); return; }
            }
            CursorLabel = "Move";
        }

        void IssueContextOrder(List<Unit> units)
        {
            var ids = new List<int>(); foreach (var u in units) ids.Add(u.Id);
            bool queue = InputState.Shift;
            var t = Hover;
            if (InputState.Ctrl)
            {
                if (t != null) _w.Commands.Attack(ids, Player, t.Id, queue);
                else if (HoverGround.HasValue) _w.Commands.ForceFire(ids, Player, Conv.ToSim(HoverGround.Value));
                Ack("ack_attack");
                return;
            }
            if (!InputState.Alt && t != null)
            {
                bool enemy = _w.IsEnemy(Player, t.Owner);
                var tp = _w.GetPlayer(t.Owner);
                bool neutral = tp != null && tp.IsNeutral;
                if (t is Building b)
                {
                    var engineers = units.FindAll(u => u.Def.Has(UnitAbility.CaptureRepair));
                    if ((enemy || neutral) && b.Def.Capturable && engineers.Count > 0)
                    {
                        _w.Commands.Capture(engineers.ConvertAll(u => u.Id), Player, b.Id);
                        var rest = units.FindAll(u => !u.Def.Has(UnitAbility.CaptureRepair));
                        if (rest.Count > 0 && enemy) _w.Commands.Attack(rest.ConvertAll(u => u.Id), Player, b.Id, queue);
                        Ack("ack"); return;
                    }
                    if (b.Owner == Player)
                    {
                        if (b.Def.Role == BuildingRole.Refinery)
                        {
                            var hv = units.FindAll(u => u.Def.Has(UnitAbility.Harvester));
                            if (hv.Count > 0) { foreach (var h in hv) h.DockId = b.Id; _w.Commands.ReturnCargo(hv.ConvertAll(u => u.Id), Player); Ack("ack"); return; }
                        }
                        if (b.Def.Role == BuildingRole.RepairDepot)
                        {
                            var veh = units.FindAll(u => u.Def.Category == UnitCategory.Vehicle);
                            if (veh.Count > 0) { _w.Commands.RepairAtDepot(veh.ConvertAll(u => u.Id), Player, b.Id); Ack("ack"); return; }
                        }
                        if (b.Def.Role == BuildingRole.Hangar || b.Def.Role == BuildingRole.AdvancedHangar)
                        {
                            var air = units.FindAll(u => u.IsAir);
                            if (air.Count > 0) { _w.Commands.ReturnToBase(air.ConvertAll(u => u.Id), Player); Ack("ack"); return; }
                        }
                        if (b.Hp < b.MaxHp && engineers.Count > 0) { _w.Commands.Capture(engineers.ConvertAll(u => u.Id), Player, b.Id); Ack("ack"); return; }
                    }
                    if (enemy) { _w.Commands.Attack(ids, Player, b.Id, queue); Ack("ack_attack"); return; }
                }
                else if (t is Unit tu)
                {
                    if (enemy || (neutral && _w.NeutralHostile)) { _w.Commands.Attack(ids, Player, tu.Id, queue); Ack("ack_attack"); return; }
                    if (tu.Owner == Player)
                    {
                        var boarders = units.FindAll(u => _w.Behavior.CanBoard(tu, u));
                        if (boarders.Count > 0) { _w.Commands.Enter(boarders.ConvertAll(u => u.Id), Player, tu.Id); Ack("ack"); return; }
                        if (units.Count == 1 && units[0].Id == tu.Id) { _w.Commands.Deploy(ids, Player); return; }
                        var guards = units.FindAll(u => u.Id != tu.Id);
                        _w.Commands.Guard(guards.ConvertAll(u => u.Id), Player, tu.Pos, tu.Id);
                        Ack("ack"); return;
                    }
                }
            }
            if (!HoverGround.HasValue) return;
            var p = Conv.ToSim(HoverGround.Value);
            var cell = Int2.Floor(p);
            if (_w.Map.HasOre(cell.X, cell.Y))
            {
                var hv = units.FindAll(u => u.Def.Has(UnitAbility.Harvester));
                if (hv.Count > 0)
                {
                    _w.Commands.Harvest(hv.ConvertAll(u => u.Id), Player, p);
                    var rest = units.FindAll(u => !u.Def.Has(UnitAbility.Harvester));
                    if (rest.Count > 0) _w.Commands.Move(rest.ConvertAll(u => u.Id), Player, p, queue);
                    Ack("ack"); return;
                }
            }
            // Transports with passengers unload when ordered onto their own position.
            _w.Commands.Move(ids, Player, p, queue, !InputState.Alt);
            OnOrderSound?.Invoke("ack", null);
            OnMoveMarker?.Invoke(HoverGround.Value);
        }

        public System.Action<Vector3> OnMoveMarker;

        // ------------------------------------------------------------------ placement
        public void BeginPlacement(ProductionQueueType q)
        {
            Mode = CursorMode.Place; PlaceQueue = q; PlaceRotated = false;
        }

        public Int2 PlacementOrigin(BuildingDef def)
        {
            if (!HoverGround.HasValue) return new Int2(-100, -100);
            int w = PlaceRotated && def.Rotatable ? def.Height : def.Width, h = PlaceRotated && def.Rotatable ? def.Width : def.Height;
            var g = HoverGround.Value;
            return new Int2(Mathf.RoundToInt(g.x - w * 0.5f), Mathf.RoundToInt(g.z - h * 0.5f));
        }

        void UpdatePlacement()
        {
            var me = Me;
            var defId = me.Ready[(int)PlaceQueue];
            var def = Catalog.Building(defId);
            if (def == null) { Mode = CursorMode.Normal; return; }
            var o = PlacementOrigin(def);
            bool ok = _w.Production.CanPlace(Player, def, o, PlaceRotated && def.Rotatable);
            CursorLabel = ok ? "Place " + def.Name : "Cannot place here";
            CursorColor = ok ? new Color(0.5f, 1f, 0.5f) : new Color(1f, 0.4f, 0.4f);
            if (InputState.MouseDown(0))
            {
                var b = _w.Commands.Place(Player, PlaceQueue, o, PlaceRotated && def.Rotatable);
                if (b != null)
                {
                    OnOrderSound?.Invoke("place", Conv.ToWorld(b.Center));
                    // walls: keep placing while more are ready (shift)
                    if (!(InputState.Shift && (def.Role == BuildingRole.Wall))) Mode = CursorMode.Normal;
                }
                else OnOrderSound?.Invoke("denied", null);
                InputState.EatMouse(0);
            }
            if (InputState.MouseDown(1)) Mode = CursorMode.Normal;
        }

        public void BeginPower(SupportPowerKind k) { Mode = CursorMode.Power; PowerKind = k; }

        void UpdatePowerTargeting()
        {
            var def = SupportPowerSystem.Def(PowerKind);
            CursorLabel = def.Name; CursorColor = new Color(1f, 0.6f, 0.2f);
            if (InputState.MouseDown(0) && HoverGround.HasValue)
            {
                if (_w.Commands.LaunchPower(Player, PowerKind, Conv.ToSim(HoverGround.Value))) Mode = CursorMode.Normal;
                else OnOrderSound?.Invoke("denied", null);
                InputState.EatMouse(0);
            }
            if (InputState.MouseDown(1)) Mode = CursorMode.Normal;
        }

        void UpdateRepairSell()
        {
            var b = Hover as Building;
            bool valid = b != null && b.Owner == Player;
            if (Mode == CursorMode.Repair) { CursorLabel = valid ? (b.Repairing ? "Stop Repair" : "Repair") : "Repair (select a structure)"; CursorColor = new Color(1f, 0.9f, 0.3f); }
            else { CursorLabel = valid ? "Sell " + b.Def.Name + " (+" + Mathf.RoundToInt(b.Def.Cost * 0.5f * b.HpRatio) + ")" : "Sell (select a structure)"; CursorColor = new Color(1f, 0.75f, 0.3f); }
            if (InputState.MouseDown(0))
            {
                if (valid)
                {
                    if (Mode == CursorMode.Repair) { _w.Commands.ToggleRepair(b.Id, Player); OnOrderSound?.Invoke("click", null); }
                    else { _w.Commands.Sell(b.Id, Player); OnOrderSound?.Invoke("sell", null); }
                }
                if (!InputState.Shift && Mode == CursorMode.Sell) Mode = CursorMode.Normal;
                InputState.EatMouse(0);
            }
            if (InputState.MouseDown(1)) Mode = CursorMode.Normal;
        }

        /// <summary>Minimap right-click: move or attack-move the selection to a map position.</summary>
        public void OrderToMinimap(Vec2 p, bool attackMove)
        {
            var units = SelectedUnits();
            if (units.Count == 0) return;
            if (attackMove || Mode == CursorMode.AttackMove) _w.Commands.AttackMove(Selection, Player, p, InputState.Shift);
            else _w.Commands.Move(Selection, Player, p, InputState.Shift);
            Mode = CursorMode.Normal;
            Ack("ack");
        }
    }
}
