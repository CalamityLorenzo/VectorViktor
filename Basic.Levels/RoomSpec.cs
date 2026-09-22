using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace Basic.Levels
{
    // Which wall of a room. North is -Z, South +Z, East +X, West -X.
    public enum Wall { North, South, East, West }

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

        public DoorSpec[] Doors { get; init; } = Array.Empty<DoorSpec>();
        public PropSpec[] Props { get; init; } = Array.Empty<PropSpec>();
        public OpeningSpec[] Openings { get; init; } = Array.Empty<OpeningSpec>();

        private static readonly Wall[] AllWalls = { Wall.North, Wall.South, Wall.East, Wall.West };

        // How high the floor is at this z (relative to the room's own floor level).
        public float FloorHeightAt(float z) =>
            Stairs == null ? 0f : Stairs.Rise * MathHelper.Clamp((Depth / 2f - z) / Depth, 0f, 1f);

        public float CeilingHeightAt(float z) => Height + FloorHeightAt(z);

        // Whether a point (in the room's own coordinates) is over the room's floor. A whisker of tolerance
        // so a point on the seam between two rooms is always in one of them.
        public bool Contains(Vector3 local) =>
            MathF.Abs(local.X) <= Width / 2f + 0.01f && MathF.Abs(local.Z) <= Depth / 2f + 0.01f;

        // Pushes a walker (a circle of `radius` on the floor) back in from the walls, except where it is
        // squarely in line with an opening.
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
            return p;
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
