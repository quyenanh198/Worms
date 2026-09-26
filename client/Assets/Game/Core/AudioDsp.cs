using System;

namespace Worms.Game.Core
{
    /// <summary>A stereo buffer being synthesized: separate left and right channels.</summary>
    public sealed class Stereo
    {
        public readonly float[] L, R;
        public int Length => L.Length;

        public Stereo(int frames)
        {
            L = new float[Math.Max(1, frames)];
            R = new float[Math.Max(1, frames)];
        }

        public static Stereo Seconds(float seconds) { return new Stereo((int)(seconds * SfxSynth.Rate)); }

        /// <summary>Adds a mono signal at a fixed position (-1 left .. 1 right), constant power.</summary>
        public void AddMono(float[] mono, float pan = 0f, float gain = 1f, int offset = 0)
        {
            float a = (Math.Max(-1f, Math.Min(1f, pan)) + 1f) * (float)Math.PI / 4f;
            float gl = (float)Math.Cos(a) * 1.4142f * gain, gr = (float)Math.Sin(a) * 1.4142f * gain;
            for (int i = 0; i < mono.Length; i++)
            {
                int j = i + offset;
                if (j < 0) continue;
                if (j >= L.Length) break;
                L[j] += mono[i] * gl;
                R[j] += mono[i] * gr;
            }
        }

        /// <summary>Adds a mono signal whose position moves linearly from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public void AddMoving(float[] mono, float from, float to, float gain = 1f, int offset = 0)
        {
            for (int i = 0; i < mono.Length; i++)
            {
                int j = i + offset;
                if (j < 0) continue;
                if (j >= L.Length) break;
                float pan = from + (to - from) * i / mono.Length;
                float a = (pan + 1f) * (float)Math.PI / 4f;
                L[j] += mono[i] * (float)Math.Cos(a) * 1.4142f * gain;
                R[j] += mono[i] * (float)Math.Sin(a) * 1.4142f * gain;
            }
        }

        public void Add(Stereo other, float gain = 1f, int offset = 0)
        {
            for (int i = 0; i < other.Length; i++)
            {
                int j = i + offset;
                if (j < 0) continue;
                if (j >= L.Length) break;
                L[j] += other.L[i] * gain;
                R[j] += other.R[i] * gain;
            }
        }

        /// <summary>A copy <paramref name="extraFrames"/> longer (silence at the end), for reverb tails.</summary>
        public Stereo Extended(int extraFrames)
        {
            var s = new Stereo(Length + extraFrames);
            Array.Copy(L, s.L, Length);
            Array.Copy(R, s.R, Length);
            return s;
        }

        /// <summary>Removes any DC offset / sub-audible drift (a 15 Hz high-pass), in place.</summary>
        public Stereo DcBlock()
        {
            const float r = 0.9979f; // 1 - 2π·15/44100
            float xl = 0, yl = 0, xr = 0, yr = 0;
            for (int i = 0; i < Length; i++)
            {
                yl = L[i] - xl + r * yl; xl = L[i]; L[i] = yl;
                yr = R[i] - xr + r * yr; xr = R[i]; R[i] = yr;
            }
            return this;
        }

        /// <summary>Gentle saturation, then scales so the louder channel peaks at <paramref name="peak"/>.</summary>
        public Stereo Finish(float peak, float drive = 1f)
        {
            if (drive > 1f)
            {
                float norm = 1f / (float)Math.Tanh(drive);
                for (int i = 0; i < Length; i++)
                {
                    L[i] = (float)Math.Tanh(L[i] * drive) * norm;
                    R[i] = (float)Math.Tanh(R[i] * drive) * norm;
                }
            }
            float max = 1e-6f;
            for (int i = 0; i < Length; i++) max = Math.Max(max, Math.Max(Math.Abs(L[i]), Math.Abs(R[i])));
            float g = peak / max;
            for (int i = 0; i < Length; i++)
            {
                L[i] *= g;
                R[i] *= g;
            }
            return this;
        }

        /// <summary>L, R, L, R, ... as Unity's <c>AudioClip.SetData</c> wants for two channels.</summary>
        public float[] Interleave()
        {
            var o = new float[Length * 2];
            for (int i = 0; i < Length; i++)
            {
                o[i * 2] = L[i];
                o[i * 2 + 1] = R[i];
            }
            return o;
        }
    }

    /// <summary>Small signal-processing building blocks for <see cref="SfxSynth"/>.</summary>
    public static class Dsp
    {
        public const float TwoPi = (float)(2 * Math.PI);

        /// <summary>One-pole low-pass coefficient for a cutoff in Hz.</summary>
        public static float K(float cutoff) { return 1f - (float)Math.Exp(-TwoPi * Math.Max(1f, cutoff) / SfxSynth.Rate); }

        /// <summary>Deterministic white noise in [-1, 1] from an integer (periodic sources use the sample index).</summary>
        public static float Hash(int i, uint salt)
        {
            uint x = (uint)i * 0x9E3779B1u ^ salt * 0x85EBCA77u;
            x ^= x >> 15; x *= 0x2C1B3C6Du; x ^= x >> 12; x *= 0x297A2D39u; x ^= x >> 15;
            return (x & 0xFFFFFF) / (float)0x800000 - 1f;
        }

