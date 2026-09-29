using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Core.Animation
{
    // A point fixed to a part of a rig: in that part's own space, so it moves with it.
    public readonly record struct CablePoint(string Node, Vector3 At);

    // A cable (a wire, a filament, a pipe) strung through points fixed to a rig's parts, straight between each point
    // and the next. Points on different parts are how it crosses a joint: that stretch is worked out afresh each
    // time the rig's solved (see Rig.Span), so the cable stays joined however the parts move. More points, a
    // little off the straight, make it bow and sag.
    public sealed class Cable
    {
        public string Name { get; }
        public IReadOnlyList<CablePoint> Points { get; }
        public float Radius { get; }

        public int Stretches => Points.Count - 1;

        public Cable(string name, float radius, IReadOnlyList<CablePoint> points)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ArgumentNullException.ThrowIfNull(points);
            if (points.Count < 2)
                throw new ArgumentException("A cable needs two points at least.", nameof(points));
            if (!(radius > 0f))
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "A cable must have some thickness.");
            Radius = radius;
            Points = points;
        }
    }
}
