using Microsoft.Xna.Framework;
using System;

namespace World.Core.Physics
{
    // A solid box that can be pushed, stacked, dropped and knocked over: always square to the world's axes.
    // Knocked over, it swings a quarter turn about one of its bottom edges (or the edge of whatever it was
    // overhanging) and lands square again, just on a different side - so Size is where it is now (X
    // across, Y up, Z deep), while OriginalSize and Rotation say how the shape it was made as has been
    // turned to get there. Position is the centre of its bottom face, the same as MeshBuilder.AddBox.
    // Mass in kilograms; infinite mass never moves, which is what something built into the world is.
    public sealed class Body
    {
        public string Name { get; }
        public Vector3 OriginalSize { get; }
        public Vector3 Size { get; private set; }
        public Matrix Rotation { get; private set; } = Matrix.Identity;   // from OriginalSize's axes to Size's
        public float Mass { get; }
        public Vector3 Position { get; set; }
        public Vector3 Velocity { get; set; }

        // Stood on something (the ground or another body), and which body, if it's a body
        public bool Resting { get; internal set; }
        public Body? Support { get; internal set; }

        // This tick's pushes, cleared once they've been applied, and how high up them they came on average
        internal Vector3 Force;
        private float _forceMoment, _forceTotal;
        internal float ForceHeight => _forceTotal > 0f ? _forceMoment / _forceTotal : 0f;

        // Mid-topple: turning `Angle` so far, about a line through Pivot along Axis
        private (Vector3 pivot, Vector3 axis, Vector3 toward, float angle, float spin)? _topple;
        public bool Toppling => _topple.HasValue;

        public Body(string name, Vector3 size, float mass, Vector3 position)
        {
            Name = name;
            OriginalSize = size;
            Size = size;
            Mass = mass;
            Position = position;
        }

        public static Body Fixed(string name, Vector3 size, Vector3 position) => new Body(name, size, float.PositiveInfinity, position);

        public bool IsStatic => float.IsPositiveInfinity(Mass);
        public float InverseMass => IsStatic || Toppling ? 0f : 1f / Mass;   // mid-topple, nothing shifts it
        // Held up by water (see PhysicsWorld): it's lighter than the water it would displace.
        public bool Floating { get; internal set; }

        // Kilograms per cubic metre: less than water's, and it floats.
        public float Density => Mass / (Size.X * Size.Y * Size.Z);

        public float Bottom => Position.Y;
        public float Top => Position.Y + Size.Y;
        public Vector3 Centre => Position + Vector3.Up * (Size.Y / 2f);
        public Vector2 Footprint => new Vector2(Position.X, Position.Z);
        public Vector2 Half => new Vector2(Size.X / 2f, Size.Z / 2f);   // of its footprint

        // How far it reaches along a level direction (one of the four), from one side to the other.
        public float Extent(Vector3 direction) => MathF.Abs(direction.X) > 0.5f ? Size.X : Size.Z;

        // A push `atHeight` above its bottom.
        public void ApplyForce(Vector3 force, float atHeight = 0f)
        {
            if (IsStatic)
                return;
            Force += force;
            var size = force.Length();
            _forceMoment += size * atHeight;
            _forceTotal += size;
        }

        internal void ClearForces()
        {
            Force = Vector3.Zero;
            _forceMoment = _forceTotal = 0f;
        }

        // Where the shape it was made as goes in the world: for drawing it. Mid-topple, part way round.
        public Matrix Pose
        {
            get
            {
                var pose = Matrix.CreateTranslation(-Vector3.Up * (OriginalSize.Y / 2f)) * Rotation * Matrix.CreateTranslation(Centre);
                if (_topple is { } t)
                    pose *= AboutPivot(t.pivot, t.axis, t.angle);
                return pose;
            }
        }

        public bool FootprintContains(float x, float z) =>
            MathF.Abs(x - Position.X) < Size.X / 2f && MathF.Abs(z - Position.Z) < Size.Z / 2f;

        // Whether the two footprints overlap by more than `margin` each way - touching edges don't count.
        public bool FootprintOverlaps(Body other, float margin) =>
            MathF.Abs(other.Position.X - Position.X) < (Size.X + other.Size.X) / 2f - margin &&
            MathF.Abs(other.Position.Z - Position.Z) < (Size.Z + other.Size.Z) / 2f - margin;

        // The box it would be after a quarter turn over `toward` (one of the four level directions),
        // about the level line through `pivot` at right angles to that: (bottom centre, size).
        public (Vector3 position, Vector3 size) AfterTopple(Vector3 pivot, Vector3 toward)
        {
            var turn = AboutPivot(pivot, TurnAxis(toward), MathHelper.PiOver2);
            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            var h = new Vector3(Size.X / 2f, Size.Y, Size.Z / 2f);
            for (var i = 0; i < 8; i++)
            {
                var corner = Position + new Vector3((i & 1) == 0 ? -h.X : h.X, (i & 2) == 0 ? 0f : h.Y, (i & 4) == 0 ? -h.Z : h.Z);
                var turned = Vector3.Transform(corner, turn);
                min = Vector3.Min(min, turned);
                max = Vector3.Max(max, turned);
            }
            return (new Vector3((min.X + max.X) / 2f, min.Y, (min.Z + max.Z) / 2f), max - min);
        }

        internal void BeginTopple(Vector3 pivot, Vector3 toward, float spin)
        {
            _topple = (pivot, TurnAxis(toward), toward, 0f, spin);
            Velocity = Vector3.Zero;
            Resting = false;
            Support = null;
        }

        // Swings it on round; once it's all the way over, lands it square on its new side. Its turn
        // speeds up as it goes over, as a falling box's does.
        internal void AdvanceTopple(float dt, float acceleration)
        {
            var (pivot, axis, toward, angle, spin) = _topple ?? throw new InvalidOperationException("It isn't toppling.");
            spin += acceleration * dt;
            angle += spin * dt;
            if (angle < MathHelper.PiOver2)
            {
                _topple = (pivot, axis, toward, angle, spin);
                return;
            }

            var (position, size) = AfterTopple(pivot, toward);
            Position = position;
            Size = size;
            Rotation = Snap(Rotation * Matrix.CreateFromAxisAngle(axis, MathHelper.PiOver2));
            _topple = null;
        }

        // The axis a quarter turn about which tips the top of the box over towards `toward`.
        private static Vector3 TurnAxis(Vector3 toward) => Vector3.Cross(Vector3.Up, toward);

        private static Matrix AboutPivot(Vector3 pivot, Vector3 axis, float angle) =>
            Matrix.CreateTranslation(-pivot) * Matrix.CreateFromAxisAngle(axis, angle) * Matrix.CreateTranslation(pivot);

        // Quarter turns only ever make 0s and 1s: round off the float dust so turns never drift.
        private static Matrix Snap(Matrix m)
        {
            static float S(float v) => MathF.Round(v);
            return new Matrix(S(m.M11), S(m.M12), S(m.M13), 0f, S(m.M21), S(m.M22), S(m.M23), 0f, S(m.M31), S(m.M32), S(m.M33), 0f, 0f, 0f, 0f, 1f);
        }

        public override string ToString() => $"{Name} at {Position}";
    }
}
