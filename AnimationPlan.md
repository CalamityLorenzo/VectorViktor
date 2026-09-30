# Animation: options and plan

Written 2026-09-27. Step 1 (section 9) was done the same day; Paul answered section 10's questions on 2026-09-30, and steps 3 to 6 were done that day. Step 2's droid-as-player waits on Paul, and step 7 isn't wanted yet. It answers four questions: what the options are for animating things, whether any libraries would help, whether turning the meshes into glTF would help, and how the wanted examples would each be done. The examples:

- spinning wheels (the droid's, a car's)
- opening cupboards and drawers
- static on the television
- in-camera cut scenes of the droid trying on new limbs

## 1. The short answer

Almost everything on that list is **rigid parts moving relative to each other**: a wheel turning on its axle, a door on its hinge, a drawer on its runners, an arm bolted onto a shoulder. Nothing bends. The engine already moves whole meshes with a matrix, so what's needed is a small layer that organises it: parts in a tree, keyframed clips, and a player that layers them. That is step 1, now done (`World.Core/Animation`).

- **Libraries**: none needed for the core. MonoGame has no animation runtime of its own. SharpGLTF, already referenced, is worth using later for glTF animation import (step 7), not before.
- **glTF**: helps with only one thing, *authoring* animations in Blender (mainly the cut scenes). It doesn't help with wheels, drawers or the TV, and the meshes stay built in code.
- **TV static** isn't movement at all but a changing surface: a noise texture on the screen.
- **Bone (skinned) animation**: not needed for a mechanical droid, and it fights the outline system (section 3). Skip it.

## 2. What the engine already animates

Four kinds of movement already work, each a pattern to build on:

| Pattern | Where | How |
|---|---|---|
| A transform worked out from time | `ScenePart(MeshSource, Func<float, Matrix> At)`; the hangar's turntables | The world view asks each moving part for its matrix, `seconds` in. |
| Simulation state, drawn | `Door` (World.Buildings) + `DoorMesh`; `Body.Pose` for toppling crates | The simulation owns an angle or pose and steps it each tick; the renderer only reads it and sets `MeshInstance.Transform`. |
| Baked frames | `BirdMesh`: 180 flap frames, keyed `bird:N` | One mesh per frame, chosen by phase. Needed where the shape itself changes (a wing bending). About one frame per 60 Hz tick is needed or it looks jerky. |
| Per-instance colour | `MeshInstance.SetColor`; the walker darkening as it gets wet | Each instance owns its palette, so a colour can change every frame at no cost. |

## 3. Constraints that shape the choice

