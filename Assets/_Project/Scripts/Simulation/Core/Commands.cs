using System;
using System.Collections.Generic;

namespace RTS.Sim
{
    /// <summary>
    /// The single gateway through which human input and AI controllers act on the world. Every command validates
    /// ownership, so the AI can never do anything the player could not.
    /// </summary>
    public class Commands
    {
        readonly World _w;
        public Commands(World w) { _w = w; }

        List<Unit> Owned(IEnumerable<int> ids, int player)
        {
            var l = new List<Unit>();
            foreach (var id in ids)
            {
                var u = _w.GetUnit(id);
                if (u != null && u.Owner == player && !u.IsInside) l.Add(u);
            }
            return l;
        }

        /// <summary>Computes formation slots so a group arrives in an orderly block instead of a pile.</summary>
        public List<Vec2> FormationSlots(List<Unit> units, Vec2 target)
        {
            var slots = new List<Vec2>();
            if (units.Count == 0) return slots;
            var centroid = Vec2.Zero;
            foreach (var u in units) centroid += u.Pos;
            centroid /= units.Count;
            var dir = (target - centroid);
            if (dir.SqrLength < 0.01f) dir = new Vec2(0, 1);
            dir = dir.Normalized;
            var right = new Vec2(dir.Y, -dir.X);
            int n = units.Count;
            int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(n * 1.5f)));
            float spacing = 1.15f;
            for (int i = 0; i < n; i++)
            {
                int row = i / cols, col = i % cols;
                int inRow = Math.Min(cols, n - row * cols);
                float x = (col - (inRow - 1) * 0.5f) * spacing;
                float y = -row * spacing;
                slots.Add(target + right * x + dir * y);
            }
            return slots;
        }

        void GroupOrder(List<Unit> units, OrderType type, Vec2 pos, bool queue, bool formation)
        {
            if (units.Count == 0) return;
            if (!formation || units.Count == 1)
            {
                foreach (var u in units) _w.Behavior.SetOrder(u, new Order(type, pos), queue);
                return;
            }
            // Sort so that combat units take the front rows and support units the back.
            units.Sort((a, b) => Rank(a).CompareTo(Rank(b)));
            var slots = FormationSlots(units, pos);
            // Greedy assignment by distance keeps crossings low.
            var free = new List<Vec2>(slots);
            foreach (var u in units)
            {
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < free.Count; i++)
                {
                    float d = Vec2.SqrDistance(free[i], u.Pos);
                    if (d < bd) { bd = d; best = i; }
                }
                var slot = free[best];
                free.RemoveAt(best);
                var mc = u.MoveClass;
                var c = Int2.Floor(slot);
                if (mc != MoveClass.Air && !_w.Passable(c.X, c.Y, mc, _w.TeamOf(u.Owner))) slot = pos;
                _w.Behavior.SetOrder(u, new Order(type, slot), queue);
            }
        }

        static int Rank(Unit u)
        {
            if (u.Def.Model == "tank_heavy" || u.Def.Model == "tank_mbt") return 0;
            if (u.Def.Category == UnitCategory.Infantry) return 1;
            if (u.Def.Model == "howitzer" || u.Def.Model == "mlrs" || u.Def.Model == "siege") return 3;
            return 2;
        }

        public void Move(IEnumerable<int> ids, int player, Vec2 pos, bool queue = false, bool formation = true) =>
            GroupOrder(Owned(ids, player), OrderType.Move, pos, queue, formation);

        public void AttackMove(IEnumerable<int> ids, int player, Vec2 pos, bool queue = false) =>
            GroupOrder(Owned(ids, player), OrderType.AttackMove, pos, queue, true);

        public void Attack(IEnumerable<int> ids, int player, int targetId, bool queue = false)
        {
            var t = _w.Get(targetId);
            if (t == null) return;
            foreach (var u in Owned(ids, player))
            {
                if (!u.IsArmed) continue;
                if (u.Def.Has(UnitAbility.Demolition) && t is Building && _w.IsEnemy(player, t.Owner))
                    _w.Behavior.SetOrder(u, new Order(OrderType.Demolish, t.Pos, targetId), queue);
                else if (_w.Combat.CanTarget(u, t))
                    _w.Behavior.SetOrder(u, new Order(OrderType.Attack, t.Pos, targetId), queue);
            }
        }

        public void ForceFire(IEnumerable<int> ids, int player, Vec2 pos)
        {
            foreach (var u in Owned(ids, player)) if (u.IsArmed && !u.IsAir) _w.Behavior.SetOrder(u, new Order(OrderType.ForceFireGround, pos), false);
        }

        public void Guard(IEnumerable<int> ids, int player, Vec2 pos, int guardTarget = 0)
        {
            foreach (var u in Owned(ids, player))
            {
                var o = new Order(OrderType.Guard, pos, guardTarget);
                _w.Behavior.SetOrder(u, o, false);
            }
        }

        public void Patrol(IEnumerable<int> ids, int player, Vec2 pos)
        {
            foreach (var u in Owned(ids, player))
            {
                var o = new Order(OrderType.Patrol, pos) { Pos2 = u.Pos };
                _w.Behavior.SetOrder(u, o, false);
            }
        }

        public void Stop(IEnumerable<int> ids, int player)
        {
            foreach (var u in Owned(ids, player)) { _w.Behavior.SetOrder(u, new Order(OrderType.Stop, u.Pos), false); u.TargetId = 0; }
        }

        public void SetStance(IEnumerable<int> ids, int player, Stance s)
        {
            foreach (var u in Owned(ids, player))
            {
                u.Stance = s;
                if (s == Stance.HoldPosition) { _w.Movement.Stop(u); u.GuardPos = u.Pos; u.HasGuardPos = true; }
                if (s == Stance.HoldFire) u.TargetId = 0;
            }
        }

        public void Deploy(IEnumerable<int> ids, int player)
        {
            foreach (var u in Owned(ids, player))
            {
                if (u.Def.Has(UnitAbility.DeployToHQ)) _w.Behavior.SetOrder(u, new Order(OrderType.Deploy, u.Pos), false);
                else if (u.Passengers.Count > 0) _w.Behavior.SetOrder(u, new Order(OrderType.Unload, u.Pos), false);
                else if (u.Def.Locomotor == Locomotor.Submarine) { u.Submerged = !u.Submerged; u.SurfaceTimer = u.Submerged ? 0 : 999; }
            }
        }

        public void Enter(IEnumerable<int> ids, int player, int transportId)
        {
            var t = _w.GetUnit(transportId);
            if (t == null) return;
            foreach (var u in Owned(ids, player))
                if (u.Id != transportId && _w.Behavior.CanBoard(t, u)) _w.Behavior.SetOrder(u, new Order(OrderType.Enter, t.Pos, transportId), false);
        }

        public void Unload(IEnumerable<int> ids, int player, Vec2? at = null)
        {
            foreach (var u in Owned(ids, player))
            {
                if (u.Passengers.Count == 0) continue;
                if (at.HasValue)
                {
                    _w.Behavior.SetOrder(u, new Order(OrderType.Move, at.Value), false);
                    _w.Behavior.SetOrder(u, new Order(OrderType.Unload, at.Value), true);
                }
                else _w.Behavior.SetOrder(u, new Order(OrderType.Unload, u.Pos), false);
            }
        }

        public void Capture(IEnumerable<int> ids, int player, int buildingId)
        {
            var b = _w.GetBuilding(buildingId);
            if (b == null) return;
            foreach (var u in Owned(ids, player))
                if (u.Def.Has(UnitAbility.CaptureRepair)) _w.Behavior.SetOrder(u, new Order(OrderType.Capture, b.Center, buildingId), false);
        }

        public void Harvest(IEnumerable<int> ids, int player, Vec2 pos)
        {
            foreach (var u in Owned(ids, player))
                if (u.Def.Has(UnitAbility.Harvester)) _w.Behavior.SetOrder(u, new Order(OrderType.Harvest, pos), false);
        }

        public void ReturnCargo(IEnumerable<int> ids, int player)
        {
            foreach (var u in Owned(ids, player))
                if (u.Def.Has(UnitAbility.Harvester)) _w.Behavior.SetOrder(u, new Order(OrderType.ReturnCargo, u.Pos), false);
        }

        public void RepairAtDepot(IEnumerable<int> ids, int player, int depotId)
        {
            foreach (var u in Owned(ids, player))
                if (u.Def.Category == UnitCategory.Vehicle) _w.Behavior.SetOrder(u, new Order(OrderType.RepairAtDepot, Vec2.Zero, depotId), false);
        }

        public void ReturnToBase(IEnumerable<int> ids, int player)
        {
            foreach (var u in Owned(ids, player)) if (u.IsAir) _w.Behavior.SetOrder(u, new Order(OrderType.Rearm, u.Pos), false);
        }

        public void Follow(IEnumerable<int> ids, int player, int targetId)
        {
            foreach (var u in Owned(ids, player)) if (u.Id != targetId) _w.Behavior.SetOrder(u, new Order(OrderType.Follow, u.Pos, targetId), false);
        }

        // ------------------------------------------------------------------ Structures
        public void Sell(int buildingId, int player)
        {
            var b = _w.GetBuilding(buildingId);
            if (b != null && b.Owner == player && b.BuildUp >= 1f) _w.Economy.StartSale(b);
        }

        public void ToggleRepair(int buildingId, int player)
        {
            var b = _w.GetBuilding(buildingId);
            if (b != null && b.Owner == player && b.Hp < b.MaxHp) b.Repairing = !b.Repairing;
            else if (b != null && b.Owner == player) b.Repairing = false;
        }

        public void SetRally(int buildingId, int player, Vec2 pos)
        {
            var b = _w.GetBuilding(buildingId);
            if (b != null && b.Owner == player) { b.Rally = pos; b.HasRally = true; }
        }

        public bool Enqueue(int player, string defId, bool tech = false, int count = 1) => _w.Production.Enqueue(player, defId, tech, count);
        public void Cancel(int player, ProductionQueueType q, string defId) => _w.Production.Cancel(player, q, defId);
        public void Hold(int player, ProductionQueueType q) => _w.Production.ToggleHold(player, q);
        public Building Place(int player, ProductionQueueType q, Int2 origin, bool rotated) => _w.Production.Place(player, q, origin, rotated);
        public bool LaunchPower(int player, SupportPowerKind kind, Vec2 target) => _w.Powers.Launch(player, kind, target);
    }
}
