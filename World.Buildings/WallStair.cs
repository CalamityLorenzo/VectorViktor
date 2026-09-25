using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core;

namespace World.Buildings
{
    // A staircase built against a room's walls, climbing round the room rather than across its floor: one
    // flight along each of a run of consecutive edges of the room's Outline (in its winding order), turning
    // at each corner on a landing that fills the corner. Every flight but the first fills its wall; the
    // first may be shorter, starting partway along its wall, from the floor. The corners it turns must be
    // convex (an octagon's are).
    //
    // It's solid rather than open-tread: treads and risers, a side face down the open (room) side, and a
    // sloping underside you can walk beneath once it's high enough (WallStairMesh draws it). Nothing is drawn
    // against the wall itself. It's in the room's own coordinates, so it goes in as a PropSpec at the origin,
    // with Ramps() in the room's Ramps so it can be climbed, and Hatch() as the room's ceiling hatch if it leads
    // up through the ceiling. The top of its last flight has no end face: that's where the slab's own edge is.
    public sealed class WallStair
    {
        // How far the underside sits below the line of the step nosings, straight down. Must be more than a
        // step's rise, or the stair is thinner than nothing at the back of each tread.
        public const float Waist = 0.35f;

        // Start and End are the flight's whole run along the foot of its wall (a full flight's length);
        // its Steps take up the last Steps * Going of it. Base is the height it climbs from.
        public readonly record struct Flight(Vector2 Start, Vector2 End, Vector2 Inward, int Steps, float Going, float Base)
        {
            // Its direction up its wall, its full run, and how far along that its first riser stands.
            public (Vector2 along, float length, float first) Run()
            {
                var length = Vector2.Distance(Start, End);
                return (Vector2.Normalize(End - Start), length, length - Steps * Going);
            }
        }

        // The corner between two flights: the room's own corner, the inner corner where the flights' open
        // sides meet, and the feet of the two flights on their walls.
        public readonly record struct Landing(Vector2 Corner, Vector2 Inner, Vector2 FootBefore, Vector2 FootAfter, float Height);

        private readonly Flight[] _flights;
        private readonly Landing[] _landings;

        public IReadOnlyList<Flight> Flights => _flights;
        public IReadOnlyList<Landing> Landings => _landings;

        public float Width { get; }
        public float Rise { get; }   // per step

        // Everything that decides its shape, so two stairs share a mesh (see MeshCache) only if they're the same stair.
        // Two with the same key are the same stair.
        public string Key { get; }
        public override bool Equals(object? obj) => obj is WallStair other && other.Key == Key;
        public override int GetHashCode() => Key.GetHashCode();

        // steps[i] is the number of steps on the i-th wall from firstWall; stepsPerWall is how many a full
        // wall holds (which sets the tread depth), so every entry but the first must equal it. The stair
        // climbs `height` in all, each step the same rise.
        public WallStair(Vector2[] outline, int firstWall, int[] steps, int stepsPerWall, float height, float width)
        {
            Key = $"wallstair:{string.Join(";", Array.ConvertAll(outline, p => $"{p.X:F3},{p.Y:F3}"))}:{firstWall}:{string.Join(",", steps)}:{stepsPerWall}:{height:F3}:{width:F3}";
            if (steps.Length == 0 || steps[0] < 1 || steps[0] > stepsPerWall)
                throw new ArgumentException("The first flight needs between 1 and stepsPerWall steps.", nameof(steps));
            for (var i = 1; i < steps.Length; i++)
                if (steps[i] != stepsPerWall)
                    throw new ArgumentException("Only the first flight can be shorter than a full wall.", nameof(steps));

            var n = outline.Length;
            Vector2 Inward(int wall) => -Geometry2D.Outward(outline[wall], outline[(wall + 1) % n]);   // matches RoomSpec.Inward
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
                var (along, length, first) = flight.Run();
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

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);
    }
}
