using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Core
{
    // The first world: rolling hills (a few long, low waves and some smaller, seeded bumps on top), a
    // flat-topped plateau to the north-east with sheer cliffs all round - bar one causeway ramp climbing
    // its west side - and a round basin to the south-west, dipping below WaterLevel for a lake later on.
    // Pads (see Pad) are levelled for buildings to stand on. Same seed and pads, same world.
    public static class TerrainGenerator
    {
        // Somewhere to build: the rectangle `Half` either side of `Centre` (world X, Z), levelled at the
        // hills' height at its middle, with a level apron `Apron` wide all round it to walk on, blending
        // back into the hills over `Blend` beyond that. Keep pads clear of the plateau and the basin.
        public readonly record struct Pad(Vector2 Centre, Vector2 Half, float Apron = 2f, float Blend = 6f);

        public const int Size = 128;          // cells each way
        public const float CellSize = 1f;

        public static readonly Vector2 PlateauCentre = new Vector2(30f, -30f);
        public const float PlateauRadius = 14f;
        public const float PlateauHeight = 8f;
        public const float RampLength = 26f;     // about 17 degrees up to the top, from level ground
        public const float RampHalfWidth = 3f;

        public static readonly Vector2 BasinCentre = new Vector2(-30f, 30f);
        public const float BasinRadius = 18f;
        public const float BasinDepth = 5f;
        public const float WaterLevel = -2f;

        public static Terrain Create(int seed = 1, IReadOnlyList<Pad> pads = null)
        {
            var random = new Random(seed);

            // A handful of long waves at random angles and phases, then shorter, lower ones: gentle
            // enough that nearly everything outside the plateau's cliffs stays walkable.
            var waves = new (Vector2 direction, float wavelength, float amplitude, float phase)[6];
            for (var k = 0; k < waves.Length; k++)
            {
                var angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                var wavelength = k < 3 ? 40f + 20f * (float)random.NextDouble() : 12f + 6f * (float)random.NextDouble();
                var amplitude = k < 3 ? 1.6f : 0.35f;
                waves[k] = (new Vector2(MathF.Cos(angle), MathF.Sin(angle)), wavelength, amplitude, (float)(random.NextDouble() * MathHelper.TwoPi));
            }

            float Hills(float x, float z)
            {
                var h = 0f;
                foreach (var (direction, wavelength, amplitude, phase) in waves)
                    h += amplitude * MathF.Sin((direction.X * x + direction.Y * z) * MathHelper.TwoPi / wavelength + phase);
                return h;
            }

            var levels = new List<(Pad pad, float height)>();
            foreach (var pad in pads ?? Array.Empty<Pad>())
                levels.Add((pad, Hills(pad.Centre.X, pad.Centre.Y)));

            return Terrain.FromFunction(Size, Size, CellSize, (x, z) =>
            {
                var h = Hills(x, z);

                // Levelled for building on: flat over the pad and its apron, easing back into the hills beyond
                foreach (var (pad, height) in levels)
                {
                    var outside = new Vector2(MathF.Max(0f, MathF.Abs(x - pad.Centre.X) - pad.Half.X - pad.Apron),
                                              MathF.Max(0f, MathF.Abs(z - pad.Centre.Y) - pad.Half.Y - pad.Apron)).Length();
                    if (outside < pad.Blend)
                        h = MathHelper.Lerp(height, h, SmoothStep(outside / pad.Blend));
                }

                // The basin: a smooth bowl pressed into the hills
                var basin = Vector2.Distance(new Vector2(x, z), BasinCentre) / BasinRadius;
                if (basin < 1f)
                    h -= BasinDepth * SmoothStep(1f - basin);

                // The plateau and its causeway stand up out of the hills wherever they're higher, and
                // since heights are only sampled at grid corners, their edges drop within a single cell
                h = MathF.Max(h, PlateauAt(x, z, h));
                return h;
            });
        }

        // The plateau's own height at (x, z), or below anything (so the hills win) outside it. The causeway
        // climbs from `ground` (the hills under it), so its foot meets them wherever they happen to be.
        private static float PlateauAt(float x, float z, float ground)
        {
            if (Vector2.Distance(new Vector2(x, z), PlateauCentre) <= PlateauRadius)
                return PlateauHeight;

            // The causeway runs due west from the plateau's rim, down to the ground
            var rampTop = PlateauCentre.X - PlateauRadius + 1f;   // a metre inside the rim, so it meets the top cleanly
            if (MathF.Abs(z - PlateauCentre.Y) <= RampHalfWidth && x <= rampTop && x >= rampTop - RampLength)
                return MathHelper.Lerp(ground, PlateauHeight, (x - (rampTop - RampLength)) / RampLength);

            return float.MinValue;
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);
    }
}
