using System;

namespace World.Core.Animation
{
    // How a keyframe is reached from the one before it (see Channel): at a steady rate, starting slowly, arriving
    // slowly, both, or not at all until its time comes. Step and Linear are glTF's STEP and LINEAR.
    public enum Ease
    {
        Linear,
        In,       // starts slowly, arrives at speed: something falling, a drawer shoved shut
        Out,      // starts at speed, arrives slowly: something thrown up, a door coming to rest
        InOut,    // both: most things moved on purpose
        Step,     // holds the value before until this key's time, then jumps: a switch, a part swapped on
    }

    public static class Easing
    {
        // How far along (0 to 1) the value is, `t` of the way (0 to 1) through the time between the keys.
        public static float Apply(this Ease ease, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return ease switch
            {
                Ease.In => t * t,
                Ease.Out => 1f - (1f - t) * (1f - t),
                Ease.InOut => t * t * (3f - 2f * t),
                Ease.Step => t < 1f ? 0f : 1f,
                _ => t,
            };
        }
    }
}
