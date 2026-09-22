using System.Collections.Generic;

using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData.Helpers
{
    // Which way a flight turns onto the landing that follows it.
    public enum StairTurn { Left, Right }

    // Shared by StaircaseMesh: an open-riser wooden staircase — treads on two diagonal stringers,
    // no risers between them and no banister, with an optional carpet runner inset on each tread.
    // Climbs from the origin along +Z; a flight that ends in a turn is followed by a square
    // landing (one flight-width deep) before the next flight continues 90 degrees left or right.
    //
    // Slots: Tread (the walked-on top face), Trim (stringers, and the tread/landing edges facing
    // the stair's open side), Landing (a turn landing's top face) and Carpet.
    public static class StaircaseBuilder
    {
        public const int Tread = 0, Trim = 1, Landing = 2, Carpet = 3;
        public const int PaletteSize = 4;

        public static Color[] Palette(Color wood, Color carpet)
        {
            var trim = new Color((int)(wood.R * 0.7f), (int)(wood.G * 0.7f), (int)(wood.B * 0.7f));
            return new[] { wood, trim, wood, carpet };
        }

        // One straight run of `Steps` steps. TurnAfter, if set, adds a landing at the top of this
        // flight and rotates the next flight 90 degrees left or right; leave it null on the last
        // flight (or on the only flight, for a straight staircase).
        public readonly record struct Flight(int Steps, StairTurn? TurnAfter = null);

        public static MeshData Build(
            GraphicsDevice device,
            Flight[] flights,
            float width = 0.9f,
            float rise = 0.18f,
            float run = 0.26f,
            float treadThickness = 0.04f,
            float stringerThickness = 0.05f,
            bool carpet = true,
            float carpetSideInset = 0.10f,
            float carpetEndInset = 0.02f,
            float carpetThickness = 0.015f)
        {
            var mesh = new MeshBuilder();

            // Collected across every flight and emitted as one batch per slot at the end, so an
            // N-step staircase costs a constant handful of draw ranges rather than growing with N.
            var treadTops = new List<Vector3[]>();
            var trimQuads = new List<Vector3[]>();
            var landingTops = new List<Vector3[]>();
            var carpetTops = new List<Vector3[]>();

            var origin = Vector3.Zero;
            var forward = Vector3.UnitZ;
            var right = Vector3.UnitX;
            var height = 0f;

            foreach (var flight in flights)
            {
                for (var i = 0; i < flight.Steps; i++)
                {
                    var stepTopY = height + (i + 1) * rise;
                    var centerAlong = (i + 0.5f) * run;
                    var bottomCenter = origin + forward * centerAlong + Vector3.Up * (stepTopY - treadThickness);
                    var (a, b, c, d, e, f, g, h) = BoxCorners(bottomCenter, forward, right, run, width, treadThickness);

                    treadTops.Add(new[] { e, f, g, h });
                    trimQuads.Add(new[] { b, c, g, f });  // nosing, facing down the flight
                    trimQuads.Add(new[] { a, b, f, e });  // left flank
                    trimQuads.Add(new[] { c, d, h, g });  // right flank
                    mesh.AddLineLoop(e, f, g, h);
                    mesh.AddLine(b, f);
                    mesh.AddLine(c, g);

                    if (carpet)
                    {
                        var carpetBottom = origin + forward * centerAlong + Vector3.Up * stepTopY;
                        var (_, _, _, _, ce, cf, cg, ch) = BoxCorners(
                            carpetBottom, forward, right, run - carpetEndInset * 2f, width - carpetSideInset * 2f, carpetThickness);
                        carpetTops.Add(new[] { ce, cf, cg, ch });
                        mesh.AddLineLoop(ce, cf, cg, ch);
                    }
                }

                var flightRun = flight.Steps * run;
                var flightRise = flight.Steps * rise;

                // Diagonal stringers along both open edges of the flight, from its foot to its head.
                foreach (var side in new[] { -1f, 1f })
                {
                    var edgeOffset = right * (side * width * 0.5f);
                    var start = origin + edgeOffset + Vector3.Up * height;
                    var end = origin + forward * flightRun + edgeOffset + Vector3.Up * (height + flightRise);
                    mesh.AddTube(start, end, stringerThickness * 0.5f, stringerThickness * 0.5f, 4, Trim);
                }

                origin += forward * flightRun;
                height += flightRise;

                if (flight.TurnAfter is { } turn)
                {
                    var landingBottom = origin + forward * (width * 0.5f) + Vector3.Up * (height - treadThickness);
                    var (a, b, c, d, e, f, g, h) = BoxCorners(landingBottom, forward, right, width, width, treadThickness);
                    landingTops.Add(new[] { e, f, g, h });
                    trimQuads.Add(new[] { b, c, g, f });
                    trimQuads.Add(new[] { a, b, f, e });
                    trimQuads.Add(new[] { c, d, h, g });
                    mesh.AddLineLoop(e, f, g, h);
                    mesh.AddLine(b, f);
                    mesh.AddLine(c, g);

                    // The next flight's width runs along this flight's forward axis, so for it to sit
                    // flush against the landing (rather than floating off past its far edge) it must
                    // start centred on the landing's depth, hinged off whichever side edge it turns
                    // toward — not off the landing's far edge, which is where its centreline used to
                    // be placed.
                    var turnSide = turn == StairTurn.Right ? 1f : -1f;
                    origin = origin + forward * (width * 0.5f) + right * (turnSide * width * 0.5f);

                    var rotation = Matrix.CreateRotationY(turn == StairTurn.Right ? MathHelper.PiOver2 : -MathHelper.PiOver2);
                    forward = Vector3.Normalize(Vector3.TransformNormal(forward, rotation));
                    right = Vector3.Normalize(Vector3.TransformNormal(right, rotation));
                }
            }

            AddQuadBatch(mesh, Tread, treadTops);
            AddQuadBatch(mesh, Trim, trimQuads);
            AddQuadBatch(mesh, Landing, landingTops);
            if (carpet)
                AddQuadBatch(mesh, Carpet, carpetTops);

            return mesh.Build(device);
        }

        // Skips the range entirely when empty — a zero-primitive draw range is a wasted draw call
        // (and there may be no landings at all, on a staircase with no turns).
        private static void AddQuadBatch(MeshBuilder mesh, int colorSlot, List<Vector3[]> quads)
        {
            if (quads.Count == 0)
                return;

            mesh.AddSolidRange(quads.Count * 2, colorSlot);
            foreach (var q in quads)
                mesh.AddQuad(q[0], q[1], q[2], q[3]);
        }

        // The 8 corners of a box whose bottom face is centred on bottomCenter, oriented by an
        // arbitrary (forward, right) pair rather than MeshBuilder.AddBox's fixed world axes — needed
        // here since a flight after a turn no longer runs along +Z.
        private static (Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e, Vector3 f, Vector3 g, Vector3 h) BoxCorners(
            Vector3 bottomCenter, Vector3 forward, Vector3 right, float length, float width, float height)
        {
            var hf = forward * (length * 0.5f);
            var hr = right * (width * 0.5f);
            var hu = Vector3.Up * height;

            var a = bottomCenter - hf - hr;
            var b = bottomCenter + hf - hr;
            var c = bottomCenter + hf + hr;
            var d = bottomCenter - hf + hr;
            return (a, b, c, d, a + hu, b + hu, c + hu, d + hu);
        }
    }
}
