using System;
using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Core;
using Worms.Game.UI;
using Worms.Protocol;
using Worms.Sim;

namespace Worms.Game.Play
{
    /// <summary>
    /// Offline match on this device: runs the same World the server runs, at
    /// 60 Hz, and every team is played from this keyboard (docs/PLAN.md P2).
    /// </summary>
    public sealed class SandboxMatch : MonoBehaviour, IHudSource
    {
        public World World { get; private set; }
        public MatchPresenter Presenter { get; private set; }
        public LocalControls Controls { get; } = new LocalControls();
        public TouchInput Touch { get; } = new TouchInput();

        readonly List<Intent> _intents = new List<Intent>();
        readonly List<SimInput> _pending = new List<SimInput>();
        Snapshot _prev = new Snapshot(), _cur = new Snapshot();
        float _accumulator;
        int _lastActiveWorm = -1;

        public void Begin(uint seed, int teams, int wormsPerTeam)
        {
            World = new World(new MatchSetup { Seed = seed, Teams = teams, WormsPerTeam = wormsPerTeam });
            Presenter = new GameObject("Match").AddComponent<MatchPresenter>();
            Presenter.Init(World.Terrain, World.WaterLevel, seed, teams);
            Snapshot.FromWorld(World, _cur);
            Snapshot.FromWorld(World, _prev);
            Presenter.SetSnapshots(_prev, _cur);
        }

        void Update()
        {
            if (World == null) return;

            bool aiming = World.Phase == Phase.Aiming;
            bool canMove = aiming || World.Phase == Phase.Retreat;
            if (World.ActiveWorm != _lastActiveWorm && aiming)
            {
                _lastActiveWorm = World.ActiveWorm;
                Controls.Reset(0.35f);
            }
            Controls.UseWeapon(World.SelectedWeapon[Math.Max(0, World.ActiveTeam)]);
            _intents.Clear();
            var frame = KeyboardInput.Read(Time.deltaTime, Presenter.Rig.Camera);
            var active = World.Active;
            Touch.Apply(ref frame, TouchInput.Context(aiming, Controls, Presenter.Actors.WormPosition(World.ActiveWorm), active != null ? active.Facing : 1, Presenter.Rig.Camera), Presenter.Rig);
            Controls.Update(frame, aiming, canMove, _intents);
            foreach (var i in _intents)
            {
                _pending.Add(new SimInput
                {
                    Team = World.ActiveTeam, Kind = i.Kind, Dir = i.Dir, Angle = i.Angle,
                    Power = i.Power, Fuse = i.Fuse, Weapon = i.Weapon, TargetX = i.TargetX, TargetY = i.TargetY,
                });
            }
            KeyboardInput.CameraControls(Presenter.Rig);

            _accumulator = Mathf.Min(_accumulator + Time.deltaTime, 0.25f);
            while (_accumulator >= C.Dt)
            {
                _accumulator -= C.Dt;
                var events = World.Step(_pending);
                _pending.Clear();
                (_prev, _cur) = (_cur, _prev);
                Snapshot.FromWorld(World, _cur);
                Presenter.SetSnapshots(_prev, _cur);
                Presenter.HandleEvents(events);
            }
            Presenter.Render(_accumulator / C.Dt, true, Controls.Aim);
        }

        public Snapshot Current => _cur;
        public bool IsLocalTurn => true;
        public string TeamName(int team) { return "Đội " + (team + 1); }
        public Action PlayAgain => Restart;

        public void SelectWeapon(WeaponId weapon)
        {
            _pending.Add(new SimInput { Team = World.ActiveTeam, Kind = InputKind.Select, Weapon = weapon });
        }
        public Action Leave { get; set; }

        public void Restart()
        {
            uint seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            int teams = World.TeamCount;
            int perTeam = World.Worms.Count / teams;
            Destroy(Presenter.gameObject);
            World = null;
            _lastActiveWorm = -1;
            Begin(seed, teams, perTeam);
        }
    }
}
