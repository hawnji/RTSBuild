using System.Collections.Generic;
using UnityEngine;

namespace RTS.Client
{
    public enum AudioBus { Music, Sfx, Voice, Ui }

    /// <summary>
    /// Mixer with four buses (music, effects, voice, interface) under a master volume, a pool of positional sources
    /// with priority-based voice stealing, per-sound concurrency limits and crossfading music.
    /// Unity's AudioMixer assets cannot be authored from code, so bus gain is applied per source.
    /// </summary>
    public static class AudioManager
    {
        class Voice
        {
            public AudioSource Src;
            public AudioBus Bus;
            public float BaseVolume;
            public int Priority;
            public float StartTime;
            public string Key;
        }

        static GameObject _root;
        static readonly List<Voice> _pool = new List<Voice>();
        static AudioSource _musicA, _musicB;
        static bool _musicOnA = true;
        static float _musicFade = 1f;
        static string _musicTheme;
        static Transform _listener;
        static readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        static readonly Dictionary<string, int> _concurrent = new Dictionary<string, int>();
        const int PoolSize = 32;

        public static void Init()
        {
            if (_root != null) return;
            _root = new GameObject("Audio");
            Object.DontDestroyOnLoad(_root);
            var lgo = new GameObject("Listener");
            lgo.transform.SetParent(_root.transform);
            lgo.AddComponent<AudioListener>();
            _listener = lgo.transform;
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Voice" + i);
                go.transform.SetParent(_root.transform);
                var s = go.AddComponent<AudioSource>();
                s.playOnAwake = false; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 6f; s.maxDistance = 45f; s.dopplerLevel = 0;
                _pool.Add(new Voice { Src = s });
            }
            _musicA = MakeMusic("MusicA"); _musicB = MakeMusic("MusicB");
        }

        static AudioSource MakeMusic(string n)
        {
            var go = new GameObject(n); go.transform.SetParent(_root.transform);
            var s = go.AddComponent<AudioSource>(); s.loop = true; s.playOnAwake = false; s.spatialBlend = 0; s.priority = 0;
            return s;
        }

        static float BusGain(AudioBus b)
        {
            var s = ClientSettings.Current;
            float g = b == AudioBus.Music ? s.MusicVolume : b == AudioBus.Sfx ? s.SfxVolume : b == AudioBus.Voice ? s.VoiceVolume : s.UiVolume;
            return g * s.MasterVolume;
        }

        public static void ApplyVolumes()
        {
            if (_root == null) return;
            foreach (var v in _pool) if (v.Src.isPlaying) v.Src.volume = v.BaseVolume * BusGain(v.Bus);
            UpdateMusicVolumes();
        }

        /// <summary>Moves the listener to the camera focus so positional audio matches what is on screen.</summary>
        public static void SetListener(Vector3 pos, float zoom)
        {
            if (_listener == null) return;
            _listener.position = pos + Vector3.up * (4f + zoom * 0.6f);
        }

        /// <summary>Plays a 2D (non-positional) sound on the given bus.</summary>
        public static void Play2D(string key, AudioBus bus = AudioBus.Ui, float volume = 1f, float pitch = 1f, int priority = 5)
            => PlayInternal(key, null, bus, volume, pitch, priority);

        /// <summary>Plays a positional sound effect.</summary>
        public static void PlayAt(string key, Vector3 pos, float volume = 1f, float pitch = 1f, int priority = 3)
            => PlayInternal(key, pos, AudioBus.Sfx, volume, pitch, priority);

        static void PlayInternal(string key, Vector3? pos, AudioBus bus, float volume, float pitch, int priority)
        {
            if (_root == null || string.IsNullOrEmpty(key)) return;
            float now = Time.unscaledTime;
            // rate-limit identical sounds (machine-gun spam, mass explosions)
            float minGap = bus == AudioBus.Sfx ? 0.045f : 0.02f;
            if (_lastPlayed.TryGetValue(key, out var last) && now - last < minGap) return;
            int maxSame = bus == AudioBus.Sfx ? 5 : 2;
            if (CountPlaying(key) >= maxSame) return;
            if (pos.HasValue && _listener != null)
            {
                float dist = Vector3.Distance(pos.Value, _listener.position);
                if (dist > 55f) return;
            }
            var clip = SynthLibrary.Get(key);
            if (clip == null) return;
            var v = Acquire(priority);
            if (v == null) return;
            _lastPlayed[key] = now;
            v.Key = key; v.Bus = bus; v.BaseVolume = volume; v.Priority = priority; v.StartTime = now;
            var s = v.Src;
            s.clip = clip;
            s.pitch = pitch * Random.Range(0.94f, 1.06f);
            s.volume = volume * BusGain(bus);
            if (pos.HasValue) { s.spatialBlend = 0.85f; s.transform.position = pos.Value; }
            else { s.spatialBlend = 0f; s.transform.position = _listener != null ? _listener.position : Vector3.zero; }
            s.Play();
        }

        static int CountPlaying(string key)
        {
            int n = 0;
            foreach (var v in _pool) if (v.Key == key && v.Src.isPlaying) n++;
            return n;
        }

        static Voice Acquire(int priority)
        {
            Voice best = null;
            foreach (var v in _pool) if (!v.Src.isPlaying) return v;
            // steal the lowest-priority, oldest voice whose priority does not exceed the request
            foreach (var v in _pool)
            {
                if (v.Priority > priority) continue;
                if (best == null || v.Priority < best.Priority || (v.Priority == best.Priority && v.StartTime < best.StartTime)) best = v;
            }
            if (best != null) best.Src.Stop();
            return best;
        }

        // ---------------------------------------------------------------- music
        public static void PlayMusic(string theme)
        {
            if (_root == null || theme == _musicTheme) return;
            _musicTheme = theme;
            var clip = SynthLibrary.Music(theme);
            var next = _musicOnA ? _musicB : _musicA;
            next.clip = clip; next.volume = 0; next.Play();
            _musicOnA = !_musicOnA;
            _musicFade = 0f;
        }

        public static void StopMusic() { _musicTheme = null; if (_musicA != null) { _musicA.Stop(); _musicB.Stop(); } }

        public static void Update(float dt)
        {
            if (_root == null) return;
            if (_musicFade < 1f) { _musicFade = Mathf.Min(1f, _musicFade + dt / 2.5f); UpdateMusicVolumes(); }
        }

        static void UpdateMusicVolumes()
        {
            if (_musicA == null) return;
            float g = BusGain(AudioBus.Music) * 0.7f;
            var cur = _musicOnA ? _musicA : _musicB; var old = _musicOnA ? _musicB : _musicA;
            cur.volume = g * _musicFade; old.volume = g * (1 - _musicFade);
            if (_musicFade >= 1f && old.isPlaying) old.Stop();
        }

        public static void StopAllSfx() { foreach (var v in _pool) v.Src.Stop(); }
    }
}
