using System;
using System.Collections.Generic;
using System.Linq;

namespace World.Core.Animation
{
    // A named, keyframed movement of some of a rig's parts (glTF's animation): a drawer sliding out, the droid
    // waving. It names parts rather than holding them, so one clip plays on any rig with those names, and the
    // parts it names that a rig hasn't got are left out: a wave played on a droid with no arm fitted moves the
    // rest of it and nothing else.
    public sealed class Clip
    {
        private readonly List<Track> _tracks = new List<Track>();
        private readonly float? _duration;

        public string Name { get; }
        public bool Loops { get; }
        public IReadOnlyList<Track> Tracks => _tracks;

        // How long it lasts: as given, or else to its last key.
        public float Duration => _duration ?? (_tracks.Count == 0 ? 0f : _tracks.Max(t => t.Duration));

        public Clip(string name, bool loops = false, float? duration = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            if (duration is { } d && !(d > 0f))
                throw new ArgumentOutOfRangeException(nameof(duration), d, "A clip's duration must be more than nothing.");
            Loops = loops;
            _duration = duration;
        }

        // The track for this part, started if it hasn't one: `clip.Track("lid").Turn(0.5f, Vector3.UnitX, -1.2f)`.
        public Track Track(string node)
        {
            var track = _tracks.Find(t => t.Node == node);
            if (track == null)
                _tracks.Add(track = new Track(node));
            return track;
        }

        // `time` into the clip, looped round if it loops, held at the end if not.
        public float Local(float time)
        {
            var duration = Duration;
            if (duration <= 0f)
                return 0f;
            if (!Loops)
                return Math.Clamp(time, 0f, duration);
            var local = time % duration;
            return local < 0f ? local + duration : local;
        }

        // Poses the rig's parts as they are `time` in, over however they were: all the way (weight 1), or only part
        // way there from it, to fade one clip into another (see Animator).
        public void Apply(Rig rig, float time, float weight = 1f)
        {
            if (weight <= 0f)
                return;
            var local = Local(time);
            foreach (var track in _tracks)
            {
                var index = rig.IndexOf(track.Node);
                if (index < 0)
                    continue;
                var node = rig[index];
                var posed = track.Sample(local, node.Pose);
                node.Pose = weight >= 1f ? posed : Pose.Lerp(node.Pose, posed, weight);
            }
        }
    }
}