- **Simulation and drawing are split.** `World.Core` and `World.Buildings` use MonoGame only for maths types and are tested without a `GraphicsDevice`. Anything gameplay depends on (a drawer that sticks out and blocks you, a door leaf) belongs there. What's only for show (a TV's static, a dish wandering) can live in the rendering.
- **Outlines are worked out from the mesh.** The white edges are fixed lines, plus, for rounded surfaces, silhouettes worked out afresh for each view (`MeshBuilder.AddOutlineTri`, `OutlineView`). Moving a whole mesh is fine, since it's the same mesh in a new place. The outline is recomputed when the eye moves in the mesh's own space, which a moving part does every frame; for a few small parts that's cheap. **Deforming** a mesh (skinning, morphing) would need the outline worked out on CPU-deformed vertices every frame, and the vertex buffers are write-only. That's the main technical reason to keep to rigid parts.
- **Meshes are built in code**, with palette slots per colour (`MeshBuilder`), and the colour-restoring mechanic (GameDesign.md 3.3) depends on that. Whatever animation route is taken must keep the meshes as the source of truth.
- **The look.** The picture is low resolution (the droid's broadcast). Motion must read at that size: a wheel needs spokes to be seen turning, a dish needs a feed horn to show which way it's pointing.

## 4. The options

### A. Procedural: a function of time or state
The pose is calculated, not keyed: a wheel's angle is the distance rolled divided by its radius; a dish wanders on a tangle of slow sine waves; a camera runs round a rail by an angle.
- **Good for**: anything that follows the simulation (wheels, lean, head camera) or runs for ever (fans, turntables, dishes).
- **Costs**: nothing; no assets.
- **Limits**: poor for choreography ("lift, pause, wave twice, lower").
- **Examples here**: `DroidRig.Roll`, `Tilt`, `Look`, `Listen`; the hangar's turntables.

### B. Simulation state, eased
The simulation holds a value (door angle, drawer open fraction) and moves it towards a target at a speed, perhaps eased; the renderer turns it into a transform. It's the `Door` pattern.
- **Good for**: anything the player interacts with, or that can block or be blocked: doors, drawers, cupboard doors, lids, lifts, hatches.
- **Costs**: a small class per kind of thing, with tests.
- **Limits**: one value per thing; not for choreography.

### C. Rigid rig + keyframed clips (step 1, now built)
A thing is a tree of named joints (`Rig`), each with a pose relative to its parent. A `Clip` keys some joints' translation, rotation and scale over time (`Track`, `Channel`), with easing per key. An `Animator` plays several clips as layers with fade in and out.
- **Good for**: choreography: the droid waving, trying on an arm, a cupboard's doors flung open for a cut scene; anything made of several moving parts.
- **Costs**: clips are written in code (or imported from glTF later).
- **Limits**: rigid parts only; a turn between two keys goes the short way, so a key is needed at least every half turn (spinning things use A).
- **Mixes with A and B**: each tick the rig is reset, clips are laid on, then code sets whatever's procedural or simulated, then it's solved. This is how the droid on show works (section 9).

### D. Baked frames
A mesh per frame, as the bird has.
- **Good for**: a shape that changes (a flapping wing, a squashing cushion) with a short loop.
- **Costs**: memory (a mesh per frame), and about 60 frames per second of loop to look smooth.
- **Limits**: can't respond to anything; each frame is fixed.

### E. Surface animation: colour and texture
The shape stays put and its surface changes: a palette slot flickers, or a textured quad shows a changing picture.
- **Good for**: TV static, screens, blinking lights, the visor glowing, the colour coming back (GameDesign.md 3.3).
- **Costs**: colour: nothing. Texture: a small textured draw path beside `MeshBatch` (the meshes are position-only, with no texture coordinates).

### F. Skinned (bone) animation: not recommended
Vertices weighted to bones and bent by them: arms and legs that flex like skin.
- The droid is mechanical, so hinged rigid parts look right and read better at low resolution.
- It would need GPU skinning (a custom effect, which means the content pipeline's effect compiler) or CPU skinning, and either way the outline (section 3) would need recomputing from the deformed shape every frame.
- Only worth revisiting for something soft or organic (an animal or a cable), and even then a chain of short rigid segments (a rig in C) usually does it.

### G. Morph targets: not recommended
Blending between versions of the same mesh. The same outline problem as F; D covers the few cases.

## 5. Libraries

| Library | What it gives | Verdict |
|---|---|---|
| MonoGame itself | Maths (`Matrix`, `Quaternion`), FBX import in the content pipeline with bones, but no player | The core is written on its maths types. |
| **SharpGLTF.Core** (already referenced, 1.0.7) | Reads and writes glTF, including animation channels (node translation, rotation and scale, cameras, morph weights) and skins | **Use it for step 7**: loading keyframes authored in Blender. |
| SharpGLTF.Toolkit | Builds glTF scenes and animations in code | For the planned mesh exporter, to write the droid's parts as a node tree. |
| SharpGLTF.Runtime | Evaluates a glTF's animations: each node's matrix at time t | Probably not needed: once channels are loaded into `Clip`s, the existing player does this. |
| Aether.Extras (Aether.Animation) | Skinned FBX/glTF animation for MonoGame via the content pipeline | Skinning only (option F), so not needed. |
| MonoGame.Extended (Tweening) | Tweens with easing | A large dependency for about 50 lines already written (`Ease`). Not needed. |

Writing the core by hand matches the choice already made for the physics: small, fitted to the game, tested, and no dependency to fight.

## 6. Would turning the meshes into glTF help?

glTF is an interchange format. It carries meshes, a **node hierarchy** (like `Rig`), **animations** as keyframed translation, rotation and scale channels per node (like `Track`), animated cameras, skins and morph targets. glTF is y-up and right-handed like MonoGame, so no axes need swapping.

**What it would help with**: authoring motion *visually*. A cut scene of the droid picking up an arm, turning it over and clicking it on is fiddly to key in code and pleasant to key on Blender's timeline, with the camera move alongside it.

**What it wouldn't help with**:
- Wheels, drawers, doors and dishes: a line or two of code each (options A and B).
- TV static: not movement.
- The rendering: `MeshBatch` still draws `MeshData`, with palette slots and outlines. glTF only supplies numbers: a node's pose at time t.

**The route, if and when it's wanted (step 7)**:
1. Keep the code-built meshes as the source of truth.
2. Export the droid's parts to a .glb with the planned exporter (`SharpGLTF.Toolkit`), **one node per rig joint, named as the rig names them** (`DroidRig.Head`, `DroidRig.Arm` and so on), with each part's mesh on its node. The palette goes as vertex colours, which Blender shows as-is.
3. Animate in Blender and export the .glb.
4. Load **only the animation channels** with SharpGLTF.Core, turning each into a `Track` on a `Clip` by node name. glTF's LINEAR and STEP samplers map onto `Ease.Linear` and `Ease.Step`; CUBICSPLINE would need a spline `Channel` or resampling at 60 Hz.
5. Play it with the `Animator` like any other clip. A clip naming parts a rig hasn't got skips them, so the same clip plays on a droid with or without its arm.

Importing glTF *geometry* (models made in Blender) is a separate, bigger job: the vertex colours would need turning back into palette slots, and the surfaces into fixed edges and outline triangles. Leave it until there's a model that can't be built in code.

## 7. The droid's rig (from Paul's sketch)

The sketch is the head from the front: about as tall as it's wide, corners rounded, on a short narrower neck. The ruby visor is a band round its upper part, a little proud of it, and it **goes all the way round**. The ears are hoops on its sides, face on from the front, level with its lower middle. The head camera runs round inside the visor like a rail, so it can look any way without the head (or the body) turning.

Sizes, in `DroidRig` (metres; the head's proportions are measured off the sketch):

| Part | Size |
|---|---|
| Wheels | radius 0.14, 0.06 wide, 0.40 apart |
| Broom handle | 0.86 long, from the axle to the neck |
| Neck | radius 0.10, 0.05 tall |
| Head | radius 0.17, 0.34 tall, edges rounded by 0.04 |
| Visor | 0.19 to 0.30 up the head, standing 0.012 proud |
| Ears | hoops of radius 0.045, their middles 0.14 up the head |
| Broom | 0.07 across (thicker than a real one, so its colour shows) |
| Arm | a 0.45 stick, 0.05 across, hanging from a servo clamped to the broom 0.62 up it, on its right |
| Hand | two orange plastic sporks, bowls facing each other front to back like a pincer, bolted and wire-lashed together (0.14 long) |
| Wheel motors | a drum just inside each wheel, on the axle, so it doesn't turn with the wheel |
| Overall | about 1.39 m to the top of the head, for this Segway-and-broom droid; it changes through the game (taller legs, a longer body) |

The tree (facing +Z, its left is +X):

```
root ─ axle ─┬ wheel-left, wheel-right         turned by Roll (distance / radius)
             └ lean ─ spine ─┬ shoulder ─ arm ─ hand
                             └ head ─┬ ear-left  ─ dish-left      wandered by Listen
                                     ├ ear-right ─ dish-right
                                     └ rail ─ camera              run round the visor by Look
```

- **lean** is a bare joint at the axle: tilting it tilts everything above the wheels, Segway style (`Tilt`).
- **rail** is a bare joint on the head's axis at the visor's middle height; turning it about y carries **camera** round the band. The camera's own turn is its pitch within the band (from straight down to 60 degrees up: `MaxLookDown`, `MaxLookUp`; when the head can't move, the camera has to do the looking) and a little aside. `DroidRig.CameraView` gives the eye, forward and up vectors to render the head camera's view from.
- **shoulder** is a bare joint (a socket): the arm hangs from it. Swapping or adding limbs means fitting parts at sockets like it (step 5).
- **The wiring.** The head works everything, so it's cabled to it (`Cable`, strung through points fixed to parts; see `Rig.Span`). Two cords run from the back of the neck down the broom: one into the servo, and on from its bottom as filament down the back of the arm to the sporks' lashing, with a strand to each spork; the other into the axle's hub, then as filament sagging along the axle to each wheel's motor. A stretch between points on different parts is worked out afresh each frame, so the cables stay joined through the lean, the wave and the head's tilt. They're drawn as faces only (yellow, no white edges), so they show close to and are next to nothing far off.
- Each joint names its mesh (`RigNode.Part`: `droid-urn-head`, `droid-wheel` and so on; `DroidMesh.Sources`). Both wheels share one mesh, as do both ears.

## 8. The examples, worked through

### Spinning wheels
**Option A.** The wheel's rotation about its axle is `distance rolled / radius` (`DroidRig.Roll`), so it never slips, and backing up turns it back. The distance comes from the character controller's movement each tick. The mesh has three spokes on each hub, so the turn shows in wireframe. A car's wheels are the same, plus a steering yaw on the front pair.

### Opening a drawer
**Options B + C.**
- *Simulation* (World.Buildings, like `Door`): a `Drawer` with a slide axis, a travel (0.4 m), an open fraction 0 to 1 and a speed. `Interact` toggles it. Open, it's a solid box sticking out (`KeepOut`/`Obstructs`), and it stops against a body in the way, as a door leaf does. Later it holds things (GameDesign.md "find X").
- *Drawing*: the chest of drawers is a rig: `carcass` with a `drawer-1`, `drawer-2` and so on under it. Each tick the drawer node's translation is set from the fraction, eased (`Ease.InOut.Apply(fraction)`) so it starts and stops softly.
- *In a cut scene*, a clip can key the same `drawer-1` node instead, with no simulation involved.

### Opening a cupboard
The same, with hinged leaves in place of the slide: a `Cupboard` owning one or two leaves, each a `Door`-like angle (`Door` itself could be reused, since a cupboard door is a small door). Rig: `carcass`, `door-left`, `door-right`, perhaps `shelf-1`. A cupboard is also a future *body* for the droid (GameDesign.md 3.1), so its rig may later hang from the droid's.

### Static on the television
**Option E.** Two levels:
1. *Now, nearly free*: flicker the TV's `Glass` palette slot between greys each tick (`MeshInstance.SetColor`). It reads as "it's on", not as static.
2. *Proper static*: a `ScreenView` (World.Rendering) that draws a quad over the screen with a small `Texture2D` (about 48 x 36, point-sampled so the pixels stay hard), refilled with random greys every tick or two (`SetData` of 1,728 pixels, trivial). It's drawn after the faces, depth-tested, with a `BasicEffect` with `TextureEnabled`. `TelevisionMesh` would say where its screen is. A bulging screen could be a slightly curved 4 x 3 grid of quads instead of one.
3. *Later*: the same screen showing a picture, such as the drone's feed or a recording from the head camera (GameDesign.md 3.2). **Paul chose a render target** (2026-09-30): a second render of the world into a render target, used as the screen's texture. So `ScreenView` should take its texture from outside from the start, with static as just one source (a noise texture) and a camera's render target as another.

Static is naturally black and white, which fits a world with no colour; in the wireframe view it could be white dots on the background.

### The droid trying on new limbs (cut scene)
**Options C + A + B**, with two things the core doesn't have yet (step 5):
- **Sockets and fitting.** A limb is its own small rig (upper arm, elbow, forearm, hand, or a set of tracks) fitted at a socket node (`shoulder`, `locomotion`). The rig needs `Attach(subRig, socket)` and `Detach`, plus moving a node to a new parent keeping its world pose, so the arm can be carried in the fork hand and then clicked onto the shoulder without jumping.
- **Clip events.** Named moments in a clip (`attach arm`, `clunk`, `visor flash`) that the `Animator` reports as it passes them, for the game to act on: reparent the arm, play a sound, put a line on the droid's console.

The scene itself might be: the droid rolls to the workbench (a procedural drive to a spot); a clip lifts the new arm in its fork; the `attach arm` event moves the arm's root from the hand to the shoulder socket; a clip flexes the new elbow; the ear dishes twitch (`Listen` with a fast rate); the head camera sweeps round to look at the new arm (`Look`, keyed).

### In-camera cut scenes generally
A **timeline** (step 6): clips on the droid's rig and on props, a camera track, and events, all on one clock. Player input is paused; a key skips to the end state.
- **Whose camera?** Both kinds (Paul, 2026-09-30): cameras that exist in the world, the **drone** (flying a keyed path), a **mirror** (GameDesign.md 3.2: how the droid sees itself, and the natural place to try on a limb) or the **head camera** running round its rail to look at itself, and a **free cinematic camera** that isn't in the world at all. The camera track keys either kind.
- The camera track keys position and look-at (or a node in the rig to look at) with the same `Channel` and `Ease` types.

## 9. Steps

This refines the five steps suggested earlier.

### Step 1: the rig and clip core, with the droid as its first rig (done 2026-09-27)
- **`World.Core/Animation`** (no graphics, tested):
  - `Pose`: translation, rotation, scale.
  - `Ease`: Linear, In, Out, InOut, Step.
  - `Channel<T>`: keys in time order, holding at the ends.
  - `Track`: translation, rotation and scale channels for one named node; unkeyed channels are left alone.
  - `Clip`: named, looping or not, with a duration; skips parts a rig hasn't got; applies with a weight.
  - `Rig` and `RigNode`: a tree of named joints, parents first, with Reset, Change and Solve; `Part` names the mesh.
  - `Cable` and `CablePoint`: cables strung through points on the rig's parts; `Rig.Span` gives the matrix for each stretch.
  - `Animator` and `ClipPlayer`: layered clips with fade in and out; a non-looping clip holds its end pose.
- **`World.Core/Characters/DroidRig`**: the starting droid's rig (section 7), with the procedural drivers `Roll`, `Tilt`, `Look`, `CameraView` and `Listen`, and a keyframed `Wave` clip.
- **`World.Rendering/DroidMesh`**: a mesh per part, sharing one palette. The head, visor and tyres are outlined per view, so they read as round in wireframe. **`World.Rendering/RigScene`**: turns a rig posed by a function of time into `ScenePart`s, so it goes through the existing moving-parts path (and through windows).
- **`Maps.Home/DroidDisplay`** (in `Basic.World` until the world moved into libraries, [ToolsPlan.md](ToolsPlan.md) step 1): the droid on show a few metres south of the start. It rocks on its wheels, leaning into each start and stop; its camera runs round the visor; its dishes wander; every seven seconds it waves. Start `Basic.World droid` to stand in front of it.
- **Tests**: 26 in `World.Core.Tests/AnimationTests.cs` (130 in that project now), including the droid's cables staying joined and short while it rolls, leans and waves. The droid's meshes are checked by `Meshes.Tests` through the world's moving parts.

### Step 2: live rigs, and the droid as the player (partly done 2026-09-27, in the playground)
Done in [ToolsPlan.md](ToolsPlan.md) step 2: `RigView` (a live rig, following part swaps and cables), `DroidMotion` (wheels rolled each their own distance, lean from acceleration), and the droid as the player in `Droid.Playground`, with its head camera run round the visor by keys. Still to do: the droid as the player in the game itself, with its own size in place of the walker's. **Waiting** (Paul, 2026-09-30): Segway movement is only one of the ways the player will move, so the walker stays the player in the game until Paul knows more about what he wants from the player character.

- A `RigView` (World.Rendering) for rigs driven by the simulation rather than by a clock: it keeps a `MeshInstance` per node, follows `RigNode.Part` changes, and adds them to the batch. `RigScene` suits things on show; this suits the player.
- Swap the walker's `PlayerMesh` for the droid's rig. `Roll` is driven by distance moved, `Tilt` by acceleration, and the camera view comes from `DroidRig.CameraView`. This overlaps GameDesign.md 3.1's droid work (its size, its Segway movement), so it may belong there instead.
- Head camera controls: running round the rail and pitching within the band, straight down to 60 degrees up (done: `DroidRig.Look` holds it there, and so do the playground's keys).

### Step 3: drawers and cupboards (done 2026-09-30)
- **Simulation** (World.Buildings, `Cabinet.cs`): `CabinetSpec` (a chest of drawers or a cupboard in a room: `RoomSpec.Cabinets`), `Drawer` (slides out along its front's facing, stops against a body or walker as a door does), and `Cabinet`, whose cupboard doors are plain `Door`s. `IOpenable` is what `BuildingGround.Interact` now returns: a door or a cabinet. Using a chest opens its top drawer still shut, and once they're all open shuts them all; using a cupboard opens or shuts both doors.
- **Blocking**: a cabinet's carcass is four walls; its open drawers' fronts and sides, and its doors wherever they've swung, are walls too, for walkers, bodies and the drone's line of sight. `StepDoors` steps them with the doors.
- **Drawing**: `CabinetRig` (carcass, `drawer-i`, `door-i`) posed from the simulation each frame, the drawers eased with `Ease.InOut`; `CabinetMesh` and `CabinetView` in World.Rendering, drawn by `BuildingView` with the rooms' insides.
- **In the world**: the two-storey houses (`Houses.TwoStorey`) have a kitchen cupboard downstairs and a chest of four drawers in the bedroom. Starts `kitchen` and `drawers`; E (Enter in the playground) uses them.
- **Tests**: `CabinetTests` (7).

### Step 4: the TV (done 2026-09-30)
A render target, as Paul chose. `ScreenSpec` (a television's screen in a room, `RoomSpec.Screens`, tuned to a channel); `ScreenView` (World.Maps) lays a textured grid over the set's bulging glass (`TelevisionMesh.ScreenPoint`, now public). `WorldRenderer.Feed(channel, camera, extra)` gives a channel a camera, drawn into a 96 x 72 render target before each frame by `DrawFeeds` (the screens are left out of a feed, so none draws into itself); any other channel shows `StaticPicture`, 48 x 36 random greys made afresh every two ticks. Both are drawn point-sampled and fogged. The cottage's television shows the drone's camera (`WorldRenderer.DroneChannel`, fed by Basic.World and the playground: sit on the sofa and watch yourself); the old set in the attic shows static. Start `telly`.

### Step 5: sockets, fitting and clip events (done 2026-09-30)
- `Rig.Attach(limb, socket)`, `Rig.Detach(part)` (its cables go with it, or are cut if they also ran to what's left), `Rig.Reparent(part, parent)` (keeps where it is in the world, as of the last Solve), and `Rig.SetRest`. The rig keeps parents before children through all of them, so Solve is still one pass.
- `Clip.Event(time, name)`: the `Animator` reports each one passed in its `Fired` list, looping clips each time round. `Animator.Lay` lays the clips over what's already on the rig (its movement), where `Apply` resets it first.
- `DroidRig.ShoulderLeft` (a socket on the broom) and `DroidRig.JointedArm(prefix)`: a limb with its own servo, an upper arm, an elbow, a forearm and sporks (`DroidMesh` has the servo mount and the limb stick).
- **Tests**: `SceneTests` (events, fitting and taking off, reparenting).

### Step 6: the cut-scene timeline (done 2026-09-30)
- `Timeline` (World.Core/Animation): cues at times, clips played and stopped on animators, handlers for clip events (`On`), all on one clock; it steps its animators, splitting each step at the cues so a clip started mid-step plays only the rest of it. `Playing` is what the game checks to leave the player's input alone; `Skip` runs it through to the end a tick at a time, so it ends exactly as if played.
- `CameraTrack`: cuts between the free cinematic camera (`CameraTrack.Free`, keyed eye and target) and named world cameras, which the game says where they are.
- **The first scene**, `FittingScene` (World.Core/Characters): the droid holds the new arm in its sporks, lying back along the stick arm; lifts it round so its servo comes to the left shoulder; the "attach arm" event reparents it there and a clip fades it into the socket; the stick arm drops back; the head camera runs round and tips down at it while the elbow flexes and the ear dishes twitch; the arm stays fitted. Seen from the free camera, then the head camera, then the drone. No mirror yet (GameDesign.md 3.2), so the drone watches instead.
- **Playground**: the `fitarm` experiment plays it on the world's clock (pause, step and slow work), K skips it; experiments can now give the camera (`Experiment.Camera`) and be skipped (`Experiment.Skip`).
- **Tests**: `SceneTests` (the timeline, the camera track, the scene: no jumps, let go of within 25 cm of the socket, skipped ends as played).

### Step 7: glTF animation import (only if keying in code gets too fiddly; clips are keyed in code for now)
Exporter for the droid's parts as a node tree; importer for animation channels into `Clip`s (section 6).

## 10. Questions for Paul (answered 2026-09-30)

1. **The droid as the player (step 2)**: not yet. Segway movement is one form of movement among several to come, so the walker stays the player until Paul knows more about what he wants from the player character.
2. **Cut-scene cameras**: both. Cameras in the world (drone, mirror, head camera) and a free cinematic camera.
3. **Authoring**: clips keyed in code for now; Blender authoring (step 7) isn't wanted yet.
4. **The head camera's freedom**: all the way round the rail, and from looking straight ahead, up to 60 degrees up and down to 90 (straight down). There are times the head can't move, so the camera has to do the looking. Done: `DroidRig.MaxLookUp`/`MaxLookDown`, held by `Look` and the playground's keys.
5. **The head's sizes**: 1.39 m is right for the Segway-and-broom droid, but its size changes through the game (taller legs, a longer body), so nothing should assume a fixed height: take it from the rig.
6. **The TV**: a render target, so the screen can later show pictures (the drone's feed, recordings), with static as one of the things it shows.
