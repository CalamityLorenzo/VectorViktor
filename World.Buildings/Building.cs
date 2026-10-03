using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core;

namespace World.Buildings
{
    // A stretch of wall, for walking into: a vertical strip standing on the line from A to B (world X, Z),
    // from Bottom up to Top (world heights).
    public readonly record struct WallSegment(Vector2 A, Vector2 B, float Bottom, float Top);

    // A building standing in the world: its rooms, each at its own WorldOffset, and the shell round them
    // - outer walls WallThickness outside each room's own, carried down into the ground as a plinth under
    // the ground floor, and a flat roof over the top floor. Everything here is in world coordinates.
    //
    // Rooms stack, one on another, the upper one's floor on a slab over the lower one's ceiling and
    // reached through hatches (see RoomSpec.CeilingHatches) - or they meet at openings, and their shell
    // leaves out the wall between them. A doorway to outside is an opening with no TargetRoom: it's cut
    // through the shell as well, lined across the wall's thickness.
    //
    // Rooms side by side on a floor can also stand a partition apart (see RoomSpec.InnerWalls), as a house's rooms
    // do: no shell along the partition, and a doorway through it lined across it, with a threshold to walk on.
    //
    // The roof is flat, unless it's pitched (see Roof, and Gable): two slopes RoofOverhang out past the
    // walls, the end walls rising to the ridge. Its underside sits on the top room's walls - on its
    // ceiling if that's flat, with a closed-off space above it, or, if the top room is itself pitched
    // (RoomSpec.Pitched), along its sloping ceiling, so that room's an attic under the roof. A pitched Roof
    // is one roof over every room of the highest storey (see RoofOver); anything lower with nothing on it, a
    // single-storey wing, has a flat one.
    public sealed class Building
    {
        public string Name { get; }
        public IReadOnlyList<RoomSpec> Rooms { get; }

        public float WallThickness { get; init; } = 0.25f;
        public float PlinthDepth { get; init; } = 0.6f;     // how far the shell goes down below the ground floor
        public float RoofThickness { get; init; } = 0.3f;
        public float RoofOverhang { get; init; } = 0.4f;
        public Gable? Roof { get; init; }

        // How far apart rooms either side of a partition stand (see RoomSpec.InnerWalls): as thick as a slab between
        // storeys, for the same reason (see RoomSpec.CeilingHatches)
        public float PartitionThickness { get; init; } = 0.3f;

        public Color WallColor { get; init; } = new Color(205, 195, 170);
        public Color RoofColor { get; init; } = new Color(130, 65, 50);
        public Color PlinthColor { get; init; } = new Color(115, 110, 105);   // and the bands between storeys
        public Color DoorColor { get; init; } = new Color(120, 75, 40);
        public Color LiningColor { get; init; } = new Color(225, 220, 205);   // a doorway through a partition's

        public Building(string name, params RoomSpec[] rooms)
        {
            if (rooms.Length == 0)
                throw new ArgumentException("A building needs at least one room.", nameof(rooms));
            Name = name;
            Rooms = rooms;
        }

        // The room standing directly on top of this one, if any: one whose floor is over some of this one's, a
        // little above its ceiling (on a storey of several rooms, any one of them). And the one this stands on.
        public RoomSpec? Above(RoomSpec room) => Find(room, above: true);
        public RoomSpec? Below(RoomSpec room) => Find(room, above: false);

        private RoomSpec? Find(RoomSpec room, bool above)
        {
            foreach (var other in Rooms)
            {
                if (other == room)
                    continue;
                var (upper, lower) = above ? (other, room) : (room, other);
                var gap = upper.WorldOffset.Y - (lower.WorldOffset.Y + lower.Height);
                if (gap < -0.01f || gap > 1f)
                    continue;
                if (Overlap(lower, upper))
                    return other;
            }
            return null;
        }

        // Whether two rooms' floor plans overlap, more than just touching: either's middle over the other, or a corner
        // of either, drawn a little in towards its middle, over the other
        private static bool Overlap(RoomSpec a, RoomSpec b)
        {
            static bool AnyOver(RoomSpec from, RoomSpec onto)
            {
                var middle = Centre(from);
                var shift = from.WorldOffset - onto.WorldOffset;
                if (onto.Contains(middle + shift))
                    return true;
                foreach (var p in from.Outline)
                {
                    var corner = new Vector3(p.X, 0f, p.Y);
                    if (onto.Contains(corner + (middle - corner) * 0.01f + shift))
                        return true;
                }
                return false;
            }
            return AnyOver(a, b) || AnyOver(b, a);
        }

