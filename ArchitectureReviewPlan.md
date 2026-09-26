# Architecture, design and speed: review plan

Reviewed 2026-09-26, by reading the code and running the tests (92 pass). The game was not run, so nothing here is a measured frame time. It replaces `BasicWorldReviewPlan.md` (in git history), whose finished work is listed under "Already done" and whose unfinished items are carried into section 5.

The aim: a codebase a team of three can work in without treading on each other. A sensible split is **world/simulation** (`World.Core`, `World.Buildings`), **rendering** (`MeshCore.Library`, `MeshRendering`, `World.Rendering`) and **content** (`MeshProps`, the districts in `Basic.World`).

## 0. How it fits together

```
Basic.World (exe) ──► World.Rendering ──► MeshRendering ──► MeshCore.Library
      │                     │                                    ▲
      ├──► World.Buildings ─┴──► World.Core   (simulation: no GraphicsDevice)
      └──► MeshProps (procedural props) ─────────────────────────┘
Basic.Levels (exe), Basic.Models (exe): the same libraries, smaller games
World.Core.Tests ──► World.Core, World.Buildings
VectorViktor, LoadingModelMeshes: old prototypes, self-contained (see 1.5)
```

What works well and should stay:
- **`IDistrict` + `WorldBuilder`.** A new part of the world is a class and one line in a list.
- **The `IGround` decorator chain** (`PhysicsWorld` → `BuildingGround` → `Terrain`).
- **`World.Core` has no graphics dependency**, which is why it can be tested.
- **`MeshSource` + `MeshCache`.** A mesh is one key, one builder and one palette; the debug check catches a key reused for a different mesh.
- **`MeshBatch`.** Frustum culling, faces then edges, far plane at `FogEnd`.
- **`RetroGame`.** The window, low-res target, keys and screenshot harness written once.
- **`BillboardDesign`.** A design is a record holding a key and a painter; adding a sign is one design and one list entry.

## Already done (from the old plan)

Draw calls cut (one range per colour slot), culling and draw order (`MeshBatch`), terrain chunk build cost, `MeshInstance` defaults, empty buffers, mesh cache keys, palette ownership, disposed-mesh guard, `MeshSource`, shared `Geometry2D` and `WorldConstants`, `IDistrict` + `WorldBuilder`, the sim/render split into `World.Rendering`, `RetroGame`, project renames, `Directory.Build.props` with pinned packages, Basic.Levels frozen with its walking code moved out, the depth-clear removal in Game2.

## 1. Structure and separation of concerns

Done 2026-09-26, except 1.5 (left for you to decide). Checked by fingerprinting the built world before and after: pads, terrain heights over 26,000 points, bare-ground tests, pools, buildings, fixtures (mesh key and transform), windows, moving parts, portals, starts, things and walls all come out identical. The only difference is the order the pads are listed in, which changes no height.

### 1.1 `Neighbourhood.cs` split — done
The 694-line file is now:
- `Street.cs` (`Street`): houses, back-garden fences, pool, billboards, and the street's road.
- `Lane.cs` (`Lane`): the lane's road, the two cottages, the corridor, the portals between them, and the starts.
- `RoadNetwork.cs`: road pieces (`Straight`, `Junction`, `Bend`), their pads, meshes and the no-grid-under-tarmac test. A district lays its own network.
- `Billboard.cs`: one billboard's pad, posts and mesh. Adding a sign is a design in `BillboardDesigns.cs` and one line in `Street`'s list.
- `LaneCottage.cs`, `Parlour.cs`, `Hangar.cs`: the cottage and what is seen through its windows.
`Lane` and `Street` are the only files that name the districts' pieces, so two people can work on the street and the lane without touching the same file.

### 1.2 Districts are instances — done
`Street` and `Lane` are ordinary classes implementing `IDistrict` with public instance members, and `Town` is converted the same way (its `Pads` and `Buildings` are instance members, its centres private). The old public statics (`Neighbourhood.Paved`, `Pads`, `Fixtures()` and the rest) are gone. Tables of constant data stay `static readonly`.

