using Microsoft.Xna.Framework;

namespace World.Core.Animation
{
    // Where a part of a rig is, relative to the part it hangs from (see Rig): moved, turned and sized, as glTF keeps a
    // node's. Sized first, then turned, then moved, so a part turns about its own origin, its joint.
    public readonly record struct Pose(Vector3 Translation, Quaternion Rotation, Vector3 Scale)
    {
        public static readonly Pose Identity = new Pose(Vector3.Zero, Quaternion.Identity, Vector3.One);

        public static Pose At(Vector3 translation) => new Pose(translation, Quaternion.Identity, Vector3.One);

        public static Pose At(Vector3 translation, Quaternion rotation) => new Pose(translation, rotation, Vector3.One);

        public Matrix Matrix => Matrix.CreateScale(Scale) * Matrix.CreateFromQuaternion(Rotation) * Matrix.CreateTranslation(Translation);

        // Part way from a to b: moved and sized in a straight line, turned the short way round.
        public static Pose Lerp(Pose a, Pose b, float t) => new Pose(
            Vector3.Lerp(a.Translation, b.Translation, t),
            Slerp(a.Rotation, b.Rotation, t),
            Vector3.Lerp(a.Scale, b.Scale, t));

        // A turn part way between two, the short way round: q and -q are the same turn, so if they're more than
        // half a turn apart one way, they're less than half the other.
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            if (Quaternion.Dot(a, b) < 0f)
                b = -b;
            return Quaternion.Normalize(Quaternion.Slerp(a, b, t));
        }

        // A turn of `angle` radians about `axis`, the right-hand way (as MathHelper's rotations are).
        public static Quaternion Turn(Vector3 axis, float angle) => Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), angle);
    }
}
