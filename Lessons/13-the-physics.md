# 13: The physics: rules on a fixed tick

**What you'll learn:** what a general physics engine does, and how this project's physics differs from one; why the world moves in fixed ticks, and what goes wrong when things move too far in one; how a tick of [PhysicsWorld](../World.Core/Physics/PhysicsWorld.cs) runs, rule by rule; how the walker and the droid move themselves and push things; and why rules that each make sense alone can still fight each other, with five real cases from this project.

**Built in:** the [player-world roadmap](../this-entire-solution-is-imperative-hartmanis.md)'s milestones 2 (bodies, stacking, pushing), 3 (knocking over) and 6 (water), and reworked in October 2026: slopes, edges, bouncing, floating, and each droid's own size and strength.

## Engine, or a series of effects?

**A series of rules, stepped in discrete ticks.** It isn't a general physics engine, and it was never meant to be: the decision was hand-rolled physics, stylised rather than realistic, but with the things that matter to a player (weight, friction, momentum, stacking, knocking things over). It *does* run like an engine in one way: the world moves on in **fixed ticks**, 60 a second, and every rule runs each tick. But each rule is written for one behaviour you can name: "a box on a slope steeper than its grip slides", "a box whose middle is past an edge tips over it". A general engine has no rule like either: both fall out of a few general laws.

## What a general engine does

Box2D, Bullet, PhysX, Havok, and .NET's own BEPUphysics and Jitter all work much the same way. Each tick:

1. **Integrate.** Every body has a mass, a velocity *and* a spin, and an inertia saying how hard it is to spin. Forces (gravity, pushes) change the velocities; the velocities move and turn the bodies.
2. **Broad phase.** Find the pairs of bodies whose bounding boxes overlap: a cheap test that rules out almost every pair.
3. **Narrow phase.** For each pair left, find exactly where they touch: **contact points**, each with a normal and a depth. For boxes turned any way this is real geometry (the separating axis test, or GJK).
4. **Solve.** Treat every contact as a **constraint** ("these two may not move into each other, and friction resists them sliding along each other"), and work out the impulses that satisfy all of them at once. Usually that's iterative: go round the contacts several times, each correcting a little, until the stack stops sinking.
5. **Sleep.** Bodies that have stopped are left out until something touches them.

From those few laws, stacking, sliding, tipping, rolling and bouncing all *emerge*: nobody wrote a rule for toppling. The cost is that you get what the laws give you. Stacks jitter, things tunnel through thin walls at speed, a box nudged on its edge may teeter for seconds, and tuning "make this box slide a bit further" means changing masses, frictions and solver settings until it happens.

## What this project does instead

[PhysicsWorld](../World.Core/Physics/PhysicsWorld.cs) is a list of rules, each for a behaviour the game wants:

