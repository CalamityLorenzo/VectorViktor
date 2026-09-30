using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core;

namespace Maps.Coast
{
    // The coast's ground, from west to east along X. At the west end, a flat plateau for the station. From there
    // the high land rolls gently on at about the same height, until far to the east a river crosses it from north
    // to south, winding as it goes, at the bottom of a gorge with sheer sides: there's no way over it (until there's
    // a bridge), and anyone who falls in can't climb out. Past the river the land ends in a sheer cliff, running the
    // whole width of the world, down to a sandy beach; and past the beach the sea, its floor shelving away and its
    // water going on beyond the terrain's edge, so it fades into the fog rather than ending.
    //
    // There's no way down the cliff but to fall: its face is far too steep to walk (see Terrain.MaxWalkSlopeDegrees)
    // everywhere along it, and nothing leads round it. Sea level is 0. Same seed and pads, same coast.
    public static class CoastTerrain
    {
        public const int Size = 1024;         // cells each way
        public const float CellSize = 1f;

        public const float LandHeight = 30f;   // the high land, above the sea
        public const float Rolling = 3.2f;     // the most its rolling takes it above or below that (see Create)

        // The station's plateau: level at LandHeight over this rectangle, the land's rolling easing in over
        // PlateauBlend beyond it
        public static readonly Vector2 PlateauCentre = new Vector2(-400f, 0f);
        public static readonly Vector2 PlateauHalf = new Vector2(70f, 45f);
        public const float PlateauBlend = 50f;

        // The river: its middle winding RiverWander either side of RiverX (see RiverAt), its surface RiverLevel, as
        // wide as RiverHalfWidth either side of its middle and RiverDepth deep there. Its gorge's walls rise sheer
        // from the water's edge to the land by GorgeHalfWidth: some 7 to 13 metres in 3, 65 degrees and steeper,
        // too steep to walk anywhere (see Terrain.MaxWalkSlopeDegrees), and 24 metres across at the top, far more
        // than anyone can jump.
        public const float RiverX = 100f;
        public const float RiverWander = 20f;
        public const float RiverBend = 260f;   // metres from one bend to the next the same way
        public const float RiverLevel = LandHeight - 10f;
        public const float RiverHalfWidth = 9f;
        public const float RiverDepth = 2.5f;
        public const float GorgeHalfWidth = RiverHalfWidth + 3f;

        // The cliff: its top edge wanders round CliffX (see CliffAt), and its face falls to the beach, CliffFoot above
        // the sea, over CliffFace: about 27 metres down in 3, some 80 degrees. The beach slopes down to the water at
        // about ShoreX (see ShoreAt), and the sea's floor on down at SeaSlope, till it's SeaDepth deep.
        public const float CliffX = 320f;
        public const float CliffFace = 3f;
        public const float CliffFoot = 3f;
        public const float ShoreX = 372f;
        public const float SeaSlope = 0.08f;
        public const float SeaDepth = 10f;

        // Where the river's middle, the cliff's top edge and the water's edge on the beach are, along X, at `z`.
        public static float RiverAt(float z) => RiverX + RiverWander * MathF.Sin(z * MathHelper.TwoPi / RiverBend + 0.7f);

        public static float CliffAt(float z) =>
            CliffX + 10f * MathF.Sin(z * MathHelper.TwoPi / 180f + 1.3f) + 4f * MathF.Sin(z * MathHelper.TwoPi / 47f + 0.4f);

        public static float ShoreAt(float z) => ShoreX + 5f * MathF.Sin(z * MathHelper.TwoPi / 300f + 2.1f);

        public static Terrain Create(int seed = 1, IReadOnlyList<TerrainGenerator.Pad>? pads = null)
        {
            var random = new Random(seed);

            // A few long, low waves at random angles and phases, and a few shorter, lower ones on top. All together
            // they never reach Rolling either way, so the land always stands above the river.
            var waves = new (Vector2 direction, float wavelength, float amplitude, float phase)[6];
            for (var k = 0; k < waves.Length; k++)
            {
                var angle = (float)(random.NextDouble() * MathHelper.TwoPi);
                var wavelength = k < 3 ? 120f + 100f * (float)random.NextDouble() : 25f + 15f * (float)random.NextDouble();
                var amplitude = k < 3 ? 0.8f : 0.25f;
                waves[k] = (new Vector2(MathF.Cos(angle), MathF.Sin(angle)), wavelength, amplitude, (float)(random.NextDouble() * MathHelper.TwoPi));
            }

            // The high land: level on the plateau, rolling beyond it
            float Land(float x, float z)
            {
                var rolling = 0f;
                foreach (var (direction, wavelength, amplitude, phase) in waves)
                    rolling += amplitude * MathF.Sin((direction.X * x + direction.Y * z) * MathHelper.TwoPi / wavelength + phase);
                var outside = new Vector2(MathF.Max(0f, MathF.Abs(x - PlateauCentre.X) - PlateauHalf.X),
                                          MathF.Max(0f, MathF.Abs(z - PlateauCentre.Y) - PlateauHalf.Y)).Length();
                return LandHeight + rolling * SmoothStep(MathF.Min(outside / PlateauBlend, 1f));
            }

            float Ground(float x, float z)
            {
                // East of the cliff's top edge: its face, the beach, then the sea's floor
                var cliff = CliffAt(z);
                if (x > cliff)
                {
                    var foot = cliff + CliffFace;
                    if (x < foot)
                        return MathHelper.Lerp(Land(cliff, z), CliffFoot, (x - cliff) / CliffFace);
                    var shore = ShoreAt(z);
                    if (x < shore)
                        return CliffFoot * (shore - x) / (shore - foot);
                    return -MathF.Min(SeaDepth, (x - shore) * SeaSlope);
                }

                // The river: a smooth channel below its surface, and the gorge's walls straight up out of it to the land
                var fromRiver = MathF.Abs(x - RiverAt(z));
                if (fromRiver < RiverHalfWidth)
                    return RiverLevel - RiverDepth * SmoothStep(1f - fromRiver / RiverHalfWidth);
                var land = Land(x, z);
                if (fromRiver < GorgeHalfWidth)
                    return MathHelper.Lerp(RiverLevel, land, (fromRiver - RiverHalfWidth) / (GorgeHalfWidth - RiverHalfWidth));
                return land;
            }

            // The river's water fills its channel from one edge of the world to the other. The sea's reaches from
            // below the cliff out past the terrain's edge, further than can be seen from it through the fog, its
            // shore sand all the way up the beach to the cliff.
            var half = Size * CellSize / 2f;
            var river = Pool.Rectangle(new Vector2(RiverX, 0f), new Vector2(RiverWander + RiverHalfWidth + 1f, half), RiverLevel);
            var seaFrom = CliffX - 20f;
            var seaTo = half + 200f;
            var sea = Pool.Rectangle(new Vector2((seaFrom + seaTo) / 2f, 0f), new Vector2((seaTo - seaFrom) / 2f, half), 0f,
                                     shore: CliffFoot + 0.5f);

            return Terrain.FromFunction(Size, Size, CellSize, TerrainGenerator.Levelled(Ground, pads)).Flood(river, sea);
        }

        private static float SmoothStep(float t) => t * t * (3f - 2f * t);
    }
}
