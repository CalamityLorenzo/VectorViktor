using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace World.Buildings
{
    // A staircase built against a room's walls, climbing round the room rather than across its floor: one
    // flight along each of a run of consecutive edges of the room's Outline (in its winding order), turning
    // at each corner on a landing that fills the corner. Every flight but the first fills its wall; the
    // first may be shorter, starting partway along its wall, from the floor. The corners it turns must be
    // convex (an octagon's are).
    //
    // It's solid rather than open-tread: treads and risers, a side face down the open (room) side, and a
    // sloping underside you can walk beneath once it's high enough. Nothing is drawn against the wall
    // itself. It's in the room's own coordinates, so it goes in as a PropSpec at the origin, with Ramps()
    // in the room's Ramps so it can be climbed, and Hatch() as the room's ceiling hatch if it leads up
    // through the ceiling. The top of its last flight has no end face: that's where the slab's own edge is.
    public sealed class WallStair
    {
        public const int Tread = 0, Riser = 1, Side = 2, Underside = 3;
        public const int PaletteSize = 4;

        // How far the underside sits below the line of the step nosings, straight down. Must be more than a
        // step's rise, or the stair is thinner than nothing at the back of each tread.
        private const float Waist = 0.35f;

        // Start and End are the flight's whole run along the foot of its wall (a full flight's length);
        // its Steps take up the last Steps * Going of it. Base is the height it climbs from.
        private readonly record struct Flight(Vector2 Start, Vector2 End, Vector2 Inward, int Steps, float Going, float Base);

        // The corner between two flights: the room's own corner, the inner corner where the flights' open
        // sides meet, and the feet of the two flights on their walls.
        private readonly record struct Landing(Vector2 Corner, Vector2 Inner, Vector2 FootBefore, Vector2 FootAfter, float Height);

        private readonly Flight[] _flights;
        private readonly Landing[] _landings;

        public float Width { get; }
        public float Rise { get; }   // per step

        // steps[i] is the number of steps on the i-th wall from firstWall; stepsPerWall is how many a full
        // wall holds (which sets the tread depth), so every entry but the first must equal it. The stair
        // climbs `height` in all, each step the same rise.
        public WallStair(Vector2[] outline, int firstWall, int[] steps, int stepsPerWall, float height, float width)
        {
            if (steps.Length == 0 || steps[0] < 1 || steps[0] > stepsPerWall)
                throw new ArgumentException("The first flight needs between 1 and stepsPerWall steps.", nameof(steps));
            for (var i = 1; i < steps.Length; i++)
                if (steps[i] != stepsPerWall)
                    throw new ArgumentException("Only the first flight can be shorter than a full wall.", nameof(steps));

            var n = outline.Length;
            Vector2 Inward(int wall)
            {
                var t = Vector2.Normalize(outline[(wall + 1) % n] - outline[wall]);
                return new Vector2(-t.Y, t.X);   // matches RoomSpec.Inward
            }
            // Where the open sides of the flights along `wall` and the next wall meet: `width` in from both.
            Vector2 InnerCorner(int wall)
            {
                var a = Inward(wall);
                var b = Inward((wall + 1) % n);
                return outline[(wall + 1) % n] + (a + b) * width / (1f + Vector2.Dot(a, b));
            }

            var total = 0;
            foreach (var s in steps)
                total += s;
            Width = width;
            Rise = height / total;

            _flights = new Flight[steps.Length];
            _landings = new Landing[steps.Length - 1];
            var climbed = 0f;
            for (var i = 0; i < steps.Length; i++)
            {
                var wall = (firstWall + i) % n;
                var inward = Inward(wall);
                var start = InnerCorner((wall - 1 + n) % n) - inward * width;
                var end = InnerCorner(wall) - inward * width;
                _flights[i] = new Flight(start, end, inward, steps[i], Vector2.Distance(start, end) / stepsPerWall, climbed);
                climbed += steps[i] * Rise;

                if (i < steps.Length - 1)
                {
                    var inner = InnerCorner(wall);
                    _landings[i] = new Landing(outline[(wall + 1) % n], inner, end, inner - Inward((wall + 1) % n) * width, climbed);
                }
            }
        }

        public static Color[] Palette(Color tread, Color riser, Color side, Color underside) => new[] { tread, riser, side, underside };

        // The hole the last flight climbs up through: its whole run, flush against its wall, and `margin`
        // wider than the stair so there's space between its open side and the slab's edge.
        public Vector2[] Hatch(float margin)
        {
            var last = _flights[^1];
            var across = last.Inward * (Width + margin);
            return new[] { last.Start, last.End, last.End + across, last.Start + across };
        }

        // What a walker climbs: each flight as a straight slope along its middle, and each landing as two
        // flat strips, one carrying on from each flight into the corner, which between them cover it. All
        // solid down to the stair's underside (see RampSpec.Thickness), so you can't walk into its side.
        public RampSpec[] Ramps()
        {
            var ramps = new List<RampSpec>();
            foreach (var flight in _flights)
            {
                var (along, length, first) = Run(flight);
                var middle = flight.Start + flight.Inward * (Width / 2f);
                ramps.Add(new RampSpec(At(middle + along * first, flight.Base), At(middle + along * length, flight.Base + flight.Steps * Rise), Width,
                                       Thickness: Waist));
            }
            foreach (var landing in _landings)
                foreach (var foot in new[] { landing.FootBefore, landing.FootAfter })
                {
                    var middle = foot + (landing.Inner - foot) / 2f;
                    ramps.Add(new RampSpec(At(middle, landing.Height), At(middle + (landing.Corner - foot), landing.Height), Width,
                                           Thickness: Waist - Rise));
                }
            return ramps.ToArray();
        }

        public MeshData Build(GraphicsDevice device)
        {
            // One draw range per colour, however many steps there are (see MeshBuilder)
            var mesh = new MeshBuilder();
            foreach (var flight in _flights)
                AddFlight(mesh, flight);
            foreach (var landing in _landings)
                AddLanding(mesh, landing);
            return mesh.Build(device);
        }

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);

        // The flight's direction up its wall, its full run, and how far along that its first riser stands.
        private static (Vector2 along, float length, float first) Run(Flight flight)
        {
            var length = Vector2.Distance(flight.Start, flight.End);
            return (Vector2.Normalize(flight.End - flight.Start), length, length - flight.Steps * flight.Going);
        }

        private void AddFlight(MeshBuilder mesh, Flight flight)
        {
            var (along, length, first) = Run(flight);
            var w = Width;

            // `a` along the wall from the flight's Start, `lateral` in from the wall
            Vector3 P(float a, float lateral, float y) => At(flight.Start + along * a + flight.Inward * lateral, y);

            // The underside runs parallel to the line of the nosings, Waist below it, and stops at the floor
            var slope = Rise / flight.Going;
            float Under(float a) => MathF.Max(0f, flight.Base + Rise + (a - first) * slope - Waist);
            var floorEnd = MathHelper.Clamp(first + (Waist - Rise - flight.Base) / slope, first, length);   // where it leaves the floor

            for (var i = 0; i < flight.Steps; i++)
            {
                var a0 = first + i * flight.Going;
                var a1 = a0 + flight.Going;
                var low = flight.Base + i * Rise;
                var high = low + Rise;

                mesh.AddQuad(Tread, P(a0, 0f, high), P(a1, 0f, high), P(a1, w, high), P(a0, w, high));
                mesh.AddQuad(Riser, P(a0, 0f, low), P(a0, w, low), P(a0, w, high), P(a0, 0f, high));

                // This step's slice of the open side, down to the underside - with a kink where that meets the floor
                var side = new List<Vector3> { P(a0, w, Under(a0)) };
                if (floorEnd > a0 && floorEnd < a1)
                    side.Add(P(floorEnd, w, 0f));
                side.Add(P(a1, w, Under(a1)));
                side.Add(P(a1, w, high));
                side.Add(P(a0, w, high));
                mesh.AddPolygon(Side, side.ToArray());

                mesh.AddLine(P(a0, 0f, low), P(a0, w, low));
                mesh.AddLine(P(a0, 0f, high), P(a0, w, high));
                foreach (var lateral in new[] { 0f, w })
                {
                    mesh.AddLine(P(a0, lateral, low), P(a0, lateral, high));
                    mesh.AddLine(P(a0, lateral, high), P(a1, lateral, high));
                }
            }
            var top = flight.Base + flight.Steps * Rise;
            mesh.AddLine(P(length, 0f, top), P(length, w, top));

            if (floorEnd < length)
            {
                mesh.AddQuad(Underside, P(floorEnd, 0f, Under(floorEnd)), P(length, 0f, Under(length)), P(length, w, Under(length)), P(floorEnd, w, Under(floorEnd)));
                foreach (var lateral in new[] { 0f, w })
                    mesh.AddLine(P(floorEnd, lateral, Under(floorEnd)), P(length, lateral, Under(length)));
            }
            if (floorEnd > first)
                mesh.AddLine(P(first, w, 0f), P(floorEnd, w, 0f));
        }

        // A slab filling the corner, level with the top of the flight before it. Its open corner is just
        // the point where the two flights' sides meet, so it has no side faces of its own - only a top and
        // an underside, which is level with where both flights' undersides reach it.
        private void AddLanding(MeshBuilder mesh, Landing landing)
        {
            var top = landing.Height;
            var under = top + Rise - Waist;
            Vector3[] Kite(float y) => new[] { At(landing.FootBefore, y), At(landing.Corner, y), At(landing.FootAfter, y), At(landing.Inner, y) };

            mesh.AddPolygon(Tread, Kite(top));
            mesh.AddPolygon(Underside, Kite(under));

            foreach (var y in new[] { top, under })
            {
                mesh.AddLine(At(landing.FootBefore, y), At(landing.Corner, y));
                mesh.AddLine(At(landing.Corner, y), At(landing.FootAfter, y));
            }
            mesh.AddLine(At(landing.Inner, under), At(landing.FootBefore, under));
            mesh.AddLine(At(landing.Inner, under), At(landing.FootAfter, under));
        }
    }
}
