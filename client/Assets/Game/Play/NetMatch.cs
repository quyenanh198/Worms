using System;
using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Core;
using Worms.Game.Net;
using Worms.Game.UI;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.Play
{
    /// <summary>
    /// Online match view: renders the server's snapshots through the same
    /// presenter as the sandbox and sends this player's intents.
    /// </summary>
    public sealed class NetMatch : MonoBehaviour, IHudSource
    {
        public NetClient Net;

        public Snapshot Current { get; private set; }
        public MatchPresenter Presenter { get; private set; }
        public LocalControls Controls { get; } = new LocalControls();
        public bool IsLocalTurn => _match != null && !_match.IsSpectator && Current != null && Current.ActiveTeam == _match.YourTeam;
        public Action PlayAgain => () => Net.Session.Rematch();
        public Action Leave => () => Net.Session.LeaveRoom();

        public void SelectWeapon(WeaponId weapon)
        {
            Net.Session.SendIntent(new Intent { Kind = InputKind.Select, Weapon = weapon });
        }

        ClientMatch _match;
        readonly List<SimEvent> _due = new List<SimEvent>();
        readonly List<CellRect> _dirty = new List<CellRect>();
        readonly List<Intent> _intents = new List<Intent>();
        int _controlsForWorm = -1;

        public string TeamName(int team)
        {
            return _match != null && team >= 0 && team < _match.TeamNames.Count ? _match.TeamNames[team] : "Đội " + (team + 1);
        }

        void Update()
        {
            var m = Net.Session.Match;
            if (m == null) return;
            if (m != _match) Rebuild(m);

            _due.Clear();
            _dirty.Clear();
            m.Advance(Time.deltaTime, _due, _dirty);
            m.Buffer.Sample(out var from, out var to, out float alpha);
            Current = to;
            Presenter.SetSnapshots(from, to);
            Presenter.HandleEvents(_due);

            // Input follows the newest state the server sent, not the delayed render.
            var latest = m.Buffer.Latest;
            bool mine = !m.IsSpectator && latest.ActiveTeam == m.YourTeam;
            bool aiming = mine && latest.Phase == Phase.Aiming;
            bool canMove = mine && (latest.Phase == Phase.Aiming || latest.Phase == Phase.Retreat);
            if (aiming && latest.ActiveWorm != _controlsForWorm)
            {
                _controlsForWorm = latest.ActiveWorm;
                var worm = latest.FindWorm(latest.ActiveWorm);
                Controls.Reset(worm.HasValue ? worm.Value.Aim : 0.35f);
            }
            if (!mine) _controlsForWorm = -1;

            Controls.UseWeapon(latest.ActiveWeapon);
            _intents.Clear();
            Controls.Update(KeyboardInput.Read(Time.deltaTime, Presenter.Rig.Camera), aiming, canMove, _intents);
            foreach (var i in _intents) Net.Session.SendIntent(i);
            KeyboardInput.CameraControls(Presenter.Rig);

            Presenter.Render(alpha, IsLocalTurn, Controls.Aim);
        }

        void Rebuild(ClientMatch m)
        {
            if (Presenter != null) Destroy(Presenter.gameObject);
            _match = m;
            _controlsForWorm = -1;
            Presenter = new GameObject("Match").AddComponent<MatchPresenter>();
            Presenter.Init(m.Terrain, m.WaterLevel, m.Seed, m.Teams);
        }

        void OnDestroy()
        {
            if (Presenter != null) Destroy(Presenter.gameObject);
        }
    }
}
