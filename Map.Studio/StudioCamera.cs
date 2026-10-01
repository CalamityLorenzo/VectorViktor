using Microsoft.Xna.Framework;
using System;
using World.Maps;

namespace MapStudio
{
    public enum ViewKind { Free, Overhead, Front, Side, ThreeQuarter }

    // The studio's camera: it looks at a point (Target) from a direction (Yaw round from north, Pitch up from level) and
    // a Distance away. Orbiting turns it round the point; looking about turns it round the eye; flying moves both.
    //
    // The snap views are where it's put to see a map as a plan or an elevation: overhead (straight down, north up),
    // front (looking north), side (looking west), each Flat - an orthographic projection, where things don't get smaller
    // further off, so distances can be read off it like a drawing - and three-quarter, a perspective view from above
    // and to one side. Snapping to one moves there smoothly (Move), rather than cutting, so you can see where you've
    // gone to. A flat view shows as much across as the perspective one did at the target, so changing between them,
    // at the end of a move, doesn't jump.
    public sealed class StudioCamera
    {
        public const float MoveTime = 0.4f;   // seconds, snapping to a view
        public const float FlatBack = 600f;   // how far back a flat view's eye stands, so nothing's behind it
        public const float MinDistance = 2f, MaxDistance = 900f;

        // Where it's looking from, and how
        public readonly record struct Pose(Vector3 Target, float Yaw, float Pitch, float Distance);

        private Pose _from, _to;
        private float _moved = 1f;   // how far through a move, 0 to 1 (1: not moving)
        private bool _flatAtEnd;

        public Vector3 Target { get; set; }
        public float Yaw { get; set; }
        public float Pitch { get; set; } = -0.5f;
        public float Distance { get; set; } = 30f;
        public bool Flat { get; private set; }
        public ViewKind Kind { get; private set; } = ViewKind.Free;
        public bool Moving => _moved < 1f;

        private static readonly float HalfView = MathF.Tan(MathHelper.ToRadians(WorldRenderer.FieldOfView) / 2f);

        // Which way it's looking: yaw 0 north (-Z), turning clockwise seen from above, as the walker's does
        public Vector3 Forward => new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), -MathF.Cos(Yaw) * MathF.Cos(Pitch));
        public Vector3 Right => new Vector3(MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));

        // Up on the screen: from the yaw, so it's still well defined looking straight down (where "the world's up" isn't)
        public Vector3 Up => Vector3.Cross(Right, Forward);

        public Vector3 Eye => Target - Forward * (Flat ? FlatBack : Distance);

        // How much of the world the picture shows from top to bottom at the target: what a flat view shows everywhere
        public float Height => 2f * Distance * HalfView;

        public Matrix View => Matrix.CreateLookAt(Eye, Target, Up);

        public Matrix Projection(float aspect, float far) => Flat
            ? Matrix.CreateOrthographic(Height * aspect, Height, 1f, FlatBack + far)
            : Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(WorldRenderer.FieldOfView), aspect, 0.1f, far);

        // ---- Snapping and moving

        // Moves smoothly to look at `target` as `kind` does; Free keeps the direction, and just leaves a flat view.
        public void SnapTo(ViewKind kind, Vector3 target)
        {
            var (yaw, pitch, flat) = kind switch
            {
                ViewKind.Overhead => (0f, -MathHelper.PiOver2, true),
                ViewKind.Front => (0f, 0f, true),
                ViewKind.Side => (-MathHelper.PiOver2, 0f, true),
                ViewKind.ThreeQuarter => (MathHelper.PiOver4, MathHelper.ToRadians(-35f), false),
                _ => (Yaw, MathF.Max(Pitch, -1.45f), false),
            };
            MoveTo(new Pose(target, yaw, pitch, Distance), flat);
            Kind = kind;
        }

        // Moves smoothly to `pose`, flat at the end of it or not.
        public void MoveTo(Pose pose, bool flat)
        {
            _from = new Pose(Target, Yaw, Pitch, Distance);
            // The short way round: from 350 degrees to 10 is 20 degrees on, not 340 back
            _to = pose with { Yaw = Yaw + MathHelper.WrapAngle(pose.Yaw - Yaw) };
            _moved = 0f;
            _flatAtEnd = flat;
            Flat = false;   // a move is seen in perspective; a flat view only begins when it's arrived
        }

        public void Step(float dt)
        {
            if (!Moving)
                return;
            _moved = MathF.Min(1f, _moved + dt / MoveTime);
            var t = _moved * _moved * (3f - 2f * _moved);   // eased: slow away, slow in
            Target = Vector3.Lerp(_from.Target, _to.Target, t);
            Yaw = MathHelper.Lerp(_from.Yaw, _to.Yaw, t);
            // Looking straight down in perspective, the picture spins about its middle if the yaw changes: hold it just off
            Pitch = MathHelper.Clamp(MathHelper.Lerp(_from.Pitch, _to.Pitch, t), -MathHelper.PiOver2 + (_moved < 1f ? 0.01f : 0f), MathHelper.PiOver2);
            // Distance changes by the same ratio each moment, not the same amount, so a big zoom doesn't rush at the end
            Distance = _from.Distance * MathF.Pow(_to.Distance / _from.Distance, t);
            if (_moved >= 1f)
                Flat = _flatAtEnd;
        }

        // ---- Moving it by hand (each stops a move, and turning leaves a flat view: a plan turned isn't a plan any more)

        // Round the target: the eye moves, the target stays.
        public void Orbit(float yaw, float pitch)
        {
            StopMoving();
            LeaveFlat();
            Yaw = MathHelper.WrapAngle(Yaw + yaw);
            Pitch = MathHelper.Clamp(Pitch + pitch, -MathHelper.PiOver2 + 0.01f, MathHelper.PiOver2 - 0.01f);
        }

        // Round the eye: looking about, the target moves.
        public void Look(float yaw, float pitch)
        {
            var eye = Eye;
            Orbit(yaw, pitch);
            Target = eye + Forward * Distance;
        }

        // The target and the eye moved together, in metres: `across` to the right, `ahead` forward along the ground (up the
        // screen, in a view from straight above), `up` straight up.
        public void Fly(float across, float ahead, float up)
        {
            StopMoving();
            var level = Flat && Kind != ViewKind.Overhead ? Vector3.Zero : Vector3.Normalize(new Vector3(MathF.Sin(Yaw), 0f, -MathF.Cos(Yaw)));
            var screenUp = Flat && Kind != ViewKind.Overhead ? Vector3.Up : Vector3.Zero;   // front and side: up the screen is up
            Target += Right * across + level * ahead + (screenUp * ahead) + Vector3.Up * up;
        }

        // Nearer (below 1) or further (above 1).
        public void Zoom(float by)
        {
            StopMoving();
            Distance = Math.Clamp(Distance * by, MinDistance, MaxDistance);
        }

        private void StopMoving()
        {
            if (!Moving)
                return;
            _moved = 1f;
            Flat = false;
        }

        private void LeaveFlat()
        {
            Flat = false;
            Kind = ViewKind.Free;
        }
    }
}
