// Prozedurale, düstere Hintergrundmusik (Dark Ambient) – komplett synthetisiert, keine Audiodateien nötig.
// Zwei Schleifen: "amb" (Drone, Wind, ferne Glocken) läuft immer; "ten" (Herzschlag-Puls, Sirren) wird in Wellen eingeblendet.
using System;
using UnityEngine;

namespace ZA
{
    public class Music
    {
        const int SR = 32000; const float Len = 48f; // Schleifenlänge; alle LFO-Perioden teilen 48 s, damit die Schleife nahtlos ist
        readonly AudioSource amb, ten;
        public float volume = 0.7f;
        float ambTarget = 1, tenTarget = 0;
        public string State => $"amb={amb.volume:0.00}/{(amb.isPlaying ? "on" : "off")} ten={ten.volume:0.00}/{(ten.isPlaying ? "on" : "off")} len={amb.clip.length:0.0}s";

        public Music()
        {
            var root = new GameObject("Music"); UnityEngine.Object.DontDestroyOnLoad(root);
            amb = Src(root, "amb", BuildAmbient()); ten = Src(root, "ten", BuildTension());
            amb.volume = 0; ten.volume = 0; amb.Play(); ten.Play();
        }

        static AudioSource Src(GameObject root, string name, float[] stereo)
        {
            var clip = AudioClip.Create(name, stereo.Length / 2, 2, SR, false); clip.SetData(stereo, 0);
            var s = root.AddComponent<AudioSource>(); s.clip = clip; s.loop = true; s.spatialBlend = 0; s.playOnAwake = false; s.priority = 0; s.dopplerLevel = 0;
            return s;
        }

        /// <summary>Jeden Frame: Zielpegel je nach Spielzustand, weiche Überblendung</summary>
        public void Update(float dt, bool inGame, bool wave, bool boss, bool gameOver)
        {
            ambTarget = gameOver ? 0.5f : 1f;
            tenTarget = !inGame ? 0f : gameOver ? 0f : wave ? (boss ? 1f : 0.65f) : 0.12f;
            amb.volume = Mathf.MoveTowards(amb.volume, ambTarget * volume * 0.32f, dt * 0.15f);
            ten.volume = Mathf.MoveTowards(ten.volume, tenTarget * volume * 0.28f, dt * (tenTarget > ten.volume ? 0.08f : 0.2f));
        }

        // ------------------------------------------------------------------ Synthese
        static float Q(float f) => Mathf.Round(f * Len) / Len; // Frequenz so runden, dass sie in 48 s ganzzahlig oft schwingt (nahtlose Schleife)
        static float Swell(float t, float period, float phase) => 0.5f + 0.5f * Mathf.Sin(2 * Mathf.PI * t / period + phase);

        static float[] BuildAmbient()
        {
            int n = (int)(Len * SR); var o = new float[n * 2];
            // Drone in d-Moll: D1, A1, D2, F2 (+ leicht verstimmte Kopie je Kanal)
            float[] notes = { Q(36.71f), Q(55f), Q(73.42f), Q(87.31f), Q(110f) };
            float[] amps = { 0.9f, 0.5f, 0.45f, 0.3f, 0.2f };
            float[] periods = { 48f, 24f, 16f, 48f / 5, 12f };
            float[] phases = { 0f, 1.3f, 2.9f, 4.1f, 0.7f };
            var rnd = new System.Random(7);
            // Glocken: Zeitpunkt, Grundfrequenz (D3, F3, A2, C3, G#2 = Tritonus, D3)
            (float t, float f)[] bells = { (3f, 146.8f), (11f, 174.6f), (19.5f, 110f), (27f, 130.8f), (35f, 103.8f), (43f, 146.8f) };
            float lpL = 0, bpL = 0, lpR = 0, bpR = 0, wl = 0, wb = 0, brown = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                float cutoff = 170f + 110f * Mathf.Sin(2 * Mathf.PI * t / 24f);
                float fc = 2f * Mathf.Sin(Mathf.PI * cutoff / SR);
                float dl = 0, dr = 0;
                for (int k = 0; k < notes.Length; k++)
                {
                    float a = amps[k] * (0.35f + 0.65f * Swell(t, periods[k], phases[k]));
                    double pl = 2 * Math.PI * notes[k] * t, pr = 2 * Math.PI * (notes[k] + 0.0208f * (k % 2 == 0 ? 1 : -1)) * t; // 1/48 Hz Verstimmung bleibt schleifentreu
                    dl += a * (float)(Math.Sin(pl) + 0.45 * Math.Sin(2 * pl + 0.3) + 0.25 * Math.Sin(3 * pl) + 0.12 * Math.Sin(5 * pl));
                    dr += a * (float)(Math.Sin(pr) + 0.45 * Math.Sin(2 * pr + 0.3) + 0.25 * Math.Sin(3 * pr) + 0.12 * Math.Sin(5 * pr));
                }
                // Tiefpass (State-Variable) je Kanal
                float hp = dl - lpL - bpL / 0.9f; bpL += fc * hp; lpL += fc * bpL;
                hp = dr - lpR - bpR / 0.9f; bpR += fc * hp; lpR += fc * bpR;
                float drone = 0.11f; float l = lpL * drone, r = lpR * drone;
                // Wind: tiefpassgefiltertes Rauschen mit langsamen Böen, dazu ein leises Pfeifen
                float x = (float)(rnd.NextDouble() * 2 - 1);
                float wfc = 2f * Mathf.Sin(Mathf.PI * 380f / SR); float whp = x - wl - wb / 0.7f; wb += wfc * whp; wl += wfc * wb;
                float gust = 0.3f + 0.7f * Swell(t, 16f, 0.4f) * Swell(t, 48f / 7, 2.2f);
                float wind = wl * 0.08f * gust;
                brown = 0.998f * brown + 0.002f * x * 40f; float rumble = Mathf.Clamp(brown, -1, 1) * 0.06f * Swell(t, 48f / 3, 1.1f);
                float whistleEnv = Mathf.Pow(Swell(t, 48f, 3.3f), 6f) * 0.015f;
                float whistle = (float)Math.Sin(2 * Math.PI * Q(690f) * t + 3 * Math.Sin(2 * Math.PI * t / 6f)) * whistleEnv;
                l += wind * 1.1f + rumble + whistle; r += wind * 0.9f + rumble + whistle * 0.7f;
                // Glocken (inharmonische Teiltöne, langer Ausklang), Position im Stereobild wechselt
                for (int b = 0; b < bells.Length; b++)
                {
                    float dtb = t - bells[b].t; if (dtb < 0 || dtb > 9f) continue;
                    float env = Mathf.Exp(-dtb / 2.6f) * Mathf.Min(1, dtb / 0.01f) * 0.07f;
                    double pb = 2 * Math.PI * bells[b].f * dtb;
                    float v = (float)(Math.Sin(pb) + 0.5 * Math.Sin(pb * 2.76) * Math.Exp(-dtb / 1.2) + 0.3 * Math.Sin(pb * 5.4) * Math.Exp(-dtb / 0.6)) * env;
                    float pan = (b % 2 == 0) ? 0.35f : 0.65f; l += v * (1 - pan) * 2; r += v * pan * 2;
                }
                o[i * 2] = l; o[i * 2 + 1] = r;
            }
            return Finish(o, n, 2f);
        }

