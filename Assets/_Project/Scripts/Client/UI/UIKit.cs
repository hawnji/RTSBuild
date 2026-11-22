using System.Collections.Generic;
using RTS.Sim;
using UnityEngine;
using Terrain = RTS.Sim.Terrain;
using FogMode = RTS.Sim.FogMode;

namespace RTS.Client
{
    /// <summary>
    /// Shared IMGUI look: generated textures and styles, scaled layout helpers and drawing primitives. The UI is laid
    /// out in a virtual 1920x1080-height space and scaled with GUI.matrix.
    /// </summary>
    public static class UIKit
    {
        public static float Scale = 1f;
        public static float VW => Screen.width / Scale;
        public static float VH => Screen.height / Scale;
        static bool _init;
        public static GUIStyle Panel, PanelDark, Button, ButtonBig, ButtonSmall, Tab, TabOn, Label, LabelSmall, LabelBold, Title, Header, CenterLabel, RightLabel, Cameo, Tooltip, TextField, Toggle, Subtitle, ListItem, ListItemOn, Badge;
        public static Texture2D White, PanelTex, PanelDarkTex, BtnTex, BtnHover, BtnActive, TabTex, TabOnTex, CameoTex, Gradient;
        public static Color Accent = new Color(0.95f, 0.72f, 0.25f);
        public static Color AccentAlliance = new Color(0.35f, 0.6f, 0.95f);
        public static Color AccentDominion = new Color(0.9f, 0.32f, 0.22f);

        static Texture2D Tex(Color fill, Color border, int size = 16, int bw = 1, Color? top = null)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool b = x < bw || y < bw || x >= size - bw || y >= size - bw;
                    var c = fill;
                    if (top.HasValue) c = Color.Lerp(fill, top.Value, y / (float)(size - 1));
                    px[y * size + x] = b ? border : c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        public static void Init()
        {
            if (_init && PanelTex != null) return;
            _init = true;
            White = Texture2D.whiteTexture;
            PanelTex = Tex(new Color(0.09f, 0.1f, 0.11f, 0.93f), new Color(0.32f, 0.34f, 0.36f, 1f), 16, 1, new Color(0.13f, 0.14f, 0.16f, 0.93f));
            PanelDarkTex = Tex(new Color(0.03f, 0.035f, 0.04f, 0.85f), new Color(0.2f, 0.21f, 0.22f, 1f));
            BtnTex = Tex(new Color(0.17f, 0.18f, 0.2f), new Color(0.38f, 0.4f, 0.42f), 16, 1, new Color(0.24f, 0.25f, 0.27f));
            BtnHover = Tex(new Color(0.24f, 0.24f, 0.22f), Accent, 16, 1, new Color(0.32f, 0.3f, 0.25f));
            BtnActive = Tex(new Color(0.35f, 0.28f, 0.12f), Accent, 16, 1, new Color(0.45f, 0.36f, 0.15f));
            TabTex = Tex(new Color(0.12f, 0.13f, 0.14f), new Color(0.28f, 0.3f, 0.32f));
            TabOnTex = Tex(new Color(0.3f, 0.24f, 0.1f), Accent);
            CameoTex = Tex(new Color(0.07f, 0.08f, 0.09f), new Color(0.3f, 0.32f, 0.34f));
            Gradient = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < 64; i++) Gradient.SetPixel(0, i, new Color(0, 0, 0, i / 63f));
            Gradient.Apply();

