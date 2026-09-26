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
                Assert.True(pcm.Length > SfxSynth.Rate / 50, sfx + " too short");
                Assert.True(pcm.Length < SfxSynth.Rate * 3, sfx + " too long");
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
        public void MusicLoopsCleanly()
        {
            var m = SfxSynth.Music(3);
            Assert.Equal(16 * SfxSynth.Rate, m.Length);
            Assert.True(Math.Abs(m[0]) < 0.01f && Math.Abs(m[m.Length - 1]) < 0.01f);
            float peak = 0;
            foreach (var v in m) peak = Math.Max(peak, Math.Abs(v));
            Assert.InRange(peak, 0.49f, 0.5001f);
        }
    }
}
