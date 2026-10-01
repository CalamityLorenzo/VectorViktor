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
    // can be more than one camera: one looking through a window onto somewhere else in the world, too.
    public sealed class TerrainView : IDisposable
    {
        public const int BuildsPerFrame = 4;

        private readonly Terrain _terrain;
        private readonly float _shore;
        private readonly Func<float, float, bool>? _bare;  // where the ground gets no grid lines (see TerrainMesh)
        private readonly Func<float, float, bool>? _covered;   // where it isn't drawn at all (see TerrainMesh)
        private readonly Color[] _palette = TerrainMesh.Palette();
        // Each chunk's ground, and its grid apart from it, to draw or not (see TerrainMesh.BuildApart)
        private readonly Dictionary<(int ci, int cj), (MeshInstance ground, MeshInstance grid)> _chunks =
            new Dictionary<(int, int), (MeshInstance, MeshInstance)>();
        private const float GroundTint = 0.3f;   // with colours off and no grid, how faintly the ground's shaded

        // Kept from one frame to the next, so looking round costs no garbage
        private readonly List<(float distance, int ci, int cj)> _wanted = new List<(float, int, int)>();
        private readonly List<(int, int)> _gone = new List<(int, int)>();
        private readonly List<Vector3> _single = new List<Vector3>();

        public float DrawDistance { get; }
        public float DropDistance => DrawDistance + 40f;

        // How many chunks are built now, and how many were in view last time they were drawn.
        public int Built => _chunks.Count;
        public int Drawn { get; private set; }

        // How long building them has taken, all told.
        public TimeSpan BuildTime => _buildTime.Elapsed;
        private readonly System.Diagnostics.Stopwatch _buildTime = new System.Diagnostics.Stopwatch();

        public TerrainView(Terrain terrain, float shore, float drawDistance, Func<float, float, bool>? bare = null,
                           Func<float, float, bool>? covered = null)
        {
            _terrain = terrain;
            _shore = shore;
            DrawDistance = drawDistance;
            _bare = bare;
            _covered = covered;
        }

        // How far a chunk's patch of ground is from the nearest camera, across the ground (0 if it's over it).
        private float Distance(int ci, int cj, IReadOnlyList<Vector3> cameras)
        {
            var nearest = float.MaxValue;
            for (var k = 0; k < cameras.Count; k++)   // not foreach: that makes garbage of an interface's enumerator
                nearest = MathF.Min(nearest, Distance(ci, cj, cameras[k]));
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
        public void Update(GraphicsDevice device, Vector3 camera, bool all = false)
        {
            _single.Clear();
            _single.Add(camera);
            Update(device, _single, all);
        }

        // The same, for what's within reach of any of the cameras.
        public void Update(GraphicsDevice device, IReadOnlyList<Vector3> cameras, bool all = false)
        {
            var reach = (int)MathF.Ceiling(DrawDistance / (Terrain.ChunkCells * _terrain.CellSize)) + 1;
            _wanted.Clear();
            for (var k = 0; k < cameras.Count; k++)
            {
                var camera = cameras[k];
                var ci0 = (int)MathF.Floor((camera.X - _terrain.OriginX) / (Terrain.ChunkCells * _terrain.CellSize));
                var cj0 = (int)MathF.Floor((camera.Z - _terrain.OriginZ) / (Terrain.ChunkCells * _terrain.CellSize));
                for (var cj = Math.Max(0, cj0 - reach); cj <= Math.Min(_terrain.ChunksZ - 1, cj0 + reach); cj++)
                    for (var ci = Math.Max(0, ci0 - reach); ci <= Math.Min(_terrain.ChunksX - 1, ci0 + reach); ci++)
                    {
                        var distance = Distance(ci, cj, camera);
                        if (distance <= DrawDistance && !_chunks.ContainsKey((ci, cj)) && !IsWanted(ci, cj))
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
                Dispose(_chunks[key]);
                _chunks.Remove(key);
            }
        }

        private bool IsWanted(int ci, int cj)
        {
            foreach (var wanted in _wanted)
                if (wanted.ci == ci && wanted.cj == cj)
                    return true;
            return false;
        }

        private void Build(GraphicsDevice device, int ci, int cj)
        {
            _buildTime.Start();
            var (i0, j0, cellsX, cellsZ) = _terrain.ChunkCellsOf(ci, cj);
            var (ground, grid) = TerrainMesh.BuildApart(device, _terrain, _shore, i0, j0, cellsX, cellsZ, _bare, _covered);
            _chunks[(ci, cj)] = (new MeshInstance(ground, _palette), new MeshInstance(grid, _palette));   // in world coordinates already
            _buildTime.Stop();
        }

        // Off: the ground's drawn without its lines, the grid, cliffs' outlines and all (see TerrainMesh).
        public bool ShowLines { get; set; } = true;

        // Off: without its grid, but still with its outlines. Without the grid, with colours off, the ground's shaded
        // faintly, so its shape still shows (see MeshInstance.ColorsOffTint).
        public bool ShowGrid { get; set; } = true;

        // Adds every chunk that's built to the batch, which leaves out those not in view.
        public void Collect(MeshBatch batch)
        {
            Drawn = 0;
            var grid = ShowLines && ShowGrid;
            foreach (var (ground, gridLines) in _chunks.Values)
            {
                ground.EdgesOn = ShowLines;
                ground.ColorsOffTint = grid ? 0f : GroundTint;
                if (batch.Add(ground))
                    Drawn++;
                if (grid && gridLines.Mesh.Edges != null)
                    batch.Add(gridLines);
            }
        }

        private static void Dispose((MeshInstance ground, MeshInstance grid) chunk)
        {
            chunk.ground.Mesh.Dispose();
            chunk.grid.Mesh.Dispose();
        }

        public void Dispose()
        {
            foreach (var chunk in _chunks.Values)
                Dispose(chunk);
            _chunks.Clear();
        }
    }
}
