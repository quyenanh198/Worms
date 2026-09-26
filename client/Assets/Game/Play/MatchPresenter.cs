using System.Collections.Generic;
using UnityEngine;
using Worms.Game.Render;
using Worms.Protocol;
using Worms.Sim;
using SimTerrain = Worms.Sim.Terrain;

namespace Worms.Game.Play
{
    /// <summary>
    /// Draws a match from snapshots and events, wherever they come from (local
    /// sandbox now, the server from P3 on). Owns the scene objects.
    /// </summary>
    public sealed class MatchPresenter : MonoBehaviour
    {
        public TerrainView Terrain { get; private set; }
        public ActorViews Actors { get; private set; }
        public CameraRig Rig { get; private set; }
        public Light Sun { get; private set; }
        public Snapshot Previous { get; private set; }
        public Snapshot Current { get; private set; }
        public float WaterLevel { get; private set; }
        public int TeamCount { get; private set; }

        public void Init(SimTerrain terrain, float waterLevel, uint seed, int teamCount)
        {
            WaterLevel = waterLevel;
            TeamCount = teamCount;
            var theme = Theme.ForSeed(seed);
            float mapWidth = terrain.Width * WorldSpace.Scale;
            float waterY = -waterLevel * WorldSpace.Scale;

            Sun = SceneBuilder.CreateSun(transform, theme);
            SceneBuilder.ConfigureEnvironment(theme, Sun);
            SceneBuilder.CreateBackdrop(transform, theme, seed, mapWidth, waterY);
            SceneBuilder.CreateWater(transform, theme, mapWidth, waterY);

            Terrain = new GameObject("Terrain").AddComponent<TerrainView>();
            Terrain.transform.SetParent(transform, false);
            Terrain.Init(terrain, Materials.Terrain(theme));

            Actors = new GameObject("Actors").AddComponent<ActorViews>();
            Actors.transform.SetParent(transform, false);

            var camGo = new GameObject("Match Camera");
            camGo.transform.SetParent(transform, false);
            Rig = camGo.AddComponent<CameraRig>();
            // Focus stays between the sea and the top of the map.
            float focusBottom = waterY + 4f;
            Rig.Init(new Rect(0, focusBottom, mapWidth, -2f - focusBottom));
            UrpSetup.ConfigureCamera(Rig.Camera);
            UrpSetup.CreateVolume(transform);
            UrpSetup.ApplyTier(QualityTier.High, Sun);
        }

        public void SetSnapshots(Snapshot previous, Snapshot current)
        {
            Previous = previous;
            Current = current;
        }

        public void HandleEvents(List<SimEvent> events)
        {
            foreach (var e in events)
            {
                if (e.Type == SimEventType.Explode) Terrain.RefreshCircle(e.X, e.Y, e.Value);
            }
        }

        /// <summary>Called every frame after the snapshots are up to date.</summary>
        public void Render(float alpha, bool localTurn, float localAim)
        {
            if (Current == null) return;
            Actors.Render(Previous, Current, alpha, localTurn, localAim);
            Rig.Follow(FollowPoint(alpha));
        }

        Vector3? FollowPoint(float alpha)
        {
            if (Current.Projectiles.Count > 0)
            {
                var p = Current.Projectiles[0];
                return WorldSpace.ToWorld(p.X, p.Y, 0);
            }
            var worm = Current.FindWorm(Current.ActiveWorm);
            if (worm.HasValue) return WorldSpace.ToWorld(worm.Value.X, worm.Value.Y, 0);
            return null;
        }
    }
}
