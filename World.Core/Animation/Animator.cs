using System;
using System.Collections.Generic;

namespace World.Core.Animation
{
    // A clip being played: how far in it is, how fast it's going, and how much of it shows (its weight, fading in
    // or out). A clip that doesn't loop stops at its end and holds its last pose - a drawer stays open - until
    // it's stopped.
    public sealed class ClipPlayer
    {
        private float _fadeRate;       // weight per second: up while fading in, down while fading out
        private bool _stopping;

        public Clip Clip { get; }
        public float Time { get; set; }
        public float Speed { get; set; } = 1f;
        public float Weight { get; private set; }

        public bool Finished => !Clip.Loops && Time >= Clip.Duration;

        // Faded all the way out after being stopped: the animator drops it.
        public bool Gone => _stopping && Weight <= 0f;

        internal ClipPlayer(Clip clip, float fadeIn)
        {
            Clip = clip;
            FadeTo(1f, fadeIn);
        }

        internal void Stop(float fadeOut)
        {
            _stopping = true;
            FadeTo(0f, fadeOut);
        }

        private void FadeTo(float target, float seconds)
        {
            if (seconds <= 0f)
            {
                Weight = target;
                _fadeRate = 0f;
            }
            else
                _fadeRate = (target - Weight) / seconds;
        }

        internal void Step(float dt)
        {
            Time += dt * Speed;
            if (!Clip.Loops)
                Time = Math.Clamp(Time, 0f, Clip.Duration);
            if (_fadeRate != 0f)
            {
                Weight = Math.Clamp(Weight + _fadeRate * dt, 0f, 1f);
                if (Weight is 0f or 1f)
                    _fadeRate = 0f;
            }
        }
    }

    // Plays clips on a rig, as layers: each clip played is laid over the ones before it, and only on the parts
    // it keys. So a droid can roll along (its wheels turned in code), wave (a clip on its arm) and look round
    // (another on its head) at once; and a clip faded in over another blends from one to the other.
    public sealed class Animator
    {
        private readonly List<ClipPlayer> _playing = new List<ClipPlayer>();

        public Rig Rig { get; }
        public IReadOnlyList<ClipPlayer> Playing => _playing;

        public Animator(Rig rig) => Rig = rig ?? throw new ArgumentNullException(nameof(rig));

        // Starts a clip from its beginning, on top of what's playing, faded in over `fadeIn` seconds.
        public ClipPlayer Play(Clip clip, float fadeIn = 0f)
        {
            ArgumentNullException.ThrowIfNull(clip);
            var player = new ClipPlayer(clip, fadeIn);
            _playing.Add(player);
            return player;
        }

        // Fades a clip out over `fadeOut` seconds (at once, for 0), then drops it: its parts go back to what's under it.
        public void Stop(ClipPlayer player, float fadeOut = 0f)
        {
            if (_playing.Contains(player))
                player.Stop(fadeOut);
        }

        // Every clip playing with this name.
        public void Stop(string clipName, float fadeOut = 0f)
        {
            foreach (var player in _playing)
                if (player.Clip.Name == clipName)
                    player.Stop(fadeOut);
        }

        public bool IsPlaying(string clipName) => _playing.Exists(p => p.Clip.Name == clipName && !p.Gone);

        public void Step(float dt)
        {
            foreach (var player in _playing)
                player.Step(dt);
            _playing.RemoveAll(p => p.Gone);
        }

        // The rig back at rest, then every clip's pose laid on it in the order they were played. After this, set
        // what's worked out in code, then Solve the rig.
        public void Apply()
        {
            Rig.Reset();
            foreach (var player in _playing)
                player.Clip.Apply(Rig, player.Time, player.Weight);
        }
    }
}
