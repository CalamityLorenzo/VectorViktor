using MeshCore.Library;
using MeshLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core;

namespace Basic.World
{
    // The terrain as drawn: a mesh for each chunk (see Terrain.ChunkCellsOf), built as the camera comes
    // within DrawDistance of it - nearest first, and no more than BuildsPerFrame a frame, so walking on
    // doesn't stutter - drawn only if it's in view, and thrown away once the camera's more than
    // DropDistance off. So only the country round you is ever built or drawn, however big the world.
    public sealed class TerrainView : IDisposable
    {
        public const int BuildsPerFrame = 4;

        private readonly Terrain _terrain;
        private readonly float _shore;
        private readonly Func<float, float, bool> _bare;   // where the ground gets no grid lines (see TerrainMesh)
        private readonly Color[] _palette = TerrainMesh.Palette();
        private readonly Dictionary<(int ci, int cj), (MeshData mesh, MeshInstance view, BoundingBox bounds)> _chunks =
            new Dictionary<(int, int), (MeshData, MeshInstance, BoundingBox)>();

        public float DrawDistance { get; }
        public float DropDistance => DrawDistance + 40f;

        // How many chunks are built now, and how many were in view last time they were drawn.
        public int Built => _chunks.Count;
        public int Drawn { get; private set; }

        public TerrainView(Terrain terrain, float shore, float drawDistance, Func<float, float, bool> bare = null)
        {
            _terrain = terrain;
            _shore = shore;
            DrawDistance = drawDistance;
            _bare = bare;
        }

        // How far a chunk's patch of ground is from the camera, across the ground (0 if it's over it).
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
            var reach = (int)MathF.Ceiling(DrawDistance / (Terrain.ChunkCells * _terrain.CellSize)) + 1;
            var ci0 = (int)MathF.Floor((camera.X - _terrain.OriginX) / (Terrain.ChunkCells * _terrain.CellSize));
            var cj0 = (int)MathF.Floor((camera.Z - _terrain.OriginZ) / (Terrain.ChunkCells * _terrain.CellSize));

            var wanted = new List<(float distance, int ci, int cj)>();
            for (var cj = Math.Max(0, cj0 - reach); cj <= Math.Min(_terrain.ChunksZ - 1, cj0 + reach); cj++)
                for (var ci = Math.Max(0, ci0 - reach); ci <= Math.Min(_terrain.ChunksX - 1, ci0 + reach); ci++)
                {
                    var distance = Distance(ci, cj, camera);
                    if (distance <= DrawDistance && !_chunks.ContainsKey((ci, cj)))
                        wanted.Add((distance, ci, cj));
                }
            wanted.Sort((a, b) => a.distance.CompareTo(b.distance));
            for (var k = 0; k < wanted.Count && (all || k < BuildsPerFrame); k++)
                Build(device, wanted[k].ci, wanted[k].cj);

            var gone = new List<(int, int)>();
            foreach (var key in _chunks.Keys)
                if (Distance(key.ci, key.cj, camera) > DropDistance)
                    gone.Add(key);
            foreach (var key in gone)
            {
                _chunks[key].mesh.Dispose();
                _chunks.Remove(key);
            }
        }

        private void Build(GraphicsDevice device, int ci, int cj)
        {
            var (i0, j0, cellsX, cellsZ) = _terrain.ChunkCellsOf(ci, cj);
            var mesh = TerrainMesh.Build(device, _terrain, _shore, i0, j0, cellsX, cellsZ, _bare);
            var view = new MeshInstance(mesh, _palette) { Transform = Matrix.Identity };
            _chunks[(ci, cj)] = (mesh, view, _terrain.ChunkBounds(ci, cj));
        }

        public void Draw(GameTime gameTime, GraphicsDevice device, BasicEffect effect, Color background, bool colorsOn)
        {
            var frustum = new BoundingFrustum(effect.View * effect.Projection);
            Drawn = 0;
            foreach (var (_, view, bounds) in _chunks.Values)
            {
                if (!frustum.Intersects(bounds))
                    continue;
                view.ColorsOn = colorsOn;
                view.Draw(gameTime, device, effect, background);
                Drawn++;
            }
        }

        public void Dispose()
        {
            foreach (var (mesh, _, _) in _chunks.Values)
                mesh.Dispose();
            _chunks.Clear();
        }
    }
}
