using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core;

namespace World.Rendering
{
    // The terrain as drawn: a mesh for each chunk (see Terrain.ChunkCellsOf), built as the camera comes
    // within DrawDistance of it - nearest first, and no more than BuildsPerFrame a frame, so walking on
    // doesn't stutter - drawn only if it's in view (see MeshBatch), and thrown away once the camera's more
    // than DropDistance off. So only the country round you is ever built or drawn, however big the world. There
    // can be more than one camera: one looking through a window onto somewhere else in the world, too. There
    // can be more than one camera: one looking through a window onto somewhere else in the world, too.
    public sealed class TerrainView : IDisposable
    {
        public const int BuildsPerFrame = 4;

        private readonly Terrain _terrain;
        private readonly float _shore;
        private readonly Func<float, float, bool>? _bare;  // where the ground gets no grid lines (see TerrainMesh)
        private readonly Color[] _palette = TerrainMesh.Palette();
        private readonly Dictionary<(int ci, int cj), (MeshData mesh, MeshInstance view)> _chunks =
            new Dictionary<(int, int), (MeshData, MeshInstance)>();

        // Kept from one frame to the next, so looking round costs no garbage
        private readonly List<(float distance, int ci, int cj)> _wanted = new List<(float, int, int)>();
        private readonly List<(int, int)> _gone = new List<(int, int)>();

        public float DrawDistance { get; }
        public float DropDistance => DrawDistance + 40f;

        // How many chunks are built now, and how many were in view last time they were drawn.
        public int Built => _chunks.Count;
        public int Drawn { get; private set; }

        // How long building them has taken, all told.
        public TimeSpan BuildTime => _buildTime.Elapsed;
        private readonly System.Diagnostics.Stopwatch _buildTime = new System.Diagnostics.Stopwatch();

        public TerrainView(Terrain terrain, float shore, float drawDistance, Func<float, float, bool>? bare = null)
        {
            _terrain = terrain;
            _shore = shore;
            DrawDistance = drawDistance;
            _bare = bare;
        }

        // How far a chunk's patch of ground is from the nearest camera, across the ground (0 if it's over it).
        private float Distance(int ci, int cj, IReadOnlyList<Vector3> cameras)
        {
            var nearest = float.MaxValue;
            foreach (var camera in cameras)
                nearest = MathF.Min(nearest, Distance(ci, cj, camera));
            return nearest;
        }

        private float Distance(int ci, int cj, Vector3 camera)
        {
            var (i0, j0, cellsX, cellsZ) = _terrain.ChunkCellsOf(ci, cj);
            var size = _terrain.CellSize;
            var minX = _terrain.OriginX + i0 * size;
            var minZ = _terrain.OriginZ + j0 * size;
            var dx = MathF.Max(0f, MathF.Max(minX - camera.X, camera.X - (minX + cellsX * size)));
            var dz = MathF.Max(0f, MathF.Max(minZ - camera.Z, camera.Z - (minZ + cellsZ * size)));
            return MathF.Sqrt(dx * dx + dz * dz);
        }

        // Builds what's come within reach of the camera (all of it at once, if `all`) and drops what's gone out.
        public void Update(GraphicsDevice device, Vector3 camera, bool all = false) => Update(device, new[] { camera }, all);

        // The same, for what's within reach of any of the cameras.
        public void Update(GraphicsDevice device, IReadOnlyList<Vector3> cameras, bool all = false)
        {
            var reach = (int)MathF.Ceiling(DrawDistance / (Terrain.ChunkCells * _terrain.CellSize)) + 1;
            _wanted.Clear();
            foreach (var camera in cameras)
            {
                var ci0 = (int)MathF.Floor((camera.X - _terrain.OriginX) / (Terrain.ChunkCells * _terrain.CellSize));
                var cj0 = (int)MathF.Floor((camera.Z - _terrain.OriginZ) / (Terrain.ChunkCells * _terrain.CellSize));
                for (var cj = Math.Max(0, cj0 - reach); cj <= Math.Min(_terrain.ChunksZ - 1, cj0 + reach); cj++)
                    for (var ci = Math.Max(0, ci0 - reach); ci <= Math.Min(_terrain.ChunksX - 1, ci0 + reach); ci++)
                    {
                        var distance = Distance(ci, cj, camera);
                        if (distance <= DrawDistance && !_chunks.ContainsKey((ci, cj)) && !_wanted.Exists(w => w.ci == ci && w.cj == cj))
                            _wanted.Add((distance, ci, cj));
                    }
            }
            _wanted.Sort((a, b) => a.distance.CompareTo(b.distance));
            for (var k = 0; k < _wanted.Count && (all || k < BuildsPerFrame); k++)
                Build(device, _wanted[k].ci, _wanted[k].cj);

            _gone.Clear();
            foreach (var key in _chunks.Keys)
                if (Distance(key.ci, key.cj, cameras) > DropDistance)
                    _gone.Add(key);
            foreach (var key in _gone)
            {
                _chunks[key].mesh.Dispose();
                _chunks.Remove(key);
            }
        }

        private void Build(GraphicsDevice device, int ci, int cj)
        {
            _buildTime.Start();
            var (i0, j0, cellsX, cellsZ) = _terrain.ChunkCellsOf(ci, cj);
            var mesh = TerrainMesh.Build(device, _terrain, _shore, i0, j0, cellsX, cellsZ, _bare);
            _chunks[(ci, cj)] = (mesh, new MeshInstance(mesh, _palette));   // in world coordinates already
            _buildTime.Stop();
        }

        // Adds every chunk that's built to the batch, which leaves out those not in view.
        public void Collect(MeshBatch batch)
        {
            Drawn = 0;
            foreach (var (_, view) in _chunks.Values)
                if (batch.Add(view))
                    Drawn++;
        }

        public void Dispose()
        {
            foreach (var (mesh, _) in _chunks.Values)
                mesh.Dispose();
            _chunks.Clear();
        }
    }
}
