using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core;

namespace World.Buildings
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
    // Walking into it puts you at TargetDoor in TargetRoom (in Basic.Levels: see its RoomWalking).
    public record DoorSpec(string Id, int WallIndex, float Offset, string TargetRoom, string TargetDoor);

    // A gap in a wall, from the floor up, that leads straight on into the neighbouring room TargetRoom.
    // The two rooms must sit edge to edge in the world (see RoomSpec.WorldOffset), and the neighbour has
    // an opening of its own in the matching wall. At most one per edge. A null TargetRoom leads outside:
    // a building's doorway (see Building), cut through its outer wall as well.
    //
    // Door hangs a door in it, in a building (see Door): one leaf, or two meeting in the middle if it's
    // wider than Door.MaxLeafWidth, hinged at the room's side of the wall and swinging into the room.
    // Between two rooms, set it on the opening in either one of them; it's hung only once.
    public record OpeningSpec(int WallIndex, float Offset, float Width, float Height, string TargetRoom, bool Door = false)
    {
        public bool LeadsOutside => TargetRoom == null;
    }

    // A hole in a room's ceiling or floor, leading into TargetRoom directly above or below it. Outline is a
    // convex polygon in the room's own (X, Z). The room below lists it in its CeilingHatches, the room above
    // in its FloorHatches, both at the same place in the world.
    //
    // SlabThickness (ceiling hatches only) is how thick the floor between the two rooms is: the room above
    // must sit that far above this one's ceiling (its WorldOffset.Y == WorldOffset.Y + Height + SlabThickness),
    // and the hatch is lined with the slab's cut edge. See RoomSpec.CeilingHatches for why it can't be zero.
    public record HatchSpec(Vector2[] Outline, string TargetRoom, float SlabThickness = 0f)
    {
        public bool Contains(Vector3 local) => Geometry2D.InPolygon(Outline, new Vector2(local.X, local.Z));

        // Whether any of `hatches` is over or under `local`: a loop, not Array.Exists, which would make a closure of `local` each time.
        public static bool AnyContain(HatchSpec[] hatches, Vector3 local)
        {
            foreach (var hatch in hatches)
                if (hatch.Contains(local))
                    return true;
            return false;
        }
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
    //
    // Thickness makes it solid: a walker beside it can't walk into it where it's more than a step above
    // their feet, from the side or head first - but can walk underneath it where its underside, Thickness
    // below its surface, clears their head (see RoomSpec.KeepOutOfRamps). A stepped ramp is solid right
    // down to the floor, whatever its Thickness. Zero (the default) is something you can walk straight
    // through at floor level: a ladder, or a ramp whose own mesh leaves room under it.
    public readonly record struct RampSpec(Vector3 Start, Vector3 End, float Width, float MaxStepUp = RoomSpec.DefaultMaxStepUp, int Steps = 0,
                                           float Thickness = 0f)
    {
        public bool IsSolid => Steps > 0 || Thickness > 0f;

        // How far down from its surface it's solid, at a point on it: to the floor, if it's stepped.
        public float Underside(float surface) => Steps > 0 ? 0f : MathF.Max(0f, surface - Thickness);

        // The ramp's own frame: along its run from Start (u) and across it (v), and how long the run is.
        private (Vector2 u, Vector2 v, float length, float a, float b) Frame(Vector3 local)
        {
            var along = new Vector2(End.X - Start.X, End.Z - Start.Z);
            var length = along.Length();
            var u = length < 1e-6f ? Vector2.UnitX : along / length;
            var v = new Vector2(-u.Y, u.X);
            var offset = new Vector2(local.X - Start.X, local.Z - Start.Z);
            return (u, v, length, Vector2.Dot(offset, u), Vector2.Dot(offset, v));
        }

        // The point of its footprint nearest to local (in X and Z; its Y is the ramp's height there), and
        // whether local is inside the footprint already.
        public (Vector3 nearest, bool inside) NearestInFootprint(Vector3 local)
        {
            var (u, v, length, a, b) = Frame(local);
            var inside = a >= 0f && a <= length && MathF.Abs(b) <= Width / 2f;
            var clamped = new Vector2(Start.X, Start.Z) + u * MathHelper.Clamp(a, 0f, length) + v * MathHelper.Clamp(b, -Width / 2f, Width / 2f);
            var point = new Vector3(clamped.X, 0f, clamped.Y);
            return (new Vector3(point.X, HeightAt(point), point.Z), inside);
        }

        // From inside the footprint, out of it the shortest way (through whichever of its four sides is
        // nearest) and `radius` further.
        public Vector3 NearestExit(Vector3 local, float radius)
        {
            var (u, v, length, a, b) = Frame(local);
            var exits = new[]
            {
                (distance: a, move: -u * (a + radius)),
                (distance: length - a, move: u * (length - a + radius)),
                (distance: Width / 2f + b, move: -v * (Width / 2f + b + radius)),
                (distance: Width / 2f - b, move: v * (Width / 2f - b + radius)),
            };
            var best = exits[0];
            foreach (var exit in exits)
                if (exit.distance < best.distance)
                    best = exit;
            return local + new Vector3(best.move.X, 0f, best.move.Y);
        }

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

    // A pitched roof: two slopes rising at `Slope` (rise over run) from the walls either side to a ridge
    // over the middle, the ridge running along X (AlongX) or along Z. On a room (RoomSpec.Pitched), its
    // ceiling follows the slopes up from Height at those two walls - an attic, with headroom only down the
    // middle; on a building (Building.Roof), the roof outside does. Only for rectangles centred on the room's
    // origin, like RoomSpec.Rectangle's.
    public readonly record struct Gable(float Slope, bool AlongX)
    {
        // A roof pitched at `degrees` from the level.
        public static Gable Pitched(float degrees, bool alongX) => new Gable(MathF.Tan(MathHelper.ToRadians(degrees)), alongX);

        // How far a point is from the ridge line, across the roof, and how far the room's walls are from it.
        public float FromRidge(Vector3 local) => MathF.Abs(AlongX ? local.Z : local.X);
        public float FromRidge(Vector2 local) => MathF.Abs(AlongX ? local.Y : local.X);
        public float HalfSpan(Vector2[] outline)
        {
            var half = 0f;
            foreach (var p in outline)
                half = MathF.Max(half, FromRidge(p));
            return half;
        }

        // The ridge line's direction, and the way across it, as plan (X, Z) vectors.
        public Vector2 Along => AlongX ? Vector2.UnitX : Vector2.UnitY;
        public Vector2 Across => AlongX ? Vector2.UnitY : Vector2.UnitX;
    }

    // A piece of furniture standing in a room: its mesh, and where. Position is where the mesh's origin goes,
    // YawDegrees turns its front (+Z) round the vertical. Half is the half-extent (X, Z) of the floor area it
    // blocks, already turned to the room's axes; leave it at zero for something you can't walk into (a TV on a sideboard).
    public record PropSpec(MeshSource Mesh, Vector3 Position, float YawDegrees, Vector2 Half = default)
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

        // A ceiling that follows a pitched roof up from Height at its walls (see Gable), or null for a flat one.
        public Gable? Pitched { get; init; }
        public HatchSpec[] FloorHatches { get; init; } = Array.Empty<HatchSpec>();

        // Every room that can be seen from this one: through an opening, or up or down through a hatch.
        public IEnumerable<string> Neighbours()
        {
            foreach (var opening in Openings)
                if (!opening.LeadsOutside)
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
        // constant along it - the first stepped ramp whose run contains the point wins - or under a pitched
        // roof (see Pitched), rising from Height at the walls to the ridge.
        public float CeilingHeightAt(Vector3 local)
        {
            foreach (var ramp in Ramps)
                if (ramp.Steps > 0 && ramp.InRange(local))
                    return Height + ramp.HeightAt(local);
            if (Pitched is { } gable)
                return Height + gable.Slope * MathF.Max(0f, gable.HalfSpan(Outline) - gable.FromRidge(local));
            return Height;
        }

        // Under a pitched ceiling, a walker (a circle of `radius`, `height` tall, feet at local.Y) kept out
        // from under the slope where it comes down below their head: no nearer the walls than that.
        public Vector3 KeepUnderRoof(Vector3 local, float radius, float height)
        {
            if (Pitched is not { } gable || local.Y < -0.05f || local.Y >= CeilingHeightAt(local) || !Contains(local))
                return local;
            var furthest = gable.HalfSpan(Outline) - MathF.Max(0f, local.Y + height - Height) / gable.Slope - radius;
            var across = gable.AlongX ? local.Z : local.X;
            var kept = MathHelper.Clamp(across, -MathF.Max(0f, furthest), MathF.Max(0f, furthest));
            return gable.AlongX ? new Vector3(local.X, local.Y, kept) : new Vector3(kept, local.Y, local.Z);
        }

        // Default RampSpec.MaxStepUp: generous enough for a shallow staircase's per-frame rise, but see
        // RampSpec for why a steep ramp (a ladder) needs to override it with something much larger.
        public const float DefaultMaxStepUp = WorldConstants.MaxStepUp;

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

        // The highest thing to stand on at local (x, z) no more than `reach` above local.Y: the floor (unless
        // there's a hatch in it there) or a ramp - one with a MaxStepUp of its own may be further above, the
        // way WalkHeightAt allows. Null outside the room, or where there's nothing within reach: a walker
        // over a floor hatch drops through to the room below.
        public float? SurfaceAt(Vector3 local, float reach)
        {
            if (!Contains(local))
                return null;
            float? best = null;
            if (local.Y + reach >= 0f && !HatchSpec.AnyContain(FloorHatches, local))
                best = 0f;
            foreach (var ramp in Ramps)
            {
                if (!ramp.Contains(local))
                    continue;
                var height = ramp.HeightAt(local);
                if (height <= local.Y + MathF.Max(reach, ramp.MaxStepUp) && (best == null || height > best.Value))
                    best = height;
            }
            return best;
        }

        // A walker (a circle of `radius` on the floor, `height` tall, its feet at local.Y) pushed out of
        // any solid ramp (see RampSpec.Thickness) it's walked into: one that's more than a step above its
        // feet where it touches it, and whose underside is below its head. So it's stopped by a staircase's
        // side, and by the underside of the stair coming down to meet it, but not at the foot of a flight,
        // which is where it steps up onto it.
        public Vector3 KeepOutOfRamps(Vector3 local, float radius, float height)
        {
            foreach (var ramp in Ramps)
            {
                if (!ramp.IsSolid)
                    continue;
                var (nearest, inside) = ramp.NearestInFootprint(local);
                var top = nearest.Y;
                if (top <= local.Y + DefaultMaxStepUp || ramp.Underside(top) >= local.Y + height)
                    continue;
                if (inside)
                {
                    local = ramp.NearestExit(local, radius);
                    continue;
                }
                var gap = new Vector2(local.X - nearest.X, local.Z - nearest.Z);
                var distance = gap.Length();
                if (distance >= radius)
                    continue;
                var pushed = new Vector2(nearest.X, nearest.Z) + gap / distance * radius;
                local = new Vector3(pushed.X, local.Y, pushed.Y);
            }
            return local;
        }

        // Whether a point (in the room's own coordinates) is over the room's floor. Only (X, Z) counts,
        // so rooms stacked on the same footprint both contain it - see Game1.ClimbThroughHatches.
        public bool Contains(Vector3 local) => Geometry2D.InPolygon(Outline, new Vector2(local.X, local.Z));

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
    }
}
