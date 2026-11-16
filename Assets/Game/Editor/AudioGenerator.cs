using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Gloomdelve.EditorTools
{
    /// <summary>Synthesises all sound effects and music loops as WAV files (original, generated from code).</summary>
    public static class AudioGenerator
    {
        public const string Root = "Assets/Game/Audio/";
        const int SR = 22050;

        public static readonly List<string> SfxIds = new List<string>();
        public static readonly List<string> MusicIds = new List<string> { "menu", "area1", "area2", "area3", "final" };

        // ------------------------------------------------------------------ helpers

        static uint noiseState = 22222;
        static float Noise() { noiseState ^= noiseState << 13; noiseState ^= noiseState >> 17; noiseState ^= noiseState << 5; return (noiseState / (float)uint.MaxValue) * 2f - 1f; }
        static float Sq(float ph, float duty = 0.5f) => (ph - Mathf.Floor(ph)) < duty ? 1f : -1f;
        static float Tri(float ph) { float f = ph - Mathf.Floor(ph); return f < 0.5f ? f * 4f - 1f : 3f - f * 4f; }
        static float Saw(float ph) => (ph - Mathf.Floor(ph)) * 2f - 1f;
        static float Sin(float ph) => Mathf.Sin(ph * Mathf.PI * 2f);
        static float NoteHz(int semisFromA4) => 440f * Mathf.Pow(2f, semisFromA4 / 12f);

        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * SR)];

        delegate float Osc(float ph);

        /// <summary>Tone with exponential pitch sweep and attack/decay envelope.</summary>
        static void Tone(float[] b, float start, float dur, float f0, float f1, Osc osc, float vol, float attack = 0.005f, float vibrato = 0f, float vibHz = 6f)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            float ph = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)n;
                float f = f0 * Mathf.Pow(f1 / f0, t);
                if (vibrato > 0f) f *= 1f + vibrato * Mathf.Sin(i / (float)SR * vibHz * Mathf.PI * 2f);
                ph += f / SR;
                float env = Mathf.Min(1f, i / (attack * SR + 1f)) * (1f - t) * (1f - t * 0.3f);
                b[s0 + i] += osc(ph) * vol * env;
            }
        }

        /// <summary>Noise burst with a one-pole low-pass (cut 0..1, lower = darker) and decay.</summary>
        static void NoiseBurst(float[] b, float start, float dur, float vol, float cut0, float cut1, float decayPow = 1.5f)
        {
            int s0 = (int)(start * SR), n = (int)(dur * SR);
            float y = 0f;
            for (int i = 0; i < n && s0 + i < b.Length; i++)
            {
                float t = i / (float)n;
                float cut = Mathf.Lerp(cut0, cut1, t);
                y += (Noise() - y) * cut;
                b[s0 + i] += y * vol * Mathf.Pow(1f - t, decayPow) * Mathf.Min(1f, i / 40f);
            }
        }

        static void WriteWav(string rel, float[] data, float gain = 1f)
        {
            string path = Path.Combine(Directory.GetCurrentDirectory(), Root + rel + ".wav");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            float peak = 0.0001f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            float norm = Mathf.Min(1f, 0.9f / peak) * gain;
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                int bytes = data.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var v in data) w.Write((short)Mathf.Clamp(v * norm * 32767f, -32768f, 32767f));
            }
        }

        static void Sfx(string id, float seconds, Action<float[]> build, float gain = 1f)
        {
            noiseState = (uint)(id.GetHashCode() | 1);
            var b = Buf(seconds);
            build(b);
            WriteWav("Sfx/" + id, b, gain);
            if (!SfxIds.Contains(id)) SfxIds.Add(id);
        }

        // ------------------------------------------------------------------ generation

        public static void GenerateAll()
        {
            SfxIds.Clear();
            Sfx("jump", 0.14f, b => Tone(b, 0, 0.14f, 260, 620, p => Sq(p, 0.25f), 0.5f), 0.7f);
            Sfx("land", 0.1f, b => NoiseBurst(b, 0, 0.1f, 0.8f, 0.15f, 0.05f), 0.6f);
            Sfx("whip", 0.2f, b => { NoiseBurst(b, 0, 0.14f, 0.6f, 0.05f, 0.5f, 1f); NoiseBurst(b, 0.12f, 0.06f, 1f, 0.9f, 0.6f); Tone(b, 0.12f, 0.05f, 1400, 900, p => Sq(p), 0.25f); });
            Sfx("hit", 0.14f, b => { Tone(b, 0, 0.12f, 220, 70, p => Sq(p, 0.3f), 0.6f); NoiseBurst(b, 0, 0.06f, 0.5f, 0.6f, 0.3f); });
            Sfx("enemy_die", 0.3f, b => { Tone(b, 0, 0.28f, 420, 60, p => Sq(p, 0.4f), 0.5f); NoiseBurst(b, 0.02f, 0.2f, 0.4f, 0.4f, 0.1f); });
            Sfx("hurt", 0.3f, b => { Tone(b, 0, 0.28f, 340, 140, p => Sq(p, 0.5f), 0.6f, 0.002f, 0.06f, 30f); NoiseBurst(b, 0, 0.08f, 0.4f, 0.5f, 0.2f); });
            Sfx("death", 0.8f, b => { int[] n = { 7, 3, 0, -5, -12 }; for (int i = 0; i < n.Length; i++) Tone(b, i * 0.13f, 0.2f, NoteHz(n[i]), NoteHz(n[i]) * 0.97f, Tri, 0.6f); });
            Sfx("coin", 0.22f, b => { Tone(b, 0, 0.05f, 988, 988, p => Sq(p, 0.5f), 0.4f); Tone(b, 0.05f, 0.17f, 1319, 1319, p => Sq(p, 0.5f), 0.4f); }, 0.6f);
            Sfx("gem", 0.3f, b => { float[] f = { 1047, 1319, 1568, 2093 }; for (int i = 0; i < 4; i++) Tone(b, i * 0.05f, 0.12f, f[i], f[i], p => Sq(p, 0.25f), 0.35f); }, 0.6f);
            Sfx("pickup", 0.1f, b => Tone(b, 0, 0.1f, 480, 900, Tri, 0.6f), 0.6f);
            Sfx("throw", 0.14f, b => NoiseBurst(b, 0, 0.14f, 0.7f, 0.03f, 0.4f, 1f), 0.7f);
            Sfx("thud", 0.14f, b => { Tone(b, 0, 0.12f, 140, 60, Sin, 0.8f); NoiseBurst(b, 0, 0.05f, 0.4f, 0.3f, 0.1f); }, 0.8f);
            Sfx("bomb_place", 0.05f, b => Tone(b, 0, 0.04f, 820, 760, p => Sq(p), 0.4f), 0.5f);
            Sfx("fuse", 0.05f, b => NoiseBurst(b, 0, 0.05f, 0.5f, 0.9f, 0.9f), 0.35f);
            Sfx("explosion", 0.9f, b => { NoiseBurst(b, 0, 0.9f, 1f, 0.5f, 0.03f, 1.6f); Tone(b, 0, 0.5f, 90, 35, Sin, 0.9f); });
            Sfx("rope_throw", 0.22f, b => Tone(b, 0, 0.22f, 300, 900, Tri, 0.5f), 0.6f);
            Sfx("rope_deploy", 0.3f, b => { for (int i = 0; i < 6; i++) NoiseBurst(b, i * 0.045f, 0.03f, 0.6f, 0.6f, 0.4f); }, 0.6f);
            Sfx("arrow", 0.25f, b => { NoiseBurst(b, 0, 0.2f, 0.5f, 0.1f, 0.6f, 1f); Tone(b, 0, 0.18f, 620, 300, Saw, 0.3f); });
            Sfx("crack", 0.3f, b => { for (int i = 0; i < 5; i++) NoiseBurst(b, i * 0.05f + (i % 2) * 0.01f, 0.04f, 0.7f, 0.8f, 0.5f); });
            Sfx("shatter", 0.3f, b => { NoiseBurst(b, 0, 0.3f, 0.8f, 0.9f, 0.3f); Tone(b, 0, 0.1f, 160, 80, Sin, 0.5f); });
            Sfx("crusher_warn", 0.4f, b => Tone(b, 0, 0.4f, 70, 60, p => Sq(p, 0.5f) * (0.5f + 0.5f * Sq(p * 0.1f)), 0.5f));
            Sfx("crusher_slam", 0.5f, b => { Tone(b, 0, 0.45f, 80, 30, Sin, 1f); NoiseBurst(b, 0, 0.3f, 0.7f, 0.3f, 0.05f); });
            Sfx("flame", 0.7f, b => { NoiseBurst(b, 0, 0.7f, 0.8f, 0.08f, 0.3f, 0.8f); Tone(b, 0, 0.5f, 110, 90, Saw, 0.15f); }, 0.7f);
            Sfx("door", 0.4f, b => { Tone(b, 0, 0.15f, 392, 392, Tri, 0.5f); Tone(b, 0.1f, 0.15f, 494, 494, Tri, 0.5f); Tone(b, 0.2f, 0.2f, 587, 587, Tri, 0.5f); });
            Sfx("buy", 0.35f, b => { Tone(b, 0, 0.08f, 660, 660, p => Sq(p, 0.25f), 0.4f); Tone(b, 0.08f, 0.08f, 880, 880, p => Sq(p, 0.25f), 0.4f); Tone(b, 0.16f, 0.18f, 1320, 1320, p => Sq(p, 0.25f), 0.4f); }, 0.7f);
            Sfx("error", 0.25f, b => { Tone(b, 0, 0.09f, 150, 150, p => Sq(p), 0.4f); Tone(b, 0.12f, 0.1f, 140, 140, p => Sq(p), 0.4f); }, 0.6f);
            Sfx("grab", 0.05f, b => Tone(b, 0, 0.05f, 700, 600, Tri, 0.5f), 0.5f);
            Sfx("climb", 0.08f, b => Tone(b, 0, 0.08f, 380, 520, Tri, 0.4f), 0.5f);
            Sfx("stomp", 0.12f, b => { Tone(b, 0, 0.1f, 180, 420, p => Sq(p, 0.3f), 0.5f); NoiseBurst(b, 0, 0.05f, 0.4f, 0.4f, 0.2f); }, 0.7f);
            Sfx("bat", 0.2f, b => { for (int i = 0; i < 4; i++) Tone(b, i * 0.045f, 0.03f, 900, 700, p => Sq(p, 0.2f), 0.3f); }, 0.5f);
            Sfx("toad", 0.22f, b => Tone(b, 0, 0.22f, 120, 100, p => Sq(p, 0.3f) * (0.6f + 0.4f * Sin(p * 0.08f)), 0.6f), 0.6f);
            Sfx("brute", 0.45f, b => Tone(b, 0, 0.45f, 95, 70, Saw, 0.7f, 0.02f, 0.08f, 22f), 0.8f);
            Sfx("shopkeeper_angry", 0.4f, b => { Tone(b, 0, 0.18f, 220, 330, Saw, 0.6f); Tone(b, 0.18f, 0.2f, 330, 240, Saw, 0.6f); });
            Sfx("warning", 1.4f, b => { Tone(b, 0, 1.4f, 220, 170, Tri, 0.6f, 0.2f, 0.03f, 4f); Tone(b, 0.1f, 1.3f, 233, 180, Sin, 0.4f, 0.3f, 0.02f, 3f); });
            Sfx("wraith", 1.6f, b => { Tone(b, 0, 1.6f, 330, 300, Sin, 0.5f, 0.3f, 0.04f, 5f); Tone(b, 0, 1.6f, 349, 318, Sin, 0.5f, 0.3f, 0.04f, 5.5f); NoiseBurst(b, 0, 1.6f, 0.25f, 0.05f, 0.02f, 0.5f); });
            Sfx("rumble", 0.6f, b => NoiseBurst(b, 0, 0.6f, 0.8f, 0.03f, 0.02f, 1f), 0.6f);
            Sfx("collapse", 1.4f, b => { NoiseBurst(b, 0, 1.4f, 1f, 0.05f, 0.02f, 1f); Tone(b, 0, 1.2f, 60, 40, Sin, 0.8f); });
            Sfx("victory", 1.2f, b => { int[] n = { 3, 7, 10, 15, 10, 15 }; float[] st = { 0, 0.12f, 0.24f, 0.36f, 0.6f, 0.72f }; for (int i = 0; i < n.Length; i++) Tone(b, st[i], i == n.Length - 1 ? 0.45f : 0.14f, NoteHz(n[i]), NoteHz(n[i]), p => Sq(p, 0.25f), 0.4f); });
            Sfx("menu_select", 0.06f, b => Tone(b, 0, 0.06f, 660, 700, p => Sq(p, 0.25f), 0.4f), 0.5f);
            Sfx("idol", 0.6f, b => { float[] f = { 784, 988, 1175, 1568, 1976 }; for (int i = 0; i < f.Length; i++) Tone(b, i * 0.07f, 0.25f, f[i], f[i], Sin, 0.35f); }, 0.7f);
            Sfx("pot_break", 0.22f, b => { NoiseBurst(b, 0, 0.18f, 0.8f, 0.7f, 0.3f); for (int i = 0; i < 3; i++) Tone(b, 0.02f + i * 0.04f, 0.03f, 1800 - i * 300, 1500, Tri, 0.2f); });
            Sfx("crate_break", 0.25f, b => { NoiseBurst(b, 0, 0.2f, 0.8f, 0.4f, 0.1f); Tone(b, 0, 0.12f, 160, 90, Sin, 0.5f); });

            MusicTrack("menu", 84, 8, 0, new[] { 0, -4, -7, -5, 0, -4, -2, -5 }, new[] { 0, 2, 3, 5, 7, 8, 10 }, 0.0f, 101, 0.55f);
            MusicTrack("area1", 116, 16, 5, new[] { 0, 0, -2, -2, 3, 3, -4, -2, 0, 0, -2, -2, 5, 3, -2, 0 }, new[] { 0, 2, 3, 5, 7, 9, 10 }, 1f, 202, 0.6f);
            MusicTrack("area2", 128, 16, 7, new[] { 0, 1, 0, -2, 0, 1, 3, 1, 0, 1, 0, -2, -4, -2, 1, 0 }, new[] { 0, 1, 3, 5, 7, 8, 10 }, 1f, 303, 0.6f);
            MusicTrack("area3", 104, 16, 3, new[] { 0, 5, 7, 5, 0, 5, -3, -5, 0, 5, 7, 9, 5, 7, 0, 0 }, new[] { 0, 2, 4, 5, 7, 9, 11 }, 0.7f, 505, 0.55f);
            MusicTrack("final", 150, 16, 0, new[] { 0, -2, -4, -5, 0, -2, -4, 2, 0, -2, -4, -5, -7, -5, -4, -2 }, new[] { 0, 2, 3, 5, 7, 8, 11 }, 1.3f, 404, 0.65f);
            AssetDatabase.Refresh();
            Debug.Log($"[Audio] Generated {SfxIds.Count} sfx + {MusicIds.Count} music loops");
        }

        /// <summary>
        /// Procedural chiptune loop: triangle bass on the chord roots, square arpeggio, seeded square-wave melody
        /// on the given scale, and noise drums. Length is an exact number of bars so it loops seamlessly.
        /// </summary>
        static void MusicTrack(string id, int bpm, int bars, int key, int[] progression, int[] scale, float drumIntensity, int seed, float gain)
        {
            var rng = new System.Random(seed);
            noiseState = (uint)seed * 7919u + 1u;
            float beat = 60f / bpm;
            float eighth = beat / 2f, sixteenth = beat / 4f;
            float total = bars * 4 * beat;
            var b = new float[Mathf.CeilToInt(total * SR)];
            int baseNote = key - 24;   // relative to A4
            bool sparse = drumIntensity <= 0.01f;

            for (int bar = 0; bar < bars; bar++)
            {
                int root = baseNote + progression[bar % progression.Length];
                float barStart = bar * 4 * beat;
                // bass: 8ths, root with octave on off-beats
                for (int e = 0; e < 8; e++)
                {
                    if (sparse && e % 2 == 1) continue;
                    int n = root + ((e % 4 == 3) ? 12 : 0) + ((e == 6 && bar % 2 == 1) ? 7 : 0);
                    Tone(b, barStart + e * eighth, eighth * 0.9f, NoteHz(n), NoteHz(n), Tri, 0.32f, 0.004f);
                }
                // arpeggio: 16ths over minor/major triad from the scale
                int[] chord = { 0, scale[2], 7 };
                for (int s = 0; s < 16; s++)
                {
                    if (sparse && s % 4 != 0) continue;
                    int n = root + 24 + chord[s % 3] + (s >= 8 ? 12 : 0);
                    Tone(b, barStart + s * sixteenth, sixteenth * 0.8f, NoteHz(n), NoteHz(n), p => Sq(p, 0.125f), sparse ? 0.08f : 0.05f, 0.002f);
                }
                // melody: random walk on scale degrees with a rhythmic motif repeated every two bars
                if (bar % 8 >= 2 || sparse)
                {
                    var motif = new System.Random(seed + (bar % 4) * 13);
                    int degree = 2 + motif.Next(3);
                    float t = 0f;
                    while (t < 4f * beat - 0.01f)
                    {
                        float len = motif.NextDouble() < 0.6 ? eighth : (motif.NextDouble() < 0.5 ? beat : eighth * 3f);
                        if (t + len > 4f * beat) len = 4f * beat - t;
                        if (motif.NextDouble() < 0.82)
                        {
                            degree = Mathf.Clamp(degree + motif.Next(-2, 3), 0, scale.Length * 2 - 1);
                            int n = root + 24 + scale[degree % scale.Length] + 12 * (degree / scale.Length) - progression[bar % progression.Length] + progression[bar % progression.Length];
                            Tone(b, barStart + t, len * 0.92f, NoteHz(n), NoteHz(n), p => Sq(p, 0.25f), sparse ? 0.12f : 0.1f, 0.01f, 0.012f, 5.5f);
                        }
                        t += len;
                    }
                }
                // drums
                if (!sparse)
                {
                    for (int s = 0; s < 16; s++)
                    {
                        float st = barStart + s * sixteenth;
                        if (s % 8 == 0 || (drumIntensity > 1.1f && s % 8 == 6)) { Tone(b, st, 0.12f, 150, 45, Sin, 0.55f * drumIntensity); }
                        if (s % 8 == 4) NoiseBurst(b, st, 0.12f, 0.28f * drumIntensity, 0.6f, 0.3f);
                        if (s % 2 == 0) NoiseBurst(b, st, 0.03f, 0.07f * drumIntensity, 0.95f, 0.95f);
                    }
                }
                else
                {
                    if (bar % 2 == 0) NoiseBurst(b, barStart, 2.5f * beat, 0.05f, 0.02f, 0.01f, 0.6f);
                }
            }
            WriteWav("Music/" + id, b, gain);
        }
    }
}
