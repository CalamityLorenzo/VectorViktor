using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Core
{
    // The first world: rolling hills (a few long, low waves and some smaller, seeded bumps on top), a
    // flat-topped plateau to the north-east with sheer cliffs all round - bar one causeway ramp climbing
    // its west side - a round basin to the south-west with a lake in it, below WaterLevel, and a small
    // pond, shallow enough round its edge to wade in and deep enough in the middle to swim. Pads (see Pad)
    // are levelled for buildings to stand on. All that is within HomeRadius of the middle; beyond it the
    // country grows, a few very long, tall waves rising out of the hills over HomeFade, out to the edge of
    // a world a kilometre across. Same seed and pads, same world.
    public static class TerrainGenerator
    {
        // Somewhere to build: the rectangle `Half` either side of `Centre` (world X, Z), levelled at the
        // hills' height at its middle, with a level apron `Apron` wide all round it to walk on, blending
        // back into the hills over `Blend` beyond that. Keep pads clear of the plateau and the basin.
        //
        // `LevelWith` levels it with the hills somewhere else instead, and `Raise` puts it that much higher
        // (or, below zero, lower): so a row of pads can be made level with each other, a house's garden a
        // little above the road in front of it, or a swimming pool dug into the garden. Later pads are
        // levelled over earlier ones, and their blends run into what those made.
        public readonly record struct Pad(Vector2 Centre, Vector2 Half, float Apron = 2f, float Blend = 6f, float Raise = 0f, Vector2? LevelWith = null);

        public const int Size = 1024;         // cells each way
        public const float CellSize = 1f;

        public const float HomeRadius = 90f;   // everything above lies within this of the middle
        public const float HomeFade = 150f;    // and the far country's waves rise out of the hills over this

        public static readonly Vector2 PlateauCentre = new Vector2(30f, -30f);
        public const float PlateauRadius = 14f;
        public const float PlateauHeight = 8f;
        public const float RampLength = 26f;     // about 17 degrees up to the top, from level ground
        public const float RampHalfWidth = 3f;

        public static readonly Vector2 BasinCentre = new Vector2(-30f, 30f);
        public const float BasinRadius = 18f;
        public const float BasinDepth = 5f;
        public const float WaterLevel = -2f;      // the lake's surface
        private const float BankHeight = 0.5f;    // the least the basin's rim stands above it

        public static readonly Vector2 PondCentre = new Vector2(-6f, 14f);
        public const float PondRadius = 6f;
        public const float PondDepth = 1.9f;      // the hollow, below the hills round it
        public const float PondFreeboard = 0.3f;  // how far below the hills at its middle its surface is

        public static Terrain Create(int seed = 1, IReadOnlyList<Pad>? pads = null)
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

            // Drawn after the home waves, so adding them left those as they were
            var far = new (Vector2 direction, float wavelength, float amplitude, float phase)[3];
            for (var k = 0; k < far.Length; k++)
            {
                var angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                far[k] = (new Vector2(MathF.Cos(angle), MathF.Sin(angle)), 250f + 150f * (float)random.NextDouble(),
                          9f + 4f * (float)random.NextDouble(), (float)(random.NextDouble() * MathHelper.TwoPi));
            }

            float Waves((Vector2 direction, float wavelength, float amplitude, float phase)[] set, float x, float z)
            {
                var h = 0f;
                foreach (var (direction, wavelength, amplitude, phase) in set)
                    h += amplitude * MathF.Sin((direction.X * x + direction.Y * z) * MathHelper.TwoPi / wavelength + phase);
                return h;
            }

            float Hills(float x, float z)
            {
                var h = Waves(waves, x, z);
                var away = (MathF.Sqrt(x * x + z * z) - HomeRadius) / HomeFade;
                if (away > 0f)
                    h += Waves(far, x, z) * SmoothStep(MathF.Min(away, 1f));
                return h;
            }

            var pondRim = Hills(PondCentre.X, PondCentre.Y);

            var levels = new List<(Pad pad, float height)>();
            foreach (var pad in pads ?? Array.Empty<Pad>())
            {
                var at = pad.LevelWith ?? pad.Centre;
                levels.Add((pad, Hills(at.X, at.Y) + pad.Raise));
            }

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
                    else if (pad.Blend <= 0f && outside <= 0f)
                        h = height;   // sheer-sided: a hole dug straight down
                }

                // The basin: a smooth bowl pressed into the hills, and a bank round its rim, raised only where
                // the hills are lower than the lake, so its water can't spill out there
                var basin = Vector2.Distance(new Vector2(x, z), BasinCentre) / BasinRadius;
                if (basin < 1f)
                    h -= BasinDepth * SmoothStep(1f - basin);
                if (basin > 0.85f && basin < 1.3f)
                {
                    var bank = basin < 1f ? SmoothStep((basin - 0.85f) / 0.15f) : SmoothStep((1.3f - basin) / 0.3f);
                    h += MathF.Max(0f, WaterLevel + BankHeight - h) * bank;
                }

                // The pond: a smaller bowl with a level rim all round, so the water can't spill out on a low
                // side, and a band half its radius wide beyond that blending back into the hills
                var pond = Vector2.Distance(new Vector2(x, z), PondCentre) / PondRadius;
                if (pond < 1f)
                    h = pondRim - PondDepth * SmoothStep(1f - pond);
                else if (pond < 1.5f)
                    h = MathHelper.Lerp(pondRim, h, SmoothStep((pond - 1f) / 0.5f));

                // The plateau and its causeway stand up out of the hills wherever they're higher, and
                // since heights are only sampled at grid corners, their edges drop within a single cell
                h = MathF.Max(h, PlateauAt(x, z, h));
                return h;
            }).Flood(new Pool(BasinCentre, BasinRadius, WaterLevel), new Pool(PondCentre, PondRadius, pondRim - PondFreeboard));
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
