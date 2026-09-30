# 07: Fitting limbs, and clip events

**What you'll learn:** how a rig grows and loses parts while the game runs (sockets, limbs, `Attach` and `Detach`); how to move a part from one parent to another without it jumping, and the matrix algebra behind it; how to keep a tree in parents-first order when its shape changes; how a clip tells the game "now" (events), including when it loops; and how fading a clip in blends a part from wherever it happens to be.

**Built in:** [AnimationPlan.md](../AnimationPlan.md) step 5: `Attach`, `Detach`, `Reparent` and `SetRest` in [Rig](../World.Core/Animation/Rig.cs), `Clip.Event` in [Clip](../World.Core/Animation/Clip.cs), `Fired` and `Lay` in [Animator](../World.Core/Animation/Animator.cs), and `ShoulderLeft` and `JointedArm` in [DroidRig](../World.Core/Characters/DroidRig.cs). Lesson 04 comes first.

Run it: `Droid.Playground.exe fitarm` plays the scene that uses all of this (lesson 08). The tests are in [SceneTests](../World.Core.Tests/SceneTests.cs).

## Sockets and limbs

The game's droid changes its body: new arms, legs, a longer body (GameDesign.md). So a rig can't be fixed when it's built.

- A **socket** is a bare joint where something *can* go. The droid has `shoulder-left`, on the broom at shoulder height, with nothing in it.
- A **limb** is a small rig of its own. `DroidRig.JointedArm(prefix)` builds one: a servo clamped to the broom (its root), an upper arm hanging from the servo's shaft, a forearm from the elbow, and a pair of sporks. Its parts are named from the prefix: `arm-left`, `arm-left-upper`, `arm-left-forearm`, `arm-left-hand`.
- **`rig.Attach(limb, socket)`** copies the limb's parts into the rig, its root hung from the socket. Names must be new here, which is why the limb takes a prefix: two jointed arms need different names.
- **`rig.Detach(part)`** takes a part off with everything hung from it, and hands it back as a rig of its own, to be fitted again elsewhere.

Cables complicate taking things off. The droid's wiring runs from the head to what it works. When a part comes off, a cable fixed only to what came off goes with it (the filament between the sporks). A cable that also ran to what's left (the cord from the neck to the servo) is **cut**, and dropped from both.

## Keeping the list in order

`Solve` relies on every parent coming before its children (lesson 04). Adding a limb at the end keeps that. Taking one off leaves holes in the list, and hanging a part from a different parent can put a parent *after* its child. So after any change of shape, `Rebuild` lays the list out again: each root, then everything hung from it, depth first, in the order they were.

Indexes change when this happens, so hold on to parts by **name**, not by index, across a change. [RigView](../World.Rendering/RigView.cs), which keeps a mesh per index, copes because it checks each index's part name every frame and makes a new mesh where it's changed.

## Hanging a part somewhere else without a jump

In the cut scene, the droid carries the new arm in its sporks, then lets go of it at its left shoulder. At that moment the arm must go from hanging off the *hand* to hanging off the *shoulder*, and it mustn't move on screen.

A part's world matrix is its local pose times its parent's world matrix:

```
world = local × parent
```

We want to keep `world`, with a new parent. So solve for the new local:

```
local = world × parent⁻¹
```

That's `Rig.Reparent`:

```csharp
var local = World(IndexOf(name)) * Matrix.Invert(World(to));
local.Decompose(out var scale, out var rotation, out var translation);
```

`Decompose` splits the matrix back into a pose's three parts. The new pose becomes the part's **rest** as well as its current pose. Otherwise the next `Reset` would put it back at its old rest, now measured from the shoulder, somewhere else entirely.

The arm now rests wherever it was let go of, near the shoulder but not in it. `SetRest` changes the rest again once a clip has eased it into place.

## Easing into the socket: fade in a clip

How do you animate from "wherever it was let go of" to "in the socket", when "wherever" is only known at that moment? A keyframed track can't start from a value it doesn't know.

