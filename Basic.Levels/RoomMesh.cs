using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Basic.Levels
{
    // The inside of a room as one mesh: floor (or steps), ceiling, four walls (with any openings cut in
    // them), the doors painted on the walls, and a grid on the floor so there is something to see moving
    // past in wireframe. Faces are seen from inside, so the mesh is drawn with culling off.
    public static class RoomMesh
    {
        public const int Floor = 0, WallNorthSouth = 1, WallEastWest = 2, Ceiling = 3, Door = 4, Frame = 5, Handle = 6, Riser = 7;
        public const int PaletteSize = 8;

        private static readonly Color DoorColor = new Color(200, 140, 40);
        private static readonly Color FrameColor = new Color(230, 230, 230);
        private static readonly Color HandleColor = new Color(255, 255, 0);

        // Stand-off from the wall, so a door sits in front of it, its frame behind it, and neither z-fights.
        private const float FrameLift = 0.005f, DoorLift = 0.012f, HandleLift = 0.02f, EdgeLift = 0.015f;
        private const float FrameBorder = 0.07f;
        private const float Tiny = 1e-4f;

        public static Color[] Palette(RoomSpec room) => new[]
        {
            room.Floor, room.WallNorthSouth, room.WallEastWest, room.Ceiling, DoorColor, FrameColor, HandleColor,
            Color.Lerp(room.Floor, Color.Black, 0.3f),   // the upright faces of steps
        };

        public static MeshData Build(GraphicsDevice device, RoomSpec room)
        {
            var hw = room.Width / 2f;
            var hd = room.Depth / 2f;
            var mesh = new MeshBuilder();

            if (room.Stairs != null)
                AddSteps(mesh, room, hw, hd);
            else if (room.Notch != null)
                AddLFloor(mesh, room, hw, hd);
            else
            {
                mesh.AddSolidRange(2, Floor);
                mesh.AddQuad(new Vector3(-hw, 0f, -hd), new Vector3(hw, 0f, -hd), new Vector3(hw, 0f, hd), new Vector3(-hw, 0f, hd));
                AddGrid(mesh, room, hw, hd);
            }

            var northSouth = new List<Vector3[]>();
            var eastWest = new List<Vector3[]>();
            if (room.Notch != null)
            {
                AddLCeiling(mesh, room, hw, hd);
                AddNotchWalls(mesh, room, hw, hd, northSouth, eastWest);
            }
            else
            {
                var north = room.CeilingHeightAt(-hd);
                var south = room.CeilingHeightAt(hd);
                mesh.AddSolidRange(2, Ceiling);
                mesh.AddQuad(new Vector3(-hw, north, -hd), new Vector3(hw, north, -hd), new Vector3(hw, south, hd), new Vector3(-hw, south, hd));

                AddWall(mesh, room, Wall.North, northSouth);
                AddWall(mesh, room, Wall.South, northSouth);
                AddWall(mesh, room, Wall.East, eastWest);
                AddWall(mesh, room, Wall.West, eastWest);
            }
            AddQuads(mesh, northSouth, WallNorthSouth);
            AddQuads(mesh, eastWest, WallEastWest);

            foreach (var door in room.Doors)
                AddDoor(mesh, room, door);

            return mesh.Build(device);
        }

        private static void AddQuads(MeshBuilder mesh, List<Vector3[]> quads, int slot)
        {
            if (quads.Count == 0)
                return;   // an empty draw range would be a zero-primitive draw call
            mesh.AddSolidRange(quads.Count * 2, slot);
            foreach (var q in quads)
                mesh.AddQuad(q[0], q[1], q[2], q[3]);
        }

        // Grid lines every GridSpacing, starting from a wall so the far edge is the only odd-sized cell
        private static void AddGrid(MeshBuilder mesh, RoomSpec room, float hw, float hd)
        {
            var spacing = room.GridSpacing;
            for (var x = -hw + spacing; x < hw - 0.01f; x += spacing)
                mesh.AddLine(new Vector3(x, 0f, -hd), new Vector3(x, 0f, hd));
            for (var z = -hd + spacing; z < hd - 0.01f; z += spacing)
                mesh.AddLine(new Vector3(-hw, 0f, z), new Vector3(hw, 0f, z));
        }

        // A notch corner splits the room's footprint into two rectangles: a full-width strip (the depth
        // the notch doesn't touch) and a narrower strip alongside it (the notch's own depth, minus the
        // notch's width). Together they tile the L exactly. Only valid when room.Notch != null.
        private static ((float x0, float x1, float z0, float z1) big, (float x0, float x1, float z0, float z1) small) LRects(RoomSpec room, float hw, float hd)
        {
            var (x0, x1, z0, z1, ex, ez) = room.NotchBounds();
            var big = ez < 0 ? (-hw, hw, z1, hd) : (-hw, hw, -hd, z0);
            var small = ex < 0 ? (x1, hw, z0, z1) : (-hw, x0, z0, z1);
            return (big, small);
        }

        private static void AddFlatQuad(MeshBuilder mesh, (float x0, float x1, float z0, float z1) r, float y) =>
            mesh.AddQuad(new Vector3(r.x0, y, r.z0), new Vector3(r.x1, y, r.z0), new Vector3(r.x1, y, r.z1), new Vector3(r.x0, y, r.z1));

        private static void AddLFloor(MeshBuilder mesh, RoomSpec room, float hw, float hd)
        {
            var (big, small) = LRects(room, hw, hd);
            mesh.AddSolidRange(4, Floor);
            AddFlatQuad(mesh, big, 0f);
            AddFlatQuad(mesh, small, 0f);
            AddGridRect(mesh, room.GridSpacing, big);
            AddGridRect(mesh, room.GridSpacing, small);
        }

        private static void AddLCeiling(MeshBuilder mesh, RoomSpec room, float hw, float hd)
        {
            var (big, small) = LRects(room, hw, hd);
            mesh.AddSolidRange(4, Ceiling);
            AddFlatQuad(mesh, big, room.Height);
            AddFlatQuad(mesh, small, room.Height);
        }

        // Grid lines within one rectangle, starting from its own low edge (see AddGrid).
        private static void AddGridRect(MeshBuilder mesh, float spacing, (float x0, float x1, float z0, float z1) r)
        {
            for (var x = r.x0 + spacing; x < r.x1 - 0.01f; x += spacing)
                mesh.AddLine(new Vector3(x, 0f, r.z0), new Vector3(x, 0f, r.z1));
            for (var z = r.z0 + spacing; z < r.z1 - 0.01f; z += spacing)
                mesh.AddLine(new Vector3(r.x0, 0f, z), new Vector3(r.x1, 0f, z));
        }

        // Steps rising to the north: each is a tread on top and a riser facing south, outlined all round
        // (including where it meets the side walls, which shows the staircase's profile).
        private static void AddSteps(MeshBuilder mesh, RoomSpec room, float hw, float hd)
        {
            var count = room.Stairs.Steps;
            var tread = room.Depth / count;
            var rise = room.Stairs.Rise / count;
            var treads = new List<Vector3[]>();
            var risers = new List<Vector3[]>();

            for (var i = 0; i < count; i++)
            {
                var z0 = hd - i * tread;     // front (south) of this step
                var z1 = z0 - tread;
                var low = i * rise;
                var high = (i + 1) * rise;

                treads.Add(new[] { new Vector3(-hw, high, z0), new Vector3(hw, high, z0), new Vector3(hw, high, z1), new Vector3(-hw, high, z1) });
                risers.Add(new[] { new Vector3(-hw, low, z0), new Vector3(hw, low, z0), new Vector3(hw, high, z0), new Vector3(-hw, high, z0) });

                mesh.AddLine(new Vector3(-hw, low, z0), new Vector3(hw, low, z0));
                mesh.AddLine(new Vector3(-hw, high, z0), new Vector3(hw, high, z0));
                foreach (var x in new[] { -hw, hw })
                {
                    mesh.AddLine(new Vector3(x, low, z0), new Vector3(x, high, z0));
                    mesh.AddLine(new Vector3(x, high, z0), new Vector3(x, high, z1));
                }
            }
            AddQuads(mesh, treads, Floor);
            AddQuads(mesh, risers, Riser);
        }

        // A whole wall, its full extent.
        private static void AddWall(MeshBuilder mesh, RoomSpec room, Wall wall, List<Vector3[]> quads)
        {
            var half = (wall is Wall.North or Wall.South ? room.Width : room.Depth) / 2f;
            AddWallSegment(mesh, room, wall, -half, half, quads);
        }

        // One wall as flat quads (up to three round its opening, if it has one) and its outline, running
        // from `lo` to `hi` along the wall rather than necessarily its full extent - a notch corner shortens
        // the wall it cuts into (see AddNotchWalls). The top of a wall follows the ceiling, so the side
        // walls of a stairwell slope.
        private static void AddWallSegment(MeshBuilder mesh, RoomSpec room, Wall wall, float lo, float hi, List<Vector3[]> quads)
        {
            var opening = Array.Find(room.Openings, o => o.Wall == wall);

            // Height of the wall's top at a distance `along` from its centre
            float Top(float along) => room.CeilingHeightAt(wall switch
            {
                Wall.North => -room.Depth / 2f,
                Wall.South => room.Depth / 2f,
                _ => along,
            });
            Vector3 P(float along, float y) => room.WallPoint(wall, along) + Vector3.Up * y;
            void Line(float a0, float y0, float a1, float y1) => mesh.AddLine(P(a0, y0), P(a1, y1));
            void Piece(float a0, float a1, float bottom)
            {
                if (a1 - a0 < Tiny || (Top(a0) - bottom < Tiny && Top(a1) - bottom < Tiny))
                    return;
                quads.Add(new[] { P(a0, bottom), P(a1, bottom), P(a1, Top(a1)), P(a0, Top(a0)) });
            }

            if (opening == null)
            {
                Piece(lo, hi, 0f);
                Line(lo, 0f, hi, 0f);
                Line(lo, Top(lo), hi, Top(hi));
                Line(lo, 0f, lo, Top(lo));
                Line(hi, 0f, hi, Top(hi));
                return;
            }

            var left = MathHelper.Clamp(opening.Offset - opening.Width / 2f, lo, hi);
            var right = MathHelper.Clamp(opening.Offset + opening.Width / 2f, lo, hi);
            var lintel = opening.Height < Math.Min(Top(left), Top(right)) - Tiny;   // wall left above the gap

            Piece(lo, left, 0f);
            Piece(right, hi, 0f);
            if (lintel)
                Piece(left, right, opening.Height);

            // Outline: the floor line and top line where there is wall, the gap's edges, and the corners
            var hasLeft = left - lo > Tiny;
            var hasRight = hi - right > Tiny;
            if (hasLeft) Line(lo, 0f, left, 0f);
            if (hasRight) Line(right, 0f, hi, 0f);
            if (lintel)
            {
                Line(lo, Top(lo), hi, Top(hi));
                Line(left, opening.Height, right, opening.Height);
            }
            else
            {
                if (hasLeft) Line(lo, Top(lo), left, Top(left));
                if (hasRight) Line(right, Top(right), hi, Top(hi));
            }
            if (hasLeft)
            {
                Line(lo, 0f, lo, Top(lo));
                Line(left, 0f, left, Math.Min(opening.Height, Top(left)));
            }
            else if (lintel)
                Line(lo, opening.Height, lo, Top(lo));
            if (hasRight)
            {
                Line(hi, 0f, hi, Top(hi));
                Line(right, 0f, right, Math.Min(opening.Height, Top(right)));
            }
            else if (lintel)
                Line(hi, opening.Height, hi, Top(hi));
        }

        // The room's six walls when it has a notch corner: North and West (say, for a north-west notch)
        // run only along the part of the room's edge the notch leaves standing, and two extra flat faces
        // close off the inner corner. Never cut by a door or opening - see RoomSpec.NotchSpec.
        private static void AddNotchWalls(MeshBuilder mesh, RoomSpec room, float hw, float hd, List<Vector3[]> northSouth, List<Vector3[]> eastWest)
        {
            var (x0, x1, z0, z1, ex, ez) = room.NotchBounds();

            // The remaining span of the north/south wall pair (whichever the notch shortens) is on
            // whichever side of x the notch isn't; likewise the east/west pair along z.
            var nx0 = ex < 0 ? x1 : -hw;
            var nx1 = ex < 0 ? hw : x0;
            var wz0 = ez < 0 ? z1 : -hd;
            var wz1 = ez < 0 ? hd : z0;

            AddWallSegment(mesh, room, Wall.North, ez < 0 ? nx0 : -hw, ez < 0 ? nx1 : hw, northSouth);
            AddWallSegment(mesh, room, Wall.South, ez > 0 ? nx0 : -hw, ez > 0 ? nx1 : hw, northSouth);
            AddWallSegment(mesh, room, Wall.West, ex < 0 ? wz0 : -hd, ex < 0 ? wz1 : hd, eastWest);
            AddWallSegment(mesh, room, Wall.East, ex > 0 ? wz0 : -hd, ex > 0 ? wz1 : hd, eastWest);

            // The notch's own two inner faces, closing off the bite taken out of the corner.
            var innerZ = ez < 0 ? z1 : z0;
            var innerX = ex < 0 ? x1 : x0;
            AddFlatWall(mesh, new Vector3(x0, 0f, innerZ), new Vector3(x1, 0f, innerZ), room.Height, northSouth);
            AddFlatWall(mesh, new Vector3(innerX, 0f, z0), new Vector3(innerX, 0f, z1), room.Height, eastWest);
        }

        // A plain wall face between two floor points, `height` tall, with its outline - used for the two
        // inner faces of a notch corner, which are never cut by a door.
        private static void AddFlatWall(MeshBuilder mesh, Vector3 a, Vector3 b, float height, List<Vector3[]> quads)
        {
            var top = a + Vector3.Up * height;
            var topB = b + Vector3.Up * height;
            quads.Add(new[] { a, b, topB, top });
            mesh.AddLine(a, b);
            mesh.AddLine(top, topB);
            mesh.AddLine(a, top);
            mesh.AddLine(b, topB);
        }

        // A frame, the door inside it, and a handle, all flat on the wall.
        private static void AddDoor(MeshBuilder mesh, RoomSpec room, DoorSpec door)
        {
            var inward = RoomSpec.Inward(door.Wall);
            var along = RoomSpec.Tangent(door.Wall);
            var origin = room.WallPoint(door.Wall, door.Offset);

            // The four corners of a rectangle on the wall: centred on the door, from the floor up to `top`.
            Vector3[] Rect(float halfWidth, float bottom, float top, float lift) =>
                new[]
                {
                    origin + along * -halfWidth + Vector3.Up * bottom + inward * lift,
                    origin + along * halfWidth + Vector3.Up * bottom + inward * lift,
                    origin + along * halfWidth + Vector3.Up * top + inward * lift,
                    origin + along * -halfWidth + Vector3.Up * top + inward * lift,
                };

            var frame = Rect(RoomSpec.DoorWidth / 2f + FrameBorder, 0f, RoomSpec.DoorHeight + FrameBorder, FrameLift);
            mesh.AddSolidRange(2, Frame);
            mesh.AddQuad(frame[0], frame[1], frame[2], frame[3]);

            var panel = Rect(RoomSpec.DoorWidth / 2f, 0f, RoomSpec.DoorHeight, DoorLift);
            mesh.AddSolidRange(2, Door);
            mesh.AddQuad(panel[0], panel[1], panel[2], panel[3]);

            // Handle near the +tangent edge of the door
            var handleCentre = RoomSpec.DoorWidth / 2f - 0.12f;
            var handle = Rect(0.05f, 0.95f, 1.03f, HandleLift);
            mesh.AddSolidRange(2, Handle);
            mesh.AddQuad(
                handle[0] + along * handleCentre, handle[1] + along * handleCentre,
                handle[2] + along * handleCentre, handle[3] + along * handleCentre);

            mesh.AddLineLoop(Rect(RoomSpec.DoorWidth / 2f + FrameBorder, 0f, RoomSpec.DoorHeight + FrameBorder, EdgeLift));
            mesh.AddLineLoop(Rect(RoomSpec.DoorWidth / 2f, 0f, RoomSpec.DoorHeight, EdgeLift + 0.005f));

            // Faces vanish in wireframe, so the handle needs its own outline, in front of the door's
            var handleOutline = Rect(0.05f, 0.95f, 1.03f, HandleLift + 0.005f);
            mesh.AddLineLoop(
                handleOutline[0] + along * handleCentre, handleOutline[1] + along * handleCentre,
                handleOutline[2] + along * handleCentre, handleOutline[3] + along * handleCentre);
        }
    }
}
