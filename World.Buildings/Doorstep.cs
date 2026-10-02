using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Buildings
{
    // A ramp up to a doorway out of a building (see Building.Doorsteps): its floor stands a step up from the ground
    // round it, more where the ground falls away, which is higher than wheels or tracks get up (see Gait.StepUp). So
    // each way in has a ramp, as a doorway for wheelchairs does: level across the threshold, from Inner (the room's
    // side of the wall) to Outer (the outside of it), then down Out, Length long, from the floor to the ground at its
    // Foot. It's GapHalf either side of the doorway's middle across the threshold, Wing more either side down the ramp.
    public sealed record Doorstep(Vector2 Inner, Vector2 Outer, Vector2 Out, float GapHalf, float Floor, float Foot, float Length, Color Color)
    {
        public const float Wing = 0.25f;     // how much wider than the doorway it is, either side: somewhere to line up
        public const float Slope = 0.25f;    // at most this far up for each metre along (about 14 degrees)
        public const float MinLength = 0.8f;
        public const float Depth = 0.2f;     // how far down into the ground it goes, under its foot

        public float HalfWidth => GapHalf + Wing;
        public Vector2 End => Outer + Out * Length;

        // How long a ramp `rise` high is
        public static float LengthFor(float rise) => MathF.Max(MinLength, rise / Slope);

        private const float StripHalf = 0.1f;   // at most: a ledge's ends are rounded, as wide as it is, so narrow strips side by
                                                // side, or the threshold's end would stick out over the ramp, a step up
        private const float Overlap = 0.01f;    // each strip a little wider, so there's no crack between them

        // As ground to stand on (see BuildingGround): the threshold, level, and the ramp, sloping
        public IEnumerable<Ledge> Ledges()
        {
            var across = new Vector2(-Out.Y, Out.X);
            // The threshold's strips stop their own width short of the wall's face, so their ends don't stick out over
            // the ramp; the ramp's strips' top ends, level, reach back over the threshold to meet them
            foreach (var (half, from, to, top) in new[] { (GapHalf, Inner, Outer - Out * StripHalf, Floor), (HalfWidth, Outer, End, Foot) })
            {
                var strips = (int)MathF.Ceiling(half / (2f * StripHalf));
                var stripHalf = half / (2 * strips);
                for (var k = 0; k < strips * 2; k++)
                {
                    var side = across * (-half + (2 * k + 1) * stripHalf);
                    yield return new Ledge(from + side, to + side, stripHalf + Overlap, Foot - Depth, Floor, TopAtB: top);
                }
            }
        }
    }
}
