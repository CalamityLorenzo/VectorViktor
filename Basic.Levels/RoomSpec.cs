using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Basic.Levels
{
    // Which wall of a room. North is -Z, South +Z, East +X, West -X.
    public enum Wall { North, South, East, West }

    // Which corner a room's notch is cut from.
    public enum Corner { NorthWest, NorthEast, SouthWest, SouthEast }

    // A rectangular bite taken out of one corner of the room, turning its square footprint into an L.
    // Width and Depth are the size of the missing rectangle, measured in from the room's outer edges at
    // that corner. The room's floor/ceiling are then two rectangles rather than one, and two extra wall
    // faces close off the inner corner - see RoomSpec.NotchBounds and RoomMesh.AddNotchWalls.
    public record NotchSpec(Corner Corner, float Width, float Depth);

    // A door in a wall. Offset is how far along the wall its centre is, measured from the room's centre
    // (X for the north and south walls, Z for the east and west ones). Walking into it puts you at
    // TargetDoor in TargetRoom.
    public record DoorSpec(string Id, Wall Wall, float Offset, string TargetRoom, string TargetDoor);

    // A gap in a wall, from the floor up, that leads straight on into the neighbouring room TargetRoom.
    // The two rooms must sit edge to edge in the world (see RoomSpec.WorldOffset), and the neighbour has
    // an opening of its own in the matching wall. At most one per wall.
    public record OpeningSpec(Wall Wall, float Offset, float Width, float Height, string TargetRoom);

    // Steps climbing to the north over the room's whole depth. The ceiling climbs with them, so the
    // headroom stays the room's Height all the way up.
    public record StairSpec(float Rise, int Steps);

    // A climbable strip in the room's own coordinates, separate from the room's own whole-depth Stairs:
    // a corridor Width wide down the line from Start to End, whose floor rises (or stays flat, if
    // Start.Y == End.Y) linearly between them. Matches a staircase flight, a landing, a ladder, or a
    // platform's own deck, so a walker's height can follow it. Every RoomSpec.WalkHeightAt call checks
    // every ramp in the room, so keep a room's Ramps to the handful that are actually walked on.
    //
    // MaxStepUp is how far above a walker's current height this ramp may still lift them in one go
    // (see RoomSpec.WalkHeightAt) — it exists to stop wandering under a platform from snapping you
    // straight up onto it, not to model real footing, so a steep ramp (a near-vertical ladder rising
    // several metres over a run of one) needs a much larger value than a shallow staircase does:
    // at running speed, a couple of frames' horizontal movement up a slope that steep already outruns
    // the default, and the walker would be dropped back to the floor instead of climbing.
    public readonly record struct RampSpec(Vector3 Start, Vector3 End, float Width, float MaxStepUp = RoomSpec.DefaultMaxStepUp)
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
    }

    // A piece of furniture standing in a room. Position is where the mesh's origin goes, YawDegrees turns
    // its front (+Z) round the vertical. Half is the half-extent (X, Z) of the floor area it blocks,
    // already turned to the room's axes; leave it at zero for something you can't walk into (a TV on a sideboard).
    public record PropSpec(string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette,
                           Vector3 Position, float YawDegrees, Vector2 Half = default)
    {
        public bool Blocks => Half != Vector2.Zero;
    }

    // A square-cornered room: X is its width, Z its depth, centred on the origin, floor at y = 0.
    public sealed class RoomSpec
    {
        public const float DoorWidth = 0.9f, DoorHeight = 2.0f;

        public required string Id { get; init; }
        public required string Name { get; init; }
        public required float Width { get; init; }
        public required float Depth { get; init; }
        public required float Height { get; init; }

        // Flat colours; the two wall pairs differ so the corners read in a solid colour scheme.
        public required Color Floor { get; init; }
        public required Color WallNorthSouth { get; init; }
        public required Color WallEastWest { get; init; }
        public required Color Ceiling { get; init; }

        // Where the room's floor centre is in the world. Only rooms joined by openings need to fit together;
        // a room reached only through doors can sit anywhere (they are never drawn side by side).
        public Vector3 WorldOffset { get; init; }
        public float GridSpacing { get; init; } = 1f;
        public StairSpec Stairs { get; init; }
        public RampSpec[] Ramps { get; init; } = Array.Empty<RampSpec>();
        public NotchSpec Notch { get; init; }

        public DoorSpec[] Doors { get; init; } = Array.Empty<DoorSpec>();
        public PropSpec[] Props { get; init; } = Array.Empty<PropSpec>();
        public OpeningSpec[] Openings { get; init; } = Array.Empty<OpeningSpec>();

        private static readonly Wall[] AllWalls = { Wall.North, Wall.South, Wall.East, Wall.West };

        // How high the floor is at this z (relative to the room's own floor level).
        public float FloorHeightAt(float z) =>
            Stairs == null ? 0f : Stairs.Rise * MathHelper.Clamp((Depth / 2f - z) / Depth, 0f, 1f);

        public float CeilingHeightAt(float z) => Height + FloorHeightAt(z);

        // Default RampSpec.MaxStepUp: generous enough for a shallow staircase's per-frame rise, but see
        // RampSpec for why a steep ramp (a ladder) needs to override it with something much larger.
        public const float DefaultMaxStepUp = 0.3f;

        // How high the floor is under a walker already standing `currentHeight` above this room's own
        // floor level, at this local (x, z): the room's own slope, or the highest ramp whose footprint
        // contains the point and whose height clears that ramp's own MaxStepUp.
        public float WalkHeightAt(Vector3 local, float currentHeight)
        {
            var height = FloorHeightAt(local.Z);
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

        // Whether a point (in the room's own coordinates) is over the room's floor. A whisker of tolerance
        // so a point on the seam between two rooms is always in one of them.
        public bool Contains(Vector3 local) =>
            MathF.Abs(local.X) <= Width / 2f + 0.01f && MathF.Abs(local.Z) <= Depth / 2f + 0.01f;

        // Pushes a walker (a circle of `radius` on the floor) back in from the walls, except where it is
        // squarely in line with an opening. A notch corner (if any) is pushed out of last, as a solid
        // obstacle - see PushOutOfBox. Applying the plain 4-wall pushes first is harmless even where a
        // notch has removed part of a wall: the notch push is always the stricter of the two there, so it
        // is what actually decides the final position.
        public Vector3 KeepInside(Vector3 p, float radius)
        {
            foreach (var wall in AllWalls)
            {
                var gap = DistanceToWall(wall, p);
                if (gap >= radius)
                    continue;
                var along = AlongWall(wall, p);
                if (Array.Exists(Openings, o => o.Wall == wall && MathF.Abs(along - o.Offset) <= o.Width / 2f - radius))
                    continue;
                p += Inward(wall) * (radius - gap);
            }
            if (Notch != null)
            {
                var (x0, x1, z0, z1, _, _) = NotchBounds();
                var pushed = PushOutOfBox(new Vector2(p.X, p.Z), new Vector2((x0 + x1) / 2f, (z0 + z1) / 2f),
                    new Vector2((x1 - x0) / 2f, (z1 - z0) / 2f), radius);
                p = new Vector3(pushed.X, p.Y, pushed.Y);
            }
            return p;
        }

        // A circle of `radius` pushed clear of an axis-aligned box (centre, half-extent), by the shortest
        // route; if its centre is already inside the box, out through whichever side is nearest. Shared by
        // a room's own notch (a box-shaped bite out of its footprint) and RoomView's furniture collision.
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

        // The corner notch's removed rectangle in the room's own coordinates (x0 < x1, z0 < z1), and which
        // way it faces along each axis (-1/+1, matching the corner it's cut from). Only valid when Notch != null.
        public (float x0, float x1, float z0, float z1, int ex, int ez) NotchBounds()
        {
            var hw = Width / 2f;
            var hd = Depth / 2f;
            var ex = Notch.Corner is Corner.NorthWest or Corner.SouthWest ? -1 : 1;
            var ez = Notch.Corner is Corner.NorthWest or Corner.NorthEast ? -1 : 1;
            var x0 = ex < 0 ? -hw : hw - Notch.Width;
            var x1 = ex < 0 ? -hw + Notch.Width : hw;
            var z0 = ez < 0 ? -hd : hd - Notch.Depth;
            var z1 = ez < 0 ? -hd + Notch.Depth : hd;
            return (x0, x1, z0, z1, ex, ez);
        }

        public DoorSpec FindDoor(string id) =>
            Array.Find(Doors, d => d.Id == id) ?? throw new ArgumentException($"Room '{Id}' has no door '{id}'.", nameof(id));

        // The way a wall's normal points, into the room.
        public static Vector3 Inward(Wall wall) => wall switch
        {
            Wall.North => Vector3.UnitZ,
            Wall.South => -Vector3.UnitZ,
            Wall.East => -Vector3.UnitX,
            _ => Vector3.UnitX,
        };

        // The direction running along a wall, in the direction its offsets increase.
        public static Vector3 Tangent(Wall wall) => wall is Wall.North or Wall.South ? Vector3.UnitX : Vector3.UnitZ;

        // The point on the floor, at the foot of the wall, `along` from its centre.
        public Vector3 WallPoint(Wall wall, float along) => wall switch
        {
            Wall.North => new Vector3(along, 0f, -Depth / 2f),
            Wall.South => new Vector3(along, 0f, Depth / 2f),
            Wall.East => new Vector3(Width / 2f, 0f, along),
            _ => new Vector3(-Width / 2f, 0f, along),
        };

        public float DistanceToWall(Wall wall, Vector3 p) => wall switch
        {
            Wall.North => p.Z + Depth / 2f,
            Wall.South => Depth / 2f - p.Z,
            Wall.East => Width / 2f - p.X,
            _ => p.X + Width / 2f,
        };

        public static float AlongWall(Wall wall, Vector3 p) => wall is Wall.North or Wall.South ? p.X : p.Z;
    }
}
