using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>Visual representation of one simulation entity.</summary>
    public class EntityView
    {
        public int Id;
        public GameObject Go;
        public Transform Body, Turret, Rotor, Extra;
        public ModelParts Parts;
        public MeshRenderer[] Renderers;
        public bool IsBuilding;
        public bool Visible;
        public bool EverSeen;
        public Vector3 SmoothPos;
        public float SmoothYaw, SmoothTurretYaw;
        public float Flash;          // damage flash timer
        public float Recoil;
        public float LastHp;
        public float Dying;          // > 0 while playing the death sink
        public float Bank;
        public int ColorKey;
        public string DefId;
        public int Owner;

        static readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        public void SetTint(Color c)
        {
            _mpb.SetColor("_Tint", c);
            foreach (var r in Renderers) if (r != null) r.SetPropertyBlock(_mpb);
        }

        public void SetVisible(bool v)
        {
            if (v == Visible) return;
            Visible = v;
            Go.SetActive(v);
        }

        /// <summary>World-space muzzle position for weapon effects.</summary>
        public Vector3 Muzzle()
        {
            var t = Turret != null ? Turret : Go.transform;
            float yaw = Turret != null ? SmoothTurretYaw : SmoothYaw;
            var fwd = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
            var basePos = Go.transform.position;
            return basePos + fwd * Parts.MuzzleForward + Vector3.up * Parts.MuzzleHeight;
        }
    }

    /// <summary>Creates, updates and destroys entity views to mirror the simulation each frame.</summary>
    public class ViewManager
    {
        readonly World _w;
        readonly Transform _root;
        public int LocalPlayer;
        public int LocalTeam;
        public bool RevealAll;
        public readonly Dictionary<int, EntityView> Views = new Dictionary<int, EntityView>();
        readonly List<EntityView> _dying = new List<EntityView>();
        readonly HashSet<int> _alive = new HashSet<int>();
        readonly List<int> _remove = new List<int>();

        public ViewManager(World w, Transform parent, int localPlayer)
        {
            _w = w;
            _root = new GameObject("Entities").transform;
            _root.SetParent(parent, false);
            LocalPlayer = localPlayer;
            LocalTeam = w.TeamOf(localPlayer);
        }

        public EntityView Get(int id) => Views.TryGetValue(id, out var v) ? v : null;

        public bool IsVisible(Entity e)
        {
            if (e == null || e.Dead) return false;
            if (RevealAll) return true;
            if (e is Unit u && u.IsInside) return false;
            return _w.Combat.VisibleTo(e, LocalTeam);
        }

        EntityView Create(Entity e)
        {
            var p = _w.GetPlayer(e.Owner);
            var team = Palette.ForPlayer(p);
            int ck = p == null || p.IsNeutral ? 7 : p.ColorIndex % 7;
            ModelParts parts;
            var v = new EntityView { Id = e.Id, IsBuilding = e.IsBuilding, ColorKey = ck, DefId = e.DefId, Owner = e.Owner, LastHp = e.Hp };
            if (e is Building b) parts = ModelLibrary.ForBuilding(b.Def, team, ck, b.W, b.H);
            else parts = ModelLibrary.ForUnit(((Unit)e).Def, team, ck);
            v.Parts = parts;
            v.Go = new GameObject((e.IsBuilding ? "B_" : "U_") + e.DefId + "_" + e.Id);
            v.Go.transform.SetParent(_root, false);
            var rs = new List<MeshRenderer>();
            v.Body = Part(v.Go.transform, "Body", parts.Body, Vector3.zero, rs);
            if (parts.Turret != null) v.Turret = Part(v.Go.transform, "Turret", parts.Turret, parts.TurretPos, rs);
            if (parts.Rotor != null) v.Rotor = Part(v.Go.transform, "Rotor", parts.Rotor, parts.RotorPos, rs);
            if (parts.Extra != null) v.Extra = Part(v.Go.transform, "Extra", parts.Extra, parts.ExtraPos, rs);
            v.Renderers = rs.ToArray();
            v.Visible = true;
            float yaw = 90f - e.Facing * Mathf.Rad2Deg;
            v.SmoothYaw = yaw; v.SmoothTurretYaw = 90f - e.TurretFacing * Mathf.Rad2Deg;
            v.SmoothPos = Target(e, 1f);
            v.Go.transform.position = v.SmoothPos;
            v.SetTint(Color.white);
            return v;
        }

        static Transform Part(Transform parent, string name, Mesh mesh, Vector3 pos, List<MeshRenderer> rs)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Materials.Unit;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            rs.Add(mr);
            return go.transform;
        }

        Vector3 Target(Entity e, float alpha)
        {
            var m = _w.Map;
            if (e is Building b)
            {
                var c = b.Center;
                float gh = Conv.Ground(m, c.X, c.Y);
                if (b.Def.OnWater) gh = Mathf.Max(gh, m.LevelAt(b.Origin.X, b.Origin.Y) * Conv.LevelHeight - 0.1f);
                return new Vector3(c.X, gh, c.Y);
            }
            var u = (Unit)e;
            var p = Vec2.Lerp(u.PrevPos, u.Pos, alpha);
            float h = Conv.Ground(m, p.X, p.Y);
            if (u.Def.Category == UnitCategory.Naval) h = Mathf.Max(h, m.LevelAt((int)p.X, (int)p.Y) * Conv.LevelHeight - 0.12f);
            if (_w.Map.Get((int)p.X, (int)p.Y) == Terrain.Bridge && !u.IsAir) h = Mathf.Max(h, m.LevelAt((int)p.X, (int)p.Y) * Conv.LevelHeight + 0.16f);
            if (u.IsAir) h += u.Height * 0.9f;
            if (u.Def.Locomotor == Locomotor.Submarine && u.Submerged) h -= 0.25f;
            return new Vector3(p.X, h, p.Y);
        }

        /// <summary>Called once per rendered frame. alpha = fraction of the way to the next simulation tick.</summary>
        public void Sync(float alpha, float dt, float time)
        {
            _alive.Clear();
            foreach (var u in _w.Units) { if (!u.Dead) { _alive.Add(u.Id); SyncEntity(u, alpha, dt, time); } }
            foreach (var b in _w.Buildings) { if (!b.Dead) { _alive.Add(b.Id); SyncEntity(b, alpha, dt, time); } }

            _remove.Clear();
            foreach (var kv in Views) if (!_alive.Contains(kv.Key)) _remove.Add(kv.Key);
            foreach (var id in _remove)
            {
                var v = Views[id];
                Views.Remove(id);
                if (v.Visible && !v.IsBuilding) { v.Dying = 1.6f; _dying.Add(v); v.SetTint(new Color(0.25f, 0.22f, 0.2f)); }
                else Object.Destroy(v.Go);
            }
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                var v = _dying[i];
                v.Dying -= dt;
                v.Go.transform.position += Vector3.down * dt * 0.25f;
                if (v.Parts.Infantry) v.Go.transform.rotation = Quaternion.Slerp(v.Go.transform.rotation, Quaternion.Euler(-85, v.SmoothYaw, 0), dt * 6f);
                if (v.Dying <= 0) { Object.Destroy(v.Go); _dying.RemoveAt(i); }
            }
        }

        void SyncEntity(Entity e, float alpha, float dt, float time)
        {
            if (!Views.TryGetValue(e.Id, out var v) || v.Owner != e.Owner || v.DefId != e.DefId)
            {
                if (v != null) Object.Destroy(v.Go);
                v = Create(e);
                Views[e.Id] = v;
            }
            bool vis = IsVisible(e);
            if (e.IsBuilding)
            {
                if (vis) v.EverSeen = true;
                var b = (Building)e;
                // Structures stay visible (as last seen) once discovered.
                if (!vis && v.EverSeen && _w.Fog.IsExplored(LocalTeam, b.Origin)) vis = true;
            }
            v.SetVisible(vis);
            if (!vis) return;

            if (e.Hp < v.LastHp - 0.5f) v.Flash = 0.12f;
            v.LastHp = e.Hp;
            if (e.SinceFired < 0.05f) v.Recoil = 1f;
            v.Flash -= dt; v.Recoil = Mathf.Max(0, v.Recoil - dt * 6f);

            Color tint = Color.white;
            if (e.Disabled) tint = new Color(0.6f, 0.75f, 1.3f);
            if (v.Flash > 0) tint = new Color(1.8f, 1.8f, 1.8f);
            if (e is Building bb)
            {
                if (!bb.Powered && bb.Def.Power < 0 && bb.Operational) tint *= 0.7f;
                if (bb.Selling) tint = Color.Lerp(tint, new Color(1.2f, 1.0f, 0.4f), 0.4f);
            }
            if (e is Unit su && su.Def.Locomotor == Locomotor.Submarine && su.Submerged) tint *= 0.55f;
            else if (e is Unit st && _w.Combat.IsStealthed(st)) tint = new Color(tint.r * 0.6f, tint.g * 0.7f, tint.b * 0.9f);
            v.SetTint(tint);

            var tf = v.Go.transform;
            var target = Target(e, alpha);
            if (e is Building b2)
            {
                tf.position = target;
                float s = Mathf.Clamp01(b2.BuildUp);
                float sell = b2.Selling ? 1f - Mathf.Clamp01(b2.SellTimer / 1.2f) : 1f;
                float sy = Mathf.Max(0.04f, Mathf.Min(s, sell));
                tf.localScale = new Vector3(1, sy, 1);
                if (v.Turret != null)
                {
                    float ty = 90f - e.TurretFacing * Mathf.Rad2Deg;
                    v.SmoothTurretYaw = Mathf.MoveTowardsAngle(v.SmoothTurretYaw, ty, dt * 400f);
                    v.Turret.localRotation = Quaternion.Euler(0, v.SmoothTurretYaw, 0);
                    v.Turret.localPosition = v.Parts.TurretPos - (Quaternion.Euler(0, v.SmoothTurretYaw, 0) * Vector3.forward) * v.Recoil * 0.06f;
                }
                if (v.Extra != null)
                {
                    if (b2.Def.Role == BuildingRole.Gate)
                    {
                        float target2 = b2.GateOpen ? -0.75f : 0f;
                        var lp = v.Extra.localPosition;
                        lp.y = Mathf.MoveTowards(lp.y, v.Parts.ExtraPos.y + target2, dt * 1.5f);
                        v.Extra.localPosition = lp;
                    }
                    else if (v.Parts.ExtraSpins)
                    {
                        float speed = b2.Def.Role == BuildingRole.ConstructionYard ? 12f : b2.Def.Role == BuildingRole.RepairDepot ? (b2.Repairing || _w.Time - b2.LastDamagedTime > 0 ? 40f : 0f) : 60f;
                        if (b2.Operational && (b2.Powered || b2.Def.Power >= 0)) v.Extra.localRotation = Quaternion.Euler(0, time * speed + b2.Id * 37f, 0);
                    }
                    else if (b2.Def.Role == BuildingRole.Superweapon)
                    {
                        // Hammerfall missile rises as the weapon charges.
                        var p = _w.GetPlayer(b2.Owner);
                        float frac = 0f;
                        if (p != null && b2.Def.Power_Kind.HasValue) frac = _w.Powers.ChargeFraction(p, b2.Def.Power_Kind.Value);
                        v.Extra.localPosition = v.Parts.ExtraPos + Vector3.up * frac * 1.1f;
                    }
                }
                return;
            }

            var u = (Unit)e;
            // smooth position (interpolation already handles tick spacing; small extra smoothing hides teleports)
            if ((v.SmoothPos - target).sqrMagnitude > 9f) v.SmoothPos = target;
            v.SmoothPos = Vector3.Lerp(v.SmoothPos, target, 1f - Mathf.Exp(-dt * 30f));
            float yaw = 90f - u.Facing * Mathf.Rad2Deg;
            float prevYaw = v.SmoothYaw;
            v.SmoothYaw = Mathf.MoveTowardsAngle(v.SmoothYaw, yaw, dt * 540f);
            float turnRate = Mathf.DeltaAngle(prevYaw, v.SmoothYaw) / Mathf.Max(dt, 0.001f);
            var pos = v.SmoothPos;
            var rot = Quaternion.Euler(0, v.SmoothYaw, 0);
            bool moving = u.Moving && u.Speed > 0.05f;
            if (v.Parts.Infantry)
            {
                if (moving) pos.y += Mathf.Abs(Mathf.Sin(time * 11f + u.Id)) * 0.04f;
                if (u.SuppressedTimer > 0) rot *= Quaternion.Euler(25, 0, 0);
            }
            else if (u.IsAir)
            {
                v.Bank = Mathf.Lerp(v.Bank, Mathf.Clamp(-turnRate * 0.12f, -35f, 35f), dt * 4f);
                if (u.Def.Locomotor == Locomotor.Helicopter)
                {
                    pos.y += Mathf.Sin(time * 2f + u.Id) * 0.03f;
                    rot *= Quaternion.Euler(moving ? 8f : 0f, 0, v.Bank * 0.4f);
                }
                else rot *= Quaternion.Euler(0, 0, v.Bank);
            }
            else if (u.Def.Category == UnitCategory.Naval)
            {
                pos.y += Mathf.Sin(time * 1.5f + u.Id) * 0.025f;
                rot *= Quaternion.Euler(Mathf.Sin(time * 1.1f + u.Id) * 2f, 0, Mathf.Sin(time * 0.9f + u.Id * 2) * 2.5f);
            }
            else if (moving)
            {
                rot *= Quaternion.Euler(Mathf.Sin(time * 9f + u.Id) * 0.6f, 0, 0);
            }
            tf.position = pos;
            tf.rotation = rot;

            if (v.Turret != null)
            {
                float ty = 90f - u.TurretFacing * Mathf.Rad2Deg;
                v.SmoothTurretYaw = Mathf.MoveTowardsAngle(v.SmoothTurretYaw, ty, dt * 400f);
                // turret rotation is in world yaw; convert to local relative to the chassis
                v.Turret.localRotation = Quaternion.Euler(0, Mathf.DeltaAngle(v.SmoothYaw, v.SmoothTurretYaw), 0);
                v.Turret.localPosition = v.Parts.TurretPos + Quaternion.Euler(0, Mathf.DeltaAngle(v.SmoothYaw, v.SmoothTurretYaw), 0) * Vector3.back * v.Recoil * 0.06f;
            }
            if (v.Rotor != null)
            {
                float spin = u.AState == AirState.Docked ? 60f : 1400f;
                v.Rotor.localRotation = Quaternion.Euler(0, time * spin + u.Id * 13f, 0);
            }
            if (v.Extra != null && u.Def.Category == UnitCategory.Naval)
                v.Extra.localRotation = Quaternion.Euler(0, 180f + Mathf.DeltaAngle(v.SmoothYaw, v.SmoothTurretYaw) * 0.5f, 0);
        }

        public void Clear()
        {
            foreach (var v in Views.Values) Object.Destroy(v.Go);
            foreach (var v in _dying) Object.Destroy(v.Go);
            Views.Clear(); _dying.Clear();
        }
    }
}
