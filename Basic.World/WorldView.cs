using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Core.Physics;
using World.Rendering;

namespace Basic.World
{
    // The world (see BuiltWorld) as drawn: the terrain a chunk at a time round the camera, the water, the
    // buildings, what's built into the ground, and the things lying about. Each frame it's gathered into a
    // MeshBatch, which leaves out whatever's not in view.
    public sealed class WorldView : IDisposable
    {
        private readonly List<MeshInstance> _fixed = new List<MeshInstance>();   // the water, the street's road, fences, paving and billboard
        private readonly List<(MeshInstance view, Func<Vector3, bool> shownTo)> _nearOrFar = new List<(MeshInstance, Func<Vector3, bool>)>();   // fixtures seen only from some places
        private readonly List<BuildingView> _buildings = new List<BuildingView>();
        private readonly List<(Body body, MeshInstance view, float turn)> _things = new List<(Body, MeshInstance, float)>();
        private readonly List<(Window window, (ScenePart part, MeshInstance view)[] beyond, MeshBatch batch)> _windows =
            new List<(Window, (ScenePart, MeshInstance)[], MeshBatch)>();
        private readonly List<(Window window, MeshBatch batch, (ScenePart part, MeshInstance view)[] beyond, float distance)> _open =
            new List<(Window, MeshBatch, (ScenePart, MeshInstance)[], float)>();
        private readonly List<(ScenePart part, MeshInstance view)> _moving = new List<(ScenePart, MeshInstance)>();
        private readonly BoundingFrustum _frustum = new BoundingFrustum(Matrix.Identity);
        private readonly List<Vector3> _cameras = new List<Vector3>();
        private readonly WindowPortals _portals;

        public TerrainView Terrain { get; }

        // `drawDistance` is how far out the terrain's built: out to where the fog has hidden it all.
        public WorldView(BuiltWorld world, GraphicsDevice device, MeshCache cache, float drawDistance)
        {
            Terrain = new TerrainView(world.Terrain, shore: 0.5f, drawDistance, bare: world.Bare);

            var pools = world.Terrain.Pools;
            for (var i = 0; i < pools.Count; i++)
            {
                var pool = pools[i];
                _fixed.Add(cache.CreateInstance(device, new MeshSource("water" + i, d => WaterMesh.Build(d, pool), WaterMesh.Palette())));
            }
            foreach (var fixture in world.Fixtures)
            {
                var view = cache.CreateInstance(device, fixture.Mesh);
                view.Transform = fixture.Transform;
                if (fixture.ShownTo is { } shownTo)
                    _nearOrFar.Add((view, shownTo));
                else
                    _fixed.Add(view);
            }
            foreach (var building in world.Buildings)
                _buildings.Add(new BuildingView(building, world.Ground.DoorsOf(building), device, cache));
            foreach (var thing in world.Things)
                _things.Add((thing.Body, cache.CreateInstance(device, thing.Mesh), thing.Turn));
            foreach (var part in world.Moving)
                _moving.Add((part, cache.CreateInstance(device, part.Mesh)));
            foreach (var window in world.Windows)
                _windows.Add((window, window.Beyond.Select(part => (part, cache.CreateInstance(device, part.Mesh))).ToArray(), new MeshBatch()));
            _portals = new WindowPortals(device);
        }

        // Builds the terrain that's come within reach of the camera (all of it at once, if `all`), and of where it'd be
        // looking out of wherever a window you're near enough to have open looks onto the world from (see Window.Onto).
        public void Update(GraphicsDevice device, Vector3 camera, Vector3 you, bool all = false)
        {
            _cameras.Clear();
            _cameras.Add(camera);
            foreach (var (window, _, _) in _windows)
                if (window.Onto != null && window.Open(you))
                    _cameras.Add(window.Through(camera));
            Terrain.Update(device, _cameras, all);
        }

        // `eye` is where it's seen from, `you` where you're standing (see Fixture.ShownTo), `seconds` how long it's been going.
        public void Collect(MeshBatch batch, Vector3 eye, Vector3 you, float seconds)
        {
            Terrain.Collect(batch);
            foreach (var view in _fixed)
                batch.Add(view);
            foreach (var (view, shownTo) in _nearOrFar)
                if (shownTo(you))
                    batch.Add(view);
            foreach (var (part, view) in _moving)
            {
                view.Transform = part.At(seconds);
                batch.Add(view);
            }
            foreach (var building in _buildings)
                building.Collect(batch, eye);
            foreach (var (body, view, turn) in _things)
            {
                view.Transform = Matrix.CreateRotationY(turn) * body.Pose;   // upright, on its side, or part way over
                batch.Add(view);
            }
        }

        // Before the world, with the effect's view and projection set for it: what's seen through each window you're
        // near enough to have open, `seconds` in (see WindowPortals). The world's drawn over it after.
        public void DrawWindows(GraphicsDevice device, BasicEffect effect, Vector3 eye, Vector3 you, float seconds, Color background, bool colorsOn)
        {
            _frustum.Matrix = effect.View * effect.Projection;
            _open.Clear();
            foreach (var (window, beyond, batch) in _windows)
                if (window.Open(you) && window.Faces(eye) && _frustum.Intersects(BoundingBox.CreateFromPoints(window.Corners())))
                    _open.Add((window, batch, beyond, Vector3.Distance(eye, window.Centre)));
            if (_open.Count == 0)
                return;

            _open.Sort((a, b) => b.distance.CompareTo(a.distance));   // furthest first
            foreach (var (window, batch, beyond, _) in _open)
            {
                var scene = window.Scene;
                batch.Begin(scene * effect.View, effect.Projection);
                if (window.Onto != null)
                    Collect(batch, window.Through(eye), window.Through(you), seconds);   // the world, from there
                foreach (var (part, view) in beyond)
                {
                    view.Transform = part.At(seconds);
                    batch.Add(view);
                }
                _portals.Draw(device, effect, window.Corners(), scene, batch, window.Pane, window.Opacity(you),
                    window.Tint, window.TintStrength, background, colorsOn);
            }
            _portals.Seal(device, effect, _open.Select(w => w.window.Corners()));
        }

        public void Dispose()
        {
            Terrain.Dispose();
            _portals.Dispose();
        }
    }
}
