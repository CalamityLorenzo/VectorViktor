# 05: Drawers and cupboards: the simulation and the picture

**What you'll learn:** how to split something that moves into what it *is* (a drawer is 0.2 m out, and it's a wall) and how it *looks* (a rig posed from that, eased); how to reuse a class you already have (a cupboard door is a `Door`); how moving parts block walkers by being walls; why the per-tick code avoids making garbage; and a trap in sharing meshes between things that differ.

**Built in:** [AnimationPlan.md](../AnimationPlan.md) step 3: [Cabinet.cs](../World.Buildings/Cabinet.cs), [CabinetRig](../World.Buildings/CabinetRig.cs), [CabinetMesh](../World.Rendering/CabinetMesh.cs), and the changes to [BuildingGround](../World.Buildings/BuildingGround.cs). Lesson 04 comes first.

Run it: `Basic.World.exe kitchen` stands you facing the cupboard in the house; `Basic.World.exe drawers` in the bedroom, facing the chest. E uses them. V switches to the drone's view, which shows them better than your own eyes do.

## Two halves

A drawer has to do two things:

- **Behave.** Slide out when used, stop if you're standing in its way, and stop *you* walking through it while it's out.
- **Look right.** A tray behind a front with a handle, sliding smoothly out of a carcass.

These are kept apart, as everything in this project is:

- The **simulation** ([Drawer](../World.Buildings/Cabinet.cs), in World.Buildings) is a few numbers: where its front is, which way is out, how far it can go (`Travel`), how far it *is* out (`Extent`), and which way it's going (`Opening`). `Step` moves it on at a steady `Speed`. It knows nothing of meshes.
- The **picture** ([CabinetRig](../World.Buildings/CabinetRig.cs) and [CabinetView](../World.Rendering/CabinetMesh.cs)) is a rig: a carcass node with a node per drawer. Each frame, `CabinetRig.Follow` sets each drawer node's translation from the drawer's state, then the rig is solved and drawn.

`Follow` doesn't use the extent as it is. It **eases** it: `Ease.InOut.Apply(drawer.Fraction) * drawer.Travel`. So the drawer starts and stops softly in the picture, while the simulation moves at a steady rate. The two disagree by a few centimetres partway out, which nobody will notice, and each is simpler for it. Easing is presentation.

This is how the doors in doorways already worked ([Door](../World.Buildings/Door.cs) and [DoorMesh](../World.Rendering/DoorMesh.cs)). The drawer follows the same pattern.

## Reusing Door

A cupboard door is a small door: a leaf on a hinge that swings a quarter turn, stops against anything in its way, and is a wall wherever it is. So a [Cabinet](../World.Buildings/Cabinet.cs) that's a cupboard simply makes `Door`s for its leaves, hinged at the front's edges and swinging out along the way it faces. Everything that already worked for doors (stepping, stopping, blocking) works for them with no new code.

Using something is `BuildingGround.Interact`. It used to find the nearest door and return it. Now it looks at cabinets too, so it returns an [IOpenable](../World.Buildings/Cabinet.cs), a one-method interface (`Toggle`) that both `Door` and `Cabinet` have. A cabinet decides for itself what using it means:

- A **cupboard** opens both doors together, or shuts them.
- A **chest** opens its top drawer that's still shut; once they're all open, it shuts them all.

A cabinet can be used only from in front of it. `Interact` checks you're on its front side (`Dot(p - FrontLeft, Facing) > 0`), within reach of its front, and facing it.

## Blocking by being walls

Everything that blocks in a building is a [WallSegment](../World.Buildings/Building.cs): a line on the floor plan, from a bottom height to a top. Walkers are pushed out of them, bodies can't be in them, and the drone can't see through them. A cabinet uses the same thing:

- Its **carcass** is four walls round its footprint. They never move, so they go in with the building's walls, in the grid that finds walls near a point quickly.
- An **open drawer** is three walls: its front, and its two sides back to the carcass. A shut one is none: it's inside the carcass.
- A **cupboard door** is its leaf's panel, wherever it has swung to.

Moving parts must stop rather than push you. Each tick, a drawer tries its next extent, and `BuildingGround.InTheWay` says whether its three walls there would be in a body or a walker. If so, it stays where it is and tries again next tick. The same check serves the doors.

### No garbage per tick

`Drawer.Panels` could have been an `IEnumerable<WallSegment>` built with `yield return`. But every call of such a method makes a small object for the garbage collector, and the walker's collision code asks for every drawer's panels twice a tick. So a drawer says how many panels it has (`Panels`: 0 or 3) and hands them out one at a time (`Panel(k)`), and the loops that ask are plain `for` loops. It's the same care taken earlier with the walls (see ArchitectureReviewPlan.md 3.2).

