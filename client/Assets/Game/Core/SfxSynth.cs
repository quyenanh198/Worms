using System;

namespace Worms.Game.Core
{
    public enum Sfx
    {
        ExplosionSmall,
        ExplosionMedium,
        ExplosionLarge,
        Launch,
        Throw,
        Shotgun,
        UziShot,
        Swing,
        Bonk,
        AirRaid,
        Bounce,
        Jump,
        Land,
        Splash,
        Hurt,
        Death,
        TurnBell,
        Tick,
        Click,
    }

    /// <summary>
    /// Every sound in the game is synthesized here at startup (docs/PLAN.md
    /// §3.11): noise, oscillators, envelopes and simple filters. No audio
    /// files, so nothing to license. Output is mono PCM in [-1, 1].
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 44100;

        public static float[] Make(Sfx sfx)
        {
            var rng = new Worms.Sim.Rng(0x5EED0000u + (uint)sfx);
            switch (sfx)
            {
                case Sfx.ExplosionSmall: return Explosion(rng, 0.45f, 900f, 0.8f);
                case Sfx.ExplosionMedium: return Explosion(rng, 0.9f, 600f, 0.9f);
                case Sfx.ExplosionLarge: return Explosion(rng, 1.5f, 380f, 1f);
                case Sfx.Launch: return Whoosh(rng, 0.45f, 2500f, 700f, 0.7f, 180f);
                case Sfx.Throw: return Whoosh(rng, 0.22f, 1800f, 3200f, 0.45f, 0f);
                case Sfx.Shotgun: return Explosion(rng, 0.35f, 2400f, 0.85f);
                case Sfx.UziShot: return Explosion(rng, 0.07f, 3500f, 0.55f);
                case Sfx.Swing: return Whoosh(rng, 0.25f, 900f, 2600f, 0.5f, 0f);
                case Sfx.Bonk: return Thump(0.18f, 420f, 180f, 0.9f, 0.6f);
                case Sfx.AirRaid: return AirRaid(rng);
                case Sfx.Bounce: return Thump(0.12f, 260f, 140f, 0.6f, 0.2f);
                case Sfx.Jump: return Sweep(0.22f, 320f, 760f, 0.35f, true);
                case Sfx.Land: return Thump(0.16f, 140f, 60f, 0.7f, 0.3f);
                case Sfx.Splash: return Splash(rng);
                case Sfx.Hurt: return Squeak(0.3f, 900f, 520f, 0.45f);
                case Sfx.Death: return Death();
                case Sfx.TurnBell: return Bell(0.9f, 880f, 0.4f);
                case Sfx.Tick: return Thump(0.05f, 1800f, 1500f, 0.35f, 0f);
                case Sfx.Click: return Thump(0.04f, 1200f, 900f, 0.3f, 0f);
                default: return new float[1];
            }
        }

        static float[] Buffer(float seconds) { return new float[Math.Max(1, (int)(seconds * Rate))]; }

        static float Noise(Worms.Sim.Rng rng) { return rng.NextFloat() * 2f - 1f; }

