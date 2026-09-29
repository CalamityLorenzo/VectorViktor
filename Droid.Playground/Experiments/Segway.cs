using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using System;
using World.Core.Movement;

namespace Droid.Playground
{
    // The starting droid balances on two wheels like a Segway (see GameDesign.md 3.1): to go it leans forward, and the
    // lean is what makes it speed up; to stop it leans back; and it wobbles as it settles. So the lean comes first and
    // the speed follows, where the walker simply goes.
    //
    // The model, each tick:
    //   the lean it wants  = Gain x (the speed asked for - its speed), no more than MaxLean either way
    //   its lean is pulled towards that by a spring (Stiffness), slowed by Damping: too little, and it wobbles
    //   its speed changes by Thrust x its lean
    // It can't sidestep, and turns a little slower than the walker. The walker's controller still does the moving
    // (the ground, walls, slopes): this only decides how fast it's asked to go.
    public sealed class Segway : Experiment
    {
        // Its settings, live in the panel (the defaults: see Defaults)
        public float TopSpeed, RunSpeed, Gain, MaxLean, Stiffness, Damping, Thrust, TurnRate;

        private float _speed, _lean, _leanRate;
        private float _asked;
        private readonly Trace _speedTrace = new Trace(), _askedTrace = new Trace(), _leanTrace = new Trace();

        public Segway() => Defaults();

        public override string Name => "segway";
        public override string About =>
            "Balances like a Segway: to go it leans forward, and the lean is what speeds it up; to stop it leans back, " +
            "and it wobbles as it settles. Hold forward and let go, and watch the plots. Shift goes faster.";

        private void Defaults()
        {
            TopSpeed = 2.5f;
            RunSpeed = 4.5f;
            Gain = 0.25f;       // radians of lean wanted for each m/s short of the speed asked for
            MaxLean = 0.3f;     // about 17 degrees
            Stiffness = 40f;
            Damping = 6f;
            Thrust = 12f;       // m/s² for each radian of lean
            TurnRate = 0.8f;    // of the walker's
        }

        public override void Start(Session session)
        {
            _speed = _lean = _leanRate = 0f;
            _speedTrace.Clear();
            _askedTrace.Clear();
            _leanTrace.Clear();
        }

        public override MoveInput Drive(Session session, MoveInput asked, float dt)
        {
            _asked = asked.Move.Y * (asked.Run ? RunSpeed : TopSpeed);
            var wanted = Math.Clamp(Gain * (_asked - _speed), -MaxLean, MaxLean);
            _leanRate += (Stiffness * (wanted - _lean) - Damping * _leanRate) * dt;
            _lean += _leanRate * dt;
            _speed += Thrust * _lean * dt;

            // The walker's controller goes at a fraction of its walking or running speed: ask for the fraction that's ours
            var run = MathF.Abs(_speed) > CharacterController.WalkSpeed;
            var top = CharacterController.WalkSpeed * (run ? CharacterController.RunMultiplier : 1f);
            return new MoveInput(new Vector2(0f, Math.Clamp(_speed / top, -1f, 1f)), asked.Turn * TurnRate, run);
        }

        public override void AfterTick(Session session, float dt)
        {
            // Run into something and it's stopped, whatever it was leaning for
            var actual = session.Motion.Speed;
            if (MathF.Abs(actual) < MathF.Abs(_speed) - 0.3f)
                _speed = actual;
            session.Motion.Lean = _lean;   // the rig shows this lean, not the one worked out from its speed

            _speedTrace.Add(actual);
            _askedTrace.Add(_asked);
            _leanTrace.Add(MathHelper.ToDegrees(_lean));
        }

        public override void Panel(Session session)
        {
            _askedTrace.Plot("asked (m/s)", -3f, 6f);
            _speedTrace.Plot("speed (m/s)", -3f, 6f);
            _leanTrace.Plot("lean (deg)", -25f, 25f, "{0:F1}");

            ImGui.SeparatorText("Settings");
            ImGui.SliderFloat("top speed", ref TopSpeed, 0.5f, 5f, "%.1f m/s");
            ImGui.SliderFloat("run speed", ref RunSpeed, 1f, 8f, "%.1f m/s");
            ImGui.SliderFloat("gain", ref Gain, 0.02f, 1f, "%.2f rad per m/s");
            ImGui.SliderAngle("max lean", ref MaxLean, 2f, 40f);
            ImGui.SliderFloat("stiffness", ref Stiffness, 5f, 150f, "%.0f");
            ImGui.SliderFloat("damping", ref Damping, 0f, 30f, "%.1f");
            ImGui.SliderFloat("thrust", ref Thrust, 1f, 40f, "%.0f m/s² per rad");
            ImGui.SliderFloat("turn rate", ref TurnRate, 0.2f, 1.5f, "%.2f");
            if (ImGui.Button("Defaults"))
                Defaults();
            ImGui.SameLine();
            ImGui.TextDisabled("less damping: more wobble");
        }
    }
}
