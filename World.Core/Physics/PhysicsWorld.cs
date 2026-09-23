using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core.Movement;

namespace World.Core.Physics
{
    // The bodies in the world, and the rules they move by: hand-rolled, not realistic, but with the
    // things that matter to a player - weight, friction, momentum, stacking.
    //
    // Each tick, for every body that can move:
    //  1. Forces and friction. A body stood on something grips it with Friction x its weight: pushed less
    //     hard than that it stays put; pushed harder it slides, friction dragging on it; left alone it
    //     slows to a stop. What it grips is what it's stood on, so a box on a moving box is carried along
    //     (up to the point friction can hold it - stop the one underneath dead, and the top one slides off).
    //  2. Across, one axis at a time, stopped by ground rising in front of it by more than StepUp, or more
    //     steeply than MaxClimb - a box is pushed up a slope, but not up a cliff (not even a little at a
    //     time, which is how it would otherwise creep up one).
    //  3. Against each other: overlapping bodies are pushed apart in proportion to their lightness, and
    //     those closing on each other exchange momentum (almost dead, Restitution) - so a light box
    //     running into a heavy one stops, and a heavy one barges a light one out of its way.
    //  4. Up and down, lowest first: gravity, landing on the ground or on the top of a body underneath. A
    //     body already resting follows the ground down by up to SnapDown, as a walker does, so it keeps
    //     its grip going downhill instead of skipping down the slope.
    //
    // It's also the ground walkers walk on: the terrain, with the tops of the bodies on it (see IGround).
    public sealed class PhysicsWorld : IGround
    {
        public const float Gravity = 9.81f;
        public const float Friction = 0.5f;       // grip, as a fraction of weight
        public const float Restitution = 0.1f;    // how much of their closing speed two bodies bounce apart with
        public const float StepUp = 0.15f;        // how much higher ground a pushed box rides up onto
        public const float MaxClimb = 1f;         // the steepest slope it's pushed up, as rise over run: 45 degrees, as for walkers
        private const float Bump = 0.02f;         // a rise this small it rides over whatever its slope
        public const float SnapDown = 0.1f;       // ground dropping away under a resting body by no more than this, it follows
        public const int Iterations = 4;          // passes at separating bodies, so a chain of them settles
        private const float Touching = 0.01f;     // overlaps smaller than this, one on top of another, don't count
        private const float SampleSpacing = 0.5f; // how finely a body's footprint feels the ground under it
        private const float KnockedOver = 1f;     // a walker shoved this much faster (m/s) loses their footing...
        private const float StaggerTime = 0.4f;   // ...for this long (seconds)

        private readonly List<Body> _bodies = new List<Body>();

        public IGround Terrain { get; }
        public IReadOnlyList<Body> Bodies => _bodies;

        public PhysicsWorld(IGround terrain) => Terrain = terrain;

        public Body Add(Body body)
        {
            _bodies.Add(body);
            return body;
        }

        public void Step(float dt)
        {
            foreach (var body in _bodies)
                if (!body.IsStatic)
                    ApplyForces(body, dt);
            foreach (var body in _bodies)
                if (!body.IsStatic)
                    MoveAcross(body, dt);
            for (var k = 0; k < Iterations; k++)
                for (var i = 0; i < _bodies.Count; i++)
                    for (var j = i + 1; j < _bodies.Count; j++)
                        Separate(_bodies[i], _bodies[j]);
            Settle(dt);
            foreach (var body in _bodies)
                body.Force = Vector3.Zero;
        }

        private static void ApplyForces(Body body, float dt)
        {
            var v = new Vector2(body.Velocity.X, body.Velocity.Z);
            var force = new Vector2(body.Force.X, body.Force.Z);

            if (body.Resting)
            {
                // Friction works on how it's moving relative to what it's stood on
                var under = body.Support == null ? Vector2.Zero : new Vector2(body.Support.Velocity.X, body.Support.Velocity.Z);
                var slip = v - under;
                var grip = Friction * body.Mass * Gravity;   // the most friction can do, in newtons
                if (slip.LengthSquared() < 1e-6f)
                {
                    slip = force.Length() <= grip ? Vector2.Zero : (force - Vector2.Normalize(force) * grip) / body.Mass * dt;
                }
                else
                {
                    var along = Vector2.Normalize(slip);
                    var next = slip + (force - along * grip) / body.Mass * dt;
                    // Friction brings it to a stop; it doesn't then push it back the other way
                    slip = Vector2.Dot(next, along) < 0f && force.Length() <= grip ? Vector2.Zero : next;
                }
                v = under + slip;
            }
            else
            {
                v += force / body.Mass * dt;
            }

            body.Velocity = new Vector3(v.X, body.Velocity.Y - Gravity * dt, v.Y);
        }

