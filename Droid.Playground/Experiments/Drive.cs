using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;

namespace Droid.Playground
{
    // The droid driven as the walker is (see CharacterController): it gets to the speed asked for almost at once, and
    // stops as quickly. Its wheels roll with it and it leans a little into each change of speed (see DroidMotion). The
    // baseline, to compare the others with.
    public sealed class Drive : Experiment
    {
        private readonly Trace _speed = new Trace(), _lean = new Trace();

        public override string Name => "drive";
        public override string About =>
            "Driven as the walker is: it reaches the speed asked for almost at once. Its wheels roll with it and it " +
            "leans a little into each change of speed. The baseline to compare the other experiments with.";

        public override void Start(Session session)
        {
            _speed.Clear();
            _lean.Clear();
        }

        public override void AfterTick(Session session, float dt)
        {
            _speed.Add(session.Motion.Speed);
            _lean.Add(MathHelper.ToDegrees(session.Motion.Lean));
        }

        public override void Panel(Session session)
        {
            _speed.Plot("speed (m/s)", -3f, 6f);
            _lean.Plot("lean (deg)", -25f, 25f, "{0:F1}");
        }
    }
}
