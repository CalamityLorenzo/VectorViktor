using Microsoft.Xna.Framework;
using System;

namespace World.Buildings
{
    // A hinged door leaf hung in a doorway (see OpeningSpec.Door): a flat panel standing on the line from
    // its Hinge, Width long, from Bottom up Height. Shut, it lies across the gap; it swings open into the
    // room, a quarter turn, at SwingSpeed. It stops against anything in its way (see Step) rather than
    // pushing through it, and wherever it is, open, shut or part way, it's a wall (see Panel).
    public sealed class Door
    {
        public const float SwingSpeed = 2.5f;                 // radians per second: open in about 0.6 s
        public const float MaxOpen = MathHelper.PiOver2;
        public const float MaxLeafWidth = 1.4f;               // a doorway wider than this gets a pair of doors
        public const float Thickness = 0.05f;

        public Vector2 Hinge { get; }
        public Vector2 Shut { get; }          // the way the leaf runs from the hinge, shut: across the gap
        public Vector2 Into { get; }          // the way it swings: into the room
        public float Width { get; }
        public float Bottom { get; }
        public float Height { get; }
        public Color Color { get; }

        public float Angle { get; private set; }   // 0 shut, MaxOpen wide open
        public bool Opening { get; private set; }  // which way it's going (or went last)
        public bool IsShut => Angle <= 0f && !Opening;

        // Where it's going is where it is: nothing for Step to do.
        public bool AtRest => Angle == (Opening ? MaxOpen : 0f);

        public Door(Vector2 hinge, Vector2 shut, Vector2 into, float width, float bottom, float height, Color color)
        {
            Hinge = hinge;
            Shut = Vector2.Normalize(shut);
            Into = Vector2.Normalize(into);
            Width = width;
            Bottom = bottom;
            Height = height;
            Color = color;
        }

        // The way the leaf runs from the hinge, turned `angle` from shut.
        public Vector2 Direction(float angle) => Shut * MathF.Cos(angle) + Into * MathF.Sin(angle);

        public Vector2 Tip => Hinge + Direction(Angle) * Width;

        // The leaf as a wall, where it is now.
        public WallSegment Panel => new WallSegment(Hinge, Tip, Bottom, Bottom + Height);

        // Open it if it's shut or closing, shut it if it's open or opening.
        public void Toggle() => Opening = !Opening;

        // Swings it on towards open or shut, unless the leaf would end up in something: `blocked` says whether
        // a leaf from the hinge to that tip would. Then it stays where it is, until the way is clear.
        public void Step(float dt, Func<Vector2, Vector2, bool> blocked)
        {
            var target = Opening ? MaxOpen : 0f;
            if (Angle == target)
                return;
            var change = SwingSpeed * dt;
            var next = MathF.Abs(target - Angle) <= change ? target : Angle + MathF.Sign(target - Angle) * change;
            if (!blocked(Hinge, Hinge + Direction(next) * Width))
                Angle = next;
        }
    }
}