        private void MoveAcross(Body body, float dt)
        {
            var v = body.Velocity;
            foreach (var axis in new[] { Vector3.UnitX, Vector3.UnitZ })
            {
                var speed = Vector3.Dot(v, axis);
                if (speed == 0f)
                    continue;
                var to = body.Position + axis * speed * dt;
                var ground = GroundUnder(to, body.Size);
                var here = GroundUnder(body.Position, body.Size) ?? body.Bottom;
                var rise = ground.HasValue ? ground.Value - here : 0f;
                if (!ground.HasValue || ground.Value > body.Bottom + StepUp || (rise > Bump && rise > MathF.Abs(speed * dt) * MaxClimb))
                {
                    v -= axis * speed;   // the ground rises too steeply (or the world ends): it stops dead
                    continue;
                }
                body.Position = to;
            }
            body.Velocity = v;
        }

        // Pushes two overlapping bodies apart (horizontally: one stood on another is Settle's business)
        // and, if they're closing on each other, exchanges their momentum.
        private static void Separate(Body a, Body b)
        {
            var inverse = a.InverseMass + b.InverseMass;
            if (inverse == 0f)
                return;
            if (a.Top - b.Bottom <= Touching || b.Top - a.Bottom <= Touching)
                return;   // one's above the other

            var dx = b.Position.X - a.Position.X;
            var dz = b.Position.Z - a.Position.Z;
            var overlapX = (a.Size.X + b.Size.X) / 2f - MathF.Abs(dx);
            var overlapZ = (a.Size.Z + b.Size.Z) / 2f - MathF.Abs(dz);
            if (overlapX <= 0f || overlapZ <= 0f)
                return;

            // Apart the shortest way, a towards b along n
            var (n, overlap) = overlapX < overlapZ
                ? (new Vector3(MathF.Sign(dx) == 0 ? 1f : MathF.Sign(dx), 0f, 0f), overlapX)
                : (new Vector3(0f, 0f, MathF.Sign(dz) == 0 ? 1f : MathF.Sign(dz)), overlapZ);
            a.Position -= n * overlap * a.InverseMass / inverse;
            b.Position += n * overlap * b.InverseMass / inverse;

            var closing = Vector3.Dot(b.Velocity - a.Velocity, n);
            if (closing < 0f)
            {
                var impulse = -(1f + Restitution) * closing / inverse;
                a.Velocity -= n * impulse * a.InverseMass;
                b.Velocity += n * impulse * b.InverseMass;
            }
        }

        // Falling and landing, lowest body first, so each lands on ones that have already settled.
        private void Settle(float dt)
        {
            var order = new List<Body>(_bodies);
            order.RemoveAll(b => b.IsStatic);
            order.Sort((p, q) => p.Bottom.CompareTo(q.Bottom));

            foreach (var body in order)
            {
                var was = body.Bottom;
                var now = was + body.Velocity.Y * dt;

                // The highest thing under it: the ground, or the top of a body it was above
                var support = GroundUnder(body.Position, body.Size);
                Body on = null;
                foreach (var other in _bodies)
                {
                    if (other == body || other.Top > was + 0.05f || !body.FootprintOverlaps(other, Touching))
                        continue;
                    if (!support.HasValue || other.Top > support.Value)
                    {
                        support = other.Top;
                        on = other;
                    }
                }

                if (support.HasValue && (now <= support.Value || (body.Resting && was - support.Value <= SnapDown)))
                {
                    body.Position = new Vector3(body.Position.X, support.Value, body.Position.Z);
                    body.Velocity = new Vector3(body.Velocity.X, 0f, body.Velocity.Z);
                    body.Resting = true;
                    body.Support = on;
                }
                else
                {
                    body.Position = new Vector3(body.Position.X, now, body.Position.Z);
                    body.Resting = false;
                    body.Support = null;
                }
            }
        }

        // The highest ground anywhere under a footprint of this size here, or null if any of it is off the world.
        private float? GroundUnder(Vector3 position, Vector3 size)
        {
            var nx = Math.Max(1, (int)MathF.Ceiling(size.X / SampleSpacing));
            var nz = Math.Max(1, (int)MathF.Ceiling(size.Z / SampleSpacing));
            var highest = float.MinValue;
            for (var i = 0; i <= nx; i++)
                for (var k = 0; k <= nz; k++)
                {
                    var point = new Vector3(position.X - size.X / 2f + size.X * i / nx, position.Y, position.Z - size.Z / 2f + size.Z * k / nz);
                    var ground = Terrain.GroundBelow(point, float.MaxValue);
                    if (!ground.HasValue)
                        return null;
                    highest = MathF.Max(highest, ground.Value);
                }
            return highest;
        }

        // The body whose top a walker with its feet here is stood on, if any.
        public Body BodyUnder(Vector3 feet)
        {
            Body best = null;
            foreach (var body in _bodies)
                if (body.FootprintContains(feet.X, feet.Z) && MathF.Abs(body.Top - feet.Y) < 0.02f && (best == null || body.Top > best.Top))
                    best = body;
            return best;
        }

