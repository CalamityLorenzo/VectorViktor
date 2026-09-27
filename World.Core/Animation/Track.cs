using Microsoft.Xna.Framework;
using System;

namespace World.Core.Animation
{
    // What a clip does to one part of a rig, named: where it is, how it's turned and its size, each keyed on its own
    // (glTF's translation, rotation and scale channels). A channel with no keys leaves that much of the part as it
    // was, so a track that only turns a lid leaves it where it's hinged.
    //
    // Turns go the short way round between keys, so a key is needed at least every half turn; something that
    // spins for ever (a wheel) is better turned in code (see DroidRig.Roll).
    public sealed class Track
    {
        public string Node { get; }
        public Channel<Vector3> Translation { get; } = new Channel<Vector3>(Vector3.Lerp);
        public Channel<Quaternion> Rotation { get; } = new Channel<Quaternion>(Pose.Slerp);
        public Channel<Vector3> Scale { get; } = new Channel<Vector3>(Vector3.Lerp);

        public Track(string node) => Node = node ?? throw new ArgumentNullException(nameof(node));

        public float Duration => MathF.Max(Translation.Duration, MathF.Max(Rotation.Duration, Scale.Duration));

        // Keys, each returning the track so they can be strung together: the value it has reached by `time`.
        public Track Move(float time, Vector3 to, Ease ease = Ease.InOut) { Translation.Add(time, to, ease); return this; }
        public Track Turn(float time, Quaternion to, Ease ease = Ease.InOut) { Rotation.Add(time, to, ease); return this; }
        public Track Turn(float time, Vector3 axis, float angle, Ease ease = Ease.InOut) => Turn(time, Pose.Turn(axis, angle), ease);
        public Track Size(float time, Vector3 to, Ease ease = Ease.InOut) { Scale.Add(time, to, ease); return this; }
        public Track Size(float time, float to, Ease ease = Ease.InOut) => Size(time, new Vector3(to), ease);

        // The part's pose `time` in, over `under` (what it was before): the keyed channels in place of its own.
        public Pose Sample(float time, Pose under) => new Pose(
            Translation.IsEmpty ? under.Translation : Translation.Sample(time),
            Rotation.IsEmpty ? under.Rotation : Rotation.Sample(time),
            Scale.IsEmpty ? under.Scale : Scale.Sample(time));
    }
}
