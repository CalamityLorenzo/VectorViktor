using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace World.Buildings
{
    // The inside of a room as one mesh: floor (or steps), ceiling, one wall per edge of its Outline (with
    // any openings cut in them), the doors painted on the walls, and a grid on the floor so there is
    // something to see moving past in wireframe. Faces are seen from inside, so the mesh is drawn with
    // culling off.
    public static class RoomMesh
    {
        public const int Floor = 0, WallA = 1, WallB = 2, Ceiling = 3, Door = 4, Frame = 5, Handle = 6, Riser = 7;
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
            room.Floor, room.WallA, room.WallB, room.Ceiling, DoorColor, FrameColor, HandleColor,
            Color.Lerp(room.Floor, Color.Black, 0.3f),   // the upright faces of steps
        };

        public static MeshData Build(GraphicsDevice device, RoomSpec room)
        {
            var mesh = new MeshBuilder();

            // A stepped ramp (RampSpec.Steps > 0) cuts its own tread/riser strip out of the floor; what's
            // left is fitted round it as one or two ordinary flat pieces, whatever shape they come out as.
            // Hatches (see HatchSpec) are cut out of the floor and ceiling the same way.
            var steppedRamps = Array.FindAll(room.Ramps, r => r.Steps > 0);
            var floorPieces = new List<Vector2[]> { room.Outline };
            foreach (var hatch in room.FloorHatches)
                floorPieces = CutOut(floorPieces, hatch.Outline);
            foreach (var ramp in steppedRamps)
                floorPieces = SplitAroundRamp(floorPieces, ramp);

            AddFlatFloor(mesh, floorPieces);
            if (steppedRamps.Length == 0)
                AddGrid(mesh, room);   // a grid over/under a staircase's treads would just be visual clutter
            foreach (var ramp in steppedRamps)
                AddSteps(mesh, ramp);
            foreach (var hatch in room.FloorHatches)
                mesh.AddLineLoop(Array.ConvertAll(hatch.Outline, p => new Vector3(p.X, 0f, p.Y)));

            var ceilingPieces = new List<Vector2[]> { room.Outline };
            foreach (var hatch in room.CeilingHatches)
                ceilingPieces = CutOut(ceilingPieces, hatch.Outline);
            if (room.Pitched is { } gable)
                ceilingPieces = SplitAtRidge(ceilingPieces, gable);   // so each slope is flat
            AddCeiling(mesh, room, ceilingPieces);
            foreach (var hatch in room.CeilingHatches)
                AddSlabEdge(mesh, room, hatch);

            for (var i = 0; i < room.Outline.Length; i++)
                AddWall(mesh, room, i, i % 2 == 0 ? WallA : WallB);

            foreach (var door in room.Doors)
                AddDoor(mesh, room, door);

            return mesh.Build(device);
        }

        private static (float minX, float maxX, float minZ, float maxZ) Bounds(Vector2[] outline)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var v in outline)
            {
                if (v.X < minX) minX = v.X;
                if (v.X > maxX) maxX = v.X;
                if (v.Y < minZ) minZ = v.Y;
                if (v.Y > maxZ) maxZ = v.Y;
            }
            return (minX, maxX, minZ, maxZ);
        }

        // Flat, at y = 0: one or more separately-triangulated pieces, since a stepped ramp can split the
        // room's floor into a piece before it and a piece after (see SplitAroundRamp).
        private static void AddFlatFloor(MeshBuilder mesh, List<Vector2[]> pieces)
        {
            foreach (var (a, b, c) in TrianglesOf(pieces))
                mesh.AddTri(Floor, new Vector3(a.X, 0f, a.Y), new Vector3(b.X, 0f, b.Y), new Vector3(c.X, 0f, c.Y));
        }

        private static List<(Vector2 a, Vector2 b, Vector2 c)> TrianglesOf(List<Vector2[]> pieces)
        {
            var triangles = new List<(Vector2 a, Vector2 b, Vector2 c)>();
            foreach (var piece in pieces)
                foreach (var (ia, ib, ic) in Triangulate(piece))
                    triangles.Add((piece[ia], piece[ib], piece[ic]));
            return triangles;
        }

        // The room's whole Outline, less any ceiling hatches - unlike the floor, never split by a stepped
        // ramp, since its headroom still needs a ceiling above it. Height varies per vertex (flat, except
        // where RoomSpec.CeilingHeightAt says a stepped ramp runs under it), so a triangle that straddles
        // the edge of one blends smoothly between the two rather than stepping - a fair approximation,
        // since that seam falls where a real room would have a landing or a change of pitch anyway.
        private static void AddCeiling(MeshBuilder mesh, RoomSpec room, List<Vector2[]> pieces)
        {
            Vector3 Up(Vector2 p) => new Vector3(p.X, room.CeilingHeightAt(new Vector3(p.X, 0f, p.Y)), p.Y);
            foreach (var (a, b, c) in TrianglesOf(pieces))
                mesh.AddTri(Ceiling, Up(a), Up(b), Up(c));
        }

        // The cut edge of the slab a ceiling hatch goes up through, from this room's (flat) ceiling to the
        // floor of the room above. Its top rim is that room's own hatch outline, so it isn't drawn twice.
        private static void AddSlabEdge(MeshBuilder mesh, RoomSpec room, HatchSpec hatch)
        {
            var bottom = room.Height;
            var top = room.Height + hatch.SlabThickness;
            for (var i = 0; i < hatch.Outline.Length; i++)
            {
                var a = hatch.Outline[i];
                var b = hatch.Outline[(i + 1) % hatch.Outline.Length];
                mesh.AddQuad(Ceiling, new Vector3(a.X, bottom, a.Y), new Vector3(b.X, bottom, b.Y), new Vector3(b.X, top, b.Y), new Vector3(a.X, top, a.Y));
                mesh.AddLine(new Vector3(a.X, bottom, a.Y), new Vector3(b.X, bottom, b.Y));
                mesh.AddLine(new Vector3(a.X, bottom, a.Y), new Vector3(a.X, top, a.Y));
            }
        }

        // What's left of each piece outside a convex hole: for each of the hole's edges, the part beyond
        // that edge but not beyond any earlier one, so no two parts overlap. Like SplitAroundRamp, the
        // pieces themselves can be any shape.
        private static List<Vector2[]> CutOut(List<Vector2[]> pieces, Vector2[] hole)
        {
            var centre = Vector2.Zero;
            foreach (var p in hole)
                centre += p;
            centre /= hole.Length;

            // A point on edge i, and its normal pointing out of the hole whichever way the hole is wound
            (Vector2 point, Vector2 outward) Edge(int i)
            {
                var a = hole[i];
                var d = hole[(i + 1) % hole.Length] - a;
                var normal = new Vector2(-d.Y, d.X);
                return (a, Vector2.Dot(normal, a - centre) < 0f ? -normal : normal);
            }

            var result = new List<Vector2[]>();
            foreach (var piece in pieces)
                for (var i = 0; i < hole.Length; i++)
                {
                    var (point, outward) = Edge(i);
                    var part = ClipToHalfPlane(piece, point, outward);
                    for (var j = 0; j < i && part.Count >= 3; j++)
                    {
                        var (earlier, earlierOutward) = Edge(j);
                        part = ClipToHalfPlane(part.ToArray(), earlier, -earlierOutward);
                    }
                    // A hole flush against a wall leaves a zero-width sliver along it
                    if (part.Count >= 3 && MathF.Abs(SignedArea(part)) > 1e-6f)
                        result.Add(part.ToArray());
                }
            return result;
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            var area = 0f;
            for (var i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += a.X * b.Y - b.X * a.Y;
            }
            return area / 2f;
        }

        // Cuts the strip a stepped ramp runs through out of each floor piece, by slicing it at two lines
        // perpendicular to the ramp - one through Start, one through End - and keeping whatever's outside
        // that band. Works for any simple polygon, convex or concave, so the room doesn't have to be a
        // rectangle just because a ramp runs through it.
        private static List<Vector2[]> SplitAroundRamp(List<Vector2[]> pieces, RampSpec ramp)
        {
            var start = new Vector2(ramp.Start.X, ramp.Start.Z);
            var end = new Vector2(ramp.End.X, ramp.End.Z);
            var dir = end - start;
            if (dir.LengthSquared() < 1e-6f)
                return pieces;
            dir.Normalize();

            var result = new List<Vector2[]>();
            foreach (var piece in pieces)
            {
                var before = ClipToHalfPlane(piece, start, -dir);
                if (before.Count >= 3)
                    result.Add(before.ToArray());
                var after = ClipToHalfPlane(piece, end, dir);
                if (after.Count >= 3)
                    result.Add(after.ToArray());
            }
            return result;
        }

        // Each piece cut in two along a pitched ceiling's ridge line (through the origin), one side of it each.
        private static List<Vector2[]> SplitAtRidge(List<Vector2[]> pieces, Gable gable)
        {
            var result = new List<Vector2[]>();
            foreach (var piece in pieces)
                foreach (var side in new[] { gable.Across, -gable.Across })
                {
                    var half = ClipToHalfPlane(piece, Vector2.Zero, side);
                    if (half.Count >= 3)
                        result.Add(half.ToArray());
                }
            return result;
        }

        // Sutherland-Hodgman: the part of a polygon (convex or concave) on the side of the line through
        // planePoint that planeNormal points into.
        private static List<Vector2> ClipToHalfPlane(Vector2[] polygon, Vector2 planePoint, Vector2 planeNormal)
        {
            var output = new List<Vector2>();
            for (var i = 0; i < polygon.Length; i++)
            {
                var curr = polygon[i];
                var prev = polygon[(i - 1 + polygon.Length) % polygon.Length];
                var currIn = Vector2.Dot(curr - planePoint, planeNormal) >= 0f;
                var prevIn = Vector2.Dot(prev - planePoint, planeNormal) >= 0f;
                if (currIn != prevIn)
                {
                    var denom = Vector2.Dot(curr - prev, planeNormal);
                    var t = MathF.Abs(denom) > 1e-9f ? Vector2.Dot(planePoint - prev, planeNormal) / denom : 0f;
                    output.Add(prev + (curr - prev) * t);
                }
                if (currIn)
                    output.Add(curr);
            }
            return output;
        }

        // Splits a simple polygon (convex or concave, wound either way) into triangles by ear clipping:
        // repeatedly cut off a "convex and empty" corner until three vertices are left. O(n^2) worst case,
        // which is fine for room footprints (a handful of vertices, built once and cached by MeshCache).
        internal static List<(int a, int b, int c)> Triangulate(Vector2[] polygon)
        {
            var n = polygon.Length;
            var indices = new List<int>(n);
            for (var i = 0; i < n; i++)
                indices.Add(i);

            var sign = SignedArea(polygon) >= 0f ? 1f : -1f;

            var triangles = new List<(int, int, int)>();
            var guard = 0;
            while (indices.Count > 3 && guard++ < n * n + 8)
            {
                var earFound = false;
                for (var k = 0; k < indices.Count; k++)
                {
                    var iPrev = indices[(k - 1 + indices.Count) % indices.Count];
                    var iCurr = indices[k];
                    var iNext = indices[(k + 1) % indices.Count];
                    var prev = polygon[iPrev]; var curr = polygon[iCurr]; var next = polygon[iNext];

                    var cross = (curr.X - prev.X) * (next.Y - prev.Y) - (curr.Y - prev.Y) * (next.X - prev.X);
                    if (cross * sign <= 0f)
                        continue;   // reflex at this vertex, not an ear

                    var isEar = true;
                    foreach (var iOther in indices)
                    {
                        if (iOther == iPrev || iOther == iCurr || iOther == iNext)
                            continue;
                        if (PointInTriangle(polygon[iOther], prev, curr, next))
                        {
                            isEar = false;
                            break;
                        }
                    }
                    if (!isEar)
                        continue;

                    triangles.Add((iPrev, iCurr, iNext));
                    indices.RemoveAt(k);
                    earFound = true;
                    break;
                }
                if (!earFound)
                    break;   // degenerate input; use what's clipped so far rather than loop forever
            }
            if (indices.Count == 3)
                triangles.Add((indices[0], indices[1], indices[2]));
            return triangles;
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float Sign(Vector2 p1, Vector2 p2, Vector2 p3) => (p1.X - p3.X) * (p2.Y - p3.Y) - (p2.X - p3.X) * (p1.Y - p3.Y);
            var d1 = Sign(p, a, b);
            var d2 = Sign(p, b, c);
            var d3 = Sign(p, c, a);
            var hasNeg = d1 < 0f || d2 < 0f || d3 < 0f;
            var hasPos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNeg && hasPos);
        }

        // Where a vertical line (fixed X) or horizontal line (fixed Z) crosses the edges of the Outline and
        // of any floor hatches, sorted - consecutive pairs bound the spans of the line that lie on the floor.
        private static List<float> Crossings(RoomSpec room, bool vertical, float fixedCoord)
        {
            var hits = new List<float>();
            AddCrossings(hits, room.Outline, vertical, fixedCoord);
            foreach (var hatch in room.FloorHatches)
                AddCrossings(hits, hatch.Outline, vertical, fixedCoord);
            hits.Sort();
            return hits;
        }

        private static void AddCrossings(List<float> hits, Vector2[] polygon, bool vertical, float fixedCoord)
        {
            var n = polygon.Length;
            for (var i = 0; i < n; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % n];
                var aFixed = vertical ? a.X : a.Y;
                var bFixed = vertical ? b.X : b.Y;
                if ((aFixed > fixedCoord) == (bFixed > fixedCoord))
                    continue;
                var aFree = vertical ? a.Y : a.X;
                var bFree = vertical ? b.Y : b.X;
                var t = (fixedCoord - aFixed) / (bFixed - aFixed);
                hits.Add(aFree + t * (bFree - aFree));
            }
        }

        // Grid lines every GridSpacing, clipped to the room's Outline - convex or concave, so a span of
        // wall or a notch just breaks a line into its separate visible pieces.
        private static void AddGrid(MeshBuilder mesh, RoomSpec room)
        {
            var (minX, maxX, minZ, maxZ) = Bounds(room.Outline);
            var spacing = room.GridSpacing;

            for (var x = MathF.Ceiling(minX / spacing) * spacing; x < maxX - 0.01f; x += spacing)
            {
                var hits = Crossings(room, vertical: true, x);
                for (var i = 0; i + 1 < hits.Count; i += 2)
                    mesh.AddLine(new Vector3(x, 0f, hits[i]), new Vector3(x, 0f, hits[i + 1]));
            }
            for (var z = MathF.Ceiling(minZ / spacing) * spacing; z < maxZ - 0.01f; z += spacing)
            {
                var hits = Crossings(room, vertical: false, z);
                for (var i = 0; i + 1 < hits.Count; i += 2)
                    mesh.AddLine(new Vector3(hits[i], 0f, z), new Vector3(hits[i + 1], 0f, z));
            }
        }

        // Steps rising from a ramp's Start to its End, Width wide, however that line happens to run -
        // north-south as the stairwell's always been, or any other direction. Each is a tread on top and
        // a riser facing back toward Start, outlined all round (including the sides, which shows the
        // staircase's profile).
        private static void AddSteps(MeshBuilder mesh, RampSpec ramp)
        {
            var start = new Vector2(ramp.Start.X, ramp.Start.Z);
            var end = new Vector2(ramp.End.X, ramp.End.Z);
            var full = end - start;
            var length = full.Length();
            if (length < 1e-4f)
                return;
            var along = full / length;
            var side = new Vector2(-along.Y, along.X);   // perpendicular, across the ramp's width
            var hw = ramp.Width / 2f;

            Vector3 P(float alongLen, float lateral, float y)
            {
                var p = start + along * alongLen + side * lateral;
                return new Vector3(p.X, y, p.Y);
            }

            var count = ramp.Steps;
            var tread = length / count;
            var rise = (ramp.End.Y - ramp.Start.Y) / count;

            for (var i = 0; i < count; i++)
            {
                var a0 = i * tread;         // near (Start) edge of this step
                var a1 = (i + 1) * tread;   // far (End) edge
                var low = ramp.Start.Y + i * rise;
                var high = ramp.Start.Y + (i + 1) * rise;

                mesh.AddQuad(Floor, P(a0, -hw, high), P(a0, hw, high), P(a1, hw, high), P(a1, -hw, high));
                mesh.AddQuad(Riser, P(a0, -hw, low), P(a0, hw, low), P(a0, hw, high), P(a0, -hw, high));

                mesh.AddLine(P(a0, -hw, low), P(a0, hw, low));
                mesh.AddLine(P(a0, -hw, high), P(a0, hw, high));
                foreach (var lateral in new[] { -hw, hw })
                {
                    mesh.AddLine(P(a0, lateral, low), P(a0, lateral, high));
                    mesh.AddLine(P(a0, lateral, high), P(a1, lateral, high));
                }
            }
        }

        // One wall (one edge of the room's Outline) as flat quads (up to three round its opening, if it
        // has one) and its outline. The top of a wall follows the ceiling, so the side walls of a
        // stairwell slope.
        private static void AddWall(MeshBuilder mesh, RoomSpec room, int wallIndex, int slot)
        {
            var half = room.WallLength(wallIndex) / 2f;
            var opening = Array.Find(room.Openings, o => o.WallIndex == wallIndex);

            // Height of the wall's top at a distance `along` from its centre
            float Top(float along) => room.CeilingHeightAt(room.WallPoint(wallIndex, along));
            Vector3 P(float along, float y) => room.WallPoint(wallIndex, along) + Vector3.Up * y;
            void Line(float a0, float y0, float a1, float y1) => mesh.AddLine(P(a0, y0), P(a1, y1));
            // Under a pitched ceiling, where along the wall it passes under the ridge: its top peaks there
            float? ridge = null;
            if (room.Pitched is { } gable)
            {
                var t = new Vector2(room.Tangent(wallIndex).X, room.Tangent(wallIndex).Z);
                var middle = room.WallPoint(wallIndex, 0f);
                var across = Vector2.Dot(t, gable.Across);
                if (MathF.Abs(across) > 1e-4f)
                    ridge = -Vector2.Dot(new Vector2(middle.X, middle.Z), gable.Across) / across;
            }
            bool Peaks(float a0, float a1) => ridge.HasValue && ridge.Value > a0 + Tiny && ridge.Value < a1 - Tiny;
            void TopLine(float a0, float a1)
            {
                if (Peaks(a0, a1))
                {
                    Line(a0, Top(a0), ridge.Value, Top(ridge.Value));
                    Line(ridge.Value, Top(ridge.Value), a1, Top(a1));
                }
                else
                    Line(a0, Top(a0), a1, Top(a1));
            }
            void Piece(float a0, float a1, float bottom)
            {
                if (a1 - a0 < Tiny || (Top(a0) - bottom < Tiny && Top(a1) - bottom < Tiny))
                    return;
                if (Peaks(a0, a1))
                {
                    var r = ridge.Value;
                    mesh.AddQuad(slot, P(a0, bottom), P(r, bottom), P(r, Top(r)), P(a0, Top(a0)));
                    mesh.AddQuad(slot, P(r, bottom), P(a1, bottom), P(a1, Top(a1)), P(r, Top(r)));
                    return;
                }
                mesh.AddQuad(slot, P(a0, bottom), P(a1, bottom), P(a1, Top(a1)), P(a0, Top(a0)));
            }

            if (opening == null)
            {
                Piece(-half, half, 0f);
                Line(-half, 0f, half, 0f);
                TopLine(-half, half);
                Line(-half, 0f, -half, Top(-half));
                Line(half, 0f, half, Top(half));
                return;
            }

            var left = MathHelper.Clamp(opening.Offset - opening.Width / 2f, -half, half);
            var right = MathHelper.Clamp(opening.Offset + opening.Width / 2f, -half, half);
            var lintel = opening.Height < Math.Min(Top(left), Top(right)) - Tiny;   // wall left above the gap

            Piece(-half, left, 0f);
            Piece(right, half, 0f);
            if (lintel)
                Piece(left, right, opening.Height);

            // Outline: the floor line and top line where there is wall, the gap's edges, and the corners
            var hasLeft = left - -half > Tiny;
            var hasRight = half - right > Tiny;
            if (hasLeft) Line(-half, 0f, left, 0f);
            if (hasRight) Line(right, 0f, half, 0f);
            if (lintel)
            {
                TopLine(-half, half);
                Line(left, opening.Height, right, opening.Height);
            }
            else
            {
                if (hasLeft) TopLine(-half, left);
                if (hasRight) TopLine(right, half);
            }
            if (hasLeft)
            {
                Line(-half, 0f, -half, Top(-half));
                Line(left, 0f, left, Math.Min(opening.Height, Top(left)));
            }
            else if (lintel)
                Line(-half, opening.Height, -half, Top(-half));
            if (hasRight)
            {
                Line(half, 0f, half, Top(half));
                Line(right, 0f, right, Math.Min(opening.Height, Top(right)));
            }
            else if (lintel)
                Line(half, opening.Height, half, Top(half));
        }

        // A frame, the door inside it, and a handle, all flat on the wall.
        private static void AddDoor(MeshBuilder mesh, RoomSpec room, DoorSpec door)
        {
            var inward = room.Inward(door.WallIndex);
            var along = room.Tangent(door.WallIndex);
            var origin = room.WallPoint(door.WallIndex, door.Offset);

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
            mesh.AddQuad(Frame, frame[0], frame[1], frame[2], frame[3]);

            var panel = Rect(RoomSpec.DoorWidth / 2f, 0f, RoomSpec.DoorHeight, DoorLift);
            mesh.AddQuad(Door, panel[0], panel[1], panel[2], panel[3]);

            // Handle near the +tangent edge of the door
            var handleCentre = RoomSpec.DoorWidth / 2f - 0.12f;
            var handle = Rect(0.05f, 0.95f, 1.03f, HandleLift);
            mesh.AddQuad(Handle,
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
