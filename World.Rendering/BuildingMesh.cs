using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core;
using World.Buildings;

namespace World.Rendering
{
    // The outside of a building (see Building), as one mesh in world coordinates: the outer walls round
    // every room, a plinth colour below each floor (going down into the ground under the ground floor, and
    // as a band between storeys), the doorways cut through and lined across the wall's thickness, and the
    // roof - flat, or pitched (see Building.Roof): two slopes with an underside and fascia boards round
    // their edges, overhanging the walls, the end walls rising under them to the ridge. The rooms' insides
    // are RoomMesh's; together they make the walls solid.
    public static class BuildingMesh
    {
        public const int WallA = 0, WallB = 1, Roof = 2, Plinth = 3, Reveal = 4, RoofUnder = 5;
        public const int PaletteSize = 6;

        public static Color[] Palette(Building building) => new[]
        {
            building.WallColor,
            Color.Lerp(building.WallColor, Color.Black, 0.15f),
            building.RoofColor,
            building.PlinthColor,
            Color.Lerp(building.WallColor, Color.Black, 0.3f),
            Color.Lerp(building.RoofColor, Color.Black, 0.35f),
        };

        public static MeshSource Source(Building building) => new MeshSource("building:" + building.Name, d => Build(d, building), Palette(building));

        public static MeshData Build(GraphicsDevice device, Building building)
        {
            var mesh = new MeshBuilder();
            foreach (var room in building.Rooms)
                AddShell(mesh, building, room);
            return mesh.Build(device);
        }

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);

        private static void AddShell(MeshBuilder mesh, Building building, RoomSpec room)
        {
            var floor = room.WorldOffset.Y;
            var (bottom, top, roofed) = building.ShellSpan(room);
            var gable = roofed ? building.RoofOf(room) : null;
            var outer = building.OuterOutline(room);
            var n = outer.Length;

            // How high the wall reaches at a point along it: the ceiling or flat roof, or up under a pitched roof
            float Top(Vector2 p) => gable is { } g ? Building.RoofUnderside(room, g, Building.FromRidge(room, g, p)) : top;

            // Where a stretch of wall from p to q passes under the ridge, if it does (a gable end)
            Vector2? Ridge(Vector2 p, Vector2 q)
            {
                if (gable is not { } g)
                    return null;
                var centre = new Vector2(room.WorldOffset.X, room.WorldOffset.Z);
                var sp = Vector2.Dot(p - centre, g.Across);
                var sq = Vector2.Dot(q - centre, g.Across);
                if (sp * sq >= 0f)
                    return null;
                return p + (q - p) * (sp / (sp - sq));
            }

            for (var edge = 0; edge < n; edge++)
            {
                if (Building.IsInside(room, edge))
                    continue;
                var a = outer[edge];
                var b = outer[(edge + 1) % n];
                var wall = edge % 2 == 0 ? WallA : WallB;

                // A strip of this wall from p to q, from y0 up to y1 (or, with no y1, all the way up to the
                // top), coloured as plinth below the floor, and peaking under the ridge if it crosses it
                void Strip(Vector2 p, Vector2 q, float y0, float? y1 = null)
                {
                    if (y1 == null && Ridge(p, q) is { } peak)
                    {
                        Strip(p, peak, y0);
                        Strip(peak, q, y0);
                        return;
                    }
                    float tp = y1 ?? Top(p), tq = y1 ?? Top(q);
                    if (MathF.Max(tp, tq) - y0 < 1e-4f)
                        return;
                    var split = MathF.Min(MathHelper.Clamp(floor, y0, tp), MathHelper.Clamp(floor, y0, tq));
                    if (split > y0)
                        mesh.AddPolygon(Plinth, At(p, y0), At(q, y0), At(q, split), At(p, split));
                    mesh.AddPolygon(wall, At(p, split), At(q, split), At(q, tq), At(p, tp));
                    mesh.AddLine(At(p, tp), At(q, tq));
                    if (split > y0 && split < MathF.Min(tp, tq))
                        mesh.AddLine(At(p, split), At(q, split));
                }

                mesh.AddLine(At(a, bottom), At(a, Top(a)));   // the corner

                var opening = Array.Find(room.Openings, o => o.WallIndex == edge && o.LeadsOutside);
                if (opening == null)
                {
                    Strip(a, b, bottom);
                    continue;
                }

                // A doorway: wall either side, over it, and the plinth under its threshold
                var push = Geometry2D.Outward(room.Outline[edge], room.Outline[(edge + 1) % n]) * building.WallThickness;
                var (left, right) = Building.Gap(room, opening);
                var innerLeft = Building.WallPoint(room, edge, left);
                var innerRight = Building.WallPoint(room, edge, right);
                var outerLeft = innerLeft + push;
                var outerRight = innerRight + push;
                var head = MathF.Min(floor + opening.Height, MathF.Min(Top(outerLeft), Top(outerRight)));

                Strip(a, outerLeft, bottom);
                Strip(outerRight, b, bottom);
                Strip(outerLeft, outerRight, bottom, floor);
                Strip(outerLeft, outerRight, head);
                mesh.AddLine(At(outerLeft, floor), At(outerLeft, head));
                mesh.AddLine(At(outerRight, floor), At(outerRight, head));
                mesh.AddLine(At(outerLeft, head), At(outerRight, head));

                // Lined across the wall's thickness: both sides, the head and the threshold
                mesh.AddPolygon(Reveal, At(innerLeft, floor), At(outerLeft, floor), At(outerLeft, head), At(innerLeft, head));
                mesh.AddPolygon(Reveal, At(innerRight, floor), At(outerRight, floor), At(outerRight, head), At(innerRight, head));
                mesh.AddPolygon(Reveal, At(innerLeft, head), At(outerLeft, head), At(outerRight, head), At(innerRight, head));
                mesh.AddPolygon(Plinth, At(innerLeft, floor), At(outerLeft, floor), At(outerRight, floor), At(innerRight, floor));
                foreach (var y in new[] { floor, head })
                {
                    mesh.AddLine(At(innerLeft, y), At(outerLeft, y));
                    mesh.AddLine(At(innerRight, y), At(outerRight, y));
                }
            }

            if (!roofed)
                return;
            if (gable is { } pitched)
            {
                AddPitchedRoof(mesh, building, room, pitched);
                return;
            }
            foreach (var (i, j, k) in Geometry2D.Triangulate(outer))
                mesh.AddPolygon(Roof, At(outer[i], top), At(outer[j], top), At(outer[k], top));
        }