        private static Vector3 Centre(RoomSpec room)
        {
            var sum = Vector2.Zero;
            foreach (var p in room.Outline)
                sum += p;
            sum /= room.Outline.Length;
            return new Vector3(sum.X, 0f, sum.Y);
        }

        // How far up and down a room's shell goes: from the ceiling of the room below (or the plinth's foot)
        // to its own ceiling, or, if nothing stands on it, the top of a flat roof or the underside of a
        // pitched one's ridge. A flat roof is over the highest of the ceiling, which rises over a stair (see
        // RoomSpec.HighestCeiling): lower, it'd cut through the room, there for anyone going up the stair to see.
        public (float bottom, float top, bool roofed) ShellSpan(RoomSpec room)
        {
            var below = Below(room);
            var above = Above(room);
            var floor = room.WorldOffset.Y;
            var bottom = below != null ? below.WorldOffset.Y + below.Height : floor - PlinthDepth;
            var top = above != null ? floor + room.Height
                : RoofOver(room) is { } roof ? roof.Underside(0f)
                : floor + room.HighestCeiling + RoofThickness;
            return (bottom, top, above == null);
        }

        // The pitched roof over a room, if it's a top one and there is one: its own ceiling's, or the building's, which
        // is only over the highest storey.
        public Gable? RoofOf(RoomSpec room) =>
            Above(room) != null ? null : room.Pitched ?? (MathF.Abs(room.WorldOffset.Y - HighestFloor) < 0.01f ? Roof : null);

        private float HighestFloor
        {
            get
            {
                var highest = float.MinValue;
                foreach (var room in Rooms)
                    highest = MathF.Max(highest, room.WorldOffset.Y);
                return highest;
            }
        }

        // A pitched roof, and where it is: its ridge's middle (world X, Z), how far it reaches along the ridge and
        // across it either side, to the walls' inside faces, and how high those walls are, where it rests on them. Over
        // an attic (see RoomSpec.Pitched) it's RoofClearance above the attic's ceiling, which follows the same slope, so
        // the two never lie in one plane and fight to be seen.
        public readonly record struct RoofFrame(Gable Gable, Vector2 Centre, float HalfAlong, float HalfAcross, float Eaves, bool Attic)
        {
            // How far a point in the world is across from the ridge line
            public float FromRidge(Vector2 world) => Gable.FromRidge(world - Centre);

            // How high its underside is, `fromRidge` across from the ridge line
            public float Underside(float fromRidge) => Eaves + Gable.Slope * (HalfAcross - fromRidge) + (Attic ? RoofClearance : 0f);
        }

        private const float RoofClearance = 0.05f;

        // The pitched roof over a room (see RoofOf), if it has one: an attic's own, round the room's middle; or the
        // building's, over the highest storey's rooms all together, round the middle of the box round their floors.
        public RoofFrame? RoofOver(RoomSpec room)
        {
            if (RoofOf(room) is not { } gable)
                return null;
            if (room.Pitched != null)
            {
                var halfAlong = 0f;
                foreach (var p in room.Outline)
                    halfAlong = MathF.Max(halfAlong, MathF.Abs(Vector2.Dot(p, gable.Along)));
                return new RoofFrame(gable, new Vector2(room.WorldOffset.X, room.WorldOffset.Z), halfAlong, gable.HalfSpan(room.Outline),
                    room.WorldOffset.Y + room.Height, Attic: true);
            }
            var (min, max, eaves) = (new Vector2(float.MaxValue), new Vector2(float.MinValue), float.MinValue);
            foreach (var other in Rooms)
            {
                if (other.Pitched != null || RoofOf(other) == null)
                    continue;
                foreach (var p in other.Outline)
                {
                    var world = p + new Vector2(other.WorldOffset.X, other.WorldOffset.Z);
                    (min, max) = (Vector2.Min(min, world), Vector2.Max(max, world));
                }
                eaves = MathF.Max(eaves, other.WorldOffset.Y + other.Height);
            }
            var half = (max - min) / 2f;
            return new RoofFrame(gable, (min + max) / 2f, Vector2.Dot(half, gable.Along), Vector2.Dot(half, gable.Across), eaves, Attic: false);
        }