### 1.3 `District.cs` split — done
`District.cs` holds only `IDistrict`. `Fixture`, `Portal`, `Start` and `Thing` have a file each. `Window` and `ScenePart` moved to `MeshRendering` beside `WindowPortals`, since they are drawing types with no `Basic.World` dependency; `ScenePart.Still` is a helper there.

### 1.4 Leftover folders — done
`BasicTests`, `MeshLoader` and `MeshRawData` (untracked build output and `.vs` files) are deleted.

### 1.5 Old prototypes in the solution — open, your call
`VectorViktor` (1,155 lines) and `LoadingModelMeshes` (777 lines) have their own low-res target, box building, input and edge drawing, and duplicate what `RetroGame`, `MeshCore.Library` and `MeshProps` now provide. If nobody works on them, move them to an `archive/` folder and out of `VectorViktor.slnx` so they stop being maintained by accident. `Basic.Levels` is already frozen: the same decision applies (keep it building, or archive it).

### 1.6 Comment clean-up — done
The duplicated `Window` paragraph and the repeated sentence in the `TerrainView` header are gone.

## 2. Render speed

Measure before changing anything: the screenshot harness (`BASIC_WORLD_SHOT`) already writes draw calls, meshes drawn and culled, and terrain build time. Record those at every start position first, so each change here has a before and after.

### 2.1 Draw calls: the effect re-apply per colour — medium payoff, bigger change
`MeshInstance.DrawRange` calls `pass.Apply()` for every colour range, which re-uploads the whole `BasicEffect`. The town start makes about 409 calls for 64 meshes. See 5.1 for the palette-shader route. Cheaper first steps:
- Sort or group instances so the effect's state changes less.
- Drop `DiffuseColor` uploads for ranges that repeat the previous colour.

### 2.2 Static matrices rebuilt every frame — small
`MeshInstance.World` builds four matrices per call, and `MeshBatch.Add` calls it for every instance every frame. Terrain chunks and fixtures never move: set `Transform` once when they're created (chunks: identity).

### 2.3 Window portals draw the world twice — watch
While a window is open, `WorldView.Collect` runs again from the window's side and the whole scene is drawn into the portal. It's only near windows, but it's the largest cost spike in the frame. Keep an eye on it in the draw-call report at `window` and `hangar` starts; if it hurts, cap what the second collect includes (nearer far plane, no interiors).

## 3. Efficiency and run time

