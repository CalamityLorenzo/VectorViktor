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
    // And bodies get knocked over - a quick quarter turn over an edge (see Body), for any of three reasons:
    //  - pushed high enough up that the push's leverage beats its weight's (force x height against
    //    weight x half its width), if that happens before it slides - so a tall, narrow box topples
    //    when you push it, while a cube just slides
    //  - hit by another body hard enough, and high enough, that the spin the knock gives it would lift
    //    its centre up over its edge
    //  - left overhanging, its centre out past the edge of what's holding it up: pushed off the top of
    //    another box, or over a cliff's edge, it tips over that edge. That's how a stack collapses.
    //    Ground too steep to stand on (a cliff face) doesn't hold a body up at all: it slides off it.
    // It tips only if there's room to: not into a wall, another body, or a bank of ground. While it's
    // going over it's fixed where it is, and anything stood on it stays put until it lands.
    //
    // In water (see IGround.WaterAt), a body lighter than the water it would displace floats, as deep in
    // it as its density says - a cardboard box high, a wooden crate lower - and slides about with nothing
    // but the water's drag to slow it. A denser one sinks, slowly, to the bottom, and drags through it.
    //
    // It's also the ground walkers walk on: the terrain, with the tops of the bodies on it (see IGround).
    public sealed class PhysicsWorld : IGround
    {
        public const float Gravity = WorldConstants.Gravity;
        public const float Friction = 0.5f;       // grip, as a fraction of weight
        public const float Restitution = 0.1f;    // how much of their closing speed two bodies bounce apart with
        public const float StepUp = 0.15f;        // how much higher ground a pushed box rides up onto
        public const float MaxClimb = 1f;         // the steepest slope it's pushed up, as rise over run: 45 degrees, as for walkers
        private const float Bump = 0.02f;         // a rise this small it rides over whatever its slope
        public const float SnapDown = 0.1f;       // ground dropping away under a resting body by no more than this, it follows
        public const int Iterations = 4;          // passes at separating bodies, so a chain of them settles
        public const float ToppleAcceleration = 14f;  // radians per second per second: over in about half a second
        private const float SupportDepth = 0.3f;  // ground this far below where a body rests still holds it up (it's bedded into a slope)
        private const float Overhang = 0.02f;     // how far past its support its centre must be before it tips
        private const float TipMargin = 1.2f;     // how clearly a push must favour tipping over sliding: a cube pushed at its top only slides
        private const float Reach = 0.5f;         // arm's length: a body within this of a walker's side, in their path, is one they can push
        private const float Touching = 0.01f;     // overlaps smaller than this, one on top of another, don't count
        private const float SampleSpacing = 0.5f; // how finely a body's footprint feels the ground under it
        private const float GroundReach = 1f;     // how far above its bottom a body looks for the ground under it: indoors, not up to the floor overhead
        private const float KnockedOver = 1f;     // a walker shoved this much faster (m/s) loses their footing...
        private const float StaggerTime = 0.4f;   // ...for this long (seconds)
        public const float WaterDensity = 1000f;  // kg per cubic metre
        public const float WaterDrag = 1.5f;      // how quickly water slows a body in it: its speed falls by this fraction per second, near enough
        public const float SinkDrag = 3f;         // and slows its sinking

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
                if (body.Toppling)
                    body.AdvanceTopple(dt, ToppleAcceleration);
            foreach (var body in _bodies)
                if (Moves(body))
                    ApplyForces(body, dt);
            foreach (var body in _bodies)
                if (Moves(body))
                    MoveAcross(body, dt);
            for (var k = 0; k < Iterations; k++)
                for (var i = 0; i < _bodies.Count; i++)
                    for (var j = i + 1; j < _bodies.Count; j++)
                        Separate(_bodies[i], _bodies[j]);
            Settle(dt);
            foreach (var body in _bodies)
                body.ClearForces();
        }

        private static bool Moves(Body body) => !body.IsStatic && !body.Toppling;

        private void ApplyForces(Body body, float dt)
        {
            var v = new Vector2(body.Velocity.X, body.Velocity.Z);
            var force = new Vector2(body.Force.X, body.Force.Z);

            if (body.Floating)
            {
                // Nothing to grip: pushes shove it along, and the water slows it
                v = v * MathF.Exp(-WaterDrag * dt) + force / body.Mass * dt;
                body.Velocity = new Vector3(v.X, body.Velocity.Y - Gravity * dt, v.Y);
                return;
            }

            if (OnSteepGround(body))
            {
                // Too steep to hold it (see CharacterController's slide): no grip, and down it goes
                var normal = Terrain.NormalAt(body.Position);
                var downhill = Vector2.Normalize(new Vector2(normal.X, normal.Z));
                v += (downhill * Gravity + force / body.Mass) * dt;
                body.Velocity = new Vector3(v.X, body.Velocity.Y - Gravity * dt, v.Y);
                return;
            }

            // Pushed over rather than along: only if it would tip before friction gave way
            if (body.Resting && force.LengthSquared() > 0f)
            {
                var toward = Dominant(body.Force);
                var push = MathF.Abs(Vector3.Dot(body.Force, toward));
                var height = body.ForceHeight;
                var width = body.Extent(toward);
                if (push * height > body.Mass * Gravity * width / 2f && 2f * Friction * height > width * TipMargin &&
                    TryTopple(body, Edge(body, toward), toward, 0.5f, againstGround: true))
                    return;
            }

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

            // Under water, a sinking body is dragged back too, and sinks slowly
            var vy = body.Velocity.Y - Gravity * dt;
            var water = Terrain.WaterAt(body.Position);
            if (water.HasValue && body.Bottom < water.Value)
            {
                v *= MathF.Exp(-WaterDrag * dt);
                vy *= MathF.Exp(-SinkDrag * dt);
            }
            body.Velocity = new Vector3(v.X, vy, v.Y);
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
                if (!ground.HasValue || ground.Value > body.Bottom + StepUp || (rise > Bump && rise > MathF.Abs(speed * dt) * MaxClimb) ||
                    Terrain.Obstructs(to, body.Size))
                {
                    v -= axis * speed;   // the ground rises too steeply, there's a wall (or the world ends): it stops dead
                    continue;
                }
                body.Position = to;
            }
            body.Velocity = v;
        }

        // Pushes two overlapping bodies apart (horizontally: one stood on another is Settle's business)
        // and, if they're closing on each other, exchanges their momentum.
        private void Separate(Body a, Body b)
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

                // Where on each of them the knock landed: halfway up the part of them that's side by side
                var contact = (MathF.Max(a.Bottom, b.Bottom) + MathF.Min(a.Top, b.Top)) / 2f;
                Knock(a, -n, impulse, contact - a.Bottom);
                Knock(b, n, impulse, contact - b.Bottom);
            }
        }

        // Knocked over by a blow of `impulse` (newton seconds) `atHeight` above its bottom, towards
        // `toward`: if the spin it gives it carries its centre up over its far bottom edge. A blow is too
        // sudden for friction to hold that edge still, so it spins the body about its own middle, and only
        // a blow above the middle spins it forwards at all - hit a box squarely and it's just shoved along.
        private void Knock(Body body, Vector3 toward, float impulse, float atHeight)
        {
            var height = body.Size.Y;
            if (!body.Resting || !Moves(body) || atHeight <= height / 2f)
                return;
            var width = body.Extent(toward);
            var inertia = body.Mass * (width * width + height * height) / 12f;   // a box's, about its middle
            var spin = impulse * (atHeight - height / 2f) / inertia;
            var lift = MathF.Sqrt(width * width + height * height) / 2f - height / 2f;   // its centre's climb to the top of the swing
            if (0.5f * inertia * spin * spin > body.Mass * Gravity * lift)
                TryTopple(body, Edge(body, toward), toward, spin, againstGround: true);
        }

        // Starts it toppling, if it has room to land where it's going: nothing else in the box it'll
        // become, and (tipping over its own edge) no bank of ground rising into it.
        private bool TryTopple(Body body, Vector3 pivot, Vector3 toward, float spin, bool againstGround)
        {
            var (position, size) = body.AfterTopple(pivot, toward);
            foreach (var other in _bodies)
            {
                if (other == body || other.Top - position.Y <= Touching || position.Y + size.Y - other.Bottom <= Touching)
                    continue;
                if (MathF.Abs(other.Position.X - position.X) < (other.Size.X + size.X) / 2f - Touching &&
                    MathF.Abs(other.Position.Z - position.Z) < (other.Size.Z + size.Z) / 2f - Touching)
                    return false;
            }
            if (againstGround)
            {
                // Leaving out the strip along the pivot, where it meets the ground it's tipping from
                var across = new Vector3(MathF.Abs(toward.X), 0f, MathF.Abs(toward.Z));
                var ground = GroundUnder(position + toward * 0.1f, size - across * 0.2f);
                if (!ground.HasValue || ground.Value > position.Y + size.Y / 2f)
                    return false;
            }
            body.BeginTopple(pivot, toward, spin);
            return true;
        }

        // The middle of its bottom edge on the `toward` side.
        private static Vector3 Edge(Body body, Vector3 toward) => body.Position + toward * (body.Extent(toward) / 2f);

        // Whichever of the four level directions is nearest to v's.
        private static Vector3 Dominant(Vector3 v) => MathF.Abs(v.X) >= MathF.Abs(v.Z)
            ? new Vector3(MathF.Sign(v.X) == 0 ? 1f : MathF.Sign(v.X), 0f, 0f)
            : new Vector3(0f, 0f, MathF.Sign(v.Z));

        // Falling and landing, lowest body first, so each lands on ones that have already settled.
        private void Settle(float dt)
        {
            var order = new List<Body>(_bodies);
            order.RemoveAll(b => !Moves(b));
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

                // Afloat, if the water's deep enough to hold it up clear of whatever's under it
                var water = Terrain.WaterAt(body.Position);
                if (water.HasValue && body.Density < WaterDensity)
                {
                    var afloat = water.Value - body.Size.Y * body.Density / WaterDensity;
                    if ((!support.HasValue || afloat > support.Value) && (body.Floating || now <= afloat))
                    {
                        body.Position = new Vector3(body.Position.X, afloat, body.Position.Z);
                        body.Velocity = new Vector3(body.Velocity.X, 0f, body.Velocity.Z);
                        body.Resting = true;
                        body.Support = null;
                        body.Floating = true;
                        continue;
                    }
                }
                body.Floating = false;

                if (support.HasValue && (now <= support.Value || (body.Resting && was - support.Value <= SnapDown)))
                {
                    body.Position = new Vector3(body.Position.X, support.Value, body.Position.Z);
                    body.Velocity = new Vector3(body.Velocity.X, 0f, body.Velocity.Z);
                    body.Resting = true;
                    body.Support = on;
                    if (!OnSteepGround(body))
                        TipIfOverhanging(body);
                }
                else
                {
                    body.Position = new Vector3(body.Position.X, now, body.Position.Z);
                    body.Resting = false;
                    body.Support = null;
                }
            }
        }

        // Resting on the ground where, under its middle, the ground's too steep to stand on: a cliff face.
        private bool OnSteepGround(Body body) => body.Resting && body.Support == null && !Terrain.IsWalkable(body.Position);

        // Tips it over the edge of whatever's holding it up, if its centre is out past it.
        private void TipIfOverhanging(Body body)
        {
            // What holds it up: the ground under its footprint at about the height it's resting at (and not
            // too steep to stand on), and the tops of bodies it's stood on - all taken together, as the
            // smallest box round all of it
            var min = new Vector2(float.MaxValue);
            var max = new Vector2(float.MinValue);
            var nx = Math.Max(1, (int)MathF.Ceiling(body.Size.X / SampleSpacing));
            var nz = Math.Max(1, (int)MathF.Ceiling(body.Size.Z / SampleSpacing));
            for (var i = 0; i <= nx; i++)
                for (var k = 0; k <= nz; k++)
                {
                    var point = new Vector3(body.Position.X - body.Size.X / 2f + body.Size.X * i / nx, body.Bottom, body.Position.Z - body.Size.Z / 2f + body.Size.Z * k / nz);
                    var ground = Terrain.GroundBelow(point, GroundReach);
                    if (ground.HasValue && ground.Value >= body.Bottom - SupportDepth && Terrain.IsWalkable(point))
                    {
                        min = Vector2.Min(min, new Vector2(point.X, point.Z));
                        max = Vector2.Max(max, new Vector2(point.X, point.Z));
                    }
                }
            foreach (var other in _bodies)
            {
                if (other == body || MathF.Abs(other.Top - body.Bottom) > 0.02f || !body.FootprintOverlaps(other, Touching))
                    continue;
                min = Vector2.Min(min, Vector2.Max(body.Footprint - body.Half, other.Footprint - other.Half));
                max = Vector2.Max(max, Vector2.Min(body.Footprint + body.Half, other.Footprint + other.Half));
            }
            if (min.X > max.X)
                return;   // nothing under it at all: it's falling, not tipping

            // Over the side it's furthest out past
            var centre = body.Footprint;
            var (toward, past, pivot) = (Vector3.Zero, Overhang, 0f);
            void Consider(Vector3 direction, float beyond, float edge)
            {
                if (beyond > past)
                    (toward, past, pivot) = (direction, beyond, edge);
            }
            Consider(Vector3.UnitX, centre.X - max.X, max.X);
            Consider(-Vector3.UnitX, min.X - centre.X, min.X);
            Consider(Vector3.UnitZ, centre.Y - max.Y, max.Y);
            Consider(-Vector3.UnitZ, min.Y - centre.Y, min.Y);
            if (toward == Vector3.Zero)
                return;

            var at = toward.X != 0f
                ? new Vector3(pivot, body.Bottom, body.Position.Z)
                : new Vector3(body.Position.X, body.Bottom, pivot);
            TryTopple(body, at, toward, 0f, againstGround: false);   // it's tipping out over nothing, not into the ground
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
                    var ground = Terrain.GroundBelow(point, GroundReach);
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
        // Pressing on a body (walking at it, within arm's length) pushes it with the walker's strength, which is limited both in force (too
        // heavy a body won't budge at all) and in power (the faster it's already going, the less a push
        // adds) - so the heavier the body, the slower you can shove it. A body running into the walker
        // instead shares its momentum with them: a crate sliding into you slows, and, if it hits hard
        // enough, knocks you off your feet so you slide back with it (CharacterController.Stagger).
        public void PushWalker(CharacterController walker, float height)
        {
            // Out of anything it's walked into - or that's run into it
            foreach (var body in _bodies)
            {
                if (!BesideWalker(body, walker, height))
                    continue;

                var p = new Vector2(walker.Position.X, walker.Position.Z);
                var pushed = Geometry2D.PushOutOfBox(p, body.Footprint, body.Half, CharacterController.Radius);
                if (pushed == p)
                    continue;

                var outward = new Vector3(pushed.X - p.X, 0f, pushed.Y - p.Y);
                var into = -Vector3.Normalize(outward);   // from the walker into the body
                walker.Position += outward;

                var walkerSpeed = Vector3.Dot(walker.Velocity, into);
                var bodySpeed = Vector3.Dot(body.Velocity, into);
                if (bodySpeed < 0f && Moves(body))
                {
                    // It's coming at you: both carry on together, at the speed their momentum makes
                    var together = (CharacterController.Mass * walkerSpeed + body.Mass * bodySpeed) / (CharacterController.Mass + body.Mass);
                    body.Velocity += into * (together - bodySpeed);
                    walker.Velocity += into * (together - walkerSpeed);
                    if (walkerSpeed - together > KnockedOver)
                        walker.Stagger(StaggerTime);
                }
                else if (walkerSpeed > bodySpeed)
                {
                    walker.Velocity -= into * (walkerSpeed - bodySpeed);   // you can go no faster than it gives way
                }
            }

            // Then what it's pressing on: whatever it's up against that it's trying to walk into
            var wish = walker.Wish;
            if (wish.LengthSquared() < 1e-6f)
                return;
            wish.Normalize();
            var chest = walker.Position.Y + CharacterController.PushHeight;
            var pressed = new List<(Body body, Vector3 into, float pressing)>();
            foreach (var body in _bodies)
            {
                if (!BesideWalker(body, walker, height))
                    continue;
                var p = new Vector2(walker.Position.X, walker.Position.Z);
                var gap = Vector2.Clamp(p, body.Footprint - body.Half, body.Footprint + body.Half) - p;
                var distance = gap.Length();
                if (distance < 1e-6f || distance > CharacterController.Radius + Reach)
                    continue;
                // Only what's in your path - ahead, and no further to one side than you are wide - not
                // whatever you pass within arm's reach of
                var ahead = wish.X * gap.X + wish.Z * gap.Y;
                var aside = MathF.Abs(wish.X * gap.Y - wish.Z * gap.X);
                if (ahead <= 0f || aside > CharacterController.Radius)
                    continue;
                var into = new Vector3(gap.X, 0f, gap.Y) / distance;
                pressed.Add((body, into, Vector3.Dot(wish, into)));
            }

            // Your push lands at chest height: on whichever of them is there if one is (the middle crate of
            // a stack), otherwise shared between them, at the top of each
            var atChest = pressed.FindAll(t => t.body.Bottom <= chest && chest <= t.body.Top);
            if (atChest.Count > 0)
                pressed = atChest;
            foreach (var (body, into, pressing) in pressed)
            {
                var bodySpeed = MathF.Max(Vector3.Dot(body.Velocity, into), 0.05f);
                var force = MathF.Min(CharacterController.PushForce, CharacterController.PushPower / bodySpeed) / pressed.Count;
                var at = MathHelper.Clamp(chest, body.Bottom, body.Top) - body.Bottom;
                body.ApplyForce(into * force * pressing, at);
            }
        }

        // Whether a body is at the height to be walked into: not one low enough to walk up onto (see
        // GroundBelow), nor one overhead.
        private static bool BesideWalker(Body body, CharacterController walker, float height) =>
            body.Top > walker.Position.Y + CharacterController.MaxStepUp && body.Bottom < walker.Position.Y + height;

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

        // Walls, ceilings and ladders are the ground's own business: bodies have nothing to add to them.
        public float StepUpAt(Vector3 feet, float step) => Terrain.StepUpAt(feet, step);
        public Vector3 KeepOut(Vector3 feet, float radius, float height) => Terrain.KeepOut(feet, radius, height);
        public float? CeilingAbove(Vector3 feet) => Terrain.CeilingAbove(feet);
        public bool Obstructs(Vector3 bottomCentre, Vector3 size) => Terrain.Obstructs(bottomCentre, size);
        public Vector3 ClearLine(Vector3 from, Vector3 to) => Terrain.ClearLine(from, to);
        public float? WaterAt(Vector3 point) => Terrain.WaterAt(point);

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