        // A room's outline pushed out to its shell, in the world: by the wall's thickness, or along a partition (see
        // RoomSpec.InnerWalls) only to its middle, where the room on its other side's meets it. Each corner is where its
        // two edges, so pushed out, meet (a mitre), so the outer walls stay parallel to the inner ones. Two edges in
        // line must be pushed out alike: a partition and an outer wall can't run straight on from each other.
        public Vector2[] OuterOutline(RoomSpec room)
        {
            var outline = room.Outline;
            var n = outline.Length;
            var outer = new Vector2[n];
            var offset = new Vector2(room.WorldOffset.X, room.WorldOffset.Z);
            float Out(int edge) => Array.IndexOf(room.InnerWalls, edge) >= 0 ? PartitionThickness / 2f : WallThickness;
            for (var i = 0; i < n; i++)
            {
                var before = Geometry2D.Outward(outline[(i - 1 + n) % n], outline[i]);
                var after = Geometry2D.Outward(outline[i], outline[(i + 1) % n]);
                var (outBefore, outAfter) = (Out((i - 1 + n) % n), Out(i));
                var det = before.X * after.Y - before.Y * after.X;
                Vector2 shift;
                if (MathF.Abs(det) > 1e-6f)   // the point outBefore out along `before` and outAfter out along `after`
                    shift = new Vector2(outBefore * after.Y - before.Y * outAfter, before.X * outAfter - outBefore * after.X) / det;
                else if (MathF.Abs(outBefore - outAfter) < 1e-4f)
                    shift = before * outBefore;
                else
                    throw new InvalidOperationException($"Room '{room.Id}': edges {(i - 1 + n) % n} and {i} run on in line, but only one's a partition.");
                outer[i] = outline[i] + shift + offset;
            }
            return outer;
        }

        // Whether a room's edge is inside the building, with no outer wall: shared with another of its rooms, through
        // an opening, the two rooms' inner walls being all there is between them; or a partition (see RoomSpec.InnerWalls).
        public static bool IsInside(RoomSpec room, int edge) =>
            Array.IndexOf(room.InnerWalls, edge) >= 0 || Array.Exists(room.Openings, o => o.WallIndex == edge && !o.LeadsOutside);

        // Whether an opening is a doorway through a partition (see RoomSpec.InnerWalls), not into a room right there.
        public static bool IsThrough(RoomSpec room, OpeningSpec opening) =>
            !opening.LeadsOutside && Array.IndexOf(room.InnerWalls, opening.WallIndex) >= 0;

        // A doorway through a partition (see IsThrough), as far as this room lines it: its gap's two sides on the room's
        // wall, and out at the partition's middle, where the lining from the room on the other side meets it (world X, Z).
        public (Vector2 Left, Vector2 Right, Vector2 MidLeft, Vector2 MidRight) Through(RoomSpec room, OpeningSpec opening)
        {
            var edge = opening.WallIndex;
            var push = Geometry2D.Outward(room.Outline[edge], room.Outline[(edge + 1) % room.Outline.Length]) * (PartitionThickness / 2f);
            var (left, right) = Gap(room, opening);
            var (l, r) = (WallPoint(room, edge, left), WallPoint(room, edge, right));
            return (l, r, l + push, r + push);
        }

        // Ground to walk on across each doorway through a partition, level with the floor: each room's half of it, from
        // its wall to the partition's middle (see BuildingGround)
        public IEnumerable<Ledge> Thresholds()
        {
            foreach (var room in Rooms)
                foreach (var opening in room.Openings)
                {
                    if (!IsThrough(room, opening))
                        continue;
                    var (l, r, ml, mr) = Through(room, opening);
                    var floor = room.WorldOffset.Y;
                    yield return new Ledge((l + r) / 2f, (ml + mr) / 2f, Vector2.Distance(l, r) / 2f, floor - PartitionThickness, floor);
                }
        }

        // Where an opening's gap is along its edge, as distances from the edge's midpoint (see RoomSpec.WallPoint).
        public static (float left, float right) Gap(RoomSpec room, OpeningSpec opening)
        {
            var half = room.WallLength(opening.WallIndex) / 2f;
            return (MathHelper.Clamp(opening.Offset - opening.Width / 2f, -half, half),
                    MathHelper.Clamp(opening.Offset + opening.Width / 2f, -half, half));
        }