        // Two slopes meeting at the ridge, RoofThickness thick, reaching RoofOverhang past the outer walls on
        // every side: their tops, their undersides, and the boards along their edges.
        private static void AddPitchedRoof(MeshBuilder mesh, Building building, RoomSpec room, Gable gable)
        {
            var centre = new Vector2(room.WorldOffset.X, room.WorldOffset.Z);
            var halfAlong = 0f;
            foreach (var p in room.Outline)
                halfAlong = MathF.Max(halfAlong, MathF.Abs(Vector2.Dot(p, gable.Along)));
            var reach = building.WallThickness + building.RoofOverhang;
            var along = halfAlong + reach;
            var across = gable.HalfSpan(room.Outline) + reach;
            float Under(float fromRidge) => Building.RoofUnderside(room, gable, fromRidge);
            Vector3 P(float a, float c, float y) => At(centre + gable.Along * a + gable.Across * c, y);
            var t = building.RoofThickness;

            foreach (var side in new[] { -1f, 1f })
            {
                var eave = side * across;
                mesh.AddPolygon(Roof, P(-along, 0f, Under(0f) + t), P(along, 0f, Under(0f) + t), P(along, eave, Under(across) + t), P(-along, eave, Under(across) + t));
                mesh.AddPolygon(RoofUnder, P(-along, 0f, Under(0f)), P(along, 0f, Under(0f)), P(along, eave, Under(across)), P(-along, eave, Under(across)));
                mesh.AddPolygon(RoofUnder, P(-along, eave, Under(across)), P(along, eave, Under(across)), P(along, eave, Under(across) + t), P(-along, eave, Under(across) + t));
                mesh.AddLine(P(-along, eave, Under(across)), P(along, eave, Under(across)));
                mesh.AddLine(P(-along, eave, Under(across) + t), P(along, eave, Under(across) + t));

                // The verges, at the gable ends
                foreach (var end in new[] { -along, along })
                {
                    mesh.AddPolygon(RoofUnder, P(end, 0f, Under(0f)), P(end, eave, Under(across)), P(end, eave, Under(across) + t), P(end, 0f, Under(0f) + t));
                    mesh.AddLine(P(end, 0f, Under(0f) + t), P(end, eave, Under(across) + t));
                    mesh.AddLine(P(end, 0f, Under(0f)), P(end, eave, Under(across)));
                    mesh.AddLine(P(end, eave, Under(across)), P(end, eave, Under(across) + t));
                }
            }
            mesh.AddLine(P(-along, 0f, Under(0f) + t), P(along, 0f, Under(0f) + t));   // the ridge
        }
    }
}
