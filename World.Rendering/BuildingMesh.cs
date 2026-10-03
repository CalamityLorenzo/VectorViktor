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
    // their edges, overhanging the walls, the end walls rising under them to the ridge. Doorways through
    // partitions between rooms (see Building.Through) are lined across the partition. The rooms' insides
    // are RoomMesh's; together they make the walls solid.
    public static class BuildingMesh
    {
        public const int WallA = 0, WallB = 1, Roof = 2, Plinth = 3, Reveal = 4, RoofUnder = 5, Lining = 6, Threshold = 7;
        public const int PaletteSize = 8;

        public static Color[] Palette(Building building) => new[]
        {
            building.WallColor,
            Color.Lerp(building.WallColor, Color.Black, 0.15f),
            building.RoofColor,
            building.PlinthColor,
            Color.Lerp(building.WallColor, Color.Black, 0.3f),
            Color.Lerp(building.RoofColor, Color.Black, 0.35f),
            building.LiningColor,
            Color.Lerp(building.LiningColor, Color.Black, 0.35f),
        };

        public static MeshSource Source(Building building) => new MeshSource("building:" + building.Name, d => Build(d, building), Palette(building));

        public static MeshData Build(GraphicsDevice device, Building building)
        {
            var mesh = new MeshBuilder();
            var roofs = new HashSet<Building.RoofFrame>();   // each pitched roof once, however many rooms are under it
            foreach (var room in building.Rooms)
                AddShell(mesh, building, room, roofs);
            return mesh.Build(device);
        }

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);

        private static void AddShell(MeshBuilder mesh, Building building, RoomSpec room, HashSet<Building.RoofFrame> roofs)
        {
            var floor = room.WorldOffset.Y;
            var (bottom, top, roofed) = building.ShellSpan(room);
            var frame = roofed ? building.RoofOver(room) : null;
            var outer = building.OuterOutline(room);
            var n = outer.Length;

            // How high the wall reaches at a point along it: the ceiling or flat roof, or up under a pitched roof
            float Top(Vector2 p) => frame is { } f ? f.Underside(f.FromRidge(p)) : top;

            // Where a stretch of wall from p to q passes under the ridge, if it does (a gable end)
            Vector2? Ridge(Vector2 p, Vector2 q)
            {
                if (frame is not { } f)
                    return null;
                var sp = Vector2.Dot(p - f.Centre, f.Gable.Across);
                var sq = Vector2.Dot(q - f.Centre, f.Gable.Across);
                if (sp * sq >= 0f)
                    return null;
                return p + (q - p) * (sp / (sp - sq));
            }

            foreach (var opening in room.Openings)
                if (Building.IsThrough(room, opening))
                    AddLining(mesh, building, room, opening);

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
            if (frame is { } pitched)
            {
                if (roofs.Add(pitched))
                    AddPitchedRoof(mesh, building, pitched);
                return;
            }
            foreach (var (i, j, k) in Geometry2D.Triangulate(outer))
                mesh.AddPolygon(Roof, At(outer[i], top), At(outer[j], top), At(outer[k], top));
        }

        // A doorway through a partition, as far as this room lines it (see Building.Through): its two sides and its
        // head, from the room's wall out to the partition's middle, and the threshold under it. The room on the other
        // side lines the rest.
        private static void AddLining(MeshBuilder mesh, Building building, RoomSpec room, OpeningSpec opening)
        {
            var (left, right, midLeft, midRight) = building.Through(room, opening);
            var floor = room.WorldOffset.Y;
            var head = floor + MathF.Min(opening.Height, room.Height);
            mesh.AddPolygon(Lining, At(left, floor), At(midLeft, floor), At(midLeft, head), At(left, head));
            mesh.AddPolygon(Lining, At(right, floor), At(midRight, floor), At(midRight, head), At(right, head));
            mesh.AddPolygon(Lining, At(left, head), At(midLeft, head), At(midRight, head), At(right, head));
            mesh.AddPolygon(Threshold, At(left, floor), At(midLeft, floor), At(midRight, floor), At(right, floor));
            foreach (var y in new[] { floor, head })
            {
                mesh.AddLine(At(left, y), At(midLeft, y));
                mesh.AddLine(At(right, y), At(midRight, y));
            }
        }

        // Two slopes meeting at the ridge, RoofThickness thick, reaching RoofOverhang past the outer walls on
        // every side: their tops, their undersides, and the boards along their edges.
        private static void AddPitchedRoof(MeshBuilder mesh, Building building, Building.RoofFrame frame)
        {
            var (gable, centre) = (frame.Gable, frame.Centre);
            var reach = building.WallThickness + building.RoofOverhang;
            var along = frame.HalfAlong + reach;
            var across = frame.HalfAcross + reach;
            float Under(float fromRidge) => frame.Underside(fromRidge);
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