        static float[] BuildTension()
        {
            int n = (int)(Len * SR); var o = new float[n * 2];
            const float beat = 0.75f; // 80 Schläge/min, 64 Schläge pro Schleife
            var rnd = new System.Random(3);
            float nl = 0, nb = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR; float tb = t % beat; int bi = (int)(t / beat);
                // Herzschlag: doppelter Schlag (dumm-DUMM) alle zwei Takte
                float kick = 0;
                float k1 = tb; if (k1 < 0.4f) kick += (float)Math.Sin(2 * Math.PI * (52 - 30 * k1) * k1) * Mathf.Exp(-k1 * 11f) * 0.7f;
                float k2 = tb - 0.22f; if (bi % 2 == 0 && k2 >= 0 && k2 < 0.4f) kick += (float)Math.Sin(2 * Math.PI * (48 - 28 * k2) * k2) * Mathf.Exp(-k2 * 12f) * 0.5f;
                // Metallisches Ticken auf der Zwischenzeit
                float x = (float)(rnd.NextDouble() * 2 - 1);
                float fc = 2f * Mathf.Sin(Mathf.PI * 2400f / SR); float hp = x - nl - nb / 3f; nb += fc * hp; nl += fc * nb;
                float tk = tb - 0.375f; float tick = (bi % 4 == 1 && tk >= 0) ? nb * Mathf.Exp(-tk * 60f) * 0.25f : 0;
                // Sirren: zwei hohe, gegeneinander verstimmte Sinustöne mit Tremolo und langsamer Schwebung
                float trem = 0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 6f * t);
                float shimmerEnv = 0.02f * (0.4f + 0.6f * Swell(t, 12f, 0f)) * trem;
                float sh = ((float)Math.Sin(2 * Math.PI * Q(1760f) * t) + (float)Math.Sin(2 * Math.PI * Q(1764f) * t)) * shimmerEnv;
                // Anschwellen alle 12 s (Rauschen, hochpass nach oben gleitend)
                float rt = t % 12f; float riser = rt > 9f ? nb * Mathf.Pow((rt - 9f) / 3f, 2.5f) * 0.12f : 0;
                // tiefe, bedrohliche Quinte unter dem Puls
                float bass = ((float)Math.Sin(2 * Math.PI * Q(36.71f) * t) + 0.5f * (float)Math.Sin(2 * Math.PI * Q(55f) * t)) * 0.08f * (0.5f + 0.5f * Swell(t, 24f, 1f));
                float l = kick + tick * 0.6f + sh + riser + bass, r = kick + tick + sh * 0.8f + riser * 0.8f + bass;
                o[i * 2] = l; o[i * 2 + 1] = r;
            }
            return Finish(o, n, 0.75f);
        }

        /// <summary>Normalisieren und Schleifennaht glätten (Ende sanft in den Anfang überblenden)</summary>
        static float[] Finish(float[] o, int n, float fadeSec)
        {
            int fade = (int)(fadeSec * SR);
            for (int i = 0; i < fade; i++)
            {
                float w = i / (float)fade; // 0 am Anfang der Naht
                for (int c = 0; c < 2; c++) { int a = i * 2 + c, b = (n - fade + i) * 2 + c; float va = o[a], vb = o[b]; o[a] = va * w + vb * (1 - w); o[b] = vb * w + va * (1 - w) * 0f + vb * 0f; }
            }
            // Ende ausblenden (der Anfang trägt nun die Überblendung)
            for (int i = 0; i < fade; i++) { float w = 1 - i / (float)fade; for (int c = 0; c < 2; c++) o[(n - fade + i) * 2 + c] *= w; }
            float peak = 0; foreach (var v in o) peak = Mathf.Max(peak, Mathf.Abs(v));
            float g = peak > 0 ? 0.8f / peak : 1; for (int i = 0; i < o.Length; i++) o[i] *= g;
            return o;
        }
    }
}