- **Bodies never turn freely.** A [Body](../World.Core/Physics/Body.cs) is a box always square to the world's axes. That makes the narrow phase trivial (two square boxes overlap when they overlap on every axis), and it suits a world of crates, lockers and rooms.
- **Toppling is a scripted quarter turn.** When a rule says a box goes over, it swings 90° about one of its bottom edges ([AdvanceTopple](../World.Core/Physics/Body.cs#L132)), speeding up as it goes, and lands square on another side. Three rules start it (see the tick below).
- **There's no solver.** Overlapping bodies are pushed straight apart, the lighter one further; bodies closing on each other swap momentum along one axis.
- **Friction is a threshold.** A box grips with a fraction of its weight: pushed less hard than that, it doesn't move at all; harder, it slides with that much drag.
- **The walker and the droid aren't bodies.** They move themselves ([CharacterController](../World.Core/Movement/CharacterController.cs)) and push what's in their way with a capped strength and power.

Why rules: they're **predictable** (a box does the same thing every time), **tunable** (each rule has its own named number: `Friction`, `StepUp`, `BounceSpeed`), **testable** (each rule has tests saying exactly what it does), **cheap**, and **it's ours**: you can read every line that decides where a box goes. The price: every new behaviour is a new rule, and **rules interact**. That's where the bugs live (see the five cases below).

## Discrete time: the fixed tick

The world doesn't move continuously; it jumps on in **ticks** of exactly 1/60 s. The playground keeps the time the frames have taken in an accumulator (`_pending` in [Playground.cs](../Droid.Playground/Playground.cs)) and runs as many whole ticks as fit: none in a short frame, two in a long one. Why not just step by however long the frame took?

- **The same input gives the same result**, on any machine, at any frame rate. That's what lets tests drive a crate for "120 ticks" and check exactly where it ends up.
- **Rules can assume a small, steady step.** Many of them would break with a step ten times longer.

Each tick, velocities change first and then positions (*semi-implicit Euler*): `v += a·dt; x += v·dt`. Simple, and stable enough for this.

**The danger of discrete steps is tunnelling.** Something moving fast enough jumps clean over a thin obstacle between one tick and the next, because it's never seen *inside* it. The defences here:
- The walker moves across the ground in **sub-steps** of at most 10 cm (`MaxSubStep`), checking the ground at each, so a runner can't skip over a kerb.
- A falling body looks for ground **as far above its feet as it fell this tick**, so it can't drop straight through the top of a box.
- A body moving sideways checks the **strip along its leading side** that it's moving into.

## A tick, rule by rule

[PhysicsWorld.Step](../World.Core/Physics/PhysicsWorld.cs#L103), in order:

**0. Toppling bodies swing on.** A box going over is frozen in place while it turns; nothing else moves it.

**1. Forces and friction** ([ApplyForces](../World.Core/Physics/PhysicsWorld.cs#L125)). For a box resting on something:
- **Weight in water.** Standing in water, it's held up by the water it displaces: only the rest of its weight counts.
- **The slope.** On sloping ground its weight pulls it downhill (weight × the slope's sine), and it's pressed into the ground less (× the cosine).
- **Grip.** It grips with `Friction` (½) × whatever weight is left. If the pushes and the slope's pull are less than that, it stays put; if more, it slides, with the grip as drag. So on a slope steeper than about 27° (where the sine overtakes half the cosine), it slides by itself.
- **The topple test.** First, though, a push high up might tip it instead: if the push × its height beats its weight × half its width (leverage), and it would tip before friction let go, it goes over.

  A floating box has nothing to grip: the water drags on the face it pushes through the water. A box on a cliff face too steep to stand on slides off with no grip at all.

**2. Across** ([MoveAcross](../World.Core/Physics/PhysicsWorld.cs#L223)), one axis at a time, so a box pushed at an angle into a wall slides along it. It's stopped by ground rising in front of it by more than `StepUp` (15 cm), or more steeply than 45°.

**3. Against each other** ([Separate](../World.Core/Physics/PhysicsWorld.cs#L254)). Overlapping boxes are pushed apart the shortest way, each in proportion to its *lightness* (a 300 kg crate barely moves; the 4 kg box goes). If they were closing, they share momentum like a nearly dead collision (`Restitution` 0.1): a light box running into a heavy one stops, a heavy one barges a light one aside. A hard enough knock high on a box spins it over ([Knock](../World.Core/Physics/PhysicsWorld.cs#L294)): the spin the blow gives it has to carry its centre up over its far edge.

**4. Up and down, lowest first** ([Settle](../World.Core/Physics/PhysicsWorld.cs#L341)). Lowest first so a stack settles from the bottom: each box lands on one that's already settled.
- **Falling:** gravity, landing on the highest ground or box top under it.
- **Downhill:** a resting box follows the ground down a little each tick, so it keeps its grip going downhill.
- **Landing hard** (faster than 2.5 m/s), it bounces, a third as fast, away from the surface.
- **Overhanging:** once it's settled, if its middle is out past the edge of whatever holds it up, it tips over that edge ([TipIfOverhanging](../World.Core/Physics/PhysicsWorld.cs#L433)). That's how a stack collapses.

The ground itself is a service, [IGround](../World.Core/IGround.cs): the terrain, then [BuildingGround](../World.Buildings/BuildingGround.cs) adding floors, stairs, walls and ledges, then `PhysicsWorld` adding the tops of the boxes. Everything asks the same few questions (the ground below here, which way it faces, the water, a wall in the way), whatever is underneath.

## The walker, the droid and pushing

The walker and the droid are **kinematic**: they aren't pushed around by the rules above; they decide how they move, and the ground only stops them. [CharacterController.Step](../World.Core/Movement/CharacterController.cs#L103) turns, steers the velocity towards what's asked for, then moves across the ground in sub-steps, each checked by [TryMove](../World.Core/Movement/CharacterController.cs#L238): no higher than a step, not up a slope too steep, and kept out of walls. Its limits are its **Gait** ([Gait.cs](../World.Core/Movement/Gait.cs)): the walker steps up 30 cm, a Segway 7 cm, a tri-star 22 cm, and only the walker can jump or go sideways.

Pushing joins the two halves ([PushWalker](../World.Core/Physics/PhysicsWorld.cs#L540)):
- **Out of the way.** First the walker is pushed out of any box it's walked into.
- **The push.** Then whatever is in its path, within arm's reach, gets a push the way it's going. The push is capped twice: by **force** (the Segway's 120 N shifts at most about 24 kg, since grip is half the weight × 9.81) and by **power**, so the faster a box already goes, the less a push adds.
- **Held in front.** The box is also held against the pusher's front, so it doesn't slide off round its side.

## When rules fight: five real cases

Each rule here is simple and made sense alone. Each bug below came from two of them meeting. They're worth knowing because this is *the* cost of a rule-based design.

1. **The endless topple.** A box tipped over a cliff edge and landed beside it, partly below the top. Then *Settle*, looking for the highest ground under the box, found the cliff top it had just gone over and lifted it back up, still overhanging, so it tipped again, forever. Fix: when landing, ground more than a step above the box's bottom is the side of a step, not something to stand on.
2. **Hillsides as edges.** A box rests level on a hill, on its highest point, its downhill side in the air. The *overhang* rule counted any ground 30 cm below the box as "no support", so on any decent slope boxes tipped instead of sliding. Fix: support is judged along the hill's slope, not against a level.
3. **The staircase ramp.** A ramp built of 27 thin level strips is, to the walker's step rule, 27 tiny steps: the droid jerked up each one. Fix: ledges that really slope.
4. **Pushing sideways.** The push went from the pusher's middle towards the box's nearest point. On a slope, the box's weight cancelled most of the push straight uphill but none of the sideways part, so the box slid off out of the way. Fix: push the way the pusher is going, and hold the box against its front.
5. **The beached crate.** A floating crate pushed into the shallows touched the bottom and was suddenly a dry 300 kg crate: the *floating* rule and the *friction* rule had no middle ground. Fix: in water, only the weight the water doesn't hold up grips the bottom (and pulls it downhill).

The lesson: in a rule-based system, **test the rules against each other**, not only one at a time. The tests that found these ([SlideAndBounceTests](../World.Core.Tests/SlideAndBounceTests.cs), [CausewayTests](../World.Core.Tests/CausewayTests.cs), [FloatingTests](../World.Core.Tests/FloatingTests.cs)) push boxes over real edges and up real slopes. They also count the topples, because "it ends up in the right place" can hide "it tipped forty times getting there".

## Why this way, and what else could be done

- **A physics library** (BEPUphysics v2 is the strongest in .NET; Jitter2 is smaller) would give real rotation, rolling, piles of junk that settle naturally, and joints for things like a hinged gate or a pendulum. It would also give back the jitter, the tunnelling and the tuning described above, and the boxes would no longer be square to the world, which the rooms, the toppling and the drawing all assume. If the game ever needs real tumbling (a car crash, a collapsing tower of mixed shapes), that's the time.
- **A mix** is common in games: a library for the few things that need it, rules and kinematic controllers for the rest. The walker and the droid would stay as they are in any case: almost no game uses rigid-body physics for its player, because players expect the character to do exactly what they asked.

## Exercise (optional): each body its own grip

Every box grips the ground with the world's `Friction` (½). Give a body its own, so an **ice block** slides down a gentle hill and a **rubber block** holds on a steep one.

1. In [Body.cs](../World.Core/Physics/Body.cs), add `public float? Grip { get; init; }`: its own grip, or, left unset, the world's. `init` means it can be set as the body's made: `new Body(...) { Grip = 0.05f }`.
2. In [PhysicsWorld.cs](../World.Core/Physics/PhysicsWorld.cs), find where `Friction` decides how hard a resting body grips (`var grip = ...` in `ApplyForces`), and use the body's own grip when it has one: `body.Grip ?? Friction`.
3. The topple test above it also uses `Friction`: a box tips when pushed only if it would tip before it slides. Use the body's grip there too. Think about why: should an ice block tip over when it's pushed?

**Check it:** add this test to `World.Core.Tests` and run it (`dotnet test World.Core.Tests --filter Grip`):

```csharp
using Microsoft.Xna.Framework;
using System;
using World.Core.Physics;
using Xunit;

namespace World.Core.Tests
{
    public class GripTests
    {
        // How far a crate with `grip` slides down a hill `degrees` steep, set down on it and left for two seconds
        private static float Slid(float degrees, float? grip)
        {
            var hill = Terrain.FromFunction(64, 64, 1f, (x, z) => 20f - x * MathF.Tan(MathHelper.ToRadians(degrees)));
            var world = new PhysicsWorld(hill);
            var crate = world.Add(new Body("crate", new Vector3(0.8f), 25f, new Vector3(5f, hill.HeightAt(4.6f, 0f) + 0.02f, 0f)) { Grip = grip });
            for (var t = 0; t < 120; t++)
                world.Step(1f / 60f);
            return crate.Position.X - 5f;
        }

        [Fact]
        public void IceSlidesDownAGentleHillWhereACrateStaysAndRubberHoldsOnASteepOne()
        {
            Assert.True(Slid(15f, 0.05f) > 1f, "the ice stayed put");
            Assert.True(Slid(15f, null) < 0.05f, "the crate slid");
            Assert.True(Slid(35f, null) > 1f, "the crate held on the steep hill");
            Assert.True(Slid(35f, 0.9f) < 0.05f, "the rubber slid");
        }
    }
}
```

**Stretch:** make a district with an ice block on a hill (see [lesson 01](01-sharing-the-world.md) for districts and [Scenery.Crate](../World.Maps/Scenery.cs) for placing one), and give it its own `Grip`. Push it in the playground.

## Thinking questions

1. Why does the world step in fixed ticks rather than by however long each frame took?
2. Why does *Settle* go lowest first? What would happen to a stack of three crates if it went in any order?
3. A box resting on a 20° hill doesn't slide; on a 30° hill it does. Where's the line, and which two numbers decide it?
4. Bodies are pushed apart "in proportion to their lightness". What does that mean for a 300 kg crate and a 4 kg box that overlap by 10 cm?
5. Why are the walker and the droid kinematic, when the crates aren't?

<details>
<summary>Answers</summary>

1. So the same input always gives the same result, at any frame rate and on any machine, which is what makes the physics testable. And so every rule can count on a small, steady step: a long frame is two ticks, not one big jump that could carry a box through a wall.
2. A box lands on the top of whatever's under it, as that was *last placed*. Lowest first, the bottom crate settles, then the middle one lands on where the bottom one now is, then the top. In any order, the top crate might land on where the middle one *was* before it moved, and float or sink by a tick each time: a stack that jitters or creeps.
3. Where the slope's pull (weight × sine) overtakes the grip (`Friction` × weight × cosine): where the tangent of the slope equals `Friction`, ½, about 26.6°. Those two numbers are the slope and `Friction`. In the exercise, each body's own `Grip` moves the line.
4. Each moves its share of the 10 cm in proportion to one over its mass: the box 1/4 ÷ (1/4 + 1/300) of it, about 9.9 cm; the crate 1/300 ÷ (1/4 + 1/300), about 1.3 mm. A light thing gives way to a heavy one.
5. Players expect their character to do exactly what they asked, at once, and to stop dead when they let go. A body pushed around by forces drifts, slides and is shoved off course. Every other game does the same: the character moves itself, and only meets the physics where it pushes things or is knocked about.

</details>

## Further reading

- Glenn Fiedler, *Fix Your Timestep!* and *Integration Basics* (gafferongames.com): the fixed tick and the accumulator, and why.
- Erin Catto's GDC talks on Box2D (box2d.org/publications): how a real engine's contact solver works, explained by the person who wrote one.
- Christer Ericson, *Real-Time Collision Detection*: the broad and narrow phases, in depth.
- Ian Millington, *Game Physics Engine Development*: building a rigid-body engine from scratch, in C++.
