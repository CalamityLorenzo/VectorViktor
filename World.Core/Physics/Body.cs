using Microsoft.Xna.Framework;

namespace World.Core.Physics
{
    // A solid box that can be pushed, stacked and dropped: always upright and square to the world's axes
    // (a 90 degree topple, when it comes, keeps it that way - it just swaps two of its sides). Position
    // is the centre of its bottom face, the same as MeshBuilder.AddBox. Mass in kilograms; infinite
    // mass never moves, which is what something built into the world is.
    public sealed class Body
    {
        public string Name { get; }
        public Vector3 Size { get; }        // X across, Y up, Z deep, in metres
        public float Mass { get; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }

        // Stood on something (the ground or another body), and which body, if it's a body
        public bool Resting { get; internal set; }
        public Body Support { get; internal set; }

        internal Vector3 Force;   // this tick's pushes, cleared once they've been applied

        public Body(string name, Vector3 size, float mass, Vector3 position)
        {
            Name = name;
            Size = size;
            Mass = mass;
            Position = position;
        }

        public static Body Fixed(string name, Vector3 size, Vector3 position) => new Body(name, size, float.PositiveInfinity, position);

        public bool IsStatic => float.IsPositiveInfinity(Mass);
        public float InverseMass => IsStatic ? 0f : 1f / Mass;
        public float Bottom => Position.Y;
        public float Top => Position.Y + Size.Y;
        public Vector2 Centre => new Vector2(Position.X, Position.Z);
        public Vector2 Half => new Vector2(Size.X / 2f, Size.Z / 2f);   // of its footprint

        public void ApplyForce(Vector3 force)
        {
            if (!IsStatic)
                Force += force;
        }

        public bool FootprintContains(float x, float z) =>
            System.MathF.Abs(x - Position.X) < Size.X / 2f && System.MathF.Abs(z - Position.Z) < Size.Z / 2f;

        // Whether the two footprints overlap by more than `margin` each way - touching edges don't count.
        public bool FootprintOverlaps(Body other, float margin) =>
            System.MathF.Abs(other.Position.X - Position.X) < (Size.X + other.Size.X) / 2f - margin &&
            System.MathF.Abs(other.Position.Z - Position.Z) < (Size.Z + other.Size.Z) / 2f - margin;

        public override string ToString() => $"{Name} at {Position}";
    }
}
