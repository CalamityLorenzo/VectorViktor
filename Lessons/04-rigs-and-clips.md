# 04: Rigs and clips

**What you'll learn:** how something made of moving parts is described as a tree of joints (a **rig**), and how a joint's place is worked out from its parent's; what a keyframed **clip** is, down to its tracks, channels, keys and easing; how several clips, and movement worked out in code, are laid over one another on the same rig; and why this project animates rigid parts and not a skinned mesh.

**Built in:** [AnimationPlan.md](../AnimationPlan.md) step 1, in [World.Core/Animation](../World.Core/Animation/Rig.cs) and [DroidRig](../World.Core/Characters/DroidRig.cs). The other animation lessons (05 to 08) build on this one.

Run it: `Basic.World\bin\Debug\net10.0-windows\Basic.World.exe droid`. You stand facing the droid on show. It rocks on its wheels, its head camera runs round its visor, its ear dishes wander, and every seven seconds it waves.

## Rigid parts, not bones

Most 3D characters are **skinned**. An invisible skeleton of bones moves, and each vertex of one continuous mesh follows a weighted blend of the bones near it, so an elbow bends smoothly. This project doesn't do that, for two reasons:

- **Nothing here bends.** The droid is a broom handle, a servo, a stick and two sporks bolted together. A cupboard is a box and a door on a hinge. Each part is a solid thing that moves as a whole.
- **The look depends on it.** The white outlines round round things (the head, the tyres) are worked out afresh for each view from the mesh's own triangles (see MeshInstance). If a mesh bent, those outlines would have to be worked out from vertices that move every frame.

So each part is its own mesh, and animating means moving whole meshes with a matrix. The engine could always do that; what's needed is a way to organise it.

## The rig: a tree of joints

A [Rig](../World.Core/Animation/Rig.cs) is a list of named **nodes** (joints). Each node has a parent (or none, for a root), and a **pose** relative to that parent. A [Pose](../World.Core/Animation/Pose.cs) is a translation, a rotation (a quaternion) and a scale, the same three things glTF keeps for a node. The pose's matrix sizes first, then turns, then moves, so a part turns about its own origin: its joint.

A node's place in the world is its own pose, then its parent's world matrix:

```csharp
_world[i] = node.Pose.Matrix * (node.Parent < 0 ? placement : _world[node.Parent]);
```

That's `Rig.Solve`. It works down the list once, which only works if **every parent comes before its children**, so `Rig.Add` insists on it. Turn the lean joint, and everything hung from it (the broom, the arm, the head and all that's on the head) goes with it, because their world matrices are built on its.

Some nodes have a **part**, the name of the mesh drawn there. Others are **bare joints**: a pivot (`lean`), a rail for the camera to run on (`rail`), or a socket with nothing in it yet. Here's the droid's tree ([DroidRig.Build](../World.Core/Characters/DroidRig.cs)):

```
root ─ axle ─┬ wheel-left, wheel-right         turned by Roll
             └ lean ─ spine ─┬ shoulder ─ arm ─ hand
                             ├ shoulder-left            a socket (lesson 07)
                             └ head ─┬ ear-left  ─ dish-left
                                     ├ ear-right ─ dish-right
                                     └ rail ─ camera
```

Each tick: `Reset` puts every node back at its **rest** pose; clips and code change some of them; `Solve` works out where everything is; then each part is drawn at its node's `World`.

## Clips: tracks, channels, keys

A [Clip](../World.Core/Animation/Clip.cs) is a named movement: the droid waving, a drawer sliding out. Here it is from the top down:

- A clip has **tracks**, one per node it moves, found by the node's *name*. So one clip plays on any rig with those names, and a clip naming a part a rig hasn't got simply skips it: a wave played on a droid with no arm moves the rest and nothing else.
- A [Track](../World.Core/Animation/Track.cs) has three **channels**: translation, rotation and scale. A channel with no keys leaves that much of the node alone, so a track that only turns a lid leaves it where it's hinged.
- A [Channel](../World.Core/Animation/Channel.cs) is a list of **keys** in time order, each a value at a time. Between two keys the value is blended: vectors in a straight line, rotations the short way round (`Pose.Slerp`). Before the first key it holds the first value, after the last the last.
- Each key says how it's reached from the one before: its [Ease](../World.Core/Animation/Ease.cs). `Linear` is a steady rate. `InOut` starts and stops softly, which suits most things moved on purpose. `Step` jumps when the time comes, for a switch.

The wave, abridged:

```csharp
var clip = new Clip("wave", loops: true, duration: 7f);
clip.Track(Arm)
    .Turn(0f, Hanging)
    .Turn(0.8f, raised)
    .Turn(3.6f, raised)
    .Turn(4.6f, Hanging);
```

