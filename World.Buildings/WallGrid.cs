using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Buildings
{
    // Walls that never move, sorted into square cells of the map, so asking what's near a place means looking at the
    // handful in the cells there rather than at every wall in the world. Each wall's in every cell its bounding
    // box touches, so a query can return more than is near - never fewer.
    //
    // What a query returns is in the order the walls were given (which is the order that pushing a walker out of
    // one wall and into the next depends on) and with none twice. The list is the grid's own, reused by the next
    // query: use it, don't keep it. Not for more than one thread.
    public sealed class WallGrid
    {
        public const float CellSize = 8f;

        // A query over more cells than this isn't worth the lookups: it returns every wall.
        private const int MaxCells = 256;

        private readonly WallSegment[] _walls;
        private readonly Dictionary<(int x, int z), List<int>> _cells = new Dictionary<(int, int), List<int>>();
        private readonly List<int> _found = new List<int>();

        public WallGrid(IReadOnlyList<WallSegment> walls)
        {
            _walls = new WallSegment[walls.Count];
            for (var i = 0; i < walls.Count; i++)
            {
                _walls[i] = walls[i];
                var (x0, z0) = Cell(Vector2.Min(walls[i].A, walls[i].B));
                var (x1, z1) = Cell(Vector2.Max(walls[i].A, walls[i].B));
                for (var z = z0; z <= z1; z++)
                    for (var x = x0; x <= x1; x++)
                    {
                        if (!_cells.TryGetValue((x, z), out var cell))
                            _cells[(x, z)] = cell = new List<int>();
                        cell.Add(i);
                    }
            }
        }

        public int Count => _walls.Length;

        public WallSegment this[int index] => _walls[index];

        private static (int x, int z) Cell(Vector2 p) => ((int)MathF.Floor(p.X / CellSize), (int)MathF.Floor(p.Y / CellSize));

        // The indexes (see this[]) of the walls whose cells the box from `min` to `max` (in X and Z) touches.
        public IReadOnlyList<int> Near(Vector2 min, Vector2 max)
        {
            _found.Clear();
            var (x0, z0) = Cell(min);
            var (x1, z1) = Cell(max);
            if ((long)(x1 - x0 + 1) * (z1 - z0 + 1) > MaxCells)
            {
                for (var i = 0; i < _walls.Length; i++)
                    _found.Add(i);
                return _found;
            }
            for (var z = z0; z <= z1; z++)
                for (var x = x0; x <= x1; x++)
                    if (_cells.TryGetValue((x, z), out var cell))
                        _found.AddRange(cell);
            _found.Sort();
            var kept = 0;
            for (var i = 0; i < _found.Count; i++)
                if (i == 0 || _found[i] != _found[i - 1])
                    _found[kept++] = _found[i];
            _found.RemoveRange(kept, _found.Count - kept);
            return _found;
        }
    }
}
