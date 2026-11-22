using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>
    /// Isometric orthographic camera: 45° pitch, yaw in 90° steps (smoothly animated), edge/keyboard/middle-drag
    /// scrolling, wheel zoom, bounds clamping and screen shake. Also provides screen→ground picking against the
    /// visual height field.
    /// </summary>
    public class RTSCamera
    {
        public readonly Camera Cam;
        public Vector3 Focus;
        public float Zoom = 11f, TargetZoom = 11f;
        public float Yaw = 45f, TargetYaw = 45f;
        public const float Pitch = 50f;
        public float MinZoom = 5f, MaxZoom = 24f;
        float _shake;
        Vector3 _dragAnchor; bool _dragging;
        readonly MapData _map;
        public bool Locked;     // cinematics

        public RTSCamera(Camera cam, MapData map)
        {
            Cam = cam; _map = map;
            Cam.orthographic = true;
            Cam.nearClipPlane = 0.3f; Cam.farClipPlane = 400f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
            Focus = new Vector3(map.Width * 0.5f, 0, map.Height * 0.5f);
            Apply();
        }

        public void LookAt(Vec2 p, bool instant = true)
        {
            Focus = new Vector3(p.X, 0, p.Y);
            Clamp();
            if (instant) Apply();
        }

        public Vec2 FocusSim => new Vec2(Focus.x, Focus.z);

        public void Shake(float amount) => _shake = Mathf.Min(1.2f, _shake + amount);

        public void Update(float dt, Keybinds keys, bool allowInput)
        {
            var s = ClientSettings.Current;
            if (allowInput && !Locked)
            {
                float speed = (12f + Zoom * 1.6f) * s.ScrollSpeed * dt;
                Vector3 fwd = Quaternion.Euler(0, Yaw, 0) * Vector3.forward, right = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
                Vector3 move = Vector3.zero;
                if (keys.Held(GameAction.CameraUp)) move += fwd;
                if (keys.Held(GameAction.CameraDown)) move -= fwd;
                if (keys.Held(GameAction.CameraRight)) move += right;
                if (keys.Held(GameAction.CameraLeft)) move -= right;
                if (s.EdgeScroll && Application.isFocused && InputState.MouseInWindow)
                {
                    var m = InputState.MouseGui; const float edge = 6f;
                    if (m.x >= 0 && m.y >= 0 && m.x <= Screen.width && m.y <= Screen.height)
                    {
                        if (m.x < edge) move -= right;
                        if (m.x > Screen.width - edge) move += right;
                        if (m.y < edge) move += fwd;
                        if (m.y > Screen.height - edge) move -= fwd;
                    }
                }
                Focus += move.normalized * speed * (move.sqrMagnitude > 0 ? 1 : 0);

                // middle-mouse drag pan
                if (InputState.MouseDown(2)) { var g = ScreenToGround(InputState.MouseScreen); if (g.HasValue) { _dragAnchor = g.Value; _dragging = true; } }
                if (!InputState.MouseHeld(2)) _dragging = false;
                if (_dragging)
                {
                    var g = ScreenToGroundPlane(InputState.MouseScreen, _dragAnchor.y);
                    if (g.HasValue) Focus += _dragAnchor - g.Value;
                }

                float wheel = InputState.OverUI ? 0 : InputState.Scroll;
                if (s.InvertZoom) wheel = -wheel;
                if (Mathf.Abs(wheel) > 0.01f) TargetZoom = Mathf.Clamp(TargetZoom * (1f - Mathf.Sign(wheel) * 0.12f), MinZoom, MaxZoom);
                if (keys.Held(GameAction.ZoomIn)) TargetZoom = Mathf.Clamp(TargetZoom - dt * 10f, MinZoom, MaxZoom);
                if (keys.Held(GameAction.ZoomOut)) TargetZoom = Mathf.Clamp(TargetZoom + dt * 10f, MinZoom, MaxZoom);
                if (keys.Pressed(GameAction.RotateLeft)) TargetYaw -= 90f;
                if (keys.Pressed(GameAction.RotateRight)) TargetYaw += 90f;
                if (keys.Pressed(GameAction.CameraReset)) { TargetYaw = 45f; TargetZoom = 11f; }
            }
            Zoom = Mathf.Lerp(Zoom, TargetZoom, 1f - Mathf.Exp(-dt * 10f));
            Yaw = Mathf.Lerp(Yaw, TargetYaw, 1f - Mathf.Exp(-dt * 8f));
            Clamp();
            _shake = Mathf.Max(0, _shake - dt * 1.8f);
            Apply();
        }

        void Clamp()
        {
            Focus.x = Mathf.Clamp(Focus.x, 2f, _map.Width - 2f);
            Focus.z = Mathf.Clamp(Focus.z, 2f, _map.Height - 2f);
            Focus.y = TerrainHeights.Current != null ? TerrainHeights.Current.Sample(Focus.x, Focus.z) * 0.5f : 0f;
        }

        public void Apply()
        {
            var rot = Quaternion.Euler(Pitch, Yaw, 0);
            Cam.orthographicSize = Zoom;
            var shake = _shake > 0 ? (Vector3)(Random.insideUnitCircle * _shake * 0.25f) : Vector3.zero;
            Cam.transform.rotation = rot;
            Cam.transform.position = Focus - rot * Vector3.forward * 120f + rot * shake;
        }

        /// <summary>Intersects a screen ray with the visual terrain height field.</summary>
        public Vector3? ScreenToGround(Vector3 screen)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var th = TerrainHeights.Current;
            if (th == null) return ScreenToGroundPlane(screen, 0);
            // Start where the ray crosses a height above any terrain, then march down.
            float top = 4f, bottom = -1f;
            if (Mathf.Abs(ray.direction.y) < 1e-4f) return null;
            float t0 = (top - ray.origin.y) / ray.direction.y, t1 = (bottom - ray.origin.y) / ray.direction.y;
            float step = 0.2f; float prevT = t0;
            for (float t = t0; t <= t1; t += step)
            {
                var p = ray.origin + ray.direction * t;
                if (p.y <= th.Sample(p.x, p.z))
                {
                    float a = prevT, b = t;
                    for (int i = 0; i < 8; i++)
                    {
                        float m = (a + b) * 0.5f; var pm = ray.origin + ray.direction * m;
                        if (pm.y <= th.Sample(pm.x, pm.z)) b = m; else a = m;
                    }
                    return ray.origin + ray.direction * b;
                }
                prevT = t;
            }
            return ray.origin + ray.direction * t1;
        }

        public Vector3? ScreenToGroundPlane(Vector3 screen, float height)
        {
            var ray = Cam.ScreenPointToRay(screen);
            var plane = new Plane(Vector3.up, new Vector3(0, height, 0));
            return plane.Raycast(ray, out float d) ? ray.GetPoint(d) : (Vector3?)null;
        }

        public Vector2 WorldToGui(Vector3 w)
        {
            var s = Cam.WorldToScreenPoint(w);
            return new Vector2(s.x, Screen.height - s.y);
        }

        /// <summary>Approximate ground-space corners of the current view, for the minimap camera box.</summary>
        public Vector3[] ViewCorners()
        {
            var res = new Vector3[4];
            var pts = new[] { new Vector3(0, 0), new Vector3(Screen.width, 0), new Vector3(Screen.width, Screen.height), new Vector3(0, Screen.height) };
            for (int i = 0; i < 4; i++) res[i] = ScreenToGroundPlane(pts[i], 0) ?? Focus;
            return res;
        }
    }
}