        /// <summary>
        /// A resonant partial: a sine at <paramref name="freq"/> decaying at <paramref name="decay"/>
        /// per second. Sums of a few give bells, wood and metal.
        /// </summary>
        public static void AddMode(float[] b, float freq, float amp, float decay, int offset = 0)
        {
            double w = TwoPi * freq / SfxSynth.Rate;
            for (int i = 0; i + offset < b.Length; i++)
            {
                float t = (float)i / SfxSynth.Rate;
                float env = (float)Math.Exp(-t * decay);
                if (env < 1e-4f) break;
                b[i + offset] += (float)Math.Sin(w * i) * amp * env * Math.Min(1f, i / 30f);
            }
        }

        /// <summary>Plucked string (Karplus-Strong): warm, natural decay, cheap.</summary>
        public static float[] Pluck(float freq, float seconds, float brightness, uint seed)
        {
            var b = new float[(int)(seconds * SfxSynth.Rate)];
            int n = Math.Max(2, (int)Math.Round(SfxSynth.Rate / freq));
            var line = new float[n];
            float lp = 0;
            float mean = 0;
            for (int i = 0; i < n; i++)
            {
                lp += (Hash(i, seed) - lp) * brightness; // softer attack for lower brightness
                line[i] = lp;
                mean += lp / n;
            }
            for (int i = 0; i < n; i++) line[i] -= mean; // a string has no DC
            float decay = 0.996f;
            int p = 0;
            for (int i = 0; i < b.Length; i++)
            {
                int q = (p + 1) % n;
                float v = line[p];
                line[p] = (line[p] + line[q]) * 0.5f * decay;
                b[i] = v;
                p = q;
            }
            return b;
        }
    }

    /// <summary>RBJ band-pass biquad (constant 0 dB peak gain): formants and resonances.</summary>
    public sealed class BandPass
    {
        float _b0, _b2, _a1, _a2, _x1, _x2, _y1, _y2;

        public BandPass(float freq, float q) { Set(freq, q); }

        public void Set(float freq, float q)
        {
            float w = Dsp.TwoPi * Math.Min(freq, SfxSynth.Rate * 0.45f) / SfxSynth.Rate;
            float alpha = (float)Math.Sin(w) / (2f * q);
            float a0 = 1f + alpha;
            _b0 = alpha / a0;
            _b2 = -alpha / a0;
            _a1 = -2f * (float)Math.Cos(w) / a0;
            _a2 = (1f - alpha) / a0;
        }

        public float Process(float x)
        {
            float y = _b0 * x + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1; _x1 = x;
            _y2 = _y1; _y1 = y;
            return y;
        }
    }

    /// <summary>
    /// Freeverb-style stereo room (8 damped combs and 4 all-passes per side, the right side
    /// slightly detuned so the tail is wide). Baked into every clip at startup.
    /// </summary>
    public sealed class Reverb
    {
        static readonly int[] Combs = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
        static readonly int[] AllPasses = { 556, 441, 341, 225 };
        const int Spread = 23;

        readonly float[][] _combL, _combR, _apL, _apR;
        readonly int[] _cIdxL, _cIdxR, _aIdxL, _aIdxR;
        readonly float[] _storeL, _storeR;
        readonly float _feedback, _damp;

        public Reverb(float room = 0.8f, float damp = 0.35f)
        {
            _feedback = 0.7f + 0.28f * room;
            _damp = damp;
            _combL = Lines(Combs, 0); _combR = Lines(Combs, Spread);
            _apL = Lines(AllPasses, 0); _apR = Lines(AllPasses, Spread);
            _cIdxL = new int[Combs.Length]; _cIdxR = new int[Combs.Length];
            _aIdxL = new int[AllPasses.Length]; _aIdxR = new int[AllPasses.Length];
            _storeL = new float[Combs.Length]; _storeR = new float[Combs.Length];
        }

        static float[][] Lines(int[] sizes, int spread)
        {
            var lines = new float[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++) lines[i] = new float[sizes[i] + spread];
            return lines;
        }

        float Side(float input, float[][] combs, int[] cIdx, float[] store, float[][] aps, int[] aIdx)
        {
            float o = 0;
            for (int c = 0; c < combs.Length; c++)
            {
                var line = combs[c];
                int i = cIdx[c];
                float y = line[i];
                store[c] = y * (1f - _damp) + store[c] * _damp;
                line[i] = input + store[c] * _feedback;
                cIdx[c] = (i + 1) % line.Length;
                o += y;
            }
            for (int a = 0; a < aps.Length; a++)
            {
                var line = aps[a];
                int i = aIdx[a];
                float b = line[i];
                line[i] = o + b * 0.5f;
                aIdx[a] = (i + 1) % line.Length;
                o = b - o;
            }
            return o;
        }

        /// <summary>Adds <paramref name="wet"/> of the room's response to <paramref name="s"/>, in place.</summary>
        public void Apply(Stereo s, float wet, Stereo send = null)
        {
            send = send ?? s;
            float g = wet * 0.04f; // Freeverb scaling: 0.015 into the combs, 3x on the way out
            for (int i = 0; i < s.Length; i++)
            {
                float input = (send.L[i] + send.R[i]) * 0.5f;
                s.L[i] += Side(input, _combL, _cIdxL, _storeL, _apL, _aIdxL) * g;
                s.R[i] += Side(input, _combR, _cIdxR, _storeR, _apR, _aIdxR) * g;
            }
        }
    }
}