            Panel = new GUIStyle { normal = { background = PanelTex }, border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(10, 10, 8, 8) };
            PanelDark = new GUIStyle(Panel) { normal = { background = PanelDarkTex } };
            Button = new GUIStyle
            {
                normal = { background = BtnTex, textColor = new Color(0.88f, 0.9f, 0.92f) },
                hover = { background = BtnHover, textColor = Color.white },
                active = { background = BtnActive, textColor = Color.white },
                onNormal = { background = BtnActive, textColor = Color.white },
                border = new RectOffset(2, 2, 2, 2), alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold,
                padding = new RectOffset(8, 8, 4, 4), richText = true
            };
            ButtonBig = new GUIStyle(Button) { fontSize = 22, padding = new RectOffset(14, 14, 8, 8) };
            ButtonSmall = new GUIStyle(Button) { fontSize = 12, fontStyle = FontStyle.Normal, padding = new RectOffset(4, 4, 2, 2) };
            Tab = new GUIStyle(Button) { normal = { background = TabTex, textColor = new Color(0.75f, 0.77f, 0.8f) }, fontSize = 11 };
            TabOn = new GUIStyle(Tab) { normal = { background = TabOnTex, textColor = Color.white } };
            Label = new GUIStyle { normal = { textColor = new Color(0.86f, 0.88f, 0.9f) }, fontSize = 16, wordWrap = true, richText = true };
            LabelSmall = new GUIStyle(Label) { fontSize = 13 };
            LabelBold = new GUIStyle(Label) { fontStyle = FontStyle.Bold };
            Title = new GUIStyle(Label) { fontSize = 54, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = Accent }, wordWrap = false };
            Header = new GUIStyle(Label) { fontSize = 26, fontStyle = FontStyle.Bold, normal = { textColor = Accent } };
            CenterLabel = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
            RightLabel = new GUIStyle(Label) { alignment = TextAnchor.MiddleRight };
            Cameo = new GUIStyle { normal = { background = CameoTex }, hover = { background = BtnHover }, border = new RectOffset(2, 2, 2, 2) };
            Tooltip = new GUIStyle(Panel) { normal = { background = PanelTex, textColor = Color.white }, fontSize = 13, wordWrap = true, richText = true };
            TextField = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal, normal = { background = PanelDarkTex, textColor = Color.white }, focused = { background = PanelDarkTex, textColor = Color.white } };
            Toggle = new GUIStyle(Button) { fontSize = 14 };
            Subtitle = new GUIStyle(Label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            ListItem = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Normal, fontSize = 15 };
            ListItemOn = new GUIStyle(ListItem) { normal = { background = BtnActive, textColor = Color.white } };
            Badge = new GUIStyle(Label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight, normal = { textColor = Color.white } };
        }

        /// <summary>Computes the UI scale for the current resolution (virtual height 1080, user multiplier).</summary>
        public static void BeginScaled()
        {
            Scale = Mathf.Max(0.5f, Screen.height / 1080f) * Mathf.Clamp(ClientSettings.Current.UiScale, 0.7f, 1.5f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Scale, Scale, 1));
        }

        /// <summary>Registers a scaled-space rect as UI so world clicks under it are ignored.</summary>
        public static void Block(Rect r) => InputState.BlockRect(new Rect(r.x * Scale, r.y * Scale, r.width * Scale, r.height * Scale));

        public static bool Btn(Rect r, string text, GUIStyle style = null, bool enabled = true)
        {
            var old = GUI.enabled; GUI.enabled = enabled && old;
            bool c = GUI.Button(r, text, style ?? Button);
            GUI.enabled = old;
            if (c) AudioManager.Play2D("click", AudioBus.Ui, 0.6f);
            return c;
        }

        public static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, White); GUI.color = o; }

        public static void Box(Rect r, float t)
        {
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), White);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), White);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), White);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), White);
        }

        public static void Line(Vector2 a, Vector2 b, Color c, float w, Rect clip)
        {
            var o = GUI.color; GUI.color = c;
            float len = Vector2.Distance(a, b);
            int n = Mathf.Max(1, (int)(len / 2f));
            for (int i = 0; i <= n; i++)
            {
                var p = Vector2.Lerp(a, b, i / (float)n);
                if (clip.Contains(p)) GUI.DrawTexture(new Rect(p.x - w * 0.5f, p.y - w * 0.5f, w, w), White);
            }
            GUI.color = o;
        }

        public static void ProgressBar(Rect r, float f, Color c)
        {
            Fill(r, new Color(0, 0, 0, 0.6f));
            Fill(new Rect(r.x + 1, r.y + 1, (r.width - 2) * Mathf.Clamp01(f), r.height - 2), c);
        }

        public static void Shadowed(Rect r, string text, GUIStyle s)
        {
            var o = s.normal.textColor;
            s.normal.textColor = new Color(0, 0, 0, 0.8f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, s);
            s.normal.textColor = o;
            GUI.Label(r, text, s);
        }

        public static string Money(float v) => "$" + Mathf.FloorToInt(v).ToString("N0");

        public static string Time(float seconds)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return s >= 3600 ? $"{s / 3600}:{(s / 60) % 60:00}:{s % 60:00}" : $"{s / 60:00}:{s % 60:00}";
        }

        public static Color FactionColor(FactionId f) => f == FactionId.Alliance ? AccentAlliance : f == FactionId.Dominion ? AccentDominion : Accent;
    }

    /// <summary>
    /// Renders build-menu cameos from the procedural models using an off-screen camera (layer 31), cached as
    /// textures. Requests are processed a few per frame from Update.
    /// </summary>
    public static class IconCache
    {
        const int Size = 128;
        const int Layer = 31;
        static readonly Dictionary<string, Texture2D> _icons = new Dictionary<string, Texture2D>();
        static readonly Queue<string> _pending = new Queue<string>();
        static readonly HashSet<string> _queued = new HashSet<string>();
        static Camera _cam;
        static RenderTexture _rt;

        public static Texture2D Get(string defId, int colorKey)
        {
            string key = defId + "|" + colorKey;
            if (_icons.TryGetValue(key, out var t)) return t;
            if (_queued.Add(key)) _pending.Enqueue(key);
            return null;
        }

        public static void Process(int budget = 4)
        {
            while (budget-- > 0 && _pending.Count > 0)
            {
                var key = _pending.Dequeue();
                try { _icons[key] = Render(key); }
                catch (System.Exception ex) { Debug.LogWarning("Icon render failed for " + key + ": " + ex.Message); _icons[key] = null; }
            }
        }

        static Texture2D Render(string key)
        {
            var parts = key.Split('|');
            string id = parts[0]; int ck = int.Parse(parts[1]);
            var team = Palette.Team[ck % Palette.Team.Length];
            ModelParts mp; float radius;
            var ud = Catalog.Unit(id);
            var bd = ud == null ? Catalog.Building(id) : null;
            var tech = ud == null && bd == null ? Catalog.Tech(id) : null;
            if (ud != null) { mp = ModelLibrary.ForUnit(ud, team, ck); radius = ud.Category == UnitCategory.Infantry ? 0.35f : ud.Category == UnitCategory.Naval ? 1.4f : 0.8f; }
            else if (bd != null) { mp = ModelLibrary.ForBuilding(bd, team, ck, bd.Width, bd.Height); radius = Mathf.Max(bd.Width, bd.Height) * 0.75f + 0.3f; }
            else if (tech != null) return TechIcon(tech);
            else return null;

            if (_cam == null)
            {
                var go = new GameObject("IconCamera");
                Object.DontDestroyOnLoad(go);
                _cam = go.AddComponent<Camera>();
                _cam.enabled = false; _cam.orthographic = true; _cam.cullingMask = 1 << Layer;
                _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.1f, 0.12f, 0.14f, 1f);
                _cam.nearClipPlane = 0.1f; _cam.farClipPlane = 50f;
                _rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            }
            var root = new GameObject("IconModel"); root.layer = Layer;
            void Add(Mesh m, Vector3 pos)
            {
                if (m == null) return;
                var g = new GameObject("p") { layer = Layer };
                g.transform.SetParent(root.transform, false); g.transform.localPosition = pos;
                g.AddComponent<MeshFilter>().sharedMesh = m;
                var mr = g.AddComponent<MeshRenderer>(); mr.sharedMaterial = Materials.LitNoFogShared;
            }
            foreach (var piece in mp.RestPieces()) Add(piece.Key, piece.Value);
            root.transform.position = new Vector3(-1000, -1000, -1000);
            root.transform.rotation = Quaternion.Euler(0, ud != null ? 200f : 180f, 0);
            float h = mp.Height * 0.5f;
            _cam.transform.rotation = Quaternion.Euler(32f, 45f, 0);
            _cam.transform.position = root.transform.position + Vector3.up * h - _cam.transform.forward * 20f;
            _cam.orthographicSize = radius;
            _cam.targetTexture = _rt;
            var prevFog = Shader.GetGlobalVector("_RTSFogParams");
            Shader.SetGlobalVector("_RTSFogParams", Vector4.zero);
            _cam.Render();
            Shader.SetGlobalVector("_RTSFogParams", prevFog);
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "icon_" + id };
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            _cam.targetTexture = null;
            Object.Destroy(root);
            return tex;
        }

        static Texture2D TechIcon(TechDef t)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var col = t.Faction == FactionId.Alliance ? UIKit.AccentAlliance : UIKit.AccentDominion;
            int h = t.Id.GetHashCode();
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 32, dy = y - 32, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Atan2(dy, dx);
                    float ring = Mathf.Abs(r - 20) < 3 ? 1 : 0;
                    float spokes = r < 18 && Mathf.Abs(Mathf.Sin(a * (3 + (h & 3)))) > 0.85f ? 0.8f : 0;
                    float core = r < 7 ? 1 : 0;
                    var c = new Color(0.1f, 0.12f, 0.14f) + col * Mathf.Max(ring, Mathf.Max(spokes, core)) * 0.9f;
                    c.a = 1;
                    tex.SetPixel(x, y, c);
                }
            tex.Apply();
            return tex;
        }

        public static void Clear() { _icons.Clear(); _pending.Clear(); _queued.Clear(); }
    }
}
