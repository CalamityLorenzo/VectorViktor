using System;
using System.Collections.Generic;
using System.Linq;
using World.Core.Animation;
using World.Core.Movement;

namespace Droid.Playground
{
    // Something being tried out in the playground. The harness runs the world in fixed ticks, and calls an experiment
    // at each point it might want a say:
    //
    //   Start      once, when it's picked (again when it's restarted)
    //   Drive      each tick: what the droid is told to do, from what the player asked for
    //   AfterTick  each tick, after the world has moved on
    //   Pose       each frame, after the droid's rig has been posed from its movement: anything more on the rig
    //   Camera     each frame, after the rig's solved: a view to show instead of the harness's own (a cut scene's), or null
    //   Skip       when K is pressed: jump to the end of whatever it's playing
    //   Panel      each frame: its own Dear ImGui panel, with its live values (sliders straight onto its fields)
    //
    // Each has a default that does nothing (Drive passes the asking straight on), so an experiment says only what's
    // different. To add one: a class here, and a line in Experiments.All.
    public abstract class Experiment
    {
        public abstract string Name { get; }
        public abstract string About { get; }

        public virtual void Start(Session session) { }
        public virtual MoveInput Drive(Session session, MoveInput asked, float dt) => asked;
        public virtual void AfterTick(Session session, float dt) { }
        public virtual void Pose(Session session, Rig rig) { }
        public virtual (CameraView view, CameraMode mode)? Camera(Session session) => null;
        public virtual void Skip(Session session) { }
        public virtual void Panel(Session session) { }
    }

    public static class Experiments
    {
        // Every experiment, by the name it's picked with (on the command line, or in the playground's panel)
        public static readonly IReadOnlyList<(string name, Func<Experiment> make)> All = new (string, Func<Experiment>)[]
        {
            ("drive", () => new Drive()),
            ("segway", () => new Segway()),
            ("fitarm", () => new FitArm()),
        };

        public const string Default = "drive";

        public static bool Has(string name) => All.Any(e => e.name == name);

        public static Experiment Make(string name) =>
            All.FirstOrDefault(e => e.name == name).make?.Invoke() ?? throw new ArgumentException($"No experiment called '{name}'.", nameof(name));
    }
}
