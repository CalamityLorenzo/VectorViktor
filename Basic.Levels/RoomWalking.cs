using Microsoft.Xna.Framework;
using System;
using World.Buildings;
using World.Core;

namespace Basic.Levels
{
    // Walking round a room the way Basic.Levels does: one room at a time, in the room's own coordinates, kept in by
    // its outline and through its doors to the room each leads to. (Basic.World walks on BuildingGround instead,
    // where the walls are what keeps you in.)
    public static class RoomWalking
    {
        // Pushes a walker (a circle of `radius` on the floor) back in from the room's boundary, convex or
        // concave corners alike: finds the single closest point anywhere on the Outline and, unless that's
        // squarely inside a door or opening's gap, pushes away from it. A concave (notch) corner falls
        // naturally out of this - the closest point may be a vertex rather than partway along an edge,
        // which is exactly what rounds the walker round it, the same way Geometry2D.PushOutOfBox rounds a box corner.
        public static Vector3 KeepInside(this RoomSpec room, Vector3 p, float radius)
        {
            var q = new Vector2(p.X, p.Z);
            var bestDist = float.MaxValue;
            var bestPoint = Vector2.Zero;
            var bestEdge = 0;
            for (var i = 0; i < room.Outline.Length; i++)
            {
                var (a, b) = Edge(room, i);
                var nearest = Geometry2D.NearestOnSegment(q, a, b);
                var dist = Vector2.Distance(q, nearest);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPoint = nearest;
                    bestEdge = i;
                }
            }

            // Squarely in an opening's gap: let the walker through regardless of which side of the wall
            // line they're on - crossing the threshold is exactly when Contains flips to false below, so
            // this has to come first rather than being gated on still being "inside".
            if (Array.Exists(room.Openings, o => o.WallIndex == bestEdge && MathF.Abs(room.AlongWall(bestEdge, p) - o.Offset) <= o.Width / 2f - radius))
                return p;

            var inside = room.Contains(p);
            if (inside && bestDist >= radius)
                return p;

            var direction = inside && bestDist > 1e-6f
                ? (q - bestPoint) / bestDist
                : new Vector2(room.Inward(bestEdge).X, room.Inward(bestEdge).Z);
            var pushed = bestPoint + direction * radius;
            return new Vector3(pushed.X, p.Y, pushed.Y);
        }

        // Pushes a walker (a circle of `radius` on the floor) out of any furniture it overlaps. The position is in the room's own coordinates.
        public static Vector3 PushOutOfProps(this RoomSpec room, Vector3 position, float radius)
        {
            var p = new Vector2(position.X, position.Z);
            foreach (var prop in room.Props)
            {
                if (!prop.Blocks)
                    continue;
                p = Geometry2D.PushOutOfBox(p, new Vector2(prop.Position.X, prop.Position.Z), prop.Half, radius);
            }
            return new Vector3(p.X, position.Y, p.Y);
        }

        public static DoorSpec FindDoor(this RoomSpec room, string id) =>
            Array.Find(room.Doors, d => d.Id == id) ?? throw new ArgumentException($"Room '{room.Id}' has no door '{id}'.", nameof(id));

        // How far a point is in from a wall's line (negative outside it).
        public static float DistanceToWall(this RoomSpec room, int wallIndex, Vector3 p)
        {
            var (a, _) = Edge(room, wallIndex);
            var inward = room.Inward(wallIndex);
            return (p.X - a.X) * inward.X + (p.Z - a.Y) * inward.Z;
        }

        // How far along a wall a point is, from its midpoint, the way its offsets are measured (see DoorSpec).
        public static float AlongWall(this RoomSpec room, int wallIndex, Vector3 p)
        {
            var (a, b) = Edge(room, wallIndex);
            var mid = (a + b) / 2f;
            var t = room.Tangent(wallIndex);
            return (p.X - mid.X) * t.X + (p.Z - mid.Y) * t.Z;
        }

        private static (Vector2 a, Vector2 b) Edge(RoomSpec room, int wallIndex) =>
            (room.Outline[wallIndex], room.Outline[(wallIndex + 1) % room.Outline.Length]);
    }
}
