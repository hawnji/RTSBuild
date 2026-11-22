using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTS.Client
{
    /// <summary>
    /// Input is collected from IMGUI events (Event.current) rather than UnityEngine.Input or the Input System package.
    /// IMGUI events are delivered regardless of the project's "Active Input Handling" setting, so the game works in a
    /// fresh Unity 6 project without extra configuration. Events gathered during OnGUI are made available to the next
    /// Update via <see cref="BeginFrame"/>.
    /// </summary>
    public static class InputState
    {
        static readonly HashSet<KeyCode> _held = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> _pendingDown = new HashSet<KeyCode>();
        static readonly HashSet<KeyCode> _down = new HashSet<KeyCode>();
        static readonly bool[] _mouseHeld = new bool[3];
        static readonly bool[] _pendingMouseDown = new bool[3], _pendingMouseUp = new bool[3];
        static readonly bool[] _mouseDown = new bool[3], _mouseUp = new bool[3];
        static readonly int[] _pendingClicks = new int[3];
        static readonly int[] _clicks = new int[3];
        static float _pendingScroll, _scroll;
        static readonly List<Rect> _uiRects = new List<Rect>();
        static readonly List<Rect> _uiRectsBuilding = new List<Rect>();

        /// <summary>Mouse position in GUI coordinates (origin top-left).</summary>
        public static Vector2 MouseGui;
        /// <summary>Mouse position in screen coordinates (origin bottom-left), for camera rays.</summary>
        public static Vector3 MouseScreen => new Vector3(MouseGui.x, Screen.height - MouseGui.y, 0);
        public static bool Shift, Ctrl, Alt;
        public static bool MouseInWindow = true;
        public static float LastInputTime;
        /// <summary>Set by the key-rebinding UI to capture the next key press.</summary>
        public static Action<KeyCode> CaptureNextKey;

        public static bool Held(KeyCode k) => _held.Contains(k);
        public static bool Down(KeyCode k) => _down.Contains(k);
        public static bool MouseHeld(int b) => _mouseHeld[b];
        public static bool MouseDown(int b) => _mouseDown[b];
        public static bool MouseUp(int b) => _mouseUp[b];
        public static int ClickCount(int b) => _clicks[b];
        public static float Scroll => _scroll;
        public static bool AnyKeyDown => _down.Count > 0 || _mouseDown[0] || _mouseDown[1];

        /// <summary>UI panels register their screen rects (GUI coordinates) every frame so world clicks ignore them.</summary>
        public static void BlockRect(Rect r) => _uiRectsBuilding.Add(r);
        public static bool OverUI
        {
            get
            {
                foreach (var r in _uiRects) if (r.Contains(MouseGui)) return true;
                return false;
            }
        }

        /// <summary>Called from the single root OnGUI for every IMGUI event.</summary>
        public static void Collect(Event e)
        {
            Shift = e.shift; Ctrl = e.control || e.command; Alt = e.alt;
            if (e.isMouse || e.type == EventType.Repaint || e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
                MouseGui = e.mousePosition;
            switch (e.type)
            {
                case EventType.KeyDown:
                    if (e.keyCode == KeyCode.None) break;
                    if (CaptureNextKey != null) { var cb = CaptureNextKey; CaptureNextKey = null; cb(e.keyCode); e.Use(); break; }
                    if (!_held.Contains(e.keyCode)) _pendingDown.Add(e.keyCode);
                    _held.Add(e.keyCode);
                    LastInputTime = Time.unscaledTime;
                    break;
                case EventType.KeyUp:
                    _held.Remove(e.keyCode);
                    break;
                case EventType.MouseDown:
                    if (e.button < 3)
                    {
                        _mouseHeld[e.button] = true; _pendingMouseDown[e.button] = true; _pendingClicks[e.button] = e.clickCount;
                        if (CaptureNextKey != null && e.button < 3) { /* mouse buttons are not rebindable */ }
                    }
                    LastInputTime = Time.unscaledTime;
                    break;
                case EventType.MouseUp:
                    if (e.button < 3) { _mouseHeld[e.button] = false; _pendingMouseUp[e.button] = true; }
                    break;
                case EventType.ScrollWheel:
                    _pendingScroll += -e.delta.y;
                    break;
            }
        }

        /// <summary>Promotes events collected during the previous OnGUI pass. Call at the start of Update.</summary>
        public static void BeginFrame()
        {
            _down.Clear();
            foreach (var k in _pendingDown) _down.Add(k);
            _pendingDown.Clear();
            for (int i = 0; i < 3; i++)
            {
                _mouseDown[i] = _pendingMouseDown[i]; _mouseUp[i] = _pendingMouseUp[i]; _clicks[i] = _pendingClicks[i];
                _pendingMouseDown[i] = false; _pendingMouseUp[i] = false; _pendingClicks[i] = 0;
            }
            _scroll = _pendingScroll; _pendingScroll = 0;
            _uiRects.Clear(); _uiRects.AddRange(_uiRectsBuilding); _uiRectsBuilding.Clear();
        }

        /// <summary>Consumes a pending click so other handlers do not act on it.</summary>
        public static void EatMouse(int b) { _mouseDown[b] = false; _mouseUp[b] = false; }
        public static void EatKey(KeyCode k) => _down.Remove(k);

        public static void ReleaseAll()
        {
            _held.Clear();
            for (int i = 0; i < 3; i++) _mouseHeld[i] = false;
        }
    }

    public enum GameAction
    {
        CameraUp, CameraDown, CameraLeft, CameraRight, ZoomIn, ZoomOut, RotateLeft, RotateRight, CameraReset,
        Stop, Guard, Deploy, AttackMove, Patrol, Scatter, CycleStance, SelectSameType, SelectAll, SelectAllCombat,
        GoToBase, JumpToAlert, RepairMode, SellMode, RotateStructure, Pause, QuickSave, QuickLoad, ToggleObjectives,
        SpeedUp, SpeedDown, ToggleHealthBars, Screenshot
    }

    [Serializable]
    public class KeyBinding
    {
        public GameAction Action;
        public KeyCode Key;
        public bool Ctrl;
    }

    /// <summary>Rebindable keyboard layout. Control groups always use Ctrl+digit / digit.</summary>
    [Serializable]
    public class Keybinds
    {
        public List<KeyBinding> Bindings = new List<KeyBinding>();

        public static Keybinds Default()
        {
            var k = new Keybinds();
            void B(GameAction a, KeyCode key, bool ctrl = false) => k.Bindings.Add(new KeyBinding { Action = a, Key = key, Ctrl = ctrl });
            B(GameAction.CameraUp, KeyCode.UpArrow); B(GameAction.CameraDown, KeyCode.DownArrow);
            B(GameAction.CameraLeft, KeyCode.LeftArrow); B(GameAction.CameraRight, KeyCode.RightArrow);
            B(GameAction.ZoomIn, KeyCode.PageUp); B(GameAction.ZoomOut, KeyCode.PageDown);
            B(GameAction.RotateLeft, KeyCode.Insert); B(GameAction.RotateRight, KeyCode.Delete);
            B(GameAction.CameraReset, KeyCode.Home);
            B(GameAction.Stop, KeyCode.S); B(GameAction.Guard, KeyCode.G); B(GameAction.Deploy, KeyCode.D);
            B(GameAction.AttackMove, KeyCode.A); B(GameAction.Patrol, KeyCode.P); B(GameAction.Scatter, KeyCode.X);
            B(GameAction.CycleStance, KeyCode.T); B(GameAction.SelectSameType, KeyCode.W);
            B(GameAction.SelectAll, KeyCode.A, true); B(GameAction.SelectAllCombat, KeyCode.Q);
            B(GameAction.GoToBase, KeyCode.H); B(GameAction.JumpToAlert, KeyCode.Space);
            B(GameAction.RepairMode, KeyCode.R); B(GameAction.SellMode, KeyCode.Z); B(GameAction.RotateStructure, KeyCode.Tab);
            B(GameAction.Pause, KeyCode.Escape); B(GameAction.QuickSave, KeyCode.F6); B(GameAction.QuickLoad, KeyCode.F9);
            B(GameAction.ToggleObjectives, KeyCode.O); B(GameAction.SpeedUp, KeyCode.Equals); B(GameAction.SpeedDown, KeyCode.Minus);
            B(GameAction.ToggleHealthBars, KeyCode.B); B(GameAction.Screenshot, KeyCode.F12);
            return k;
        }

        public KeyBinding Get(GameAction a)
        {
            foreach (var b in Bindings) if (b.Action == a) return b;
            var d = Default().Bindings.Find(x => x.Action == a);
            if (d != null) Bindings.Add(d);
            return d;
        }

        public bool Pressed(GameAction a)
        {
            var b = Get(a);
            if (b == null || b.Key == KeyCode.None) return false;
            return InputState.Down(b.Key) && (b.Ctrl == InputState.Ctrl);
        }

        public bool Held(GameAction a)
        {
            var b = Get(a);
            if (b == null || b.Key == KeyCode.None) return false;
            return InputState.Held(b.Key);
        }

        public static string Pretty(GameAction a)
        {
            var s = a.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++) { if (i > 0 && char.IsUpper(s[i])) sb.Append(' '); sb.Append(s[i]); }
            return sb.ToString();
        }

        public static string KeyName(KeyBinding b) => b == null ? "-" : (b.Ctrl ? "Ctrl+" : "") + b.Key;
    }
}
