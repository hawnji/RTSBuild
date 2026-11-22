using System;
using System.Collections.Generic;
using UnityEngine;

namespace RTS.Client
{
    /// <summary>
    /// Procedurally synthesizes every sound effect and music loop at runtime (no audio files are shipped).
    /// Weapons use a parameterised "shot" model (noise burst + pitched body + sub thump), explosions a filtered noise
    /// boom, UI a set of short tones, and radio messages a squelch + chirp pattern accompanied by subtitles.
    /// </summary>
    public static class SynthLibrary
    {
        public const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        static System.Random _rng = new System.Random(1234);

        static float Rand() => (float)(_rng.NextDouble() * 2 - 1);

        public static AudioClip Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_clips.TryGetValue(key, out var c)) return c;
            c = Build(key);
            if (c != null) _clips[key] = c;
            return c;
        }

        static AudioClip Make(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            return c;
        }

        struct Shot
        {
            public float Len, Noise, NoiseDecay, Tone, ToneEnd, ToneDecay, ToneAmp, Sub, SubDecay, Lowpass, Crackle;
        }

        static AudioClip Build(string key)
        {
            switch (key)
            {
                // ---- small arms
                case "rifle": return ShotClip(key, new Shot { Len = 0.18f, Noise = 0.8f, NoiseDecay = 40, Tone = 900, ToneEnd = 300, ToneDecay = 50, ToneAmp = 0.3f, Sub = 0.3f, SubDecay = 35, Lowpass = 0.55f });
                case "pistol": return ShotClip(key, new Shot { Len = 0.12f, Noise = 0.6f, NoiseDecay = 55, Tone = 1200, ToneEnd = 500, ToneDecay = 60, ToneAmp = 0.25f, Sub = 0.1f, SubDecay = 50, Lowpass = 0.6f });
                case "smg":
                case "mg": return ShotClip(key, new Shot { Len = 0.1f, Noise = 0.7f, NoiseDecay = 60, Tone = 800, ToneEnd = 350, ToneDecay = 70, ToneAmp = 0.25f, Sub = 0.25f, SubDecay = 60, Lowpass = 0.5f });
                case "mg_heavy":
                case "minigun": return ShotClip(key, new Shot { Len = 0.13f, Noise = 0.8f, NoiseDecay = 45, Tone = 500, ToneEnd = 200, ToneDecay = 50, ToneAmp = 0.35f, Sub = 0.4f, SubDecay = 40, Lowpass = 0.4f });
                case "sniper": return ShotClip(key, new Shot { Len = 0.45f, Noise = 0.9f, NoiseDecay = 18, Tone = 1500, ToneEnd = 200, ToneDecay = 20, ToneAmp = 0.35f, Sub = 0.4f, SubDecay = 18, Lowpass = 0.7f, Crackle = 0.2f });
                case "shotgun": return ShotClip(key, new Shot { Len = 0.3f, Noise = 1f, NoiseDecay = 22, Tone = 300, ToneEnd = 120, ToneDecay = 25, ToneAmp = 0.3f, Sub = 0.5f, SubDecay = 25, Lowpass = 0.35f });
                case "autocannon":
                case "flak": return ShotClip(key, new Shot { Len = 0.25f, Noise = 0.8f, NoiseDecay = 25, Tone = 350, ToneEnd = 120, ToneDecay = 28, ToneAmp = 0.45f, Sub = 0.6f, SubDecay = 22, Lowpass = 0.35f, Crackle = key == "flak" ? 0.3f : 0 });
                // ---- cannons
                case "cannon": return ShotClip(key, new Shot { Len = 0.55f, Noise = 0.9f, NoiseDecay = 12, Tone = 160, ToneEnd = 55, ToneDecay = 10, ToneAmp = 0.6f, Sub = 0.9f, SubDecay = 9, Lowpass = 0.22f });
                case "cannon_heavy":
                case "naval_gun": return ShotClip(key, new Shot { Len = 0.75f, Noise = 1f, NoiseDecay = 9, Tone = 120, ToneEnd = 40, ToneDecay = 7, ToneAmp = 0.7f, Sub = 1f, SubDecay = 6, Lowpass = 0.18f });
                case "cannon_huge":
                case "naval_huge":
                case "artillery_heavy":
                case "mortar_huge": return ShotClip(key, new Shot { Len = 1.1f, Noise = 1f, NoiseDecay = 6, Tone = 90, ToneEnd = 30, ToneDecay = 5, ToneAmp = 0.8f, Sub = 1f, SubDecay = 4, Lowpass = 0.12f, Crackle = 0.15f });
                case "artillery": return ShotClip(key, new Shot { Len = 0.8f, Noise = 1f, NoiseDecay = 8, Tone = 110, ToneEnd = 40, ToneDecay = 6, ToneAmp = 0.7f, Sub = 1f, SubDecay = 5, Lowpass = 0.15f });
                case "depthcharge": return ShotClip(key, new Shot { Len = 0.9f, Noise = 0.5f, NoiseDecay = 5, Tone = 60, ToneEnd = 30, ToneDecay = 4, ToneAmp = 0.8f, Sub = 1f, SubDecay = 4, Lowpass = 0.08f });
                // ---- rockets / missiles: whoosh
                case "rocket":
                case "missile_aa":
                case "rocket_salvo":
                case "cruise_missile":
                case "torpedo": return Whoosh(key, key == "cruise_missile" ? 1.4f : key == "torpedo" ? 0.9f : 0.6f, key == "torpedo" ? 0.08f : 0.3f);
                case "bomb": return Whoosh(key, 0.9f, 0.15f, true);
                // ---- energy
                case "railgun":
                case "railgun_heavy": return Zap(key, key == "railgun_heavy" ? 0.6f : 0.4f, 2400, 300, 0.3f);
                case "arc": return Zap(key, 0.45f, 600, 1800, 0.9f);
                case "thermal": return Zap(key, 0.6f, 200, 120, 0.6f);
                case "flame": return Flame(key);
                // ---- explosions
                case "explosion_small": return Boom(key, 0.6f, 0.35f);
                case "explosion_medium": return Boom(key, 1.1f, 0.22f);
                case "explosion_large": return Boom(key, 2.0f, 0.12f);
                case "explosion_huge": return Boom(key, 3.5f, 0.06f);
                case "impact": return ShotClip(key, new Shot { Len = 0.12f, Noise = 0.5f, NoiseDecay = 50, Tone = 2000, ToneEnd = 800, ToneDecay = 60, ToneAmp = 0.1f, Lowpass = 0.7f });
                case "splash": return Boom(key, 0.8f, 0.5f);
                case "crush": return ShotClip(key, new Shot { Len = 0.25f, Noise = 0.6f, NoiseDecay = 20, Tone = 200, ToneEnd = 80, ToneDecay = 20, ToneAmp = 0.4f, Sub = 0.2f, SubDecay = 20, Lowpass = 0.3f, Crackle = 0.5f });
                case "building_collapse": return Boom(key, 2.6f, 0.08f, 0.6f);
                // ---- UI / notifications
                case "click": return Tone(key, 0.04f, new[] { 1800f }, 0.25f, 0);
                case "click_soft": return Tone(key, 0.05f, new[] { 1100f }, 0.18f, 0);
                case "build_start": return Tone(key, 0.18f, new[] { 660f, 880f }, 0.25f, 1);
                case "denied": return Tone(key, 0.25f, new[] { 220f, 180f }, 0.3f, 2);
                case "ready": return Tone(key, 0.35f, new[] { 523f, 659f, 784f }, 0.28f, 1);
                case "place": return ShotClip(key, new Shot { Len = 0.4f, Noise = 0.4f, NoiseDecay = 12, Tone = 140, ToneEnd = 70, ToneDecay = 10, ToneAmp = 0.6f, Sub = 0.6f, SubDecay = 10, Lowpass = 0.2f });
                case "sell": return Tone(key, 0.3f, new[] { 988f, 784f, 659f }, 0.25f, 1);
                case "alarm": return Siren(key);
                case "power_down": return Sweep(key, 0.9f, 600, 90, 0.3f);
                case "power_up": return Sweep(key, 0.7f, 120, 700, 0.3f);
                case "promote": return Tone(key, 0.5f, new[] { 784f, 988f, 1175f, 1568f }, 0.22f, 1);
                case "radio": return Radio(key, 0.6f);
                case "radio_long": return Radio(key, 1.1f);
                case "ack": return Radio(key, 0.3f);
                case "superweapon_charge": return Sweep(key, 2.5f, 80, 900, 0.35f);
                case "lance": return Zap(key, 2.2f, 3000, 80, 0.6f);
                case "emp": return Sweep(key, 1.5f, 1500, 40, 0.5f);
                case "money": return Tone(key, 0.06f, new[] { 2093f }, 0.12f, 0);
                case "heal": return Tone(key, 0.25f, new[] { 880f, 1320f }, 0.12f, 1);
                case "rotor": return Rotor(key);
                case "engine": return Engine(key);
            }
            return null;
        }

        static float[] Buf(float seconds) => new float[Mathf.Max(1, (int)(seconds * Rate))];

        static AudioClip ShotClip(string key, Shot s)
        {
            var d = Buf(s.Len);
            float lp = 0, ph = 0, sph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float n = Rand();
                lp += (n - lp) * s.Lowpass;
                float noise = lp * s.Noise * Mathf.Exp(-t * s.NoiseDecay);
                float f = Mathf.Lerp(s.Tone, s.ToneEnd, Mathf.Clamp01(t / s.Len * 2f));
                ph += f / Rate * Mathf.PI * 2;
                float tone = Mathf.Sin(ph) * s.ToneAmp * Mathf.Exp(-t * s.ToneDecay);
                sph += Mathf.Lerp(70f, 35f, t / s.Len) / Rate * Mathf.PI * 2;
                float sub = Mathf.Sin(sph) * s.Sub * Mathf.Exp(-t * Mathf.Max(1, s.SubDecay));
                float cr = s.Crackle > 0 && _rng.NextDouble() < 0.004 ? Rand() * s.Crackle : 0;
                float attack = Mathf.Clamp01(t * 800f);
                d[i] = Mathf.Clamp((noise + tone + sub + cr) * attack * 0.8f, -1, 1);
            }
            return Make(key, d);
        }

        static AudioClip Boom(string key, float len, float lowpass, float rumble = 0.3f)
        {
            var d = Buf(len);
            float lp = 0, lp2 = 0, ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Rand() - lp) * lowpass;
                lp2 += (lp - lp2) * lowpass;
                float env = Mathf.Exp(-t * 3.5f / len) * Mathf.Clamp01(t * 300f);
                ph += Mathf.Lerp(55f, 25f, t / len) / Rate * Mathf.PI * 2;
                float crackle = _rng.NextDouble() < 0.002 * (1 - t / len) ? Rand() * 0.5f : 0;
                d[i] = Mathf.Clamp((lp2 * 2.2f + Mathf.Sin(ph) * rumble * Mathf.Exp(-t * 2f / len) + crackle) * env, -1, 1);
            }
            return Make(key, d);
        }

        static AudioClip Whoosh(string key, float len, float lowpass, bool falling = false)
        {
            var d = Buf(len);
            float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, u = t / len;
                float lpk = falling ? Mathf.Lerp(0.05f, lowpass, u) : Mathf.Lerp(lowpass, 0.04f, u);
                lp += (Rand() - lp) * lpk;
                float env = falling ? Mathf.Sin(u * Mathf.PI) : Mathf.Clamp01(t * 60f) * Mathf.Exp(-u * 3f);
                float pop = !falling && t < 0.05f ? Rand() * (1 - t / 0.05f) * 0.6f : 0;
                d[i] = Mathf.Clamp(lp * 1.8f * env + pop, -1, 1);
            }
            return Make(key, d);
        }

        static AudioClip Zap(string key, float len, float f0, float f1, float buzz)
        {
            var d = Buf(len);
            float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, u = t / len;
                float f = Mathf.Lerp(f0, f1, Mathf.Sqrt(u));
                ph += f / Rate * Mathf.PI * 2;
                float sq = Mathf.Sign(Mathf.Sin(ph * 0.5f)) * buzz * 0.4f;
                float env = Mathf.Clamp01(t * 500f) * Mathf.Exp(-u * 4f);
                d[i] = Mathf.Clamp((Mathf.Sin(ph) * 0.6f + sq + Rand() * 0.15f) * env * 0.8f, -1, 1);
            }
            return Make(key, d);
        }

        static AudioClip Flame(string key)
        {
            var d = Buf(0.5f); float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Rand() - lp) * 0.2f;
                float env = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((0.5f - t) * 8f);
                d[i] = lp * 1.6f * env * (0.8f + 0.2f * Mathf.Sin(t * 70f));
            }
            return Make(key, d);
        }

        static AudioClip Tone(string key, float len, float[] notes, float amp, int shape)
        {
            float each = len / notes.Length;
            var d = Buf(len);
            float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                int n = Mathf.Min(notes.Length - 1, (int)(t / each));
                float lt = t - n * each;
                ph += notes[n] / Rate * Mathf.PI * 2;
                float w = shape == 2 ? Mathf.Sign(Mathf.Sin(ph)) * 0.5f : Mathf.Sin(ph) + (shape == 1 ? 0.3f * Mathf.Sin(ph * 2) : 0);
                float env = Mathf.Clamp01(lt * 400f) * Mathf.Exp(-lt / each * 2.5f);
                d[i] = w * env * amp;
            }
            return Make(key, d);
        }

        static AudioClip Sweep(string key, float len, float f0, float f1, float amp)
        {
            var d = Buf(len); float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate, u = t / len;
                ph += Mathf.Lerp(f0, f1, u) / Rate * Mathf.PI * 2;
                float env = Mathf.Clamp01(t * 50f) * Mathf.Clamp01((len - t) * 6f);
                d[i] = (Mathf.Sin(ph) + 0.3f * Mathf.Sin(ph * 1.5f)) * env * amp;
            }
            return Make(key, d);
        }

        static AudioClip Siren(string key)
        {
            var d = Buf(1.6f); float ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 600 + 300 * Mathf.Sin(t * Mathf.PI * 2 * 1.25f);
                ph += f / Rate * Mathf.PI * 2;
                float env = Mathf.Clamp01(t * 20f) * Mathf.Clamp01((1.6f - t) * 5f);
                d[i] = (Mathf.Sin(ph) * 0.6f + Mathf.Sign(Mathf.Sin(ph)) * 0.1f) * env * 0.35f;
            }
            return Make(key, d);
        }

        /// <summary>Radio squelch and a short chirp pattern used under subtitled voice lines.</summary>
        static AudioClip Radio(string key, float len)
        {
            var d = Buf(len); float lp = 0, ph = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Rand() - lp) * 0.6f;
                float squelch = (t < 0.06f || t > len - 0.08f) ? lp * 0.35f : 0;
                // "voice-like" band-limited warble
                float syll = Mathf.Max(0, Mathf.Sin(t * 22f)) * Mathf.Clamp01((t - 0.07f) * 30f) * Mathf.Clamp01((len - 0.1f - t) * 30f);
                ph += (380 + 120 * Mathf.Sin(t * 13f) + 60 * Mathf.Sin(t * 41f)) / Rate * Mathf.PI * 2;
                float v = (Mathf.Sin(ph) * 0.5f + Mathf.Sin(ph * 2.01f) * 0.25f + Mathf.Sin(ph * 3.02f) * 0.12f) * syll * 0.25f;
                d[i] = Mathf.Clamp(squelch + v + lp * 0.03f, -1, 1);
            }
            return Make(key, d);
        }

        static AudioClip Rotor(string key)
        {
            var d = Buf(1.0f); float lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Rand() - lp) * 0.15f;
                d[i] = lp * (0.5f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * Mathf.PI * 12f)), 4)) * 0.6f;
            }
            return Make(key, d);
        }

        static AudioClip Engine(string key)
        {
            var d = Buf(1.0f); float ph = 0, lp = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                ph += 45f / Rate * Mathf.PI * 2;
                lp += (Rand() - lp) * 0.08f;
                d[i] = (Mathf.Sign(Mathf.Sin(ph)) * 0.15f + lp * 0.4f) * 0.5f;
            }
            return Make(key, d);
        }

        // =============================================================================================
        // Music: a small procedural composer. Each theme picks tempo, key, progression and instrument mix,
        // then renders a seamless loop.
        // =============================================================================================
        public static AudioClip Music(string theme)
        {
            string key = "music_" + theme;
            if (_clips.TryGetValue(key, out var c)) return c;
            c = Make(key, ComposeMusic(theme));
            _clips[key] = c;
            return c;
        }

        static float[] ComposeMusic(string theme)
        {
            int seed; float bpm; int root; int[] prog; float drums, pad, lead, bass; bool major = false;
            switch (theme)
            {
                case "menu": seed = 11; bpm = 92; root = 45; prog = new[] { 0, 5, 3, 4 }; drums = 0.5f; pad = 1f; lead = 0.6f; bass = 0.8f; break;
                case "alliance": seed = 21; bpm = 120; root = 50; prog = new[] { 0, 3, 5, 4 }; drums = 0.9f; pad = 0.7f; lead = 0.8f; bass = 1f; major = true; break;
                case "dominion": seed = 31; bpm = 112; root = 40; prog = new[] { 0, 0, 5, 6 }; drums = 1f; pad = 0.6f; lead = 0.6f; bass = 1.1f; break;
                case "tension": seed = 41; bpm = 84; root = 42; prog = new[] { 0, 1, 0, 6 }; drums = 0.45f; pad = 1f; lead = 0.35f; bass = 0.7f; break;
                case "naval": seed = 51; bpm = 104; root = 47; prog = new[] { 0, 5, 6, 4 }; drums = 0.7f; pad = 0.9f; lead = 0.7f; bass = 0.9f; break;
                case "finale": seed = 61; bpm = 132; root = 43; prog = new[] { 0, 6, 5, 4 }; drums = 1.1f; pad = 0.8f; lead = 0.9f; bass = 1.1f; break;
                case "victory": seed = 71; bpm = 100; root = 48; prog = new[] { 0, 4, 5, 3 }; drums = 0.6f; pad = 1f; lead = 0.9f; bass = 0.8f; major = true; break;
                case "defeat": seed = 81; bpm = 70; root = 41; prog = new[] { 0, 5, 3, 4 }; drums = 0.2f; pad = 1f; lead = 0.4f; bass = 0.6f; break;
                default: seed = 91; bpm = 124; root = 45; prog = new[] { 0, 5, 6, 4 }; drums = 1f; pad = 0.6f; lead = 0.75f; bass = 1f; break;
            }
            var rng = new System.Random(seed);
            int[] scale = major ? new[] { 0, 2, 4, 5, 7, 9, 11 } : new[] { 0, 2, 3, 5, 7, 8, 10 };
            float beat = 60f / bpm;
            int bars = 8, beatsPerBar = 4;
            int total = (int)(bars * beatsPerBar * beat * Rate);
            var d = new float[total];
            float Freq(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);
            int Degree(int chordRoot, int deg) { int idx = chordRoot + deg; int oct = Mathf.FloorToInt(idx / 7f); idx = ((idx % 7) + 7) % 7; return scale[idx] + oct * 12; }

            // melodic motif (16 steps) re-used with variation
            var motif = new int[16];
            for (int i = 0; i < 16; i++) motif[i] = rng.NextDouble() < 0.3 ? -99 : rng.Next(0, 5);

            for (int bar = 0; bar < bars; bar++)
            {
                int chord = prog[bar % prog.Length];
                float barStart = bar * beatsPerBar * beat;
                // pad: triad, whole bar
                for (int v = 0; v < 3; v++)
                {
                    float f = Freq(root + 12 + Degree(chord, v * 2));
                    AddNote(d, barStart, beatsPerBar * beat, f, pad * 0.06f, 2, 0.3f, 0.6f);
                    AddNote(d, barStart, beatsPerBar * beat, f * 1.005f, pad * 0.05f, 2, 0.3f, 0.6f);
                }
                // bass: eighth-note ostinato
                for (int s = 0; s < 8; s++)
                {
                    int deg = (s % 4 == 3) ? 4 : 0;
                    if (theme == "dominion" && s % 2 == 1) deg = 0;
                    float f = Freq(root - 12 + Degree(chord, deg));
                    AddNote(d, barStart + s * beat * 0.5f, beat * 0.45f, f, bass * 0.16f, 1, 0.005f, 0.08f);
                }
                // lead: motif from bar 2 on, variation every other bar
                if (bar >= 2 && lead > 0)
                    for (int s = 0; s < 8; s++)
                    {
                        int m = motif[(s + (bar % 2) * 8) % 16];
                        if (m == -99) continue;
                        float f = Freq(root + 24 + Degree(chord, m));
                        AddNote(d, barStart + s * beat * 0.5f, beat * 0.42f, f, lead * 0.07f, 3, 0.01f, 0.15f);
                    }
                // drums
                if (drums > 0)
                    for (int s = 0; s < 16; s++)
                    {
                        float t = barStart + s * beat * 0.25f;
                        if (s % 4 == 0 && (bar >= 1 || theme != "tension")) Kick(d, t, drums * 0.5f);
                        if (s % 8 == 4) Snare(d, t, drums * (theme == "tension" ? 0.12f : 0.25f));
                        if (s % 2 == 0) Hat(d, t, drums * 0.05f);
                        if (theme == "finale" || theme == "dominion") if (s == 14 || s == 15) Snare(d, t, drums * 0.12f);
                    }
            }
            // soft limiter + loop crossfade of the tail into the head
            int fade = Rate / 10;
            for (int i = 0; i < fade; i++) { float k = i / (float)fade; d[i] = d[i] * k + d[total - fade + i] * (1 - k); }
            for (int i = 0; i < total; i++) d[i] = (float)Math.Tanh(d[i] * 1.2f) * 0.85f;
            return d;
        }

        static void AddNote(float[] d, float start, float len, float f, float amp, int wave, float attack, float release)
        {
            int s0 = (int)(start * Rate), n = (int)((len + release) * Rate);
            float ph = 0; float lp = 0;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                ph += f / Rate;
                float x = ph - Mathf.Floor(ph);
                float w = wave == 1 ? (x * 2 - 1) : wave == 2 ? (x * 2 - 1) : wave == 3 ? (x < 0.5f ? 1 : -1) * 0.6f + Mathf.Sin(ph * Mathf.PI * 2) * 0.4f : Mathf.Sin(ph * Mathf.PI * 2);
                lp += (w - lp) * (wave == 2 ? 0.08f : wave == 1 ? 0.2f : 0.35f);
                float env = Mathf.Clamp01(t / Mathf.Max(0.001f, attack)) * (t > len ? Mathf.Clamp01(1 - (t - len) / Mathf.Max(0.001f, release)) : 1f);
                d[s0 + i] += lp * env * amp;
            }
        }

        static void Kick(float[] d, float start, float amp)
        {
            int s0 = (int)(start * Rate), n = Rate / 4; float ph = 0;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                ph += Mathf.Lerp(120f, 40f, Mathf.Clamp01(t * 12f)) / Rate * Mathf.PI * 2;
                d[s0 + i] += Mathf.Sin(ph) * Mathf.Exp(-t * 14f) * amp;
            }
        }

        static void Snare(float[] d, float start, float amp)
        {
            int s0 = (int)(start * Rate), n = Rate / 5; float lp = 0;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (Rand() - lp) * 0.5f;
                d[s0 + i] += (lp + Mathf.Sin(t * 1100f) * 0.3f) * Mathf.Exp(-t * 18f) * amp;
            }
        }

        static void Hat(float[] d, float start, float amp)
        {
            int s0 = (int)(start * Rate), n = Rate / 25; float prev = 0;
            for (int i = 0; i < n && s0 + i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float r = Rand(); float hp = r - prev; prev = r;
                d[s0 + i] += hp * Mathf.Exp(-t * 60f) * amp;
            }
        }
    }
}