## Meshes shared by shape

Two chests the same size share one set of meshes, even in different colours: a mesh is only a shape, and colours come from the palette each instance has. So a part's name, which is also its mesh's key in the [MeshCache](../MeshRendering/MeshCache.cs), holds the cabinet's sizes and nothing else (`CabinetRig.CarcassPart`).

The trap is in the build lambda. In a debug build, `MeshCache` checks that a key is always built the same way, by comparing the values the build lambda captured. `d => BuildCarcass(d, spec)` captures `spec`, and two chests the same size but in different places have *different* specs. Same key, different captures: the check throws. So `CabinetMesh.Sources` strips the spec down to its shape first:

```csharp
spec = spec with { Position = Vector3.Zero, YawDegrees = 0f, Color = default };
```

That check exists to catch the opposite bug: two meshes that really differ, given one key, where the second would silently be drawn as the first.

The fronts stand 2 cm proud of the carcass. If a front were flush, its face would lie in the same plane as the carcass sides' front edges, and the two would **z-fight**: flicker as the depth buffer can't decide which is nearer.

## Exercise (optional): a door hinged on the right

One-door cupboards are always hinged on their left (as you face them). Add the choice of the right, and put a tall one in the cottage.

1. Add `bool HingeRight = false` to the end of `CabinetSpec`'s parameters. Because it has a default, every cabinet already written stays as it is.
2. In `Cabinet`'s constructor, a one-door cupboard with `HingeRight` gets its door hinged at `FrontRight`, running back across the front (`-across`).
3. In `CabinetRig.Build`, hang `door-0` at the right edge, turned half round, as `door-1` is for a pair.
4. In `CabinetRig.Follow`, turn it the way the right-hand door of a pair turns.
5. In `Town.Cottage`, add a cupboard against the west wall facing east: `new CabinetSpec(CabinetKind.Cupboard, new Vector3(-3.7f, 0f, -1f), 90f, 0.6f, 0.5f, 1.8f, 1, new Color(120, 90, 160), HingeRight: true)`, in the room's `Cabinets`.

Steps 2, 3 and 4 must agree: the simulation (where the door blocks) and the picture (where it's drawn) are separate, so nothing makes them match but you.

**Check it:** run `Basic.World.exe telly`, turn round (the cupboard is behind you), walk up to it and press E. The door should swing out from its right-hand edge. Then walk into the open door: you should stop where it's *drawn*.

**Stretch:** a test in `CabinetTests` that opens a right-hinged door fully (`leaf.Toggle()`, then `leaf.Step` until `leaf.AtRest`), poses the rig with `CabinetRig.Follow`, and checks the door node's world X axis (`Vector3.TransformNormal(Vector3.UnitX, world)`) points along `leaf.Direction(leaf.Angle)`. That's steps 2 to 4 agreeing, checked for good.

## Thinking questions

1. A drawer only checks for something in its way while it's coming *out*. Why is shutting never blocked?
2. `Interact` returned `Door?` before, and the door tests used what it returned. What would have been wrong with keeping `Door?` and returning `null` when a cabinet was used?
3. The drawer's picture is eased, but its walls aren't. Where exactly, partway out, would you find the picture and the walls disagree, and by how much at most?

<details>
<summary>Answers</summary>

1. Shutting, the drawer goes back into its own carcass, where nothing else can be: the carcass is solid, so nothing can be in the way.
2. Callers couldn't tell "nothing was in reach" from "a cabinet was used": both would be `null`. A game that shows "nothing to use here" would say it wrongly, and tests couldn't check which cabinet opened. An interface keeps the answer and hides only what the caller doesn't need.
3. `Ease.InOut` (`t² (3 − 2t)`) is behind a steady rate for the first half and ahead for the second, so going out the picture lags the walls, then leads. The gap is largest about a fifth of the way along in time (t = 0.21, where the eased value is 0.115 against 0.211): 0.096 × `Travel`, 3 cm for the house's chest. In the first half you'd stop up to 3 cm short of the drawer as drawn; in the second half you could brush up to 3 cm into it.

</details>

## Further reading

- *Game Programming Patterns* by Robert Nystrom: "Component" (behaviour and presentation kept apart) and "Object Pool" (why games avoid garbage in the loop).
- On z-fighting and depth precision: any explanation of "depth buffer precision z-fighting". This project also pushes every face back a little in depth so its own edges win (`DepthBias` in MeshInstance), which is why coplanar faces fight here even more than usual.
