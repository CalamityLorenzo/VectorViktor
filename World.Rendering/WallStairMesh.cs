using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Buildings;

namespace World.Rendering
{
    // A WallStair as drawn: treads and risers, a side face down its open (room) side, and its sloping underside;
    // the landings' tops and undersides. In the room's own coordinates, like the stair.
    public static class WallStairMesh
    {
        public const int Tread = 0, Riser = 1, Side = 2, Underside = 3;
        public const int PaletteSize = 4;

        public static Color[] Palette(Color tread, Color riser, Color side, Color underside) => new[] { tread, riser, side, underside };

        // Keyed by the stair's shape (see WallStair.Key), so identical stairs share one mesh.
        public static MeshSource Source(WallStair stair, Color[] palette) => new MeshSource(stair.Key, d => Build(d, stair), palette);

        public static MeshData Build(GraphicsDevice device, WallStair stair)
        {
            // One draw range per colour, however many steps there are (see MeshBuilder)
            var mesh = new MeshBuilder();
            foreach (var flight in stair.Flights)
                AddFlight(mesh, stair, flight);
            foreach (var landing in stair.Landings)
                AddLanding(mesh, stair, landing);
            return mesh.Build(device);
        }

        private static void AddFlight(MeshBuilder mesh, WallStair stair, WallStair.Flight flight)
        {
            var (along, length, first) = flight.Run();
            var w = stair.Width;

            // `a` along the wall from the flight's Start, `lateral` in from the wall
            Vector3 P(float a, float lateral, float y) => At(flight.Start + along * a + flight.Inward * lateral, y);

            // The underside runs parallel to the line of the nosings, the stair's Waist below it, and stops at the floor
            var slope = stair.Rise / flight.Going;
            float Under(float a) => MathF.Max(0f, flight.Base + stair.Rise + (a - first) * slope - WallStair.Waist);
            var floorEnd = MathHelper.Clamp(first + (WallStair.Waist - stair.Rise - flight.Base) / slope, first, length);   // where it leaves the floor

            for (var i = 0; i < flight.Steps; i++)
            {
                var a0 = first + i * flight.Going;
                var a1 = a0 + flight.Going;
                var low = flight.Base + i * stair.Rise;
                var high = low + stair.Rise;

                mesh.AddQuad(Tread, P(a0, 0f, high), P(a1, 0f, high), P(a1, w, high), P(a0, w, high));
                mesh.AddQuad(Riser, P(a0, 0f, low), P(a0, w, low), P(a0, w, high), P(a0, 0f, high));

                // This step's slice of the open side, down to the underside - with a kink where that meets the floor
                var side = new List<Vector3> { P(a0, w, Under(a0)) };
                if (floorEnd > a0 && floorEnd < a1)
                    side.Add(P(floorEnd, w, 0f));
                side.Add(P(a1, w, Under(a1)));
                side.Add(P(a1, w, high));
                side.Add(P(a0, w, high));
                mesh.AddPolygon(Side, side.ToArray());

                mesh.AddLine(P(a0, 0f, low), P(a0, w, low));
                mesh.AddLine(P(a0, 0f, high), P(a0, w, high));
                foreach (var lateral in new[] { 0f, w })
                {
                    mesh.AddLine(P(a0, lateral, low), P(a0, lateral, high));
                    mesh.AddLine(P(a0, lateral, high), P(a1, lateral, high));
                }
            }
            var top = flight.Base + flight.Steps * stair.Rise;
            mesh.AddLine(P(length, 0f, top), P(length, w, top));

            if (floorEnd < length)
            {
                mesh.AddQuad(Underside, P(floorEnd, 0f, Under(floorEnd)), P(length, 0f, Under(length)), P(length, w, Under(length)), P(floorEnd, w, Under(floorEnd)));
                foreach (var lateral in new[] { 0f, w })
                    mesh.AddLine(P(floorEnd, lateral, Under(floorEnd)), P(length, lateral, Under(length)));
            }
            if (floorEnd > first)
                mesh.AddLine(P(first, w, 0f), P(floorEnd, w, 0f));
        }

        // A slab filling the corner, level with the top of the flight before it. Its open corner is just
        // the point where the two flights' sides meet, so it has no side faces of its own - only a top and
        // an underside, which is level with where both flights' undersides reach it.
        private static void AddLanding(MeshBuilder mesh, WallStair stair, WallStair.Landing landing)
        {
            var top = landing.Height;
            var under = top + stair.Rise - WallStair.Waist;
            Vector3[] Kite(float y) => new[] { At(landing.FootBefore, y), At(landing.Corner, y), At(landing.FootAfter, y), At(landing.Inner, y) };

            mesh.AddPolygon(Tread, Kite(top));
            mesh.AddPolygon(Underside, Kite(under));

            foreach (var y in new[] { top, under })
            {
                mesh.AddLine(At(landing.FootBefore, y), At(landing.Corner, y));
                mesh.AddLine(At(landing.Corner, y), At(landing.FootAfter, y));
            }
            mesh.AddLine(At(landing.Inner, under), At(landing.FootBefore, under));
            mesh.AddLine(At(landing.Inner, under), At(landing.FootAfter, under));
        }

        private static Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);
    }
}
