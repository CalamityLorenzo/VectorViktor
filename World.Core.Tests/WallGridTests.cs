using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using Xunit;

namespace World.Core.Tests
{
    // The wall grid must give the same answers as looking at every wall, only faster (see WallGrid, BuildingGround).
    public class WallGridTests
    {
        private static List<WallSegment> RandomWalls(Random random, int count, float area = 200f)
        {
            Vector2 Point() => new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * area;
            var walls = new List<WallSegment>();
            for (var i = 0; i < count; i++)
            {
                var a = Point();
                var b = a + new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 30f;
                walls.Add(new WallSegment(a, b, (float)random.NextDouble() * 0.5f, 1f + (float)random.NextDouble() * 3f));
            }
            return walls;
        }

        [Fact]
        public void A_query_finds_every_wall_the_box_touches_in_the_order_they_were_given_with_none_twice()
        {
            var random = new Random(1);
            var walls = RandomWalls(random, 300);
            var grid = new WallGrid(walls);
            for (var k = 0; k < 500; k++)
            {
                var min = new Vector2((float)random.NextDouble() - 0.5f, (float)random.NextDouble() - 0.5f) * 200f;
                var max = min + new Vector2((float)random.NextDouble(), (float)random.NextDouble()) * 12f;
                var found = grid.Near(min, max).ToList();
                var touching = Enumerable.Range(0, walls.Count).Where(i => Geometry2D.SegmentHitsBox(walls[i].A, walls[i].B, min, max)).ToList();
                Assert.All(touching, i => Assert.Contains(i, found));
                Assert.Equal(found.OrderBy(i => i).Distinct().ToList(), found);
                Assert.All(found, i => Assert.Equal(walls[i], grid[i]));
            }
        }

        [Fact]
        public void A_huge_query_returns_every_wall()
        {
            var walls = RandomWalls(new Random(2), 50);
            var grid = new WallGrid(walls);
            Assert.Equal(Enumerable.Range(0, 50), grid.Near(new Vector2(-5000f), new Vector2(5000f)));
        }

        [Fact]
        public void A_grid_of_no_walls_finds_none()
        {
            Assert.Empty(new WallGrid(Array.Empty<WallSegment>()).Near(new Vector2(-10f), new Vector2(10f)));
        }

        private static BuildingGround GroundWith(List<WallSegment> walls) =>
            new BuildingGround(Grounds.Flat(), Array.Empty<Building>(), walls);

        [Fact]
        public void KeepOut_pushes_a_walker_out_of_walls_just_as_it_did_looking_at_all_of_them()
        {
            var random = new Random(3);
            var walls = RandomWalls(random, 250, 60f);   // packed close: plenty of corners
            var ground = GroundWith(walls);
            const float radius = 0.35f, height = 1.8f;
            for (var k = 0; k < 2000; k++)
            {
                var feet = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 60f;

                // The old way: every wall, in order, twice round
                var expected = feet;
                for (var pass = 0; pass < 2; pass++)
                    foreach (var wall in walls)
                    {
                        if (wall.Bottom >= expected.Y + height || wall.Top <= expected.Y + 0.01f)
                            continue;
                        var q = new Vector2(expected.X, expected.Z);
                        var nearest = Geometry2D.NearestOnSegment(q, wall.A, wall.B);
                        var gap = q - nearest;
                        var distance = gap.Length();
                        if (distance >= radius)
                            continue;
                        var away = distance > 1e-6f ? gap / distance : Geometry2D.Outward(wall.A, wall.B);
                        var pushed = nearest + away * radius;
                        expected = new Vector3(pushed.X, expected.Y, pushed.Y);
                    }

                var actual = ground.KeepOut(feet, radius, height);
                Assert.True(Vector3.Distance(expected, actual) < 1e-5f, $"{feet}: expected {expected}, got {actual}");
            }
        }

        [Fact]
        public void Obstructs_finds_the_same_walls_as_looking_at_all_of_them()
        {
            var random = new Random(4);
            var walls = RandomWalls(random, 250, 60f);
            var ground = GroundWith(walls);
            for (var k = 0; k < 2000; k++)
            {
                var at = new Vector3((float)random.NextDouble() - 0.5f, 0f, (float)random.NextDouble() - 0.5f) * 60f;
                var size = new Vector3(0.4f + (float)random.NextDouble() * 2f, 0.5f + (float)random.NextDouble() * 2f, 0.4f + (float)random.NextDouble() * 2f);
                var min = new Vector2(at.X - size.X / 2f + 0.01f, at.Z - size.Z / 2f + 0.01f);
                var max = new Vector2(at.X + size.X / 2f - 0.01f, at.Z + size.Z / 2f - 0.01f);
                var expected = walls.Any(w => w.Bottom < at.Y + size.Y && w.Top > at.Y + 0.01f && Geometry2D.SegmentHitsBox(w.A, w.B, min, max));
                Assert.Equal(expected, ground.Obstructs(at, size));
            }
        }

        [Fact]
        public void ClearLine_stops_at_the_same_wall_as_looking_at_all_of_them()
        {
            var random = new Random(5);
            var walls = RandomWalls(random, 250, 60f);
            var ground = GroundWith(walls);
            for (var k = 0; k < 2000; k++)
            {
                var from = new Vector3((float)random.NextDouble() - 0.5f, 1.5f, (float)random.NextDouble() - 0.5f) * 60f;
                var to = from + new Vector3((float)random.NextDouble() - 0.5f, 0.2f, (float)random.NextDouble() - 0.5f) * 80f;
                to.Y = 1.5f + (float)random.NextDouble();

                var d = to - from;
                var p = new Vector2(from.X, from.Z);
                var r = new Vector2(d.X, d.Z);
                var hit = 1f;
                foreach (var wall in walls)
                {
                    var s = wall.B - wall.A;
                    var denominator = Geometry2D.Cross(r, s);
                    if (MathF.Abs(denominator) < 1e-9f)
                        continue;
                    var t = Geometry2D.Cross(wall.A - p, s) / denominator;
                    var u = Geometry2D.Cross(wall.A - p, r) / denominator;
                    if (t < 0f || t >= hit || u < 0f || u > 1f)
                        continue;
                    var y = from.Y + d.Y * t;
                    if (y >= wall.Bottom && y <= wall.Top)
                        hit = t;
                }
                var expected = hit >= 1f ? to : from + d * MathF.Max(0f, hit - 0.3f / MathF.Max(d.Length(), 1e-6f));

                var actual = ground.ClearLine(from, to);
                Assert.True(Vector3.Distance(expected, actual) < 1e-4f, $"{from} to {to}: expected {expected}, got {actual}");
            }
        }
    }
}
