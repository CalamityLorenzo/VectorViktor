using MeshLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core.Physics;

namespace Basic.World
{
    // The world (see BuiltWorld) as drawn: the terrain a chunk at a time round the camera, the water, the
    // buildings, what's built into the ground, and the things lying about. Each frame it's gathered into a
    // MeshBatch, which leaves out whatever's not in view.
    public sealed class WorldView : IDisposable
    {
        private readonly List<MeshInstance> _fixed = new List<MeshInstance>();   // the water, the street's road, fences, paving and billboard
        private readonly List<BuildingView> _buildings = new List<BuildingView>();
        private readonly List<(Body body, MeshInstance view, float turn)> _things = new List<(Body, MeshInstance, float)>();

        public TerrainView Terrain { get; }

        // `drawDistance` is how far out the terrain's built: out to where the fog has hidden it all.
        public WorldView(BuiltWorld world, GraphicsDevice device, MeshCache cache, float drawDistance)
        {
            Terrain = new TerrainView(world.Terrain, shore: 0.5f, drawDistance, bare: world.Bare);

            var pools = world.Terrain.Pools;
            for (var i = 0; i < pools.Count; i++)
            {
                var pool = pools[i];
                _fixed.Add(new MeshInstance(cache.GetOrAdd(device, "water" + i, d => WaterMesh.Build(d, pool)), WaterMesh.Palette()));
            }
            foreach (var fixture in world.Fixtures)
                _fixed.Add(new MeshInstance(cache.GetOrAdd(device, fixture.Key, fixture.Build), fixture.Palette) { Transform = fixture.Transform });
            foreach (var building in world.Buildings)
                _buildings.Add(new BuildingView(building, world.Ground.DoorsOf(building), device, cache));
            foreach (var thing in world.Things)
                _things.Add((thing.Body, new MeshInstance(cache.GetOrAdd(device, thing.Key, thing.Build), thing.Palette), thing.Turn));
        }

        // Builds the terrain that's come within reach of the camera (all of it at once, if `all`).
        public void Update(GraphicsDevice device, Vector3 camera, bool all = false) => Terrain.Update(device, camera, all);

        public void Collect(MeshBatch batch, Vector3 eye)
        {
            Terrain.Collect(batch);
            foreach (var view in _fixed)
                batch.Add(view);
            foreach (var building in _buildings)
                building.Collect(batch, eye);
            foreach (var (body, view, turn) in _things)
            {
                view.Transform = Matrix.CreateRotationY(turn) * body.Pose;   // upright, on its side, or part way over
                batch.Add(view);
            }
        }

        public void Dispose() => Terrain.Dispose();
    }
}
