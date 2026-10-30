using System;
using System.IO;
using Gargote.Presentation;
using UnityEngine;

namespace Gargote.EditorTools
{
    /// <summary>
    /// Sons placeholders synthétisés (aucun échantillon externe : libres de droits par construction).
    /// À remplacer en M4 via la SoundBank. WAV 22,05 kHz mono 16 bits.
    /// </summary>
    internal static class PlaceholderAudio
    {
        private const int Rate = 22050;

        public static void WriteAll(string folder)
        {
            Directory.CreateDirectory(folder);
            Write(folder, SoundId.Tap, Tap());
            Write(folder, SoundId.Bill, Bill());
            Write(folder, SoundId.Cash, Cash());
            Write(folder, SoundId.Buy, Buy());
            Write(folder, SoundId.Milestone, Milestone());
            Write(folder, SoundId.Unlock, SabarRoll());
            Write(folder, SoundId.Shutter, Shutter());
            Write(folder, SoundId.Grill, Sizzle(2f, 0.55f, 11));
            Write(folder, SoundId.Fry, Sizzle(2f, 0.8f, 23));
            Write(folder, SoundId.CrowdLoop, Crowd());
            Write(folder, SoundId.Horn, Horn());
            Write(folder, SoundId.Collect, Whoosh());
            Write(folder, SoundId.Special, Chime(new[] { 1318.5f, 1568f, 2093f }, 0.11f));
            Write(folder, SoundId.Manager, Chime(new[] { 1046.5f, 1318.5f, 1568f, 2093f }, 0.06f));
            Write(folder, SoundId.Button, Click(900f, 0.04f));
            Write(folder, SoundId.MusicDay, Music(false));
            Write(folder, SoundId.MusicNight, Music(true));
        }

        public static string FileName(SoundId id) => "sfx_" + id.ToString().ToLowerInvariant() + ".wav";

        private static void Write(string folder, SoundId id, float[] samples)
        {
            Normalize(samples, 0.85f);
            using (var stream = new FileStream(Path.Combine(folder, FileName(id)), FileMode.Create))
            using (var w = new BinaryWriter(stream))
            {
                int bytes = samples.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' });
                w.Write(36 + bytes);
                w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(Rate);
                w.Write(Rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' });
                w.Write(bytes);
                foreach (float s in samples) w.Write((short)Mathf.Clamp(s * 32767f, -32768f, 32767f));
            }
        }

        private static void Normalize(float[] s, float peak)
        {
            float max = 1e-6f;
            foreach (float v in s) max = Mathf.Max(max, Mathf.Abs(v));
            float k = peak / max;
            for (int i = 0; i < s.Length; i++) s[i] *= k;
        }

        private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        private static readonly System.Random Rng = new System.Random(1960);
        private static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);
        private static float Sine(float hz, int i) => Mathf.Sin(2f * Mathf.PI * hz * i / Rate);
        private static float Triangle(float hz, int i) => 2f * Mathf.Abs(2f * Mathf.Repeat(hz * i / Rate, 1f) - 1f) - 1f;
        private static float Square(float hz, int i) => Mathf.Repeat(hz * i / Rate, 1f) < 0.5f ? 1f : -1f;

        private static float[] Click(float hz, float seconds)
        {
            var s = Buffer(seconds);
            for (int i = 0; i < s.Length; i++)
            {
                float env = Mathf.Exp(-i / (Rate * seconds * 0.25f));
                s[i] = (Sine(hz, i) * 0.7f + Noise() * 0.3f) * env;
            }
            return s;
        }

        private static float[] Tap() => Click(1250f, 0.06f);

        private static float[] Bill()
        {
            var s = Buffer(0.09f);
            float prev = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float n = Noise();
                float hp = n - prev; // passe-haut grossier : froissement de papier
                prev = n;
                float env = Mathf.Sin(Mathf.PI * i / s.Length);
                s[i] = hp * env;
            }
            return s;
        }

        private static float[] Cash()
        {
            var s = Buffer(0.5f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float bell1 = Sine(1760f, i) * Mathf.Exp(-t * 9f);
                float bell2 = t > 0.08f ? Sine(2349f, i) * Mathf.Exp(-(t - 0.08f) * 7f) : 0f;
                float drawer = t < 0.05f ? Noise() * (1f - t / 0.05f) * 0.5f : 0f;
                s[i] = bell1 * 0.6f + bell2 * 0.6f + drawer;
            }
            return s;
        }

        private static float[] Buy()
        {
            var s = Buffer(0.13f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / s.Length;
                phase += 2f * Mathf.PI * Mathf.Lerp(620f, 980f, t) / Rate;
                s[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t);
            }
            return s;
        }