        // A point on a room's wall, in the world's (X, Z).
        public static Vector2 WallPoint(RoomSpec room, int edge, float along)
        {
            var p = room.WallPoint(edge, along) + room.WorldOffset;
            return new Vector2(p.X, p.Z);
        }

        // Every wall a walker can walk into: each room's own walls, with gaps where its openings are, and
        // the building's outer walls, with gaps for its doorways. (Under a doorway, the plinth is left out:
        // that's where you step up over the threshold.)
        public IEnumerable<WallSegment> Walls()
        {
            foreach (var room in Rooms)
            {
                var floor = room.WorldOffset.Y;
                var (shellBottom, shellTop, _) = ShellSpan(room);
                var outer = OuterOutline(room);
                var n = room.Outline.Length;
                for (var edge = 0; edge < n; edge++)
                {
                    var half = room.WallLength(edge) / 2f;
                    var top = floor + MathF.Max(room.CeilingHeightAt(room.WallPoint(edge, -half)), room.CeilingHeightAt(room.WallPoint(edge, half)));
                    var holes = Holes(room, edge);

                    // The room's own wall
                    var a = WallPoint(room, edge, -half);
                    var b = WallPoint(room, edge, half);
                    foreach (var piece in Split(a, b, room, edge, holes, Vector2.Zero, floor, top))
                        yield return piece;

                    // Through a partition, each doorway's sides, out to the partition's middle
                    foreach (var opening in room.Openings)
                    {
                        if (opening.WallIndex != edge || !IsThrough(room, opening))
                            continue;
                        var (l, r, ml, mr) = Through(room, opening);
                        yield return new WallSegment(l, ml, floor, top);
                        yield return new WallSegment(r, mr, floor, top);
                    }

                    // The shell's, outside it
                    if (IsInside(room, edge))
                        continue;
                    var push = Geometry2D.Outward(room.Outline[edge], room.Outline[(edge + 1) % n]) * WallThickness;
                    foreach (var piece in Split(outer[edge], outer[(edge + 1) % n], room, edge, holes, push, shellBottom, shellTop))
                        yield return piece;
                }

                // Railings round its floor hatches (see HatchSpec.Railed)
                var offset = new Vector2(room.WorldOffset.X, room.WorldOffset.Z);
                foreach (var hatch in room.FloorHatches)
                    foreach (var (a, b) in hatch.Rails())
                        yield return new WallSegment(a + offset, b + offset, floor, floor + HatchSpec.RailHeight);
            }
        }

        // Every chest of drawers and cupboard in its rooms (see RoomSpec.Cabinets), standing in the world, shut.
        public List<Cabinet> FitCabinets()
        {
            var cabinets = new List<Cabinet>();
            foreach (var room in Rooms)
                foreach (var spec in room.Cabinets)
                    cabinets.Add(new Cabinet(spec, room.WorldOffset));
            return cabinets;
        }

        public const float MaxDoorstep = 1f;   // a floor higher than this over the ground outside its doorway gets no ramp

        // A ramp (see Doorstep) up to every doorway out of the building, where its floor's above the ground outside
        // (`groundAt`, the ground's height at a point), but not so far above it that a ramp's no answer: a door out
        // onto nothing.
        public IEnumerable<Doorstep> Doorsteps(Func<Vector2, float> groundAt)
        {
            foreach (var room in Rooms)
                foreach (var opening in room.Openings)
                {
                    if (!opening.LeadsOutside)
                        continue;
                    var (left, right) = Gap(room, opening);
                    var inner = WallPoint(room, opening.WallIndex, (left + right) / 2f);
                    var inward = room.Inward(opening.WallIndex);
                    var outward = -new Vector2(inward.X, inward.Z);
                    var outer = inner + outward * WallThickness;
                    var floor = room.WorldOffset.Y;
                    var rise = floor - groundAt(outer + outward * Doorstep.MinLength);
                    if (rise <= 0.01f || rise > MaxDoorstep)
                        continue;
                    var length = Doorstep.LengthFor(rise);
                    yield return new Doorstep(inner, outer, outward, (right - left) / 2f, floor, groundAt(outer + outward * length), length, PlinthColor);
                }
        }