        // Keeps a walker (a circle CharacterController.Radius across, `height` tall) out of the bodies, and
        // lets it push them. Anything low enough to step onto, the walker walks on instead (see GroundBelow).
        // Pressing into a body pushes it with the walker's strength, which is limited both in force (too
        // heavy a body won't budge at all) and in power (the faster it's already going, the less a push
        // adds) - so the heavier the body, the slower you can shove it. A body running into the walker
        // instead shares its momentum with them: a crate sliding into you slows, and, if it hits hard
        // enough, knocks you off your feet so you slide back with it (CharacterController.Stagger).
        public void PushWalker(CharacterController walker, float height)
        {
            foreach (var body in _bodies)
            {
                var feet = walker.Position.Y;
                if (body.Top <= feet + CharacterController.MaxStepUp || body.Bottom >= feet + height)
                    continue;   // one you walk on, or pass under

                var p = new Vector2(walker.Position.X, walker.Position.Z);
                var pushed = PushOutOfBox(p, body.Centre, body.Half, CharacterController.Radius);
                if (pushed == p)
                    continue;

                var outward = new Vector3(pushed.X - p.X, 0f, pushed.Y - p.Y);
                var into = -Vector3.Normalize(outward);   // from the walker into the body
                walker.Position += outward;

                var walkerSpeed = Vector3.Dot(walker.Velocity, into);
                var bodySpeed = Vector3.Dot(body.Velocity, into);
                if (bodySpeed < 0f && !body.IsStatic)
                {
                    // It's coming at you: both carry on together, at the speed their momentum makes
                    var together = (CharacterController.Mass * walkerSpeed + body.Mass * bodySpeed) / (CharacterController.Mass + body.Mass);
                    body.Velocity += into * (together - bodySpeed);
                    walker.Velocity += into * (together - walkerSpeed);
                    if (walkerSpeed - together > KnockedOver)
                        walker.Stagger(StaggerTime);
                    continue;
                }
                if (walkerSpeed > bodySpeed)
                    walker.Velocity -= into * (walkerSpeed - bodySpeed);   // you can go no faster than it gives way

                var wish = walker.Wish;
                if (wish.LengthSquared() > 1e-6f)
                {
                    var pressing = Vector3.Dot(Vector3.Normalize(wish), into);
                    if (pressing > 0f)
                    {
                        var force = MathF.Min(CharacterController.PushForce, CharacterController.PushPower / MathF.Max(bodySpeed, 0.05f));
                        body.ApplyForce(into * force * pressing);
                    }
                }
            }
        }

        // A circle of `radius` at p pushed clear of a box (centre, half-extent) by the shortest route; from
        // inside the box, out through whichever side is nearest. The same rule as RoomSpec.PushOutOfBox.
        public static Vector2 PushOutOfBox(Vector2 p, Vector2 centre, Vector2 half, float radius)
        {
            var d = p - centre;
            var nearest = Vector2.Clamp(d, -half, half);
            var gap = d - nearest;
            var distance = gap.Length();
            if (distance >= radius)
                return p;
            if (distance > 1e-6f)
                return centre + nearest + gap / distance * radius;

            var toX = half.X - MathF.Abs(d.X);
            var toZ = half.Y - MathF.Abs(d.Y);
            if (toX < toZ)
                return new Vector2(centre.X + (d.X < 0f ? -1f : 1f) * (half.X + radius), p.Y);
            return new Vector2(p.X, centre.Y + (d.Y < 0f ? -1f : 1f) * (half.Y + radius));
        }

        // As ground: the terrain, raised wherever a body's top is under the feet and within reach of them.
        public float? GroundBelow(Vector3 feet, float reach)
        {
            var ground = Terrain.GroundBelow(feet, reach);
            if (!ground.HasValue)
                return null;
            var best = ground.Value;
            foreach (var body in _bodies)
                if (body.Top > best && body.Top <= feet.Y + reach && body.FootprintContains(feet.X, feet.Z))
                    best = body.Top;
            return best;
        }

        // A body's top is level, and always good to stand on.
        public Vector3 NormalAt(Vector3 feet) => TopWithinReach(feet) != null ? Vector3.Up : Terrain.NormalAt(feet);
        public bool IsWalkable(Vector3 feet) => TopWithinReach(feet) != null || Terrain.IsWalkable(feet);

        // The highest body whose top is the ground a walker here would be asking about (within a step and
        // a stride of their feet, the furthest the controller ever looks) and above the terrain.
        private Body TopWithinReach(Vector3 feet)
        {
            var reach = CharacterController.MaxStepUp + CharacterController.Radius;
            var terrain = Terrain.GroundBelow(feet, reach) ?? float.MinValue;
            Body best = null;
            foreach (var body in _bodies)
                if (body.Top > terrain && body.Top <= feet.Y + reach && body.FootprintContains(feet.X, feet.Z) && (best == null || body.Top > best.Top))
                    best = body;
            return best;
        }
    }
}