The arm swings up in 0.8 s, stays up while the hand waggles (the `Hand` track), and comes down by 4.6 s. The clip lasts 7 s, so it rests for the last 2.4 s before it loops.

## Laying clips over one another, and over code

A clip's `Apply(rig, time, weight)` sets the nodes it keys and leaves the rest alone. So several clips play at once by being applied one after another. The [Animator](../World.Core/Animation/Animator.cs) does that for the clips it's playing, in the order they were started:

- **Layers.** The wave keys the arm, the hand and the head; a clip keying only the ears could play at the same time and they'd never meet. Where two clips key the *same* node, the one applied last wins.
- **Weight.** With a weight between 0 and 1, a clip's pose is only part-way from what the node was to what the clip says (`Pose.Lerp`). Fading a clip in over half a second means its weight climbs from 0 to 1, so the part eases over from wherever it was. This comes back in lesson 07.
- **Code.** Not everything is a clip. The wheels turn by `distance / radius` (`DroidRig.Roll`), so they never slip, however fast the droid goes. The camera runs round the visor wherever the player steers it (`DroidRig.Look`), and the dishes wander by a sum of slow sine waves (`DroidRig.Listen`). These are just functions that set nodes. The order each frame is: reset, clips, then code (or code, then clips: lesson 08 needs that way round, so `Animator.Lay` lays clips over what code has already set).

A wheel is a good example of what *not* to key. A rotation key is reached the short way round, so a clip needs a key at least every half turn, and a wheel's turn depends on how far it's gone, not how long it's been going.

## Drawing a rig

World.Core knows nothing of meshes, so it can be tested without a window (`AnimationTests`). Two classes in World.Rendering draw a rig:

- [RigScene](../World.Rendering/RigScene.cs) is for a rig whose every move is a function of time, like the droid on show ([DroidDisplay](../Maps.Home/DroidDisplay.cs)). It turns each part into a `ScenePart` that asks "where are you at *t* seconds?", so it goes through the world's existing path for moving things, windows included.
- [RigView](../World.Rendering/RigView.cs) is for a rig moved by the simulation. After you've posed and solved it, `Add` puts a mesh at each part. It follows the rig as it changes: a part's mesh swapped, a limb fitted. The playground's droid uses it.

## Exercise (optional): perk up its ears

Give the droid on show a clip of its own: every three seconds, its ear hoops turn quickly outwards and back, as if something caught its attention.

1. In `DroidRig`, add `public static Clip Perk()`: a looping clip called `"perk"`, 3 s long.
2. Key `EarLeft`'s rotation: rest (`Quaternion.Identity`) at 0 s, turned 0.5 rad about `Vector3.Up` at 0.2 s, and back to rest at 0.5 s.
3. Key `EarRight` the same way, but look at how `Build` hangs it first. A key replaces the node's *whole* rotation, rest included, so a key of `Quaternion.Identity` would undo whatever turn its rest pose has.
4. In `DroidDisplay.Parts`, make a `Perk()` clip alongside the wave, and apply it in `Pose` after `wave.Apply(r, t)`.

**Check it:** run `Basic.World.exe droid` and watch the ears flick every three seconds. If one ear swings round to face the head, look at step 3 again.

**Stretch:** write a test in `AnimationTests` that applies `Perk()` at 0 s and 0.2 s and checks the right ear is at its rest turn at 0 s and half a radian from it at 0.2 s. The `Apart` helper measures the angle between two rotations.

## Thinking questions

1. `Rig.Solve` works down the list once. What would go wrong if a child came before its parent?
2. The wave keys the head (a tilt while it waves). Suppose you added a "nod" clip that keys the head too, and applied it *after* the wave. What happens to the wave's tilt, even between nods?
3. Why do both wheels use the same mesh, and both ears, when they're on opposite sides?

<details>
<summary>Answers</summary>

1. Its world matrix would be built on its parent's world matrix from the *last* Solve (or garbage the first time), so it would lag a frame behind its parent, or be somewhere else entirely.
2. It vanishes. A channel holds its first key's value before it and its last key's after, so the nod's head track sets the head's rotation at every moment of the loop, not just while nodding. Applied last, it always wins. Keying different nodes (or fading clips in and out) avoids the fight.
3. A mesh is only a shape; where it goes is the node's business. The right wheel is the left one moved across, and the right ear is the left one turned half round (its rest pose), so one mesh each does for both.

</details>

## Further reading

- The glTF 2.0 specification, sections on nodes and animations: the same node / translation-rotation-scale / channel / sampler model, with `STEP` and `LINEAR` interpolation.
- *Game Engine Architecture* by Jason Gregory, the animation chapter: skeletons, poses, clips and blending, including the skinning this project leaves out.
- On quaternions and slerp: any short introduction to "quaternion slerp shortest path" explains why `q` and `-q` are the same turn.