The trick is the clip's **weight** (lesson 04). The seat clip is one key: the arm's root at the socket (`Vector3.Zero`, no turn). Played with `fadeIn: 1f`, its weight climbs from 0 to 1 over a second, and each tick the root's pose is `Pose.Lerp(where it rests, the socket, weight)`. At weight 0 it's where it was let go of; at 1 it's in the socket. The fade *is* the animation.

## Clip events

A clip is about movement, but some moments in it are things the game must *do*: let go of the arm, play a clunk, flash the visor. `clip.Event(1.5f, "attach arm")` names such a moment.

The [Animator](../World.Core/Animation/Animator.cs) reports the events each clip *passes* as it's stepped. After `Step(dt)`, `Fired` lists them, with the clip each came from, in order. The rules, in `Clip.Passed`:

- An event fires when the clip's time goes from before it to at or after it: over the interval `(from, to]`. So one exactly at the end of a step fires in that step, and never again in the next.
- An event at 0 s must fire too, but no interval `(0, to]` contains 0. So the first step starts a hair before 0.
- A **looping** clip's time keeps growing (1.5 s, then 8.5 s, then 15.5 s for a 7 s clip), so the event at `e` fires at `e`, `e + duration`, `e + 2 × duration`, and so on. A step long enough to go round twice fires it twice, all in time order.
- A clip that doesn't loop holds at its end, and passes nothing more.

Why a list to read, not a C# `event` to subscribe to? Nothing to unsubscribe and forget; the order is plain; and a test just steps and looks (see `AClipsEventsFireAsItPassesThem`).

## Lay, not Apply

`Animator.Apply` resets the rig, then lays every clip over it. That's right when clips do everything. In the playground, the droid's movement (its wheels, its lean, its head camera) is set on the rig *first*, and the cut scene's clips go over it. `Animator.Lay` lays the clips without resetting, so what the clips don't key keeps what the movement set.

## Exercise (optional): predict, then test

Two tests for `SceneTests`. Write down what you expect *before* you run each one.

1. **Take its head off.** Build a droid (`DroidRig.Build()`), solve it at `Matrix.Identity`, and `Detach(DroidRig.Head)`. How many parts come off? How many cables come off with them, and how many are left on the droid? (Look at `DroidRig.Build` and `Wiring` to work it out: which cables have a point on the head, and which have *every* point on what came off?) Assert your predictions.
2. **Say hello while waving.** `DroidRig.Wave()` returns a new clip each time, so you can add an event to it in the test: `DroidRig.Wave().Event(0.8f, "hello")`. Play it on an `Animator` and step it 1/60 s at a time for 14 s, counting the `"hello"`s in `Fired` after each step. How many do you expect?

**Check it:** `dotnet test World.Core.Tests --filter SceneTests`. If a prediction was wrong, work out why before changing the number.

**Stretch:** after taking the head off, put it back: `Attach` the detached head at `DroidRig.Spine`. Where does it end up, and why? (Look at what its root's rest pose is relative to.) Are the cut cables back?

## Thinking questions

1. `Reparent` refuses to hang the spine from the head. What would happen to `Rebuild` if it didn't?
2. The arm is let go of *about* 5 cm from its socket. Why not fit it straight into the socket at that moment instead of fading?
3. An event fires on the step that reaches it. At 60 steps a second, how late can it be, and does that matter?

<details>
<summary>Answers</summary>

1. The head hangs from the spine; if the spine hung from the head, neither would lead back to a root. `Rebuild` visits from the roots down, so it would never reach them, and they'd silently drop out of the rig.
2. It would jump 5 cm in one frame: small, but a jump is exactly what the eye catches. Fading over a second turns the same 5 cm into a slide, which reads as the arm being pushed home.
3. Up to one step, 1/60 s, after its time. For things the game does (fit a part, play a sound) that's far below what anyone notices. What matters more is that it fires exactly once, which the `(from, to]` rule makes sure of.

</details>

## Further reading

- Any introduction to **scene graphs**: parent-relative transforms, and "reparenting while keeping the world transform" (Unity's `SetParent(parent, worldPositionStays: true)` is the same idea).
- On animation events: Unity's "Animation Events" and Unreal's "Anim Notifies" documentation show how bigger engines expose the same thing.
