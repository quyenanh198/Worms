using System;
using Worms.Game.Core;
using Xunit;

namespace Worms.Game.Core.Tests
{
    public class SfxSynthTests
    {
        [Fact]
        public void EverySoundIsAudibleAndInRange()
        {
            foreach (Sfx sfx in Enum.GetValues(typeof(Sfx)))
            {
                var pcm = SfxSynth.Make(sfx);
                Assert.Equal(0, pcm.Length % SfxSynth.Channels);
                int frames = pcm.Length / SfxSynth.Channels;
                Assert.True(frames > SfxSynth.Rate / 50, sfx + " too short");
                Assert.True(frames < SfxSynth.Rate * 3, sfx + " too long");
                float peak = 0, energy = 0;
                foreach (var v in pcm)
                {
                    Assert.False(float.IsNaN(v) || float.IsInfinity(v), sfx + " has NaN");
                    peak = Math.Max(peak, Math.Abs(v));
                    energy += v * v;
                }
                Assert.InRange(peak, 0.2f, 1.0001f);
                Assert.True(energy > 1f, sfx + " is nearly silent");
            }
        }

        [Fact]
        public void SoundsAreDeterministic()
        {
            Assert.Equal(SfxSynth.Make(Sfx.ExplosionMedium), SfxSynth.Make(Sfx.ExplosionMedium));
        }

        [Fact]
        public void NoisySoundsAreWideStereo()
        {
            foreach (var sfx in new[] { Sfx.ExplosionLarge, Sfx.Splash, Sfx.Launch })
            {
                var s = SfxSynth.MakeStereo(sfx);
                double lr = 0, ll = 0, rr = 0;
                for (int i = 0; i < s.Length; i++)
                {
                    lr += s.L[i] * s.R[i];
                    ll += s.L[i] * s.L[i];
                    rr += s.R[i] * s.R[i];
                }
                double correlation = lr / Math.Sqrt(ll * rr);
                // Neither mono (1) nor two unrelated sounds (0).
                Assert.InRange(correlation, 0.2, 0.97);
            }
        }

        [Fact]
        public void MusicLoopsWithoutASeam()
        {
            var m = SfxSynth.MusicStereo(3);
            Assert.Equal(16 * SfxSynth.Rate, m.Length);
            float peak = 0, step = 0;
            for (int i = 0; i < m.Length; i++)
            {
                peak = Math.Max(peak, Math.Max(Math.Abs(m.L[i]), Math.Abs(m.R[i])));
                if (i > 0) step = Math.Max(step, Math.Max(Math.Abs(m.L[i] - m.L[i - 1]), Math.Abs(m.R[i] - m.R[i - 1])));
            }
            Assert.InRange(peak, 0.49f, 0.5001f);
            // Jumping from the last sample back to the first is no bigger than any step inside the tune.
            float seam = Math.Max(Math.Abs(m.L[0] - m.L[m.Length - 1]), Math.Abs(m.R[0] - m.R[m.Length - 1]));
            Assert.True(seam <= step, $"seam {seam} > largest step {step}");
            Assert.Equal(16 * SfxSynth.Rate * 2, SfxSynth.Music(3).Length);
        }
    }
}
