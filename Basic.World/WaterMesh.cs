using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using World.Core;

namespace Basic.World
{
    // A pool's surface (see Pool): a flat disc at its level, as wide as the pool's circle - the terrain
    // rising out of it hides whatever's past the shore - with a few ripple rings on it in white, so it
    // still shows with the colours off. A rectangular pool's is its rectangle, rippled the same way. In
    // world coordinates.
    public static class WaterMesh
    {
        private const int Sides = 48;
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
            var outline = pool.IsRectangle ? Box(1f) : Ring(pool.Radius);
            mesh.AddSolidRange(outline.Length - 2, 0);
            mesh.AddPolygon(outline);
            foreach (var ripple in Ripples)
                mesh.AddLineLoop(pool.IsRectangle ? Box(ripple) : Ring(pool.Radius * ripple));
            return mesh.Build(device);
        }
    }
}
