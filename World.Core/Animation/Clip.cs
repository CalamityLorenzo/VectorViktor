using System;
using System.Collections.Generic;
using System.Linq;

namespace World.Core.Animation
{
    // A named moment in a clip (glTF has none; engines call them notifies or markers): "attach arm", "clunk". The
    // Animator reports each one as its clip passes it (see Animator.Fired), for the game to act on.
    public readonly record struct ClipEvent(float Time, string Name);

    // A named, keyframed movement of some of a rig's parts (glTF's animation): a drawer sliding out, the droid
    // waving. It names parts rather than holding them, so one clip plays on any rig with those names, and the
    // parts it names that a rig hasn't got are left out: a wave played on a droid with no arm fitted moves the
    // rest of it and nothing else.
    public sealed class Clip
    {
        private readonly List<Track> _tracks = new List<Track>();
        private readonly List<ClipEvent> _events = new List<ClipEvent>();
        private readonly float? _duration;

        public string Name { get; }
        public bool Loops { get; }
        public IReadOnlyList<Track> Tracks => _tracks;
        public IReadOnlyList<ClipEvent> Events => _events;

        // How long it lasts: as given, or else to its last key or event.
        public float Duration => _duration ?? MathF.Max(_tracks.Count == 0 ? 0f : _tracks.Max(t => t.Duration),
                                                        _events.Count == 0 ? 0f : _events[^1].Time);

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

        // A named moment, `time` in: `clip.Event(2f, "attach arm")`. Events can go in in any order.
        public Clip Event(float time, string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (!float.IsFinite(time) || time < 0f)
                throw new ArgumentOutOfRangeException(nameof(time), time, "An event's time must be zero or more.");
            var at = _events.FindIndex(e => e.Time > time);
            _events.Insert(at < 0 ? _events.Count : at, new ClipEvent(time, name));
            return this;
        }

        // The events passed going from `from` to `to` (a player's time, not looped round: see ClipPlayer.Time), those at
        // `to` included and at `from` not, each as often as it's passed, in the order they're passed.
        public void Passed(float from, float to, List<ClipEvent> into)
        {
            if (to <= from || _events.Count == 0)
                return;
            var duration = Duration;
            var start = into.Count;
            foreach (var e in _events)
            {
                if (!Loops || duration <= 0f)
                {
                    if (e.Time > from && e.Time <= to)
                        into.Add(e);
                    continue;
                }
                // Once each time round: at e.Time, e.Time + duration, and so on
                for (var at = e.Time + (MathF.Floor((from - e.Time) / duration) + 1f) * duration; at <= to; at += duration)
                    into.Add(e with { Time = at });
            }
            if (Loops)
                into.Sort(start, into.Count - start, Comparer<ClipEvent>.Create((a, b) => a.Time.CompareTo(b.Time)));
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
