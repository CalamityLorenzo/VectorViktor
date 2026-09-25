using System;
using System.Collections.Generic;
using System.Linq;

using MeshCore.Library;
using MeshRawData.Helpers;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData
{
    // Which ways a roundabout's roads leave it. North is -Z, the way a yaw of 0 faces; East is +X.
    [Flags]
    public enum RoadArms { None = 0, North = 1, East = 2, South = 4, West = 8, All = North | East | South | West }

    // Road pieces to lay out a town with (see RoadBuilder): two-lane roads, 7 m of tarmac between kerbs
    // with a broken white centre line, and a 2 m pavement along each side. Every piece is centred on the
    // origin, and every road leaving it ends at the edge of its square with the centre line in the middle
    // of that edge, so with the defaults they fit a 10 m grid: a straight is 10 m long, a corner and a
    // T-junction fill 20 x 20, a roundabout 40 x 40. Turn a piece by yaw, in quarter turns, to point it
    // where you need it. Junction markings assume traffic drives on the left unless told otherwise.
    public static class RoadMesh
    {
        public const int PaletteSize = RoadBuilder.PaletteSize;

        private const float Half = RoadBuilder.LaneWidth;   // centre line to kerb

        public static Color[] Palette(Color tarmac, Color pavement, Color paint, Color verge) =>
            RoadBuilder.Palette(tarmac, pavement, paint, verge);

        // Runs along Z, from -length/2 to +length/2.
        public static MeshData Straight(GraphicsDevice device, float length = 10f)
        {
            var road = new RoadBuilder();
            var end = length / 2f;
            road.AddTarmac(new Vector2(-Half, -end), new Vector2(Half, -end), new Vector2(Half, end), new Vector2(-Half, end));
            road.AddPavement(new[] { new Vector2(Half, -end), new Vector2(Half, end) });
            road.AddPavement(new[] { new Vector2(-Half, end), new Vector2(-Half, -end) });
            road.AddCentreLine(RoadBuilder.Line(new Vector2(0f, -end), new Vector2(0f, end)), length);
            return road.Build(device);
        }

        // A bend: in at the middle of the -Z edge of a square 2 x radius across, heading +Z, curving towards +X
        // round a centre line `radius` from the square's (+X, -Z) corner. A quarter turn (the default) comes
        // out at the middle of the square's +X edge, heading +X; driven the other way it bends the other way,
        // so this one piece, turned, makes every corner. A wider radius makes a gentler, shallower bend, and
        // still fits the 10 m grid when it's a multiple of 10 (radius 20 fills 40 x 40). Less than a quarter
        // turn (`degrees`) comes out part way round, heading off at that angle to +Z. `segments`, if not
        // given, is enough for pieces about 2 m long round the centre line, and never fewer than 16.
        public static MeshData Corner(GraphicsDevice device, float radius = 10f, int segments = 0, float degrees = 90f)
        {
            if (radius <= Half + RoadBuilder.PavementWidth)
                throw new ArgumentOutOfRangeException(nameof(radius), "The inside pavement needs room inside the bend.");
            if (degrees <= 0f || degrees > 90f)
                throw new ArgumentOutOfRangeException(nameof(degrees), "A bend turns more than nothing and no more than a quarter turn.");

            var turn = MathHelper.ToRadians(degrees);
            if (segments <= 0)
                segments = Math.Max(16, (int)MathF.Ceiling(radius * turn / 2f));

            var road = new RoadBuilder();
            var centre = new Vector2(radius, -radius);
            var inner = RoadBuilder.Arc(centre, radius - Half, MathHelper.Pi, MathHelper.Pi - turn, segments);
            var outer = RoadBuilder.Arc(centre, radius + Half, MathHelper.Pi, MathHelper.Pi - turn, segments);
            road.AddTarmacStrip(inner, outer);
            road.AddPavement(inner);
            road.AddPavement(Enumerable.Reverse(outer).ToList());
            road.AddCentreLine(RoadBuilder.Circle(centre, radius, MathHelper.Pi, -1f), radius * turn);
            return road.Build(device);
        }

        // A straight along Z, from -length/2 to +length/2, with a pedestrian refuge in the middle: a kerbed
        // island `islandWidth` across with rounded ends, a keep-left bollard at each end. The kerbs bow out
        // round it by half the island's width, tapering over `taper` either side, so each lane keeps its
        // full width past it; the centre line stops short and splits round it in two solid lines. With the
        // defaults it's 20 m long, two grid squares.
        public static MeshData StraightWithIsland(GraphicsDevice device, float length = 20f, float islandLength = 4f,
            float islandWidth = 2f, float taper = 4f, int segments = 8)
        {
            var end = length / 2f;
            var islandEnd = islandLength / 2f;
            var wide = islandEnd + 1f;          // the kerbs are out at their widest this far either side of the middle
            var narrow = wide + taper;          // and back in line from here on
            if (islandWidth >= islandLength || narrow >= end)
                throw new ArgumentOutOfRangeException(nameof(length), "Too short for the island and the taper either side of it.");

            var road = new RoadBuilder();
            var bulge = Half + islandWidth / 2f;
            var zs = new[] { -end, -narrow, -wide, wide, narrow, end };
            var xs = new[] { Half, Half, bulge, bulge, Half, Half };
            var east = zs.Select((z, i) => new Vector2(xs[i], z)).ToList();
            var west = east.Select(p => new Vector2(-p.X, p.Y)).ToList();
            road.AddTarmacStrip(west, east);
            road.AddPavement(east);
            road.AddPavement(Enumerable.Reverse(west).ToList());

            // The island: straight sides, and a half circle at each end
            var radius = islandWidth / 2f;
            var straightEnd = islandEnd - radius;
            var outline = RoadBuilder.Path(
                RoadBuilder.Arc(new Vector2(0f, straightEnd), radius, 0f, MathHelper.Pi, segments),
                RoadBuilder.Arc(new Vector2(0f, -straightEnd), radius, MathHelper.Pi, MathHelper.TwoPi, segments));
            road.AddIsland(outline);
            var top = RoadBuilder.SurfaceHeight + RoadBuilder.KerbHeight;
            foreach (var z in new[] { -straightEnd, straightEnd })
                road.Mesh.AddFrustum(new Vector3(0f, top, z), 0.15f, 0.12f, 0.9f, 8, RoadBuilder.Paint, topSlot: RoadBuilder.Paint);

            // Centre line up to where the kerbs start to bow out, then splitting to pass either side of the island
            road.AddCentreLine(RoadBuilder.Line(new Vector2(0f, -end), new Vector2(0f, -narrow)), end - narrow);
            road.AddCentreLine(RoadBuilder.Line(new Vector2(0f, narrow), new Vector2(0f, end)), end - narrow);
            var pass = radius + 0.3f;
            foreach (var side in new[] { -1f, 1f })
                foreach (var way in new[] { -1f, 1f })
                {
                    var from = new Vector2(0f, way * narrow);
                    var to = new Vector2(side * pass, way * straightEnd);
                    road.AddSolidLine(RoadBuilder.Line(from, to), Vector2.Distance(from, to));
                }
            return road.Build(device);
        }

        // A road running through along Z, the width of the square, with a side road off to +X; the kerbs round
        // into the side road with `cornerRadius`. Give-way lines across the side road's lane coming in.
        public static MeshData TJunction(GraphicsDevice device, float size = 20f, float cornerRadius = 4f, int segments = 8,
            bool driveOnLeft = true)
        {
            var end = size / 2f;
            CheckCorner(cornerRadius);
            if (end <= Half + cornerRadius)
                throw new ArgumentOutOfRangeException(nameof(size), "Too small for the corners into the side road.");

            var road = new RoadBuilder();
            road.AddTarmac(new Vector2(-Half, -end), new Vector2(Half, -end), new Vector2(Half, end), new Vector2(-Half, end));
            road.AddTarmac(new Vector2(Half, -Half), new Vector2(end, -Half), new Vector2(end, Half), new Vector2(Half, Half));

            // The corners into the side road, and the tarmac they round off
            var lower = RoadBuilder.Arc(new Vector2(Half + cornerRadius, -Half - cornerRadius), cornerRadius,
                MathHelper.Pi, MathHelper.PiOver2, segments);
            var upper = RoadBuilder.Arc(new Vector2(Half + cornerRadius, Half + cornerRadius), cornerRadius,
                MathHelper.Pi * 1.5f, MathHelper.Pi, segments);
            road.AddTarmacFan(new Vector2(Half, -Half), lower);
            road.AddTarmacFan(new Vector2(Half, Half), upper);

            road.AddPavement(new[] { new Vector2(-Half, end), new Vector2(-Half, -end) });
            road.AddPavement(RoadBuilder.Path(new[] { new Vector2(Half, -end) }, lower, new[] { new Vector2(end, -Half) }));
            road.AddPavement(RoadBuilder.Path(new[] { new Vector2(end, Half) }, upper, new[] { new Vector2(Half, end) }));

            road.AddCentreLine(RoadBuilder.Line(new Vector2(0f, -end), new Vector2(0f, end)), size);
            var mouth = Half + 0.4f;
            road.AddGiveWay(new Vector2(mouth, 0f), Vector2.UnitX, InLane(Vector2.UnitX, driveOnLeft));
            var lineStart = mouth + 1.5f;
            road.AddCentreLine(RoadBuilder.Line(new Vector2(lineStart, 0f), new Vector2(end, 0f)), end - lineStart);
            return road.Build(device);
        }

        // A roundabout: a kerbed island with a grassy mound on it `islandHeight` high, `ringWidth` of tarmac round it, and a road out through the
        // middle of each edge of the square named in `arms`, their kerbs rounding onto the ring with
        // `cornerRadius`. The island and the ring's kerb have `sides` sides. Give-way lines across each
        // road's lane coming in.
        public static MeshData Roundabout(GraphicsDevice device, float size = 40f, float islandRadius = 6f, float ringWidth = 8f,
            int sides = 32, float cornerRadius = 4f, RoadArms arms = RoadArms.All, bool driveOnLeft = true, int cornerSegments = 8,
            float islandHeight = 1.5f)
        {
            var end = size / 2f;
            var ring = islandRadius + ringWidth;   // to the kerb round the outside
            CheckCorner(cornerRadius);
            // how far out along a road its kerb comes off the rounded corner and runs straight
            var straightFrom = MathF.Sqrt((ring + cornerRadius) * (ring + cornerRadius) - (Half + cornerRadius) * (Half + cornerRadius));
            if (straightFrom >= end)
                throw new ArgumentOutOfRangeException(nameof(size), "Too small for the ring and the corners onto it.");

            var road = new RoadBuilder();

            // Going round anticlockwise (+X towards +Z): East, South, West, North
            var ways = new List<Vector2>();
            foreach (var (arm, angle) in new[] { (RoadArms.East, 0f), (RoadArms.South, MathHelper.PiOver2),
                                                  (RoadArms.West, MathHelper.Pi), (RoadArms.North, MathHelper.Pi * 1.5f) })
                if (arms.HasFlag(arm))
                    ways.Add(new Vector2(MathF.Cos(angle), MathF.Sin(angle)));

            if (ways.Count == 0)
            {
                var kerb = RoadBuilder.Arc(Vector2.Zero, ring, 0f, MathHelper.TwoPi, sides);
                kerb.RemoveAt(kerb.Count - 1);
                road.AddTarmacFan(Vector2.Zero, kerb, closed: true);
                road.AddPavement(kerb, closed: true);
            }
            else
            {
                // The kerb all the way round, a pavement at a time: from where one road leaves the square, in along
                // its kerb, round a corner onto the ring, round to the next road, round its corner and out along
                // it. Every point is further round than the one before, so the tarmac is a fan from the middle.
                var outline = new List<Vector2>();
                for (var k = 0; k < ways.Count; k++)
                {
                    var from = ways[k];
                    var to = ways[(k + 1) % ways.Count];
                    var fromSide = Side(from);      // towards the next road round
                    var toSide = -Side(to);         // back towards this one

                    var fromCorner = fromSide * (Half + cornerRadius) + from * straightFrom;
                    var toCorner = toSide * (Half + cornerRadius) + to * straightFrom;
                    var fromOnRing = fromCorner * (ring / (ring + cornerRadius));
                    var toOnRing = toCorner * (ring / (ring + cornerRadius));

                    var start = MathF.Atan2(fromOnRing.Y, fromOnRing.X);
                    var turn = MathF.Atan2(toOnRing.Y, toOnRing.X) - start;
                    while (turn <= 0f)
                        turn += MathHelper.TwoPi;
                    var ringSegments = Math.Max(1, (int)MathF.Ceiling(sides * turn / MathHelper.TwoPi));

                    var kerb = RoadBuilder.Path(
                        new[] { fromSide * Half + from * end },
                        RoadBuilder.Fillet(fromCorner, fromSide * Half + from * straightFrom, fromOnRing, cornerSegments),
                        RoadBuilder.Arc(Vector2.Zero, ring, start, start + turn, ringSegments),
                        RoadBuilder.Fillet(toCorner, toOnRing, toSide * Half + to * straightFrom, cornerSegments),
                        new[] { toSide * Half + to * end });
                    road.AddPavement(kerb);
                    outline.AddRange(kerb);
                }
                road.AddTarmacFan(Vector2.Zero, outline, closed: true);

                foreach (var way in ways)
                {
                    var mouth = ring + 0.5f;
                    road.AddGiveWay(way * mouth, way, InLane(way, driveOnLeft));
                    var lineStart = mouth + 1.5f;
                    road.AddCentreLine(RoadBuilder.Line(way * lineStart, way * end), end - lineStart);
                }
            }

            // The island: kerbed all round, and a grassy mound on it, sloping up from just inside the kerb to a
            // flat top half as wide, so it stands up out of the ring and hides the far side from a driver
            var kerbTop = RoadBuilder.SurfaceHeight + RoadBuilder.KerbHeight;
            road.Mesh.AddFrustum(new Vector3(0f, RoadBuilder.SurfaceHeight, 0f), islandRadius, islandRadius, RoadBuilder.KerbHeight,
                sides, RoadBuilder.Kerb, topSlot: RoadBuilder.Verge);
            if (islandHeight > RoadBuilder.KerbHeight)
                road.Mesh.AddFrustum(new Vector3(0f, kerbTop, 0f), islandRadius - 0.3f, islandRadius * 0.5f,
                    islandHeight - RoadBuilder.KerbHeight, sides, RoadBuilder.Verge, topSlot: RoadBuilder.Verge);
            return road.Build(device);
        }

        private static void CheckCorner(float cornerRadius)
        {
            if (cornerRadius <= RoadBuilder.PavementWidth)
                throw new ArgumentOutOfRangeException(nameof(cornerRadius), "The pavement round a corner needs a wider corner.");
        }

        // A quarter turn from `way` (+X towards +Z): +Z for a road going +X.
        private static Vector2 Side(Vector2 way) => new Vector2(-way.Y, way.X);

        // From the centre line of a road leaving along `way`, towards its lane coming in.
        private static Vector2 InLane(Vector2 way, bool driveOnLeft) => driveOnLeft ? Side(way) : -Side(way);
    }
}