### 3.1 Collision cost grows with the size of the world — high
`BuildingGround.KeepOut`, `Obstructs` and `ClearLine` loop over every wall in the world ([BuildingGround.cs:188](World.Buildings/BuildingGround.cs#L188), and lines 247 and 262), `KeepOut` twice over. That is every fence, post, cottage wall and door leaf, for every walker and every body, every tick. Rooms already get a broad-phase test (`room.Near`); free-standing walls don't.
- Bucket the walls into a uniform grid (or per-building lists) and only look at the cells the walker's box touches.
- `AllWalls()` is an iterator, so it also allocates on each call: iterate the two lists directly.

### 3.2 Small per-frame allocations — small, batch them together
None is a hot spot; together they mean garbage every frame.
- A closure per chunk cell: [TerrainView.cs:87](World.Rendering/TerrainView.cs#L87), and `new[] { camera }` at line 72.
- `new[] { (…) }` walker array every tick: [Game1.cs:152](Basic.World/Game1.cs#L152).
- `new[] { UnitX, UnitZ }` per body per tick: [PhysicsWorld.cs:175](World.Core/Physics/PhysicsWorld.cs#L175); `new List<Body>` per tick at line 286; `new List<…>` at line 474.
- The slide candidates array: [CharacterController.cs:248](World.Core/Movement/CharacterController.cs#L248).
- `Window`: `Corners()`, `Matrix.Invert` in `Scene` and `Through`, and `BoundingBox.CreateFromPoints` each frame ([Window.cs:24](MeshRendering/Window.cs#L24), [WorldView.cs:106](Basic.World/WorldView.cs#L106)); `_open.Select(...)` at line 126. A window's frame never changes: work these out once in its constructor.
- Hoist arrays into `static readonly` or fields; sort in place.

### 3.3 `AddPolygon` trusts its input — small
`MeshBuilder.AddPolygon` fans triangles from the first point, which is only right for convex polygons and nothing checks that. A concave outline draws wrongly with no error. Add a debug-build assertion (cross products all one sign), and say so in the doc comment.

## 4. Testing and safeguards

- **The rendering side has no tests.** `MeshBuilder`, the ~37 `MeshProps` builders (billboards included), `MeshBatch` and `BoundingBox` culling are untested, because `MeshBuilder.Build` needs a `GraphicsDevice`. Split it: a device-free step returns the vertex arrays, ranges and bounds; `Build(device)` uploads them. Then test range tiling, that every mesh's `Palette()` covers its slots, and that no builder produces an empty or NaN mesh.
- **No CI.** A workflow that restores, builds the solution and runs `World.Core.Tests` on every push is enough.
- **Nullable checks are off** in the games, `VectorViktor`, `LoadingModelMeshes` and the tests. Turn them on for `Basic.World` next, since it's now mostly plumbing.
- **Shot regression.** The screenshot harness could compare against saved images at a handful of starts; useful for the visual work, but only once the results are stable.

## 5. Carried over from the old plan (unfinished)

Each is tied to a feature or a measured problem; do it when that arrives.

### 5.1 Palette shader and instancing (old 1.4)
Give each vertex a colour slot (a `byte` beside the position) and pass the palette as a constant array, so every mesh is one solids draw and one edges draw. The palette stays per instance (the player's wet darkening still works) and repeated props (fence runs, road straights, crates) can be instanced. Do 2.1's cheaper steps first; take this on only if the draw-call count still matters in the measurements.

### 5.2 Fixed timestep and interpolation (old 1.6)
`IsFixedTimeStep` is never set, so MonoGame already calls `Update` at 60 Hz and the accumulator in `Game1.UpdateWorld` rarely does anything; on a 144 Hz display you still see 60. Option: set `IsFixedTimeStep = false`, keep the accumulator, and interpolate poses (player, drone, bodies, doors) by `_pending / StepTime` when drawing. Also try a near plane of 0.15–0.2 (now 0.1): it decides how much depth bias the faces need.

### 5.3 Outline throttling, before trees go in (old 1.6)
Every instance with an outline (trees, bush, space plane) recomputes it whenever the camera moves, and `DrawOutline` inverts two matrices per draw ([MeshInstance.cs:142](MeshRendering/MeshInstance.cs#L142)). None of these are in Basic.World in numbers yet. Before a forest: skip the recompute until the eye has moved a small fraction of its distance from the tree, or cap recomputes per frame (round robin), or use fixed edges beyond some distance.

### 5.4 Terrain chunks off the main thread (old 3.5, second half)
The chunk lookup is already thread-safe. Still to do: build the vertex arrays on a worker and create the `MeshData` on the main thread (GPU upload must stay there), so `BuildsPerFrame` (4) stops being a hitch risk. Needs 4's device-free `MeshBuilder` step. Only if walking into new country stutters.

### 5.5 A registry of walkers (old 3.7 and 4.4)
Only the player blocks doors (`StepDoors` is given one walker), and `PhysicsWorld.PushWalker` and `Player.Step` each take one controller. For NPCs, keep an `IReadOnlyList<CharacterController>` (or an `IWalker`: feet, radius, height, velocity) in the world, so door blocking, pushing and "who's stood on this body" all work over the list.

### 5.6 Tests for the rendering-side builders (old 5.5)
Covered by section 4's first item.

## 6. Suggested order

| Phase | Items | Why |
|---|---|---|
| 1. Quick wins | 3.3, CI (4) | Small, safe, nothing else depends on them |
| 2. Baseline | Measure draw calls and frame times at every start | Everything in 2 and 5.1 needs a before and after |
| 3. Collision | 3.1 (wall grid), then 3.2 | The one cost that grows with the world; the allocations are worth fixing while in the same code |
| 4. Structure | 1.1–1.4 and 1.6 done; 1.5 open | Lets three people work on districts and rendering without colliding |
| 5. Testability | 4's device-free `MeshBuilder` step, then the mesh tests | Also unlocks 5.4 |
| 6. As needed | 2.1, 2.2, 5.1–5.5 | Each tied to a feature (trees, NPCs, high-refresh displays) or a measurement |
