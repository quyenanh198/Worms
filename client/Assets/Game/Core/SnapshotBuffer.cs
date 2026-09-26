using System;
using System.Collections.Generic;
using Worms.Protocol;

namespace Worms.Game.Core
{
    /// <summary>
    /// Holds recent server snapshots and a render clock that trails the newest
    /// one by <see cref="DelayTicks"/> (100 ms), so worms move smoothly between
    /// 20 Hz updates (docs/PLAN.md §3.4.1).
    /// </summary>
    public sealed class SnapshotBuffer
    {
        public const double DelayTicks = 6;
        const int Keep = 40;

        readonly List<Snapshot> _snaps = new List<Snapshot>();

        public double RenderTick { get; private set; }
        public Snapshot Latest => _snaps.Count > 0 ? _snaps[_snaps.Count - 1] : null;
        public int Count => _snaps.Count;

        public void Add(Snapshot s)
        {
            if (_snaps.Count > 0 && s.Tick <= Latest.Tick) return;
            if (_snaps.Count == 0) RenderTick = s.Tick - DelayTicks;
            _snaps.Add(s);
            if (_snaps.Count > Keep) _snaps.RemoveAt(0);
        }

        /// <summary>Moves the render clock by real time, easing toward Latest - Delay.</summary>
        public void Advance(float dt)
        {
            if (_snaps.Count == 0) return;
            RenderTick += dt * 60.0;
            double target = Latest.Tick - DelayTicks;
            double error = target - RenderTick;
            if (Math.Abs(error) > 30) RenderTick = target;           // far off (tab was hidden, lag spike): jump
            else RenderTick += error * Math.Min(1.0, dt * 2.0);        // otherwise drift back gently
            if (RenderTick > Latest.Tick) RenderTick = Latest.Tick;
            if (RenderTick < _snaps[0].Tick) RenderTick = _snaps[0].Tick;
        }

        /// <summary>The two snapshots around the render clock and how far between them it is.</summary>
        public void Sample(out Snapshot from, out Snapshot to, out float alpha)
        {
            from = to = Latest;
            alpha = 0;
            if (_snaps.Count == 0) return;
            for (int i = _snaps.Count - 1; i >= 0; i--)
            {
                if (_snaps[i].Tick > RenderTick) continue;
                from = _snaps[i];
                to = i + 1 < _snaps.Count ? _snaps[i + 1] : _snaps[i];
                alpha = to.Tick == from.Tick ? 0f : (float)((RenderTick - from.Tick) / (to.Tick - from.Tick));
                return;
            }
            from = to = _snaps[0];
        }
    }
}
