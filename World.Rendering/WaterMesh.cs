using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core;

namespace World.Rendering
{
    // A pool's surface (see Pool): a flat disc at its level, as wide as the pool's circle - the terrain
    // rising out of it hides whatever's past the shore - with a few ripple rings on it in white, so it
    // still shows with the colours off. A rectangular pool's is its rectangle, rippled the same way, and drawn
    // in tiles no more than Tile across, its ripples in pieces as long: fog is worked out at the corners of what's
    // drawn and blended between them, so a sea drawn as one rectangle, its corners far off in the fog, would hardly
    // be fogged at all. In world coordinates.
    public static class WaterMesh
    {
        private const int Sides = 48;
        private const float Tile = 8f;   // metres
        private static readonly float[] Ripples = { 0.25f, 0.5f, 0.75f };   // as fractions of the radius

        public static Color[] Palette() => new[] { new Color(40, 90, 170) };

        public static MeshData Build(GraphicsDevice device, Pool pool)
        {
            Vector3[] Ring(float radius)
            {
                var ring = new Vector3[Sides];
                for (var k = 0; k < Sides; k++)
                {
                    var angle = k * MathHelper.TwoPi / Sides;
                    ring[k] = new Vector3(pool.Centre.X + radius * MathF.Cos(angle), pool.Level, pool.Centre.Y + radius * MathF.Sin(angle));
                }
                return ring;
            }

            Vector3[] Box(float fraction)
            {
                var h = pool.Half * fraction;
                return new[]
                {
                    new Vector3(pool.Centre.X - h.X, pool.Level, pool.Centre.Y - h.Y), new Vector3(pool.Centre.X + h.X, pool.Level, pool.Centre.Y - h.Y),
                    new Vector3(pool.Centre.X + h.X, pool.Level, pool.Centre.Y + h.Y), new Vector3(pool.Centre.X - h.X, pool.Level, pool.Centre.Y + h.Y),
                };
            }

            var mesh = new MeshBuilder();
            if (pool.IsRectangle)
            {
                var min = pool.Centre - pool.Half;
                var across = (int)MathF.Ceiling(2f * pool.Half.X / Tile);
                var down = (int)MathF.Ceiling(2f * pool.Half.Y / Tile);
                Vector3 At(int i, int j) => new Vector3(min.X + 2f * pool.Half.X * i / across, pool.Level, min.Y + 2f * pool.Half.Y * j / down);
                for (var j = 0; j < down; j++)
                    for (var i = 0; i < across; i++)
                        mesh.AddQuad(0, At(i, j), At(i + 1, j), At(i + 1, j + 1), At(i, j + 1));
                foreach (var ripple in Ripples)
                    mesh.AddLineLoop(InPieces(Box(ripple)));
            }
            else
            {
                mesh.AddPolygon(0, Ring(pool.Radius));
                foreach (var ripple in Ripples)
                    mesh.AddLineLoop(Ring(pool.Radius * ripple));
            }
            return mesh.Build(device);
        }

        // The same loop, with points added along each side so no piece of it is longer than Tile.
        private static Vector3[] InPieces(Vector3[] loop)
        {
            var points = new List<Vector3>();
            for (var k = 0; k < loop.Length; k++)
            {
                var a = loop[k];
                var b = loop[(k + 1) % loop.Length];
                var pieces = Math.Max(1, (int)MathF.Ceiling(Vector3.Distance(a, b) / Tile));
                for (var p = 0; p < pieces; p++)
                    points.Add(Vector3.Lerp(a, b, (float)p / pieces));
            }
            return points.ToArray();
        }
    }
}
