using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace World.Buildings
{
    // The outside of a building (see Building), as one mesh in world coordinates: the outer walls round
    // every room, a plinth colour below each floor (going down into the ground under the ground floor, and
    // as a band between storeys), the doorways cut through and lined across the wall's thickness, and a
    // flat roof. The rooms' insides are RoomMesh's; together they make the walls solid.
    public static class BuildingMesh
    {
        public const int WallA = 0, WallB = 1, Roof = 2, Plinth = 3, Reveal = 4;
        public const int PaletteSize = 5;

        public static Color[] Palette(Building building) => new[]
        {
            building.WallColor,
            Color.Lerp(building.WallColor, Color.Black, 0.15f),
            building.RoofColor,
            building.PlinthColor,
            Color.Lerp(building.WallColor, Color.Black, 0.3f),
        };

        public static MeshData Build(GraphicsDevice device, Building building)
        {
            var mesh = new MeshBuilder();
            var faces = new List<Vector3[]>[PaletteSize];
            for (var slot = 0; slot < PaletteSize; slot++)
                faces[slot] = new List<Vector3[]>();

            foreach (var room in building.Rooms)
                AddShell(mesh, faces, building, room);

            // One draw range per colour
            for (var slot = 0; slot < PaletteSize; slot++)
            {
                var triangles = 0;
                foreach (var polygon in faces[slot])
                    triangles += polygon.Length - 2;
                if (triangles == 0)
                    continue;
                mesh.AddSolidRange(triangles, slot);
                foreach (var polygon in faces[slot])
                    mesh.AddPolygon(polygon);
            }
            return mesh.Build(device);
        }

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);

        private static void AddShell(MeshBuilder mesh, List<Vector3[]>[] faces, Building building, RoomSpec room)
        {
            var floor = room.WorldOffset.Y;
            var (bottom, top, roofed) = building.ShellSpan(room);
            var outer = building.OuterOutline(room);
            var n = outer.Length;

            for (var edge = 0; edge < n; edge++)
            {
                if (Building.IsInside(room, edge))
                    continue;
                var a = outer[edge];
                var b = outer[(edge + 1) % n];
                var wall = edge % 2 == 0 ? WallA : WallB;

                // A strip of this wall from p to q, from y0 to y1, coloured as plinth below the floor
                void Strip(Vector2 p, Vector2 q, float y0, float y1)
                {
                    if (y1 - y0 < 1e-4f)
                        return;
                    var split = MathHelper.Clamp(floor, y0, y1);
                    if (split > y0)
                        faces[Plinth].Add(new[] { At(p, y0), At(q, y0), At(q, split), At(p, split) });
                    if (y1 > split)
                        faces[wall].Add(new[] { At(p, split), At(q, split), At(q, y1), At(p, y1) });
                    mesh.AddLine(At(p, y1), At(q, y1));
                    if (split > y0 && split < y1)
                        mesh.AddLine(At(p, split), At(q, split));
                }

                mesh.AddLine(At(a, bottom), At(a, top));   // the corner

                var opening = Array.Find(room.Openings, o => o.WallIndex == edge && o.LeadsOutside);
                if (opening == null)
                {
                    Strip(a, b, bottom, top);
                    continue;
                }

                // A doorway: wall either side, over it, and the plinth under its threshold
                var push = Building.Outward(room.Outline[edge], room.Outline[(edge + 1) % n]) * building.WallThickness;
                var (left, right) = Building.Gap(room, opening);
                var innerLeft = Building.WallPoint(room, edge, left);
                var innerRight = Building.WallPoint(room, edge, right);
                var outerLeft = innerLeft + push;
                var outerRight = innerRight + push;
                var head = MathF.Min(floor + opening.Height, top);

                Strip(a, outerLeft, bottom, top);
                Strip(outerRight, b, bottom, top);
                Strip(outerLeft, outerRight, bottom, floor);
                Strip(outerLeft, outerRight, head, top);
                mesh.AddLine(At(outerLeft, floor), At(outerLeft, head));
                mesh.AddLine(At(outerRight, floor), At(outerRight, head));
                mesh.AddLine(At(outerLeft, head), At(outerRight, head));

                // Lined across the wall's thickness: both sides, the head and the threshold
                faces[Reveal].Add(new[] { At(innerLeft, floor), At(outerLeft, floor), At(outerLeft, head), At(innerLeft, head) });
                faces[Reveal].Add(new[] { At(innerRight, floor), At(outerRight, floor), At(outerRight, head), At(innerRight, head) });
                faces[Reveal].Add(new[] { At(innerLeft, head), At(outerLeft, head), At(outerRight, head), At(innerRight, head) });
                faces[Plinth].Add(new[] { At(innerLeft, floor), At(outerLeft, floor), At(outerRight, floor), At(innerRight, floor) });
                foreach (var y in new[] { floor, head })
                {
                    mesh.AddLine(At(innerLeft, y), At(outerLeft, y));
                    mesh.AddLine(At(innerRight, y), At(outerRight, y));
                }
            }

            if (!roofed)
                return;
            foreach (var (i, j, k) in RoomMesh.Triangulate(outer))
                faces[Roof].Add(new[] { At(outer[i], top), At(outer[j], top), At(outer[k], top) });
        }
    }
}
