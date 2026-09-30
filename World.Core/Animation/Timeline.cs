using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Core.Animation
{
    // Where a camera is and which way it looks: the eye to see the world from.
    public readonly record struct CameraView(Vector3 Eye, Vector3 Forward, Vector3 Up);

    // The cameras a cut scene is seen through, one after another: a cut to a camera at a time. A camera is either one
    // in the world, named - the drone, a mirror, the droid's head camera - which the game says where it is (see
    // ViewAt), or the free cinematic camera (Free), which isn't in the world at all and goes where its keys put it.
    public sealed class CameraTrack
    {
        public const string Free = "free";

        private readonly List<(float at, string camera)> _cuts = new List<(float, string)>();

        // The free camera's keys: where it is, and the point it looks at.
        public Channel<Vector3> Eye { get; } = new Channel<Vector3>(Vector3.Lerp);
        public Channel<Vector3> Target { get; } = new Channel<Vector3>(Vector3.Lerp);

        public IReadOnlyList<(float at, string camera)> Cuts => _cuts;

        // From `at`, the picture's from this camera (Free, or a world camera's name). Cuts go in in time order.
        public CameraTrack Cut(float at, string camera)
        {
            ArgumentNullException.ThrowIfNull(camera);
            if (_cuts.Count > 0 && at < _cuts[^1].at)
                throw new ArgumentException($"Cuts go in in time order: {at} comes before the last cut's {_cuts[^1].at}.", nameof(at));
            _cuts.Add((at, camera));
            return this;
        }

        // A key for the free camera: by `at`, it's at `eye`, looking at `target`.
        public CameraTrack Key(float at, Vector3 eye, Vector3 target, Ease ease = Ease.InOut)
        {
            Eye.Add(at, eye, ease);
            Target.Add(at, target, ease);
            return this;
        }

        // The camera cut to at `time`, or null before the first cut.
        public string? CameraAt(float time)
        {
            string? camera = null;
            foreach (var (at, name) in _cuts)
                if (at <= time)
                    camera = name;
            return camera;
        }

        // The view `time` in: the free camera's from its keys, a world camera's from `world` (null if there's no such camera
        // just now, or no cut yet: then the game keeps whatever view it had).
        public CameraView? ViewAt(float time, Func<string, CameraView?> world)
        {
            var camera = CameraAt(time);
            if (camera == null)
                return null;
            if (camera != Free)
                return world(camera);
            if (Eye.IsEmpty)
                return null;
            var eye = Eye.Sample(time);
            var forward = Target.Sample(time) - eye;
            return new CameraView(eye, forward.LengthSquared() > 1e-8f ? Vector3.Normalize(forward) : Vector3.Forward, Vector3.Up);
        }
    }

    // A cut scene: clips on the rigs in it (each through its Animator), things to do at set times, what to do when a clip
    // passes one of its events, and a camera track, all on one clock. While it plays, the player's input is left alone
    // (see Playing), and Skip jumps to how it ends.
    //
    // It steps the animators it plays clips on, so the game mustn't step them as well while it's playing (it still poses
    // the rigs from them, with Animator.Apply, each frame as usual).
    public sealed class Timeline
    {
        public const float Tick = 1f / 60f;   // how finely Skip runs it through to the end

        private readonly List<(float at, Action run)> _cues = new List<(float, Action)>();   // in time order, then the order given
        private readonly List<Animator> _animators = new List<Animator>();
        private readonly Dictionary<string, List<Action<ClipPlayer>>> _on = new Dictionary<string, List<Action<ClipPlayer>>>();
        private int _next;   // the first cue not yet run

        public float Duration { get; }
        public float Time { get; private set; }
        public CameraTrack Camera { get; } = new CameraTrack();

        public bool Playing => Time < Duration;
        public bool Finished => !Playing;

        public Timeline(float duration)
        {
            if (!(duration > 0f))
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "A cut scene must last some time.");
            Duration = duration;
        }

        // At `at` seconds in, does `run`. Cues at the same time run in the order they were given.
        public Timeline At(float at, Action run)
        {
            ArgumentNullException.ThrowIfNull(run);
            if (!(at >= 0f))
                throw new ArgumentOutOfRangeException(nameof(at), at, "A cue's time must be zero or more.");
            var index = _cues.FindIndex(c => c.at > at);
            _cues.Insert(index < 0 ? _cues.Count : index, (at, run));
            return this;
        }

        // At `at`, starts a clip on an animator (faded in over `fadeIn`).
        public Timeline Play(float at, Animator animator, Clip clip, float fadeIn = 0f)
        {
            Use(animator);
            return At(at, () => animator.Play(clip, fadeIn));
        }

        // At `at`, fades out every clip playing on an animator with this name.
        public Timeline Stop(float at, Animator animator, string clipName, float fadeOut = 0f)
        {
            Use(animator);
            return At(at, () => animator.Stop(clipName, fadeOut));
        }

        // Whenever a clip on one of its animators passes an event with this name (see Clip.Event), does `run`.
        public Timeline On(string clipEvent, Action<ClipPlayer> run)
        {
            if (!_on.TryGetValue(clipEvent, out var list))
                _on[clipEvent] = list = new List<Action<ClipPlayer>>();
            list.Add(run);
            return this;
        }

        public Timeline On(string clipEvent, Action run) => On(clipEvent, _ => run());

        private void Use(Animator animator)
        {
            ArgumentNullException.ThrowIfNull(animator);
            if (!_animators.Contains(animator))
                _animators.Add(animator);
        }

        // Moves it on by dt: its animators up to each cue whose time comes in it, and the cue, then on to the end of the
        // step, so a clip started partway through a step plays only the rest of it. Whatever the clips' events call for is
        // done as they pass them.
        public void Step(float dt)
        {
            if (Finished)
                return;
            var to = MathF.Min(Time + dt, Duration);
            while (_next < _cues.Count && _cues[_next].at <= to)
            {
                Advance(MathF.Max(_cues[_next].at, Time));
                _cues[_next++].run();
            }
            Advance(to);
            if (Finished)   // the last cues, at its very end
                while (_next < _cues.Count)
                    _cues[_next++].run();
        }

        private void Advance(float to)
        {
            if (to <= Time)
                return;
            foreach (var animator in _animators)
            {
                animator.Step(to - Time);
                foreach (var (player, e) in animator.Fired)
                    if (_on.TryGetValue(e.Name, out var runs))
                        foreach (var run in runs)
                            run(player);
            }
            Time = to;
        }

        // Straight to the end, everything done on the way that would have been, so it ends as it would have.
        public void Skip()
        {
            while (Playing)
                Step(Tick);
        }
    }
}
