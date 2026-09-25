using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using World.Core;

namespace Basic.World
{
    // A pool's surface (see Pool): a flat disc at its level, as wide as the pool's circle - the terrain
    // rising out of it hides whatever's past the shore - with a few ripple rings on it in white, so it
    // still shows with the colours off. In world coordinates.
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

            var mesh = new MeshBuilder();
            mesh.AddSolidRange(Sides - 2, 0);
            mesh.AddPolygon(Ring(pool.Radius));
            foreach (var ripple in Ripples)
                mesh.AddLineLoop(Ring(pool.Radius * ripple));
            return mesh.Build(device);
        }
    }
}