        private static float[] Milestone()
        {
            float[] notes = { 523.3f, 659.3f, 784f, 1046.5f };
            const float step = 0.09f;
            var s = Buffer(step * notes.Length + 0.35f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * Rate);
                for (int i = start; i < s.Length; i++)
                {
                    float t = (float)(i - start) / Rate;
                    s[i] += Triangle(notes[n], i) * Mathf.Exp(-t * (n == notes.Length - 1 ? 4f : 10f)) * 0.5f;
                }
            }
            return s;
        }

        /// <summary>Roulement façon sabar : frappes de tambour qui accélèrent.</summary>
        private static float[] SabarRoll()
        {
            var s = Buffer(1.0f);
            float t0 = 0f, gap = 0.12f;
            int k = 0;
            while (t0 < 0.85f)
            {
                int start = (int)(t0 * Rate);
                float pitch = k % 2 == 0 ? 190f : 260f;
                for (int i = start; i < s.Length && i < start + Rate / 5; i++)
                {
                    float t = (float)(i - start) / Rate;
                    s[i] += (Sine(pitch * (1f + Mathf.Exp(-t * 40f)), i) * Mathf.Exp(-t * 18f) + Noise() * Mathf.Exp(-t * 60f) * 0.6f) * 0.6f;
                }
                t0 += gap;
                gap = Mathf.Max(0.045f, gap * 0.86f);
                k++;
            }
            return s;
        }

        private static float[] Shutter()
        {
            var s = Buffer(0.8f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float rattle = Square(28f, i) > 0f ? 1f : 0.3f;
                float env = Mathf.Min(1f, t * 20f) * Mathf.Exp(-t * 2.5f);
                s[i] = (Noise() * 0.5f * rattle + Sine(95f, i) * 0.3f) * env;
            }
            return s;
        }

        private static float[] Sizzle(float seconds, float brightness, int seed)
        {
            var rng = new System.Random(seed);
            var s = Buffer(seconds);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float n = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (n - lp) * brightness;
                float crackle = rng.NextDouble() < 0.0015 ? (float)rng.NextDouble() * 3f : 0f;
                s[i] = (n - lp) * 0.6f + crackle * Mathf.Sign(n);
            }
            return Loopable(s, Rate / 10);
        }

        private static float[] Crowd()
        {
            var s = Buffer(4f);
            float lp1 = 0f, lp2 = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float n = Noise();
                lp1 += (n - lp1) * 0.08f;
                lp2 += (lp1 - lp2) * 0.15f;
                float t = (float)i / Rate;
                float murmur = 0.6f + 0.25f * Mathf.Sin(t * 2.1f) + 0.15f * Mathf.Sin(t * 5.3f + 1f);
                s[i] = lp2 * murmur;
            }
            return Loopable(s, Rate / 4);
        }

        private static float[] Horn()
        {
            var s = Buffer(0.55f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t * 30f) * Mathf.Min(1f, (0.55f - t) * 20f);
                float hz = t < 0.22f ? 410f : 330f;
                s[i] = (Square(hz, i) * 0.4f + Square(hz * 1.26f, i) * 0.3f) * env;
            }
            return s;
        }

        private static float[] Whoosh()
        {
            var s = Buffer(0.38f);
            float lp = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = (float)i / s.Length;
                lp += (Noise() - lp) * Mathf.Lerp(0.03f, 0.45f, t);
                s[i] = lp * Mathf.Sin(Mathf.PI * t);
            }
            return s;
        }

        private static float[] Chime(float[] notes, float step)
        {
            var s = Buffer(step * notes.Length + 0.4f);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = (int)(n * step * Rate);
                for (int i = start; i < s.Length; i++)
                {
                    float t = (float)(i - start) / Rate;
                    s[i] += (Sine(notes[n], i) + 0.3f * Sine(notes[n] * 2f, i)) * Mathf.Exp(-t * 7f) * 0.4f;
                }
            }
            return s;
        }

        /// <summary>Boucle de 8 temps : arpège pentatonique + basse (nuit : plus lent, plus grave).</summary>
        private static float[] Music(bool night)
        {
            float bpm = night ? 84f : 104f;
            float beat = 60f / bpm;
            int beats = 16;
            var s = Buffer(beat * beats);
            float root = night ? 196f : 261.6f;
            float[] scale = night ? new[] { 1f, 1.189f, 1.335f, 1.498f, 1.782f } : new[] { 1f, 1.122f, 1.26f, 1.498f, 1.682f };
            int[] pattern = { 0, 2, 4, 2, 3, 1, 4, 2, 0, 3, 4, 1, 2, 4, 3, 1 };
            for (int b = 0; b < beats; b++)
            {
                int start = (int)(b * beat * Rate);
                float note = root * 2f * scale[pattern[b]];
                float bass = root * 0.5f * (b % 8 < 4 ? 1f : scale[3] / 2f * 1.335f);
                for (int i = start; i < s.Length && i < start + (int)(beat * Rate); i++)
                {
                    float t = (float)(i - start) / Rate;
                    s[i] += Triangle(note, i) * Mathf.Exp(-t * 6f) * 0.35f;
                    s[i] += Sine(bass, i) * Mathf.Exp(-t * 2.5f) * 0.45f;
                    if (b % 2 == 0) s[i] += Noise() * Mathf.Exp(-t * 45f) * 0.12f;
                }
            }
            return Loopable(s, Rate / 50);
        }

        /// <summary>Boucle sans clic : la fin est fondue dans le début puis retirée.</summary>
        private static float[] Loopable(float[] s, int length)
        {
            length = Math.Min(length, s.Length / 2);
            int kept = s.Length - length;
            for (int i = 0; i < length; i++)
            {
                float k = (float)i / length;
                s[i] = s[i] * k + s[kept + i] * (1f - k);
            }
            var result = new float[kept];
            Array.Copy(s, result, kept);
            return result;
        }
    }
}