        // A new door (see Door) for every leaf hung in the building's doorways (see OpeningSpec.Door), shut.
        // A doorway between two rooms gets its door once, from whichever room's opening asks for it (the
        // one whose Id sorts first, if both do).
        public List<Door> HangDoors()
        {
            var doors = new List<Door>();
            foreach (var room in Rooms)
                foreach (var opening in room.Openings)
                {
                    if (!opening.Door || !HangsHere(room, opening))
                        continue;
                    var (left, right) = Gap(room, opening);
                    var l = WallPoint(room, opening.WallIndex, left);
                    var r = WallPoint(room, opening.WallIndex, right);
                    var inward = room.Inward(opening.WallIndex);
                    var into = new Vector2(inward.X, inward.Z);
                    var width = right - left;
                    var height = opening.Height - 0.02f;
                    const float clearance = 0.01f;   // so a shut leaf doesn't touch the jamb
                    if (width <= Door.MaxLeafWidth)
                        doors.Add(new Door(l, r - l, into, width - clearance, room.WorldOffset.Y, height, DoorColor));
                    else
                    {
                        doors.Add(new Door(l, r - l, into, width / 2f - clearance, room.WorldOffset.Y, height, DoorColor));
                        doors.Add(new Door(r, l - r, into, width / 2f - clearance, room.WorldOffset.Y, height, DoorColor));
                    }
                }
            return doors;
        }

        private bool HangsHere(RoomSpec room, OpeningSpec opening)
        {
            if (opening.LeadsOutside)
                return true;
            foreach (var other in Rooms)
                if (other.Id == opening.TargetRoom && Array.Exists(other.Openings, o => o.Door && o.TargetRoom == room.Id))
                    return string.CompareOrdinal(room.Id, other.Id) < 0;
            return true;
        }

        // A hole in one of a room's walls: a doorway (see OpeningSpec) or a window (see WindowSpec), from Left to Right
        // along it (distances from the edge's midpoint, as Gap's are) and from Bottom to Top above the room's floor.
        public readonly record struct Hole(float Left, float Right, float Bottom, float Top, bool Window);

        // Every hole in a room's wall, in order along it.
        public static List<Hole> Holes(RoomSpec room, int edge)
        {
            var holes = new List<Hole>();
            foreach (var opening in room.Openings)
                if (opening.WallIndex == edge)
                {
                    var (left, right) = Gap(room, opening);
                    holes.Add(new Hole(left, right, 0f, opening.Height, Window: false));
                }
            var half = room.WallLength(edge) / 2f;
            foreach (var window in room.Windows)
                if (window.WallIndex == edge)
                    holes.Add(new Hole(MathHelper.Clamp(window.Offset - window.Width / 2f, -half, half), MathHelper.Clamp(window.Offset + window.Width / 2f, -half, half),
                        window.Sill, window.Sill + window.Height, Window: true));
            holes.Sort((x, y) => x.Left.CompareTo(y.Left));
            return holes;
        }

        // A wall from a to b, from `bottom` to `top`, less its holes, each where it is on the room's wall pushed out by
        // `push` (to the shell's face): the wall between them, under a window, and over each that stops short of the
        // top. Not under a doorway: below the floor, that's where you step up over the threshold.
        private static IEnumerable<WallSegment> Split(Vector2 a, Vector2 b, RoomSpec room, int edge, List<Hole> holes, Vector2 push, float bottom, float top)
        {
            var floor = room.WorldOffset.Y;
            var from = a;
            foreach (var hole in holes)
            {
                var gapLeft = WallPoint(room, edge, hole.Left) + push;
                var gapRight = WallPoint(room, edge, hole.Right) + push;
                if (Vector2.DistanceSquared(from, gapLeft) > 1e-8f)
                    yield return new WallSegment(from, gapLeft, bottom, top);
                if (hole.Window && floor + hole.Bottom > bottom)
                    yield return new WallSegment(gapLeft, gapRight, bottom, floor + hole.Bottom);
                if (floor + hole.Top < top)
                    yield return new WallSegment(gapLeft, gapRight, floor + hole.Top, top);
                from = gapRight;
            }
            if (Vector2.DistanceSquared(from, b) > 1e-8f)
                yield return new WallSegment(from, b, bottom, top);
        }
    }
}
