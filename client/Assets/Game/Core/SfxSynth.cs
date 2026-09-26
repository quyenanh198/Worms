using System;
using System.Collections.Generic;
using Worms.Sim;

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
    /// Every sound in the game is synthesized here at startup (docs/PLAN.md §3.11), on the
    /// client: the server only sends events. No audio files, so nothing to license.
    /// Sounds are stereo: noise layers are decorrelated between the ears, tonal layers are
    /// slightly detuned, voices go through formant filters, and each clip carries its own
    /// short room reverb. Output is interleaved stereo PCM (L, R, ...) in [-1, 1].
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 44100;
        public const int Channels = 2;

        public static float[] Make(Sfx sfx) { return MakeStereo(sfx).Interleave(); }

        public static Stereo MakeStereo(Sfx sfx)
        {
            uint seed = 0x5EED0000u + (uint)sfx;
            switch (sfx)
            {
                case Sfx.ExplosionSmall: return Explosion(seed, 0.5f, 1500f, 72f, 0.8f, 0.2f, 0.6f);
                case Sfx.ExplosionMedium: return Explosion(seed, 1.0f, 950f, 55f, 0.9f, 0.28f, 0.9f);
                case Sfx.ExplosionLarge: return Explosion(seed, 1.6f, 650f, 42f, 1f, 0.34f, 1.15f);
                case Sfx.Launch: return Launch(seed);
                case Sfx.Throw: return Whoosh(seed, 0.3f, 700f, 2200f, -0.3f, 0.3f, 0.5f);
                case Sfx.Shotgun: return Shotgun(seed);
                case Sfx.UziShot: return UziShot(seed);
                case Sfx.Swing: return Whoosh(seed, 0.28f, 500f, 2600f, -0.5f, 0.5f, 0.6f);
                case Sfx.Bonk: return Bonk(seed);
                case Sfx.AirRaid: return AirRaid(seed);
                case Sfx.Bounce: return Clank(seed);
                case Sfx.Jump: return Room(Voiced(0.17f, 430f, 560f, U, A, 0f, seed, 0.35f), 0.08f, 0.25f, 0.5f);
                case Sfx.Land: return Land(seed);
                case Sfx.Splash: return Splash(seed);
                case Sfx.Hurt: return Room(Voiced(0.34f, 700f, 460f, O, U, 0.05f, seed, 0.3f), 0.12f, 0.35f, 0.6f);
                case Sfx.Death: return Death(seed);
                case Sfx.TurnBell: return Chime();
                case Sfx.Tick: return Wood(0.09f, 1900f, 4300f, 0.35f, 0.05f);
                case Sfx.Click: return Wood(0.07f, 1300f, 3100f, 0.3f, 0.04f);
                default: return new Stereo(1);
            }
        }

        // ---- shared pieces -------------------------------------------------------------

        static int Frames(float seconds) { return Math.Max(1, (int)(seconds * Rate)); }

        static float Noise(Rng rng) { return rng.NextFloat() * 2f - 1f; }

        /// <summary>Adds a room tail, then normalizes.</summary>
        static Stereo Room(Stereo dry, float wet, float tailSeconds, float peak, float room = 0.75f)
        {
            var s = dry.Extended(Frames(tailSeconds)).DcBlock();
            new Reverb(room, 0.4f).Apply(s, wet);
            return s.Finish(peak);
        }

        /// <summary>
        /// Renders a noisy layer twice with different random streams (each with fresh filter
        /// state from <paramref name="voice"/>) and blends them so the sound is wide but keeps a center.
        /// </summary>
        static Stereo WideNoise(float seconds, uint seed, Func<Func<Rng, int, float>> voice)
        {
            var s = Stereo.Seconds(seconds);
            var a = voice();
            var b = voice();
            var ra = new Rng(seed * 31 + 1);
            var rb = new Rng(seed * 31 + 2);
            for (int i = 0; i < s.Length; i++)
            {
                float x = a(ra, i), y = b(rb, i);
                s.L[i] = x * 0.75f + y * 0.25f;
                s.R[i] = y * 0.75f + x * 0.25f;
            }
            return s;
        }

        /// <summary>A pitch drop: the "boom" under explosions and shots.</summary>
        static float[] Sub(float seconds, float from, float to, float speed, float decay)
        {
            var b = new float[Frames(seconds)];
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = to + (from - to) * (float)Math.Exp(-t * speed);
                phase += Dsp.TwoPi * f / Rate;
                b[i] = (float)Math.Sin(phase) * (float)Math.Exp(-t * decay) * Math.Min(1f, t * 1500f);
            }
            return b;
        }

        // ---- weapons -------------------------------------------------------------------

        /// <summary>Crack, filtered roar, sub boom, low rumble and a tail of falling debris.</summary>
        static Stereo Explosion(uint seed, float seconds, float cutoff, float boomHz, float peak, float wet, float tail)
        {
            var s = WideNoise(seconds, seed, () =>
            {
                float lp1 = 0, lp2 = 0, rum = 0, crackle = 0;
                return (rng, i) =>
                {
                    float t = (float)i / Rate, u = t / seconds;
                    float n = Noise(rng);
                    float k = Dsp.K(cutoff * (0.15f + 0.85f * (float)Math.Exp(-t * 6f / seconds)));
                    lp1 += (n - lp1) * k;
                    lp2 += (lp1 - lp2) * k;
                    float body = lp2 * 2.6f * (float)Math.Exp(-t * 4f / seconds) * Math.Min(1f, t * 800f);
                    float hit = (n - lp1) * (float)Math.Exp(-t * 260f) * 0.9f;
                    rum += (n - rum) * Dsp.K(90f);
                    float rumble = rum * 7f * (float)Math.Exp(-t * 2.2f / seconds) * Math.Min(1f, t * 30f);
                    if (u > 0.08f && rng.NextFloat() < 0.0012f * (1f - u)) crackle = 0.6f + rng.NextFloat() * 0.4f;
                    crackle *= 0.993f;
                    return body + hit + rumble + (n - lp1) * crackle * 0.45f;
                };
            });
            s.AddMono(Sub(seconds, boomHz * 2.4f, boomHz, 14f, 5f / seconds), 0f, 0.95f);
            s.Finish(1f, 1.8f);
            return Room(s, wet, tail, peak, 0.82f);
        }

        /// <summary>Ignition thump, then a rocket's hiss and growl.</summary>
        static Stereo Launch(uint seed)
        {
            const float seconds = 0.8f;
            var s = WideNoise(seconds, seed, () =>
            {
                var bp = new BandPass(2600f, 1.2f);
                float growl = 0;
                return (rng, i) =>
                {
                    float t = (float)i / Rate, u = t / seconds;
                    float n = Noise(rng);
                    if (i % 64 == 0) bp.Set(2600f - 1400f * u, 1.2f);
                    float env = Math.Min(1f, t * 50f) * (float)Math.Pow(1f - u, 1.5f);
                    growl += (n - growl) * Dsp.K(320f);
                    float am = 0.6f + 0.4f * (float)Math.Sin(Dsp.TwoPi * 31f * t);
                    return bp.Process(n) * 2.2f * env + growl * 4f * am * env;
                };
            });
            s.AddMono(Sub(0.3f, 140f, 55f, 20f, 16f), 0f, 0.8f);
            s.Finish(1f, 1.5f);
            return Room(s, 0.15f, 0.45f, 0.75f);
        }

        /// <summary>Air rushing past: band-passed noise sweeping up, moving across the stereo field.</summary>
        static Stereo Whoosh(uint seed, float seconds, float from, float to, float panFrom, float panTo, float peak)
        {
            var rng = new Rng(seed);
            var bp = new BandPass(from, 1.5f);
            var b = new float[Frames(seconds)];
            for (int i = 0; i < b.Length; i++)
            {
                float u = (float)i / b.Length;
                if (i % 64 == 0) bp.Set(from + (to - from) * u * u, 1.5f);
                b[i] = bp.Process(Noise(rng)) * (float)Math.Sin(Math.PI * u) * (1f - 0.3f * u);
            }
            var s = Stereo.Seconds(seconds);
            s.AddMoving(b, panFrom, panTo);
            return Room(s, 0.1f, 0.3f, peak);
        }

        static Stereo Shotgun(uint seed)
        {
            var s = WideNoise(0.3f, seed, () =>
            {
                float lp = 0, lpHi = 0;
                return (rng, i) =>
                {
                    float t = (float)i / Rate;
                    float n = Noise(rng);
                    lpHi += (n - lpHi) * Dsp.K(3000f);
                    lp += (n - lp) * Dsp.K(1300f * (0.3f + 0.7f * (float)Math.Exp(-t * 20f)));
                    return (n - lpHi) * (float)Math.Exp(-t * 120f) * 1.3f
                           + lp * 3f * (float)Math.Exp(-t * 9f) * Math.Min(1f, t * 3000f);
                };
            }).Extended(Frames(0.3f));
            s.AddMono(Sub(0.3f, 100f, 48f, 25f, 10f), 0f, 0.9f);
            s.Finish(1f, 2f);
            // Pump action: chk-chk.
            var r = new Rng(seed ^ 0x9A3Du);
            foreach (float at in new[] { 0.34f, 0.45f })
            {
                var click = new float[Frames(0.08f)];
                Dsp.AddMode(click, 1850f, 0.5f, 70f);
                Dsp.AddMode(click, 3300f, 0.35f, 90f);
                Dsp.AddMode(click, 5200f, 0.2f, 120f);
                for (int i = 0; i < Frames(0.004f); i++) click[i] += Noise(r) * 0.6f;
                s.AddMono(click, 0.15f, 0.35f, Frames(at));
            }
            return Room(s, 0.25f, 0.5f, 0.85f);
        }

        static Stereo UziShot(uint seed)
        {
            var s = WideNoise(0.12f, seed, () =>
            {
                float lp = 0, lpHi = 0;
                return (rng, i) =>
                {
                    float t = (float)i / Rate;
                    float n = Noise(rng);
                    lpHi += (n - lpHi) * Dsp.K(3500f);
                    lp += (n - lp) * Dsp.K(2500f);
                    return (n - lpHi) * (float)Math.Exp(-t * 170f) + lp * 2f * (float)Math.Exp(-t * 32f);
                };
            });
            s.AddMono(Sub(0.12f, 150f, 70f, 40f, 40f), 0f, 0.5f);
            s.Finish(1f, 1.6f);
            return Room(s, 0.12f, 0.22f, 0.55f);
        }

        /// <summary>A bat on a worm: a hollow wooden "tock".</summary>
        static Stereo Bonk(uint seed)
        {
            var b = new float[Frames(0.3f)];
            Dsp.AddMode(b, 520f, 1f, 28f);
            Dsp.AddMode(b, 1370f, 0.5f, 40f);
            Dsp.AddMode(b, 2410f, 0.3f, 60f);
            var rng = new Rng(seed);
            for (int i = 0; i < Frames(0.003f); i++) b[i] += Noise(rng) * 0.8f;
            var s = new Stereo(b.Length);
            s.AddMono(b);
            return Room(s, 0.15f, 0.3f, 0.9f);
        }

        /// <summary>Plane drone passing from left to right, then falling whistles.</summary>
        static Stereo AirRaid(uint seed)
        {
            var s = Stereo.Seconds(2.2f);
            var drone = new float[Frames(1.8f)];
            double p1 = 0, p2 = 0;
            float lp = 0;
            for (int i = 0; i < drone.Length; i++)
            {
                float t = (float)i / Rate;
                float doppler = 1f + 0.06f * (float)Math.Tanh((0.8f - t) * 3f);
                p1 += Dsp.TwoPi * 92f * doppler / Rate;
                p2 += Dsp.TwoPi * 95.5f * doppler / Rate;
                float v = 0;
                for (int h = 1; h <= 5; h++) v += (float)(Math.Sin(p1 * h) + Math.Sin(p2 * h)) / (h * 1.3f);
                lp += (v - lp) * Dsp.K(900f);
                float pass = (float)Math.Exp(-Math.Pow((t - 0.8f) / 0.55f, 2));
                float prop = 0.65f + 0.35f * (float)Math.Sin(Dsp.TwoPi * 24f * t);
                drone[i] = lp * pass * prop;
            }
            s.AddMoving(drone, -0.9f, 0.9f, 0.5f);

            var rng = new Rng(seed);
            float[] pans = { -0.4f, 0.1f, 0.5f };
            for (int w = 0; w < pans.Length; w++)
            {
                var whistle = new float[Frames(0.8f)];
                double phase = 0;
                for (int i = 0; i < whistle.Length; i++)
                {
                    float u = (float)i / whistle.Length;
                    float f = (1900f - 1000f * u) * (1f + 0.01f * (float)Math.Sin(Dsp.TwoPi * 6f * u));
                    phase += Dsp.TwoPi * f / Rate;
                    whistle[i] = ((float)Math.Sin(phase) + Noise(rng) * 0.08f) * Math.Min(1f, u * 6f) * (1f - u * 0.4f);
                }
                s.AddMono(whistle, pans[w], 0.22f, Frames(1.0f + w * 0.18f));
            }
            return Room(s, 0.18f, 0.5f, 0.6f);
        }

        /// <summary>A grenade hitting ground: small metallic clank over a dull thud.</summary>
        static Stereo Clank(uint seed)
        {
            var b = new float[Frames(0.25f)];
            Dsp.AddMode(b, 740f, 0.7f, 35f);
            Dsp.AddMode(b, 1980f, 0.45f, 45f);
            Dsp.AddMode(b, 3350f, 0.3f, 60f);
            Dsp.AddMode(b, 5100f, 0.15f, 80f);
            var thud = Sub(0.25f, 220f, 150f, 30f, 30f);
            var rng = new Rng(seed);
            for (int i = 0; i < b.Length; i++) b[i] += thud[i] * 0.7f + (i < Frames(0.004f) ? Noise(rng) * 0.5f : 0f);
            var s = new Stereo(b.Length);
            s.AddMono(b);
            return Room(s, 0.1f, 0.2f, 0.6f);
        }

        static Stereo Land(uint seed)
        {
            var s = WideNoise(0.25f, seed, () =>
            {
                float lp = 0;
                return (rng, i) =>
                {
                    float t = (float)i / Rate;
                    float n = Noise(rng);
                    lp += (n - lp) * Dsp.K(500f);
                    return lp * 3f * (float)Math.Exp(-t * 20f) + (n - lp) * 0.2f * (float)Math.Exp(-t * 35f);
                };
            });
            s.AddMono(Sub(0.25f, 110f, 55f, 25f, 25f), 0f, 1f);
            return Room(s, 0.06f, 0.2f, 0.7f);
        }

        static Stereo Splash(uint seed)
        {
            const float seconds = 0.9f;
            var s = WideNoise(seconds, seed, () =>
            {
                float lp = 0;
                return (rng, i) =>
                {
                    float t = (float)i / Rate, u = t / seconds;
                    float n = Noise(rng);
                    lp += (n - lp) * Dsp.K(2500f - 1800f * u);
                    return lp * 2f * Math.Min(1f, t * 150f) * (float)Math.Exp(-t * 4.5f) + (n - lp) * (float)Math.Exp(-t * 60f);
                };
            });
            var r = new Rng(seed ^ 0xB0Bu);
            for (int k = 0; k < 7; k++)
            {
                var bubble = new float[Frames(0.07f)];
                float f0 = r.Range(300f, 600f);
                double phase = 0;
                for (int i = 0; i < bubble.Length; i++)
                {
                    float bt = (float)i / Rate;
                    phase += Dsp.TwoPi * f0 * (1f + 12f * bt) / Rate;
                    bubble[i] = (float)Math.Sin(phase) * (1f - (float)i / bubble.Length);
                }
                s.AddMono(bubble, r.Range(-0.6f, 0.6f), 0.35f, Frames(r.Range(0.08f, 0.7f)));
            }
            return Room(s, 0.22f, 0.5f, 0.7f);
        }

        // ---- voices ----------------------------------------------------------------------

        // Formants (F1, F2) of a small cartoon creature: human vowels shifted up.
        static readonly (float, float) A = (950f, 1500f), O = (650f, 1150f), U = (450f, 1000f), I = (400f, 2800f);

        /// <summary>
        /// A tiny voice: a buzzing glottal source (saw plus breath) through three formant
        /// filters that glide from vowel <paramref name="v0"/> to <paramref name="v1"/>.
        /// </summary>
        static Stereo Voiced(float seconds, float p0, float p1, (float, float) v0, (float, float) v1, float vibrato, uint seed, float breath)
        {
            var b = new float[Frames(seconds)];
            var f1 = new BandPass(v0.Item1, 6f);
            var f2 = new BandPass(v0.Item2, 9f);
            var f3 = new BandPass(3600f, 10f);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate, u = (float)i / b.Length;
                float pitch = (p0 + (p1 - p0) * u) * (1f + vibrato * (float)Math.Sin(Dsp.TwoPi * 7f * t));
                phase += pitch / Rate;
                phase -= Math.Floor(phase);
                float src = (float)(2 * phase - 1) + Dsp.Hash(i, seed) * breath;
                if (i % 64 == 0)
                {
                    float m = u * u * (3f - 2f * u);
                    f1.Set(v0.Item1 + (v1.Item1 - v0.Item1) * m, 6f);
                    f2.Set(v0.Item2 + (v1.Item2 - v0.Item2) * m, 9f);
                }
                float y = f1.Process(src) + f2.Process(src) * 0.6f + f3.Process(src) * 0.2f;
                float env = Math.Min(1f, t * 80f) * (u > 0.7f ? (1f - u) / 0.3f : 1f);
                b[i] = y * env;
            }
            var s = new Stereo(b.Length);
            s.AddMono(b);
            return s;
        }

        /// <summary>"Bye-bye", with a little sigh in the pitch.</summary>
        static Stereo Death(uint seed)
        {
            var bye1 = Voiced(0.2f, 620f, 580f, A, I, 0.03f, seed, 0.25f);
            var bye2 = Voiced(0.32f, 540f, 360f, A, I, 0.05f, seed + 1, 0.25f);
            int gap = Frames(0.07f);
            var s = new Stereo(bye1.Length + gap + bye2.Length);
            s.Add(bye1);
            s.Add(bye2, 1f, bye1.Length + gap);
            return Room(s, 0.2f, 0.5f, 0.55f);
        }

        // ---- interface -------------------------------------------------------------------

        /// <summary>Your-turn chime: three glockenspiel notes rising, slightly chorused.</summary>
        static Stereo Chime()
        {
            var s = Stereo.Seconds(1.4f);
            float[] notes = { 1046.5f, 1318.5f, 1568f };
            float[] pans = { -0.3f, 0f, 0.3f };
            for (int n = 0; n < notes.Length; n++)
            {
                for (int ch = 0; ch < 2; ch++)
                {
                    var b = new float[Frames(1.2f)];
                    float f = notes[n] * (ch == 0 ? 1.0015f : 0.9985f);
                    Dsp.AddMode(b, f, 1f, 2.6f);
                    Dsp.AddMode(b, f * 2.76f, 0.45f, 5f);
                    Dsp.AddMode(b, f * 5.4f, 0.22f, 9f);
                    Dsp.AddMode(b, f * 8.93f, 0.1f, 14f);
                    float gain = ch == 0 ? 1f - pans[n] * 0.5f : 1f + pans[n] * 0.5f;
                    var target = ch == 0 ? s.L : s.R;
                    int at = Frames(n * 0.11f);
                    for (int i = 0; i < b.Length && i + at < target.Length; i++) target[i + at] += b[i] * gain;
                }
            }
            return Room(s, 0.35f, 0.75f, 0.45f, 0.85f);
        }

        /// <summary>A small wood block.</summary>
        static Stereo Wood(float seconds, float f1, float f2, float peak, float wet)
        {
            var b = new float[Frames(seconds)];
            Dsp.AddMode(b, f1, 1f, 55f);
            Dsp.AddMode(b, f2, 0.4f, 90f);
            var s = new Stereo(b.Length);
            s.AddMono(b);
            return Room(s, wet, 0.12f, peak);
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

        // ---- music -----------------------------------------------------------------------

        public static float[] Music(uint seed, float seconds = 16f) { return MusicStereo(seed, seconds).Interleave(); }

        static float Midi(int note) { return 440f * (float)Math.Pow(2, (note - 69) / 12.0); }

        // One chord per two bars: Cmaj9, Am9, Fmaj7, G6/9. Bass root, pad voicing, arpeggio notes (MIDI).
        static readonly int[] Roots = { 36, 33, 29, 31 };
        static readonly int[][] Pads =
        {
            new[] { 55, 60, 64, 71, 74 },
            new[] { 57, 60, 64, 67, 71 },
            new[] { 53, 57, 60, 64, 69 },
            new[] { 55, 59, 62, 64, 69 },
        };
        static readonly int[][] Arps =
        {
            new[] { 72, 76, 79, 83 },
            new[] { 69, 72, 76, 79 },
            new[] { 69, 72, 76, 77 },
            new[] { 67, 71, 74, 79 },
        };
        static readonly int[] Pentatonic = { 84, 86, 88, 91, 93 };
        static readonly int[] ArpOrder = { 0, 1, 2, 3, 2, 1, 2, 3 };
        static readonly int[] BassEighths = { 0, 3, 4, 6 };

        /// <summary>One cycle of a soft saw (six harmonics), indexed by phase * 4096.</summary>
        static readonly float[] PadWave = BuildPadWave();

        static float[] BuildPadWave()
        {
            var w = new float[4096];
            for (int i = 0; i < w.Length; i++)
            {
                double x = 2 * Math.PI * i / w.Length;
                double v = 0;
                for (int h = 1; h <= 6; h++) v += Math.Sin(x * h) / Math.Pow(h, 1.5);
                w[i] = (float)(v * 0.5);
            }
            return w;
        }

        /// <summary>
        /// A relaxed looping tune (8 bars at 120 bpm for 16 s): warm pad, plucked bass, a
        /// ping-pong arpeggio, a sparse bell melody and light drums, all in one stereo room.
        /// It is rendered twice and the second pass kept, so reverb and plucks that ring past
        /// the loop point wrap round to its start and the loop has no seam.
        /// </summary>
        public static Stereo MusicStereo(uint seed, float seconds = 16f)
        {
            int n = Frames(seconds);
            var mix = new Stereo(n * 2);
            var send = new Stereo(n * 2);
            var arpBus = new Stereo(n * 2);
            float beat = seconds / 32f;
            int bar = Frames(beat * 4);
            var rng = new Rng(seed);

            // The melody is chosen once so both passes are identical.
            var melody = new List<(int at, int note, float pan)>();
            for (int b = 0; b < 8; b++)
                for (int q = 0; q < 4; q++)
                    if (rng.NextFloat() < (q == 0 ? 0.5f : 0.22f))
                        melody.Add((b * bar + Frames(beat * q), Pentatonic[rng.Range(0, Pentatonic.Length)], rng.Range(-0.35f, 0.35f)));

            // Instrument notes are the same every time they play: render each once.
            var plucks = new Dictionary<(int, bool), float[]>();
            float[] PluckOf(int note, bool bass)
            {
                if (plucks.TryGetValue((note, bass), out var p)) return p;
                if (bass)
                {
                    p = Dsp.Pluck(Midi(note), 0.7f, 0.25f, (uint)note * 7u);
                    var sub = Sub(0.5f, Midi(note), Midi(note), 1f, 5f);
                    for (int i = 0; i < sub.Length; i++) p[i] = p[i] * 0.8f + sub[i] * 0.5f;
                }
                else p = Dsp.Pluck(Midi(note), 0.8f, 0.55f, (uint)note * 13u);
                plucks[(note, bass)] = p;
                return p;
            }
            var drums = DrumKit();

            for (int pass = 0; pass < 2; pass++)
            {
                int o = pass * n;
                for (int c = 0; c < 4; c++)
                {
                    int start = o + c * 2 * bar;
                    Pad(mix, send, Pads[c], start, 2 * bar);
                    for (int b = 0; b < 2; b++)
                    {
                        int barStart = start + b * bar;
                        // Bass on eighths 0, 3, 4 and 6 of the bar; the fifth on the last one.
                        foreach (int e in BassEighths)
                            mix.AddMono(PluckOf(Roots[c] + (e == 6 ? 7 : 0), true), 0f, 0.42f, barStart + Frames(beat * e / 2f));
                        // Arpeggio: eighths up and down the chord, alternating sides.
                        for (int e = 0; e < 8; e++)
                            arpBus.AddMono(PluckOf(Arps[c][ArpOrder[e]], false), e % 2 == 0 ? -0.4f : 0.4f,
                                           e % 2 == 0 ? 0.13f : 0.1f, barStart + Frames(beat * e / 2f));
                        Drums(mix, send, drums, barStart, beat, b + c * 2);
                    }
                }
                foreach (var m in melody)
                {
                    var bell = new float[Frames(1.5f)];
                    float f = Midi(m.note);
                    Dsp.AddMode(bell, f, 1f, 3f);
                    Dsp.AddMode(bell, f * 2.76f, 0.3f, 7f);
                    Dsp.AddMode(bell, f * 5.4f, 0.1f, 12f);
                    mix.AddMono(bell, m.pan, 0.07f, o + m.at);
                    send.AddMono(bell, m.pan, 0.1f, o + m.at);
                }
            }

            PingPong(arpBus, Frames(beat * 0.75f), 0.38f);
            mix.Add(arpBus);
            send.Add(arpBus, 0.7f);
            new Reverb(0.85f, 0.45f).Apply(mix, 1f, send);
            mix.DcBlock();

            var loop = new Stereo(n);
            Array.Copy(mix.L, n, loop.L, 0, n);
            Array.Copy(mix.R, n, loop.R, 0, n);
            return loop.Finish(0.5f, 1.3f);
        }

        /// <summary>Soft-saw chord, chorused (each ear slightly detuned), fading in and ringing into the next chord.</summary>
        static void Pad(Stereo mix, Stereo send, int[] notes, int start, int length)
        {
            int attack = Frames(0.6f), release = Frames(0.8f);
            var tmpL = new float[length + release];
            var tmpR = new float[length + release];
            foreach (int note in notes)
            {
                float f = Midi(note);
                double pl = 0, pr = 0.37;
                double dl = f * 1.0025 / Rate, dr = f * 0.9975 / Rate;
                for (int i = 0; i < tmpL.Length; i++)
                {
                    float env = Math.Min(1f, (float)i / attack) * (i < length ? 1f : 1f - (float)(i - length) / release);
                    tmpL[i] += PadWave[(int)(pl * 4096) & 4095] * env;
                    tmpR[i] += PadWave[(int)(pr * 4096) & 4095] * env;
                    pl += dl; pl -= Math.Floor(pl);
                    pr += dr; pr -= Math.Floor(pr);
                }
            }
            float k = Dsp.K(1600f), lpL = 0, lpR = 0;
            for (int i = 0; i < tmpL.Length; i++)
            {
                int j = start + i;
                if (j >= mix.Length) break;
                lpL += (tmpL[i] - lpL) * k;
                lpR += (tmpR[i] - lpR) * k;
                mix.L[j] += lpL * 0.035f;
                mix.R[j] += lpR * 0.035f;
                send.L[j] += lpL * 0.02f;
                send.R[j] += lpR * 0.02f;
            }
        }

        sealed class Kit
        {
            public float[] Kick, Snare, Hat, OpenHat;
        }

        static Kit DrumKit()
        {
            var kit = new Kit { Kick = Sub(0.35f, 150f, 48f, 28f, 9f), Snare = new float[Frames(0.25f)] };
            var bp = new BandPass(1900f, 0.8f);
            for (int i = 0; i < kit.Snare.Length; i++)
            {
                float t = (float)i / Rate;
                kit.Snare[i] = bp.Process(Dsp.Hash(i, 0x5A4Eu)) * 2.2f * (float)Math.Exp(-t * 18f)
                               + (float)Math.Sin(Dsp.TwoPi * 185f * t) * (float)Math.Exp(-t * 25f) * 0.5f;
            }
            kit.Hat = Hat(0.08f, 55f, 0x4A7u);
            kit.OpenHat = Hat(0.3f, 12f, 0x4A8u);
            return kit;
        }

        static float[] Hat(float seconds, float decay, uint salt)
        {
            var hat = new float[Frames(seconds)];
            float lp = 0;
            for (int i = 0; i < hat.Length; i++)
            {
                float x = Dsp.Hash(i, salt);
                lp += (x - lp) * Dsp.K(6500f);
                hat[i] = (x - lp) * (float)Math.Exp(-(float)i / Rate * decay);
            }
            return hat;
        }

        static void Drums(Stereo mix, Stereo send, Kit kit, int barStart, float beat, int barIndex)
        {
            // Kick on 1 and 3, plus the "and" of 3 every fourth bar.
            mix.AddMono(kit.Kick, 0f, 0.5f, barStart);
            mix.AddMono(kit.Kick, 0f, 0.5f, barStart + Frames(beat * 2f));
            if (barIndex % 4 == 3) mix.AddMono(kit.Kick, 0f, 0.4f, barStart + Frames(beat * 2.5f));
            // Snare on 2 and 4.
            foreach (float at in new[] { 1f, 3f })
            {
                mix.AddMono(kit.Snare, 0.05f, 0.2f, barStart + Frames(beat * at));
                send.AddMono(kit.Snare, 0.05f, 0.25f, barStart + Frames(beat * at));
            }
            // Swung eighth hats, the last one open.
            for (int e = 0; e < 8; e++)
            {
                float swing = e % 2 == 1 ? 0.12f : 0f;
                mix.AddMono(e == 7 ? kit.OpenHat : kit.Hat, 0.3f, e % 2 == 0 ? 0.06f : 0.04f, barStart + Frames(beat * (e / 2f + swing)));
            }
        }

        /// <summary>Echoes that bounce between the ears.</summary>
        static void PingPong(Stereo s, int delay, float feedback)
        {
            for (int i = delay; i < s.Length; i++)
            {
                s.L[i] += s.R[i - delay] * feedback;
                s.R[i] += s.L[i - delay] * feedback;
            }
        }
    }
}
