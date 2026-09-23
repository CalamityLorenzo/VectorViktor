using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Basic.Levels
{
    // Names for the edges of a room built by RoomSpec.Rectangle: 0 = North, 1 = East, 2 = South, 3 = West.
    // A room with its own arbitrary Outline has no fixed wall count, so its doors and openings just use
    // the edge's index into Outline directly - these are only for the common rectangular case.
    public static class Walls
    {
        public const int North = 0, East = 1, South = 2, West = 3;
    }

    // A door in a wall. WallIndex is which edge of the room's Outline it's in (see RoomSpec.Rectangle for
    // the rectangular case's numbering). Offset is how far along that edge its centre is, measured from
    // the edge's own midpoint in the direction the edge runs (Outline[WallIndex] -> Outline[WallIndex+1]).
    // Walking into it puts you at TargetDoor in TargetRoom.
    public record DoorSpec(string Id, int WallIndex, float Offset, string TargetRoom, string TargetDoor);

    // A gap in a wall, from the floor up, that leads straight on into the neighbouring room TargetRoom.
    // The two rooms must sit edge to edge in the world (see RoomSpec.WorldOffset), and the neighbour has
    // an opening of its own in the matching wall. At most one per edge.
    public record OpeningSpec(int WallIndex, float Offset, float Width, float Height, string TargetRoom);

    // A hole in a room's ceiling or floor, leading into TargetRoom directly above or below it. Outline is a
    // convex polygon in the room's own (X, Z). The room below lists it in its CeilingHatches, the room above
    // in its FloorHatches, both at the same place in the world.
    //
    // SlabThickness (ceiling hatches only) is how thick the floor between the two rooms is: the room above
    // must sit that far above this one's ceiling (its WorldOffset.Y == WorldOffset.Y + Height + SlabThickness),
    // and the hatch is lined with the slab's cut edge. See RoomSpec.CeilingHatches for why it can't be zero.
    public record HatchSpec(Vector2[] Outline, string TargetRoom, float SlabThickness = 0f)
    {
        public bool Contains(Vector3 local) => RoomSpec.InPolygon(Outline, local);
    }

    // A climbable strip in the room's own coordinates: a corridor Width wide down the line from Start to
    // End, whose floor rises (or stays flat, if Start.Y == End.Y) linearly between them. Matches a
    // staircase flight, a landing, a ladder, or a platform's own deck, so a walker's height can follow
    // it. Every RoomSpec.WalkHeightAt call checks every ramp in the room, so keep a room's Ramps to the
    // handful that are actually walked on.
    //
    // Steps > 0 also gives it treads and risers, cut into the room's own floor - a staircase built into
    // the room's own shell rather than a separate prop, the room's ceiling sloping to match so headroom
    // stays constant along it (see RoomSpec.CeilingHeightAt). It can run any direction, not just north
    // to south, and the room doesn't have to be a rectangle - RoomMesh splits the floor at Start and End
    // and fits the flat part(s) round whatever shape is left. Steps == 0 (the default) is an ordinary
    // invisible ramp: nothing is cut, so it needs its own visible mesh (a PropSpec) if it should be seen,
    // the way the hangar's balcony staircase and ladder are.
    //
    // MaxStepUp is how far above a walker's current height this ramp may still lift them in one go
    // (see RoomSpec.WalkHeightAt) — it exists to stop wandering under a platform from snapping you
    // straight up onto it, not to model real footing, so a steep ramp (a near-vertical ladder rising
    // several metres over a run of one) needs a much larger value than a shallow staircase does:
    // at running speed, a couple of frames' horizontal movement up a slope that steep already outruns
    // the default, and the walker would be dropped back to the floor instead of climbing.
    public readonly record struct RampSpec(Vector3 Start, Vector3 End, float Width, float MaxStepUp = RoomSpec.DefaultMaxStepUp, int Steps = 0)
    {
        public bool Contains(Vector3 local)
        {
            var along = new Vector3(End.X - Start.X, 0f, End.Z - Start.Z);
            var lengthSq = along.LengthSquared();
            if (lengthSq < 1e-6f)
                return false;

            var offset = new Vector3(local.X - Start.X, 0f, local.Z - Start.Z);
            var t = Vector3.Dot(offset, along) / lengthSq;
            if (t < 0f || t > 1f)
                return false;

            return Vector3.Distance(offset, along * t) <= Width / 2f;
        }

        public float HeightAt(Vector3 local)
        {
            var along = new Vector3(End.X - Start.X, 0f, End.Z - Start.Z);
            var lengthSq = along.LengthSquared();
            if (lengthSq < 1e-6f)
                return Start.Y;

            var offset = new Vector3(local.X - Start.X, 0f, local.Z - Start.Z);
            var t = MathHelper.Clamp(Vector3.Dot(offset, along) / lengthSq, 0f, 1f);
            return MathHelper.Lerp(Start.Y, End.Y, t);
        }

        // Whether local falls between Start and End along the ramp's own run, regardless of Width -
        // unlike Contains, which is only true within the ramp's own footprint. Used for the ceiling
        // above a stepped ramp, which follows the slope across the room's whole breadth, not just the
        // ramp's own width.
        public bool InRange(Vector3 local)
        {
            var along = new Vector3(End.X - Start.X, 0f, End.Z - Start.Z);
            var lengthSq = along.LengthSquared();
            if (lengthSq < 1e-6f)
                return false;
            var offset = new Vector3(local.X - Start.X, 0f, local.Z - Start.Z);
            var t = Vector3.Dot(offset, along) / lengthSq;
            return t >= 0f && t <= 1f;
        }
    }

    // A piece of furniture standing in a room. Position is where the mesh's origin goes, YawDegrees turns
    // its front (+Z) round the vertical. Half is the half-extent (X, Z) of the floor area it blocks,
    // already turned to the room's axes; leave it at zero for something you can't walk into (a TV on a sideboard).
    public record PropSpec(string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette,
                           Vector3 Position, float YawDegrees, Vector2 Half = default)
    {
        public bool Blocks => Half != Vector2.Zero;
    }

    // A room's footprint is an arbitrary simple polygon (Outline) in its own (X, Z) coordinates, floor at
    // y = 0: convex or concave, any number of sides, so long as it doesn't cross itself. A Vector2's X, Y
    // here are the room's X, Z. Outline.RoomSpec.Rectangle and RoomSpec.RegularPolygon build the two common
    // shapes; anything else (an L, a hexagon, ...) is just its own list of points.
    //
    // The vertices must run the same way Rectangle's do (north edge, then east, then south, then west, as
    // seen from above with Z increasing southward) - RoomMesh's walls, and RoomSpec's own Inward, are
    // derived from that winding, and get the wrong sign if it's reversed.
    public sealed class RoomSpec
    {
        public const float DoorWidth = 0.9f, DoorHeight = 2.0f;

        public required string Id { get; init; }
        public required string Name { get; init; }
        public required Vector2[] Outline { get; init; }
        public required float Height { get; init; }

        // Flat colours; the two wall colours alternate by edge index (even/odd) so a rectangular room's
        // two wall pairs still read as a solid colour scheme, and any other room's walls still alternate.
        public required Color Floor { get; init; }
        public required Color WallA { get; init; }
        public required Color WallB { get; init; }
        public required Color Ceiling { get; init; }

        // Where the room's floor centre is in the world. Only rooms joined by openings need to fit together;
        // a room reached only through doors can sit anywhere (they are never drawn side by side).
        public Vector3 WorldOffset { get; init; }
        public float GridSpacing { get; init; } = 1f;
        public RampSpec[] Ramps { get; init; } = Array.Empty<RampSpec>();

        public DoorSpec[] Doors { get; init; } = Array.Empty<DoorSpec>();
        public PropSpec[] Props { get; init; } = Array.Empty<PropSpec>();
        public OpeningSpec[] Openings { get; init; } = Array.Empty<OpeningSpec>();

        // Holes through to rooms stacked directly above or below (see HatchSpec). Stacked rooms need a real
        // slab between them, not the upper floor lying on this ceiling: two faces in one plane z-fight, and
        // every face is drawn pushed back in depth (MeshInstance's DepthBias, which grows with distance) so
        // that its own edges win - enough to let the upper room's floor grid show through a ceiling it is
        // flush with, or only a few centimetres above.
        public HatchSpec[] CeilingHatches { get; init; } = Array.Empty<HatchSpec>();
        public HatchSpec[] FloorHatches { get; init; } = Array.Empty<HatchSpec>();

        // Every room that can be seen from this one: through an opening, or up or down through a hatch.
        public IEnumerable<string> Neighbours()
        {
            foreach (var opening in Openings)
                yield return opening.TargetRoom;
            foreach (var hatch in CeilingHatches)
                yield return hatch.TargetRoom;
            foreach (var hatch in FloorHatches)
                yield return hatch.TargetRoom;
        }

        // An axis-aligned rectangle, width x depth, centred on the origin - its four edges come out as
        // Walls.North, .East, .South, .West in that order.
        public static Vector2[] Rectangle(float width, float depth)
        {
            var hw = width / 2f;
            var hd = depth / 2f;
            return new[] { new Vector2(-hw, -hd), new Vector2(hw, -hd), new Vector2(hw, hd), new Vector2(-hw, hd) };
        }

        // A regular polygon with `sides` sides (3 or more), each vertex `radius` out from the centre, the
        // first one due north - wound the same way Rectangle is.
        public static Vector2[] RegularPolygon(int sides, float radius)
        {
            var points = new Vector2[sides];
            for (var i = 0; i < sides; i++)
            {
                var angle = i * MathHelper.TwoPi / sides;
                points[i] = new Vector2(radius * MathF.Sin(angle), -radius * MathF.Cos(angle));
            }
            return points;
        }

        // How high the ceiling is above this point: flat at Height, except above a stepped ramp (see
        // RampSpec.Steps), which it slopes to follow across the room's whole breadth, so headroom stays
        // constant along it. The first stepped ramp whose run contains the point wins.
        public float CeilingHeightAt(Vector3 local)
        {
            foreach (var ramp in Ramps)
                if (ramp.Steps > 0 && ramp.InRange(local))
                    return Height + ramp.HeightAt(local);
            return Height;
        }

        // Default RampSpec.MaxStepUp: generous enough for a shallow staircase's per-frame rise, but see
        // RampSpec for why a steep ramp (a ladder) needs to override it with something much larger.
        public const float DefaultMaxStepUp = 0.3f;

        // How high the floor is under a walker already standing `currentHeight` above this room's own
        // floor level, at this local (x, z): the room's own (flat) floor, or the highest ramp whose
        // footprint contains the point and whose height clears that ramp's own MaxStepUp - a stepped
        // ramp (see RampSpec.Steps) is walked exactly the same way as an invisible one.
        public float WalkHeightAt(Vector3 local, float currentHeight)
        {
            var height = 0f;
            foreach (var ramp in Ramps)
            {
                if (!ramp.Contains(local))
                    continue;
                var rampHeight = ramp.HeightAt(local);
                if (rampHeight <= currentHeight + ramp.MaxStepUp && rampHeight > height)
                    height = rampHeight;
            }
            return height;
        }

        // Whether a point (in the room's own coordinates) is over the room's floor. Only (X, Z) counts,
        // so rooms stacked on the same footprint both contain it - see Game1.ClimbThroughHatches.
        public bool Contains(Vector3 local) => InPolygon(Outline, local);

        // Whether (local.X, local.Z) is inside a polygon, convex or concave: the standard even-odd,
        // ray-casting test.
        public static bool InPolygon(Vector2[] polygon, Vector3 local)
        {
            var p = new Vector2(local.X, local.Z);
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

        // Pushes a walker (a circle of `radius` on the floor) back in from the room's boundary, convex or
        // concave corners alike: finds the single closest point anywhere on the Outline and, unless that's
        // squarely inside a door or opening's gap, pushes away from it. A concave (notch) corner falls
        // naturally out of this - the closest point may be a vertex rather than partway along an edge,
        // which is exactly what rounds the walker round it, the same way PushOutOfBox rounds a box corner.
        public Vector3 KeepInside(Vector3 p, float radius)
        {
            var q = new Vector2(p.X, p.Z);
            var bestDist = float.MaxValue;
            var bestPoint = Vector2.Zero;
            var bestEdge = 0;
            for (var i = 0; i < Outline.Length; i++)
            {
                var (a, b) = Edge(i);
                var nearest = NearestOnSegment(q, a, b);
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
            if (Array.Exists(Openings, o => o.WallIndex == bestEdge && MathF.Abs(AlongWall(bestEdge, p) - o.Offset) <= o.Width / 2f - radius))
                return p;

            var inside = Contains(p);
            if (inside && bestDist >= radius)
                return p;

            var direction = inside && bestDist > 1e-6f
                ? (q - bestPoint) / bestDist
                : new Vector2(Inward(bestEdge).X, Inward(bestEdge).Z);
            var pushed = bestPoint + direction * radius;
            return new Vector3(pushed.X, p.Y, pushed.Y);
        }

        private static Vector2 NearestOnSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lenSq = ab.LengthSquared();
            if (lenSq < 1e-9f)
                return a;
            var t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }

        // A circle of `radius` pushed clear of an axis-aligned box (centre, half-extent), by the shortest
        // route; if its centre is already inside the box, out through whichever side is nearest. Used for
        // furniture collision (see RoomView.PushOutOfProps) - a room's own boundary uses KeepInside instead.
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

        public DoorSpec FindDoor(string id) =>
            Array.Find(Doors, d => d.Id == id) ?? throw new ArgumentException($"Room '{Id}' has no door '{id}'.", nameof(id));

        // Outline[i] -> Outline[i + 1] (wrapping round), as a start point and its far point.
        private (Vector2 a, Vector2 b) Edge(int wallIndex) => (Outline[wallIndex], Outline[(wallIndex + 1) % Outline.Length]);

        public float WallLength(int wallIndex)
        {
            var (a, b) = Edge(wallIndex);
            return Vector2.Distance(a, b);
        }

        // The direction running along a wall, in the direction its offsets increase: Outline[i] toward Outline[i + 1].
        public Vector3 Tangent(int wallIndex)
        {
            var (a, b) = Edge(wallIndex);
            var d = Vector2.Normalize(b - a);
            return new Vector3(d.X, 0f, d.Y);
        }

        // The way a wall's normal points, into the room (Outline's winding decides which side that is).
        public Vector3 Inward(int wallIndex)
        {
            var t = Tangent(wallIndex);
            return new Vector3(-t.Z, 0f, t.X);
        }

        // The point on the floor, at the foot of the wall, `along` from its centre (the edge's midpoint).
        public Vector3 WallPoint(int wallIndex, float along)
        {
            var (a, b) = Edge(wallIndex);
            var mid = (a + b) / 2f;
            var p = mid + new Vector2(Tangent(wallIndex).X, Tangent(wallIndex).Z) * along;
            return new Vector3(p.X, 0f, p.Y);
        }

        public float DistanceToWall(int wallIndex, Vector3 p)
        {
            var (a, _) = Edge(wallIndex);
            var inward = Inward(wallIndex);
            return (p.X - a.X) * inward.X + (p.Z - a.Y) * inward.Z;
        }

        public float AlongWall(int wallIndex, Vector3 p)
        {
            var (a, b) = Edge(wallIndex);
            var mid = (a + b) / 2f;
            var t = Tangent(wallIndex);
            return (p.X - mid.X) * t.X + (p.Z - mid.Y) * t.Z;
        }
    }
}
