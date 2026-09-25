using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

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
    // The roof is flat, unless it's pitched (see Roof, and Gable): two slopes RoofOverhang out past the
    // walls, the end walls rising to the ridge. Its underside sits on the top room's walls - on its
    // ceiling if that's flat, with a closed-off space above it, or, if the top room is itself pitched
    // (RoomSpec.Pitched), along its sloping ceiling, so that room's an attic under the roof.
    public sealed class Building
    {
        public string Name { get; }
        public IReadOnlyList<RoomSpec> Rooms { get; }

        public float WallThickness { get; init; } = 0.25f;
        public float PlinthDepth { get; init; } = 0.6f;     // how far the shell goes down below the ground floor
        public float RoofThickness { get; init; } = 0.3f;
        public float RoofOverhang { get; init; } = 0.4f;
        public Gable? Roof { get; init; }

        public Color WallColor { get; init; } = new Color(205, 195, 170);
        public Color RoofColor { get; init; } = new Color(130, 65, 50);
        public Color PlinthColor { get; init; } = new Color(115, 110, 105);   // and the bands between storeys
        public Color DoorColor { get; init; } = new Color(120, 75, 40);

        public Building(string name, params RoomSpec[] rooms)
        {
            if (rooms.Length == 0)
                throw new ArgumentException("A building needs at least one room.", nameof(rooms));
            Name = name;
            Rooms = rooms;
        }

        // The room standing directly on top of this one, if any: one whose floor is over this one's
        // middle, a little above its ceiling. And the one this stands on.
        public RoomSpec Above(RoomSpec room) => Find(room, above: true);
        public RoomSpec Below(RoomSpec room) => Find(room, above: false);

        private RoomSpec Find(RoomSpec room, bool above)
        {
            foreach (var other in Rooms)
            {
                if (other == room)
                    continue;
                var (upper, lower) = above ? (other, room) : (room, other);
                var gap = upper.WorldOffset.Y - (lower.WorldOffset.Y + lower.Height);
                if (gap < -0.01f || gap > 1f)
                    continue;
                var middle = Centre(lower) + new Vector3(lower.WorldOffset.X, 0f, lower.WorldOffset.Z);
                if (upper.Contains(middle - upper.WorldOffset))
                    return other;
            }
            return null;
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
        // pitched one's ridge.
        public (float bottom, float top, bool roofed) ShellSpan(RoomSpec room)
        {
            var below = Below(room);
            var above = Above(room);
            var floor = room.WorldOffset.Y;
            var bottom = below != null ? below.WorldOffset.Y + below.Height : floor - PlinthDepth;
            var top = above != null ? floor + room.Height
                : RoofOf(room) is { } gable ? RoofUnderside(room, gable, 0f)
                : floor + room.Height + RoofThickness;
            return (bottom, top, above == null);
        }

        // The pitched roof over a room, if it's the top one and there is one: its own ceiling's, or the building's.
        public Gable? RoofOf(RoomSpec room) => Above(room) != null ? null : room.Pitched ?? Roof;

        // How high a pitched roof's underside is, `fromRidge` across from its ridge line: rising from the top
        // of the room's walls at their inside faces, at the roof's slope - RoofClearance above an attic's
        // ceiling, which follows the same slope, so the two never lie in one plane and fight to be seen.
        public static float RoofUnderside(RoomSpec room, Gable gable, float fromRidge) =>
            room.WorldOffset.Y + room.Height + gable.Slope * (gable.HalfSpan(room.Outline) - fromRidge) +
            (room.Pitched != null ? RoofClearance : 0f);

        private const float RoofClearance = 0.05f;

        // How far a point in the world is across from the ridge line over a room.
        public static float FromRidge(RoomSpec room, Gable gable, Vector2 world) =>
            gable.FromRidge(world - new Vector2(room.WorldOffset.X, room.WorldOffset.Z));

        // A room's outline pushed out by the wall's thickness, in the world: each corner moved out along
        // both its edges' outward normals (a mitre), so the outer walls stay parallel to the inner ones.
        public Vector2[] OuterOutline(RoomSpec room)
        {
            var outline = room.Outline;
            var n = outline.Length;
            var outer = new Vector2[n];
            var offset = new Vector2(room.WorldOffset.X, room.WorldOffset.Z);
            for (var i = 0; i < n; i++)
            {
                var before = Outward(outline[(i - 1 + n) % n], outline[i]);
                var after = Outward(outline[i], outline[(i + 1) % n]);
                var mitre = Vector2.Normalize(before + after);
                outer[i] = outline[i] + mitre * (WallThickness / Vector2.Dot(mitre, before)) + offset;
            }
            return outer;
        }

        // The outward normal of an edge from a to b (RoomSpec.Inward, the other way).
        public static Vector2 Outward(Vector2 a, Vector2 b)
        {
            var t = Vector2.Normalize(b - a);
            return new Vector2(t.Y, -t.X);
        }

        // Whether a room's edge is shared with another of the building's rooms, through an opening: then
        // it has no outer wall, the two rooms' inner walls being all there is between them.
        public static bool IsInside(RoomSpec room, int edge) =>
            Array.Exists(room.Openings, o => o.WallIndex == edge && !o.LeadsOutside);

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
                    var opening = Array.Find(room.Openings, o => o.WallIndex == edge);

                    // The room's own wall
                    var a = WallPoint(room, edge, -half);
                    var b = WallPoint(room, edge, half);
                    foreach (var piece in Split(a, b, room, edge, opening, floor, top))
                        yield return piece;

                    // The shell's, outside it
                    if (IsInside(room, edge))
                        continue;
                    var oa = outer[edge];
                    var ob = outer[(edge + 1) % n];
                    if (opening == null)
                    {
                        yield return new WallSegment(oa, ob, shellBottom, shellTop);
                        continue;
                    }
                    var push = Outward(room.Outline[edge], room.Outline[(edge + 1) % n]) * WallThickness;
                    var (left, right) = Gap(room, opening);
                    var gapLeft = WallPoint(room, edge, left) + push;
                    var gapRight = WallPoint(room, edge, right) + push;
                    yield return new WallSegment(oa, gapLeft, shellBottom, shellTop);
                    yield return new WallSegment(gapRight, ob, shellBottom, shellTop);
                    if (floor + opening.Height < shellTop)
                        yield return new WallSegment(gapLeft, gapRight, floor + opening.Height, shellTop);
                }
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

        // A wall from a to b, from `bottom` to `top`, less an opening's gap, if it has one: the wall either
        // side of it, and above it if the gap stops short of the top.
        private static IEnumerable<WallSegment> Split(Vector2 a, Vector2 b, RoomSpec room, int edge, OpeningSpec opening, float bottom, float top)
        {
            if (opening == null)
            {
                yield return new WallSegment(a, b, bottom, top);
                yield break;
            }
            var (left, right) = Gap(room, opening);
            var gapLeft = WallPoint(room, edge, left);
            var gapRight = WallPoint(room, edge, right);
            if (Vector2.DistanceSquared(a, gapLeft) > 1e-8f)
                yield return new WallSegment(a, gapLeft, bottom, top);
            if (Vector2.DistanceSquared(gapRight, b) > 1e-8f)
                yield return new WallSegment(gapRight, b, bottom, top);
            if (bottom + opening.Height < top)
                yield return new WallSegment(gapLeft, gapRight, bottom + opening.Height, top);
        }
    }
}
