using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Core.Animation;
using World.Core.Characters;
using World.Core.Movement;

namespace Droid.Playground
{
    // A cut scene (AnimationPlan.md step 6): the droid fits itself a second arm (see FittingScene). It plays on the world's
    // clock, so pausing, stepping and slowing time work on it too. While it plays, what you ask the droid to do is left
    // alone and the picture is the scene's: the free camera, then the head camera, then the drone. K skips to the end;
    // "restart it" plays it again, the arm back in the droid's hand.
    public sealed class FitArm : Experiment
    {
        private Animator _animator = null!;
        private Timeline _scene = null!;
        private readonly List<string> _log = new List<string>();

        public override string Name => "fitarm";
        public override string About =>
            "A cut scene: the droid fits itself a second arm, keyed in code. It's seen from a free cinematic camera, then " +
            "through the droid's own head camera, then from its drone. Your driving waits till it's over; K skips to the end.";

        public override void Start(Session session)
        {
            _log.Clear();
            _animator = new Animator(session.Rig);
            FittingScene.Hold(session.Rig);
            _scene = FittingScene.Build(session.Rig, _animator, session.Placement);
            _scene.On(FittingScene.AttachEvent, () => _log.Add($"{_scene.Time:F2} s: {FittingScene.AttachEvent}"));
        }

        public override MoveInput Drive(Session session, MoveInput asked, float dt) => _scene.Playing ? MoveInput.None : asked;

        public override void AfterTick(Session session, float dt)
        {
            if (_scene.Playing)
                _scene.Step(dt);
            else
                _animator.Step(dt);   // the scene's over: its animator is ours again
        }

        // The scene's clips, over the droid's movement
        public override void Pose(Session session, Rig rig) => _animator.Lay();

        public override (CameraView view, CameraMode mode)? Camera(Session session)
        {
            if (!_scene.Playing)
                return null;
            var name = _scene.Camera.CameraAt(_scene.Time);
            var view = _scene.Camera.ViewAt(_scene.Time, camera => camera switch
            {
                FittingScene.HeadCamera => Head(session),
                FittingScene.DroneCamera => Drone(session),
                _ => null,
            });
            if (view is not { } seen)
                return null;
            return (seen, name == FittingScene.HeadCamera ? CameraMode.Head : name == FittingScene.DroneCamera ? CameraMode.Drone : CameraMode.Free);
        }

        private static CameraView Head(Session session)
        {
            var (eye, forward, up) = DroidRig.CameraView(session.Rig);
            return new CameraView(eye, forward, up);
        }

        private static CameraView Drone(Session session)
        {
            var eye = session.Player.Drone.Position;
            return new CameraView(eye, Vector3.Normalize(session.Feet + Vector3.Up * 1.0f - eye), Vector3.Up);
        }

        public override void Skip(Session session) => _scene.Skip();

        public override void Panel(Session session)
        {
            ImGui.Text(_scene.Playing ? $"playing: {_scene.Time:F2} of {_scene.Duration:F0} s" : "over: the arm's fitted");
            ImGui.Text($"camera: {_scene.Camera.CameraAt(_scene.Time) ?? "-"}");
            ImGui.ProgressBar(_scene.Time / _scene.Duration);
            if (ImGui.Button("skip to the end (K)"))
                _scene.Skip();
            ImGui.SeparatorText("Clips playing");
            foreach (var player in _animator.Playing)
                ImGui.Text($"{player.Clip.Name}: {player.Time:F2} s, weight {player.Weight:F2}");
            ImGui.SeparatorText("Events");
            foreach (var line in _log)
                ImGui.Text(line);
        }
    }
}