        /// <summary>Filtered noise with a punchy low sine under it.</summary>
        static float[] Explosion(Worms.Sim.Rng rng, float seconds, float cutoff, float gain)
        {
            var b = Buffer(seconds);
            float lp = 0, lp2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = (float)Math.Exp(-t * 5f / seconds) * Math.Min(1f, t * 400f);
                // Cutoff falls over time: bright crack, then rumble.
                float c = cutoff * (0.25f + 0.75f * (float)Math.Exp(-t * 8f));
                float k = 1f - (float)Math.Exp(-2 * Math.PI * c / Rate);
                lp += (Noise(rng) - lp) * k;
                lp2 += (lp - lp2) * k;
                float thump = (float)Math.Sin(2 * Math.PI * (55f + 60f * Math.Exp(-t * 12f)) * t) * (float)Math.Exp(-t * 9f);
                b[i] = (lp2 * 2.2f + thump * 0.8f) * env;
            }
            return Normalize(b, gain);
        }

        static float[] Whoosh(Worms.Sim.Rng rng, float seconds, float from, float to, float gain, float tone)
        {
            var b = Buffer(seconds);
            float lp = 0, hp = 0, prev = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float u = t / seconds;
                float env = (float)Math.Sin(Math.PI * Math.Min(1f, u)) * (1f - u * 0.3f);
                float c = from + (to - from) * u;
                float k = 1f - (float)Math.Exp(-2 * Math.PI * c / Rate);
                lp += (Noise(rng) - lp) * k;
                hp = 0.97f * (hp + lp - prev);
                prev = lp;
                float s = hp;
                if (tone > 0) s += (float)Math.Sin(2 * Math.PI * tone * t * (1 + u)) * 0.25f;
                b[i] = s * env;
            }
            return Normalize(b, gain);
        }

        static float[] Thump(float seconds, float f0, float f1, float gain, float noise)
        {
            var b = Buffer(seconds);
            var rng = new Worms.Sim.Rng((uint)(f0 * 7 + f1));
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = f1 + (f0 - f1) * (float)Math.Exp(-t * 30f);
                phase += 2 * Math.PI * f / Rate;
                float env = (float)Math.Exp(-t * 6f / seconds) * Math.Min(1f, t * 2000f);
                b[i] = ((float)Math.Sin(phase) + Noise(rng) * noise) * env;
            }
            return Normalize(b, gain);
        }

        static float[] Sweep(float seconds, float from, float to, float gain, bool square)
        {
            var b = Buffer(seconds);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float u = (float)i / b.Length;
                float f = from + (to - from) * u;
                phase += 2 * Math.PI * f / Rate;
                float s = (float)Math.Sin(phase);
                if (square) s = Math.Sign(s) * 0.4f + s * 0.6f;
                b[i] = s * (1f - u) * Math.Min(1f, i / 200f);
            }
            return Normalize(b, gain);
        }

        /// <summary>Cartoon "ouch": a vibrato squeak falling in pitch.</summary>
        static float[] Squeak(float seconds, float from, float to, float gain)
        {
            var b = Buffer(seconds);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float u = t / seconds;
                float f = (from + (to - from) * u) * (1f + 0.06f * (float)Math.Sin(2 * Math.PI * 28 * t));
                phase += 2 * Math.PI * f / Rate;
                float s = (float)(Math.Sin(phase) + 0.5 * Math.Sin(phase * 2) + 0.25 * Math.Sin(phase * 3));
                b[i] = s * (float)Math.Sin(Math.PI * u) * Math.Min(1f, i / 300f);
            }
            return Normalize(b, gain);
        }

        /// <summary>A sad two-note "bye bye".</summary>
        static float[] Death()
        {
            var a = Squeak(0.22f, 700f, 640f, 0.4f);
            var c = Squeak(0.35f, 560f, 380f, 0.4f);
            var b = new float[a.Length + c.Length + Rate / 20];
            Array.Copy(a, b, a.Length);
            Array.Copy(c, 0, b, a.Length + Rate / 20, c.Length);
            return b;
        }

        static float[] Bell(float seconds, float f, float gain)
        {
            var b = Buffer(seconds);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float s = (float)(Math.Sin(2 * Math.PI * f * t) + 0.5 * Math.Sin(2 * Math.PI * f * 2.76 * t) * Math.Exp(-t * 6)
                                  + 0.3 * Math.Sin(2 * Math.PI * f * 5.4 * t) * Math.Exp(-t * 10));
                b[i] = s * (float)Math.Exp(-t * 4f) * Math.Min(1f, t * 3000f);
            }
            return Normalize(b, gain);
        }

        static float[] Splash(Worms.Sim.Rng rng)
        {
            var b = Buffer(0.8f);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float k = 1f - (float)Math.Exp(-2 * Math.PI * 1800f / Rate);
                lp += (Noise(rng) - lp) * k;
                float env = (float)Math.Exp(-t * 5f) * Math.Min(1f, t * 200f);
                // A few rising "bloop" bubbles.
                float bubbles = 0;
                for (int j = 1; j <= 3; j++)
                {
                    float start = 0.1f * j, bt = t - start;
                    if (bt > 0 && bt < 0.08f) bubbles += (float)Math.Sin(2 * Math.PI * (400 + 3000 * bt) * bt) * (1 - bt / 0.08f);
                }
                b[i] = lp * env * 1.5f + bubbles * 0.3f;
            }
            return Normalize(b, 0.7f);
        }

        /// <summary>Plane drone passing by, then falling whistles.</summary>
        static float[] AirRaid(Worms.Sim.Rng rng)
        {
            var b = Buffer(2.2f);
            double p1 = 0, p2 = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float pass = (float)Math.Exp(-Math.Pow((t - 0.8f) / 0.5f, 2));
                p1 += 2 * Math.PI * 95 / Rate;
                p2 += 2 * Math.PI * 97.5 / Rate;
                float drone = (float)(Math.Sign(Math.Sin(p1)) + Math.Sign(Math.Sin(p2))) * 0.25f * pass;
                float whistle = t > 1.1f ? (float)Math.Sin(2 * Math.PI * (1600 - 500 * (t - 1.1f)) * t) * 0.3f * (float)Math.Min(1, (t - 1.1f) * 5) : 0;
                b[i] = drone + whistle + Noise(rng) * 0.05f * pass;
            }
            return Normalize(b, 0.6f);
        }

        /// <summary>Scales to the given peak.</summary>
        public static float[] Normalize(float[] b, float peak)
        {
            float max = 1e-6f;
            foreach (var v in b) max = Math.Max(max, Math.Abs(v));
            float g = peak / max;
            for (int i = 0; i < b.Length; i++) b[i] *= g;
            return b;
        }

        /// <summary>
        /// A calm looping background tune (about 16 s): soft pad chords,
        /// a plucked melody and brushed percussion, generated from a seed.
        /// </summary>
        public static float[] Music(uint seed, float seconds = 16f)
        {
            var rng = new Worms.Sim.Rng(seed);
            var b = Buffer(seconds);
            // I - vi - IV - V in C, one chord per 4 s.
            float[][] chords =
            {
                new[] { 261.63f, 329.63f, 392.00f },
                new[] { 220.00f, 261.63f, 329.63f },
                new[] { 174.61f, 220.00f, 261.63f },
                new[] { 196.00f, 246.94f, 293.66f },
            };
            float[] scale = { 523.25f, 587.33f, 659.25f, 783.99f, 880.00f };
            float beat = 0.5f;
            int beats = (int)(seconds / beat);
            var melody = new int[beats];
            for (int i = 0; i < beats; i++) melody[i] = rng.NextFloat() < 0.55f ? rng.Range(0, scale.Length) : -1;

            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                int chord = (int)(t / (seconds / 4)) % 4;
                float pad = 0;
                foreach (var f in chords[chord])
                    pad += (float)(Math.Sin(2 * Math.PI * f * t) + 0.3 * Math.Sin(2 * Math.PI * f * 2.003 * t));
                pad *= 0.06f;

                int bi = (int)(t / beat);
                float bt = t - bi * beat;
                float pluck = 0;
                if (bi < beats && melody[bi] >= 0)
                    pluck = (float)Math.Sin(2 * Math.PI * scale[melody[bi]] * t) * (float)Math.Exp(-bt * 7) * 0.18f;
                float hat = (bi % 2 == 1) ? (rng.NextFloat() * 2 - 1) * (float)Math.Exp(-bt * 60) * 0.05f : 0;
                float kick = (bi % 4 == 0) ? (float)Math.Sin(2 * Math.PI * (50 + 80 * Math.Exp(-bt * 30)) * bt) * (float)Math.Exp(-bt * 12) * 0.25f : 0;
                b[i] = pad + pluck + hat + kick;
            }
            // Short crossfade at the loop point.
            int fade = Rate / 20;
            for (int i = 0; i < fade; i++)
            {
                float g = (float)i / fade;
                b[i] *= g;
                b[b.Length - 1 - i] *= g;
            }
            return Normalize(b, 0.5f);
        }
    }
}
