using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Core
{
    // Geometry on the ground plan: points, segments and polygons in (X, Z), held as a Vector2's X and Y. Shared by
    // the physics, the buildings and the meshes drawn from them, so each rule is written once.
    public static class Geometry2D
    {
        // The 2D cross product: positive if b turns left (anticlockwise, seen with Y up) from a.
        public static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        // The outward normal of an edge from a to b, for an outline wound the way RoomSpec.Rectangle's is (the
        // inward one is its negative).
        public static Vector2 Outward(Vector2 a, Vector2 b)
        {
            var t = Vector2.Normalize(b - a);
            return new Vector2(t.Y, -t.X);
        }

        // The point on the segment from a to b nearest to p.
        public static Vector2 NearestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.LengthSquared();
            if (lengthSq < 1e-12f)
                return a;
            return a + ab * MathHelper.Clamp(Vector2.Dot(p - a, ab) / lengthSq, 0f, 1f);
        }

        // Whether the segment a-b passes through the box: clipped to it one axis at a time (Liang-Barsky).
        public static bool SegmentHitsBox(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            var d = b - a;
            float t0 = 0f, t1 = 1f;
            bool Clip(float p, float q)
            {
                if (MathF.Abs(p) < 1e-12f)
                    return q >= 0f;
                var t = q / p;
                if (p < 0f)
                {
                    if (t > t1) return false;
                    if (t > t0) t0 = t;
                }
                else
                {
                    if (t < t0) return false;
                    if (t < t1) t1 = t;
                }
                return true;
            }
            return Clip(-d.X, a.X - min.X) && Clip(d.X, max.X - a.X) && Clip(-d.Y, a.Y - min.Y) && Clip(d.Y, max.Y - a.Y);
        }

        // A circle of `radius` at p pushed clear of an axis-aligned box (centre, half-extent) by the shortest
        // route; if its centre is already inside the box, out through whichever side is nearest.
        public static Vector2 PushOutOfBox(Vector2 p, Vector2 centre, Vector2 half, float radius)
        {
            var d = p - centre;
            var nearest = Vector2.Clamp(d, -half, half);
            var gap = d - nearest;
            var distance = gap.Length();
            if (distance >= radius)
                return p;
            if (distance > 1e-6f)
                return centre + nearest + gap / distance * radius;

            // The circle's centre is inside the box: leave by whichever side is nearest
            var toX = half.X - MathF.Abs(d.X);
            var toZ = half.Y - MathF.Abs(d.Y);
            if (toX < toZ)
                return new Vector2(centre.X + (d.X < 0f ? -1f : 1f) * (half.X + radius), p.Y);
            return new Vector2(p.X, centre.Y + (d.Y < 0f ? -1f : 1f) * (half.Y + radius));
        }

        // Whether p is inside a polygon, convex or concave: the standard even-odd, ray-casting test.
        public static bool InPolygon(Vector2[] polygon, Vector2 p)
        {
            var inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        public static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            var area = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += a.X * b.Y - b.X * a.Y;
            }
            return area / 2f;
        }

        // Sutherland-Hodgman: the part of a polygon (convex or concave) on the side of the line through
        // planePoint that planeNormal points into.
        public static List<Vector2> ClipToHalfPlane(Vector2[] polygon, Vector2 planePoint, Vector2 planeNormal)
        {
            var output = new List<Vector2>();
            for (var i = 0; i < polygon.Length; i++)
            {
                var curr = polygon[i];
                var prev = polygon[(i - 1 + polygon.Length) % polygon.Length];
                var currIn = Vector2.Dot(curr - planePoint, planeNormal) >= 0f;
                var prevIn = Vector2.Dot(prev - planePoint, planeNormal) >= 0f;
                if (currIn != prevIn)
                {
                    var denom = Vector2.Dot(curr - prev, planeNormal);
                    var t = MathF.Abs(denom) > 1e-9f ? Vector2.Dot(planePoint - prev, planeNormal) / denom : 0f;
                    output.Add(prev + (curr - prev) * t);
                }
                if (currIn)
                    output.Add(curr);
            }
            return output;
        }

        // Splits a simple polygon (convex or concave, wound either way) into triangles by ear clipping:
        // repeatedly cut off a "convex and empty" corner until three vertices are left. O(n^2) worst case,
        // which is fine for room footprints (a handful of vertices, built once and cached).
        public static List<(int a, int b, int c)> Triangulate(Vector2[] polygon)
        {
            var n = polygon.Length;
            var indices = new List<int>(n);
            for (var i = 0; i < n; i++)
                indices.Add(i);

            var sign = SignedArea(polygon) >= 0f ? 1f : -1f;

            var triangles = new List<(int, int, int)>();
            var guard = 0;
            while (indices.Count > 3 && guard++ < n * n + 8)
            {
                var earFound = false;
                for (var k = 0; k < indices.Count; k++)
                {
                    var iPrev = indices[(k - 1 + indices.Count) % indices.Count];
                    var iCurr = indices[k];
                    var iNext = indices[(k + 1) % indices.Count];
                    var prev = polygon[iPrev]; var curr = polygon[iCurr]; var next = polygon[iNext];

                    var cross = (curr.X - prev.X) * (next.Y - prev.Y) - (curr.Y - prev.Y) * (next.X - prev.X);
                    if (cross * sign <= 0f)
                        continue;   // reflex at this vertex, not an ear

                    var isEar = true;
                    foreach (var iOther in indices)
                    {
                        if (iOther == iPrev || iOther == iCurr || iOther == iNext)
                            continue;
                        if (PointInTriangle(polygon[iOther], prev, curr, next))
                        {
                            isEar = false;
                            break;
                        }
                    }
                    if (!isEar)
                        continue;

                    triangles.Add((iPrev, iCurr, iNext));
                    indices.RemoveAt(k);
                    earFound = true;
                    break;
                }
                if (!earFound)
                    break;   // degenerate input; use what's clipped so far rather than loop forever
            }
            if (indices.Count == 3)
                triangles.Add((indices[0], indices[1], indices[2]));
            return triangles;
        }

        // Whether p is inside the triangle a, b, c (or on its edge), wound either way.
        public static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            static float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
            var d1 = Sign(p, a, b);
            var d2 = Sign(p, b, c);
            var d3 = Sign(p, c, a);
            var hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }
    }
}
