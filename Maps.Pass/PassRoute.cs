using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace Maps.Pass
{
    // The way the road goes: from the trailhead in the south-west corner, north up a winding valley to the first pass,
    // down to a shelf along the north shore of the lake under the cliffs, up again by switchbacks to the second pass,
    // and by more switchbacks down the mountainside into the basin at the far end, to the town's lake. Its middle line
    // is a smooth curve through the Controls, each with the height the road has there, and between them the height
    // eases from one to the next (see Heights), never steeper than MaxGrade.
    //
    // The terrain's cut to it (see PassTerrain): level across, HalfWidth either side of the middle line, the only
    // ground that leads anywhere.
    public static class PassRoute
    {
        public const float HalfWidth = 5f;       // the level strip the road's laid on, either side of its middle
        public const float MaxGrade = 0.15f;     // rise over run, anywhere along it: steep, as mountain roads go

        // Where the road goes through (world X, Z), and how high it is there. Named ones are places along it.
        public static readonly (Vector2 at, float height)[] Controls =
        {
            (new Vector2(-462f, 420f), 20f),   // the trailhead
            (new Vector2(-440f, 360f), 26f),
            (new Vector2(-452f, 290f), 35.7f),
            (new Vector2(-405f, 235f), 45.7f),
            (new Vector2(-440f, 165f), 56.5f),
            (new Vector2(-395f, 100f), 67.3f),
            (new Vector2(-330f, 55f), 78.1f),
            (new Vector2(-270f, 20f), 87.4f),
            (new Vector2(-215f, 0f), 93f),     // the first pass
            (new Vector2(-160f, 15f), 87.5f),
            (new Vector2(-120f, 50f), 80.3f),
            (new Vector2(-70f, 62f), 74f),     // on the shelf above the lake
            (new Vector2(-10f, 66f), 72f),
            (new Vector2(50f, 78f), 73f),
            (new Vector2(105f, 60f), 80f),
            (new Vector2(125f, 5f), 87.9f),
            (new Vector2(80f, -50f), 97.3f),
            (new Vector2(95f, -110f), 105.6f),
            (new Vector2(160f, -140f), 115f),
            (new Vector2(215f, -190f), 122.1f),   // the second pass
            (new Vector2(290f, -215f), 114.6f),
            (new Vector2(390f, -225f), 100.8f),
            (new Vector2(450f, -195f), 91.4f),
            (new Vector2(440f, -140f), 83.2f),
            (new Vector2(360f, -125f), 72f),
            (new Vector2(270f, -100f), 59.2f),
            (new Vector2(245f, -45f), 50.5f),
            (new Vector2(300f, -10f), 41.3f),
            (new Vector2(390f, -5f), 29f),
            (new Vector2(445f, 30f), 19.8f),
            (new Vector2(430f, 90f), 14f),     // into the basin
            (new Vector2(400f, 120f), 14f),
        };

        public const int Trailhead = 0, FirstPass = 8, LakeShelf = 11, SecondPass = 19, BasinEntry = 30;

        private const int StepsPerSpan = 16;

        // The middle line, point by point from the trailhead, a metre or so apart; how far along it each is; and how
        // high the road is there
        public static IReadOnlyList<Vector2> Points { get; }
        public static IReadOnlyList<float> Along { get; }
        public static IReadOnlyList<float> Heights { get; }
        public static float Length => Along[^1];

        // How far along it each control is
        public static IReadOnlyList<float> ControlAlong { get; }

        static PassRoute()
        {
            var points = new List<Vector2>();
            var along = new List<float>();
            var controlAlong = new List<float>();
            Vector2 Control(int k) => Controls[Math.Clamp(k, 0, Controls.Length - 1)].at;

            // A Catmull-Rom curve through the controls
            for (var k = 0; k < Controls.Length - 1; k++)
            {
                controlAlong.Add(along.Count == 0 ? 0f : along[^1]);
                for (var step = k == 0 ? 0 : 1; step <= StepsPerSpan; step++)
                {
                    var p = Vector2.CatmullRom(Control(k - 1), Control(k), Control(k + 1), Control(k + 2), (float)step / StepsPerSpan);
                    along.Add(points.Count == 0 ? 0f : along[^1] + Vector2.Distance(points[^1], p));
                    points.Add(p);
                }
            }
            controlAlong.Add(along[^1]);

            // Heights: a cubic between each control and the next, by distance along, its slope at each control the
            // mean of the grades either side - or level, at a top or a bottom - so it never overshoots either
            var n = Controls.Length;
            var grades = new float[n - 1];
            for (var k = 0; k < n - 1; k++)
                grades[k] = (Controls[k + 1].height - Controls[k].height) / (controlAlong[k + 1] - controlAlong[k]);
            var slopes = new float[n];
            for (var k = 1; k < n - 1; k++)
                slopes[k] = MathF.Sign(grades[k - 1]) == MathF.Sign(grades[k]) ? (grades[k - 1] + grades[k]) / 2f : 0f;

            var heights = new float[points.Count];
            var span = 0;
            for (var i = 0; i < points.Count; i++)
            {
                while (span < n - 2 && along[i] > controlAlong[span + 1])
                    span++;
                var length = controlAlong[span + 1] - controlAlong[span];
                var t = Math.Clamp((along[i] - controlAlong[span]) / length, 0f, 1f);
                heights[i] = Hermite(Controls[span].height, slopes[span] * length, Controls[span + 1].height, slopes[span + 1] * length, t);
            }

            Points = points;
            Along = along;
            Heights = heights;
            ControlAlong = controlAlong;

            // Each segment, in every cell it comes within IndexReach of
            for (var k = 0; k < points.Count - 1; k++)
            {
                var min = Vector2.Min(points[k], points[k + 1]) - new Vector2(IndexReach);
                var max = Vector2.Max(points[k], points[k + 1]) + new Vector2(IndexReach);
                for (var j = (int)MathF.Floor(min.Y / IndexCell); j <= (int)MathF.Floor(max.Y / IndexCell); j++)
                    for (var i = (int)MathF.Floor(min.X / IndexCell); i <= (int)MathF.Floor(max.X / IndexCell); i++)
                    {
                        if (!Index.TryGetValue((i, j), out var cell))
                            Index[(i, j)] = cell = new List<int>();
                        cell.Add(k);
                    }
            }
        }

        // The segments of the middle line near each square cell, to find the nearest of them to a point near it quickly
        private const float IndexCell = 8f, IndexReach = 16f;
        private static readonly Dictionary<(int, int), List<int>> Index = new Dictionary<(int, int), List<int>>();

        private static float Hermite(float h0, float m0, float h1, float m1, float t)
        {
            var t2 = t * t;
            var t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * h0 + (t3 - 2f * t2 + t) * m0 + (-2f * t3 + 3f * t2) * h1 + (t3 - t2) * m1;
        }

        // Where the middle line is `s` along it, how high the road is there, and which way it's heading.
        public static (Vector2 at, float height, Vector2 heading) At(float s)
        {
            s = Math.Clamp(s, 0f, Length);
            var i = FirstAtOrPast(s);
            if (i == 0)
                i = 1;
            var t = (s - Along[i - 1]) / MathF.Max(1e-6f, Along[i] - Along[i - 1]);
            var heading = Points[i] - Points[i - 1];
            return (Vector2.Lerp(Points[i - 1], Points[i], t), MathHelper.Lerp(Heights[i - 1], Heights[i], t),
                    heading == Vector2.Zero ? Vector2.UnitX : Vector2.Normalize(heading));
        }

        private static int FirstAtOrPast(float s)
        {
            int lo = 0, hi = Along.Count - 1;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (Along[mid] < s)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        // The nearest point of the middle line to `p`: how far along it that is, how far away, and how high the road is there.
        public static (float s, float distance, float height) Nearest(Vector2 p)
        {
            // Near the road, from the segments near it: any within IndexReach are among them
            var best = (s: 0f, distance: float.MaxValue, height: 0f);
            if (Index.TryGetValue(((int)MathF.Floor(p.X / IndexCell), (int)MathF.Floor(p.Y / IndexCell)), out var near))
            {
                foreach (var k in near)
                    Closer(k + 1);
                if (best.distance <= IndexReach)
                    return best;
            }
            for (var i = 1; i < Points.Count; i++)
                Closer(i);
            return best;

            void Closer(int i)
            {
                var (t, distance) = OnSegment(p, Points[i - 1], Points[i]);
                if (distance < best.distance)
                    best = (MathHelper.Lerp(Along[i - 1], Along[i], t), distance, MathHelper.Lerp(Heights[i - 1], Heights[i], t));
            }
        }

        // Whether `p` is within `distance` of the middle line (no more than IndexReach): quickly, for asking everywhere.
        public static bool Within(Vector2 p, float distance)
        {
            if (!Index.TryGetValue(((int)MathF.Floor(p.X / IndexCell), (int)MathF.Floor(p.Y / IndexCell)), out var near))
                return false;
            foreach (var k in near)
                if (OnSegment(p, Points[k], Points[k + 1]).distance <= distance)
                    return true;
            return false;
        }

        // How far along the segment from a to b (0 to 1) the nearest point of it to p is, and how far p is from that.
        internal static (float t, float distance) OnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSquared = ab.LengthSquared();
            var t = lengthSquared < 1e-12f ? 0f : Math.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0f, 1f);
            return (t, Vector2.Distance(p, a + ab * t));
        }

        // The yaw (see Start) that faces along the road `s` along it, the way to the town.
        public static float YawAt(float s)
        {
            var (_, _, heading) = At(s);
            return MathF.Atan2(heading.X, -heading.Y);
        }
    }
}
