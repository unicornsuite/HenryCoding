// Prozedurale Soundeffekte (keine Audiodateien nötig) – Nachbau der WebAudio-Rezepte
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZA
{
    public class Sfx
    {
        const int SR = 32000;
        public float volume = 0.7f;
        readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Transform root;
        System.Random rnd = new System.Random(1);
        public Transform listener;

        public Sfx()
        {
            root = new GameObject("Sfx").transform;
            for (int i = 0; i < 24; i++) { var s = new GameObject("src").AddComponent<AudioSource>(); s.transform.SetParent(root, false); s.playOnAwake = false; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = 3; s.maxDistance = 45; s.dopplerLevel = 0; pool.Add(s); }
            Build();
        }

        // ------------------------------------------------------------------ Synthese
        float Noise() => (float)(rnd.NextDouble() * 2 - 1);
        static float Env(float t, float dur, float attack) => t < attack ? t / attack : Mathf.Exp(-6f * (t - attack) / Mathf.Max(0.001f, dur - attack));
        /// <summary>Gefiltertes Rauschen: type 0=lowpass 1=bandpass 2=highpass, Frequenz gleitet exponentiell von f0 nach f1</summary>
        float[] NoiseBurst(float dur, float f0, float f1, int type, float gain, float q = 0.7f, float attack = 0.002f)
        {
            int n = (int)(dur * SR); var o = new float[n];
            float lp = 0, bp = 0; // State-Variable-Filter
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float f = f0 * Mathf.Pow(f1 / f0, t / dur);
                float fc = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(f, SR * 0.45f) / SR);
                float x = Noise();
                float hp = x - lp - bp / q; bp += fc * hp; lp += fc * bp;
                float y = type == 0 ? lp : type == 1 ? bp : hp;
                o[i] = Mathf.Clamp(y * gain * Env(t, dur, attack), -1, 1);
            }
            return o;
        }
        float[] Tone(float dur, float f0, float f1, int wave, float gain, float attack = 0.005f)
        {
            int n = (int)(dur * SR); var o = new float[n]; double ph = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float f = f1 > 0 ? f0 * Mathf.Pow(f1 / f0, t / dur) : f0;
                ph += f / SR; double p = ph - Math.Floor(ph);
                float v = wave == 0 ? (float)Math.Sin(p * Math.PI * 2) : wave == 1 ? (p < 0.5 ? 1 : -1) : wave == 2 ? (float)(1 - 4 * Math.Abs(p - 0.5)) : (float)(2 * p - 1);
                o[i] = v * gain * Env(t, dur, attack);
            }
            return o;
        }
        static float[] Mix(params (float[] s, float offset)[] parts)
        {
            int len = 0; foreach (var (s, off) in parts) len = Mathf.Max(len, (int)(off * SR) + s.Length);
            var o = new float[len];
            foreach (var (s, off) in parts) { int st = (int)(off * SR); for (int i = 0; i < s.Length; i++) o[st + i] += s[i]; }
            for (int i = 0; i < o.Length; i++) o[i] = Mathf.Clamp(o[i], -1, 1);
            return o;
        }
        void Add(string key, params float[][] variants)
        {
            var arr = new AudioClip[variants.Length];
            for (int i = 0; i < variants.Length; i++) { var c = AudioClip.Create(key + i, variants[i].Length, 1, SR, false); c.SetData(variants[i], 0); arr[i] = c; }
            clips[key] = arr;
        }

        void Build()
        {
            var guns = new (string id, float f, float d, float g, float thump)[] {
                ("pistol", 2600, 0.16f, 0.55f, 140), ("smg", 3200, 0.1f, 0.4f, 170), ("shotgun", 1500, 0.38f, 0.9f, 80), ("rifle", 2200, 0.18f, 0.6f, 110),
                ("sniper", 1800, 0.55f, 1.0f, 70), ("launcher", 600, 0.3f, 0.6f, 60), ("minigun", 3000, 0.08f, 0.38f, 150), ("mg", 2800, 0.09f, 0.35f, 160), ("cannon", 700, 0.45f, 0.9f, 55) };
            foreach (var (id, f, d, g, thump) in guns)
            {
                var v = new float[3][];
                for (int i = 0; i < 3; i++) v[i] = Mix((NoiseBurst(d, f * (0.9f + i * 0.1f), 300, 0, g), 0), (Tone(d * 0.8f, thump, 40, 2, 0.5f), 0), (id == "sniper" ? NoiseBurst(0.8f, 800, 100, 0, 0.15f) : new float[0], 0.02f));
                Add("shoot_" + id, v);
            }
            Add("explosion", Mix((NoiseBurst(1.2f, 900, 60, 0, 1.0f, 0.7f, 0.005f), 0), (Tone(0.8f, 90, 25, 0, 0.9f), 0)));
            Add("hit", Tone(0.05f, 1800, 0, 1, 0.08f));
            Add("headshot", Mix((Tone(0.18f, 2400, 2200, 0, 0.18f), 0), (Tone(0.05f, 1200, 0, 1, 0.06f), 0)));
            Add("kill", Tone(0.12f, 900, 1500, 2, 0.15f));
            var groans = new float[3][];
            for (int i = 0; i < 3; i++) { float b = 70 + i * 20; groans[i] = NoiseAndSaw(1.0f, b); }
            Add("groan", groans);
            Add("hurt", Mix((Tone(0.25f, 160, 60, 1, 0.25f), 0), (NoiseBurst(0.15f, 500, 500, 0, 0.3f), 0)));
            Add("reload", Mix((NoiseBurst(0.05f, 3000, 3000, 1, 0.3f, 4), 0), (NoiseBurst(0.06f, 2200, 2200, 1, 0.35f, 4), 0.25f)));
            Add("empty", NoiseBurst(0.04f, 4000, 4000, 1, 0.25f, 6));
            Add("buy", Mix((Tone(0.12f, 1046, 0, 1, 0.1f), 0), (Tone(0.25f, 1568, 0, 1, 0.1f), 0.1f)));
            Add("deny", Tone(0.25f, 180, 0, 1, 0.12f));
            Add("wave", Mix((Tone(1.6f, 110, 0, 3, 0.09f, 0.15f), 0), (Tone(1.6f, 138.6f, 0, 3, 0.09f, 0.15f), 0), (Tone(1.6f, 164.8f, 0, 3, 0.09f, 0.15f), 0)));
            Add("waveboss", Mix((Tone(2.4f, 82.4f, 0, 3, 0.09f, 0.15f), 0), (Tone(2.4f, 98, 0, 3, 0.09f, 0.15f), 0), (Tone(2.4f, 123.5f, 0, 3, 0.09f, 0.15f), 0), (Tone(1.2f, 55, 40, 1, 0.12f), 0.3f)));
            Add("waveclear", Mix((Tone(0.35f, 523, 0, 2, 0.15f), 0), (Tone(0.35f, 659, 0, 2, 0.15f), 0.1f), (Tone(0.35f, 784, 0, 2, 0.15f), 0.2f), (Tone(0.35f, 1046, 0, 2, 0.15f), 0.3f)));
            Add("zap", Mix((Tone(0.25f, 1200, 120, 3, 0.2f), 0), (NoiseBurst(0.2f, 5000, 5000, 2, 0.25f), 0)));
            Add("step", NoiseBurst(0.07f, 500, 500, 0, 0.08f));
            Add("place", Mix((Tone(0.1f, 300, 0, 1, 0.15f), 0), (NoiseBurst(0.2f, 1200, 1200, 0, 0.3f), 0), (Tone(0.2f, 600, 0, 2, 0.12f), 0.12f)));
            Add("spin", Tone(0.5f, 80, 400, 3, 0.06f));
            Add("streak", Mix((Tone(0.2f, 660, 0, 1, 0.1f), 0), (Tone(0.2f, 880, 0, 1, 0.1f), 0.07f), (Tone(0.2f, 1320, 0, 1, 0.1f), 0.14f)));
            Add("powerup", Mix((Tone(0.3f, 523, 0, 2, 0.14f), 0), (Tone(0.3f, 659, 0, 2, 0.14f), 0.06f), (Tone(0.3f, 784, 0, 2, 0.14f), 0.12f), (Tone(0.3f, 1046, 0, 2, 0.14f), 0.18f), (Tone(0.3f, 1318, 0, 2, 0.14f), 0.24f), (NoiseBurst(0.4f, 3000, 8000, 1, 0.1f, 2), 0)));
            Add("spit", Mix((NoiseBurst(0.3f, 600, 200, 1, 0.3f, 3), 0), (Tone(0.25f, 300, 120, 3, 0.1f), 0)));
            Add("crate", BuildCrate());
            Add("throw", NoiseBurst(0.15f, 1200, 400, 1, 0.2f, 1));
            Add("swing", NoiseBurst(0.18f, 2500, 600, 1, 0.25f, 1.5f));
            Add("woodhit", Mix((NoiseBurst(0.08f, 900, 900, 1, 0.35f, 2), 0), (Tone(0.1f, 180, 90, 2, 0.2f), 0)));
            Add("woodbreak", Mix((NoiseBurst(0.15f, 800, 800, 1, 0.35f, 1.5f), 0), (NoiseBurst(0.15f, 1200, 1200, 1, 0.35f, 1.5f), 0.05f), (NoiseBurst(0.15f, 600, 600, 1, 0.35f, 1.5f), 0.1f), (Tone(0.3f, 120, 50, 2, 0.3f), 0)));
        }
        float[] NoiseAndSaw(float dur, float baseF)
        {
            int n = (int)(dur * SR); var o = new float[n]; double ph = 0, lfo = 0;
            float lp = 0, bp = 0; float q = 2f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float f = baseF * (1 - 0.3f * t / dur) + (float)Math.Sin(lfo) * 8; lfo += 2 * Math.PI * 8 / SR;
                ph += f / SR; double p = ph - Math.Floor(ph); float x = (float)(2 * p - 1);
                float fc = 2f * Mathf.Sin(Mathf.PI * 500 / SR); float hp = x - lp - bp / q; bp += fc * hp; lp += fc * bp;
                o[i] = Mathf.Clamp(bp * 0.5f * Env(t, dur, 0.15f), -1, 1);
            }
            return o;
        }
        float[] BuildCrate() { var parts = new List<(float[], float)>(); for (int i = 0; i < 10; i++) parts.Add((Tone(0.12f, 700 + (i % 3) * 200, 0, 1, 0.07f), i * 0.22f)); return Mix(parts.ToArray()); }

        // ------------------------------------------------------------------ Abspielen
        AudioSource Free() { foreach (var s in pool) if (!s.isPlaying) return s; return pool[0]; }
        public void Play(string key, Vector3? pos = null, float vol = 1f, float pitch = 1f)
        {
            if (!clips.TryGetValue(key, out var arr)) return;
            var s = Free(); s.clip = arr[rnd.Next(arr.Length)]; s.volume = vol * volume; s.pitch = pitch;
            if (pos.HasValue) { s.spatialBlend = 1; s.transform.position = pos.Value; } else { s.spatialBlend = 0; if (listener) s.transform.position = listener.position; }
            s.Play();
        }
        public void Shoot(string type, Vector3? pos) => Play("shoot_" + (clips.ContainsKey("shoot_" + type) ? type : "pistol"), pos);
        public void Groan(Vector3 pos, float pitch = 1f) => Play("groan", pos, 0.6f, pitch);
    }
}
