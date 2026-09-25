# Basic.World review: architecture, rendering speed, and cleanup plan

Covers `Basic.World` and what it builds on: `World.Core`, `World.Buildings`, `MeshCore.Library`, `MeshLoader`, `MeshRawData`. `Basic.Levels` and `Basic.Models` are included only where they share code or duplicate it. This is a plan to come back to.

## Status (2026-09-25)

Done:
- **3.3 `MeshInstance` defaults.** Defaults are now identity. `YawSpeed`, `PitchSpeed` and `Update` are gone, and so are the `Placed`/`Place` helpers. Basic.Models' spin moved into `Basic.Models/SpinningMeshInstance.cs`.
- **2.1 `IDistrict` + `WorldBuilder`.** `Countryside`, `Town` and `Neighbourhood` are districts. `WorldBuilder.Build` returns a `BuiltWorld`. The barn's crates moved from the scenery list into `Town`. Each district now owns its starts.
- **1.2 culling, 1.3 draw order, 2.2 render list.**
  - `MeshData.Bounds` and `MeshInstance.BoundsIn(world)` give every mesh a box to cull against.
  - `MeshLoader/MeshBatch.cs` frustum-culls each instance and draws all faces, then all edges.
  - `WorldView` and `BuildingView` gather the scene into the batch. A building is skipped when its shell box is out of view. Its interiors are skipped when the eye is more than 10 m from it and every door is shut.
  - The far plane is now `FogEnd`, so the frustum also culls what the fog would hide.
  - Side effect: distant buildings no longer show their interior room lines through their outer walls. That was a depth-bias artefact.
- **1.5 terrain chunks.** Each triangle's normal, rock flag and colour is worked out once. About 390 ms → 245 ms for 52 chunks (Debug build), pixel-identical, and no extra height chunks are worked out.
- **3.5 (part).** `Terrain`'s lazy chunk fill is thread-safe. Chunk meshes are still built on the main thread.
- **1.1 + 4.5 fewer draw calls.**
  - `MeshBuilder.AddTri`, `AddQuad` and `AddPolygon` now take the colour slot, and `Build()` joins each slot's triangles into one draw range. `AddSolidRange` is gone.
  - The hand-written per-colour grouping is gone from 13 builders:
    - Basic.World: `TerrainMesh`
    - World.Buildings: `BuildingMesh`, `RoomMesh`, `WallStair`
    - MeshRawData: `FenceMesh`, `BillboardMesh`, `PoolSurroundMesh`, `OakMesh`, `SpikyBushMesh`, `TreeMesh`, `RoadBuilder`, `StaircaseBuilder`, and `CarMesh`'s wheel passes
  - Draw calls per mesh: player 22 → 10, drone 22 → 9, door leaf 10 → 7.
  - `MeshBatch.DrawCalls` goes in the screenshot report. For example, the town start now makes 409 calls for 64 meshes.
- **4.1 `MeshSource`.**
  - `MeshCore.Library/MeshSource.cs` holds the mesh key, builder and palette together. `MeshCache.CreateInstance(device, source)` gets or builds the mesh and returns an instance of it.
  - `PropSpec`, `Thing`, `Fixture`, Game2's `Piece` and Basic.Models' two `Showcase` records now hold a `MeshSource` instead of repeating its three fields.
  - `CrateMesh`, `DoorMesh` and `BuildingMesh` each have a `Source(...)` method.
  - `RoomSpec` no longer depends on `GraphicsDevice`.
- **4.2 `World.Core/Geometry2D.cs`.** One copy each of `PushOutOfBox`, `NearestOnSegment`, `SegmentHitsBox`, `Cross`, `Outward`, `InPolygon`, `SignedArea`, `ClipToHalfPlane`, `Triangulate` and `PointInTriangle`. Their duplicates in `PhysicsWorld`, `RoomSpec`, `RoomMesh`, `Building`, `BuildingGround` and `WallStair` are gone.
- **4.3 Shared constants.**
  - `World.Core/WorldConstants.cs` holds gravity and the walker's height, radius, eye height, step height and speeds. `CharacterController`, `PhysicsWorld`, `Player`, `RoomSpec` and Basic.Levels refer to it.
  - `MeshLoader/RetroStyle.Background` holds the background colour for the three games that use it.
- **5.3** `MonoGame.Extended` is removed from `MeshRawData` and `VectorViktor`.
- **5.4** `MeshRefactorPlan.md` and `MeshGeneralisationPlan.md` are deleted (they're still in git history).

Screenshots at all 17 starts were compared with the old build. Any differences are those interior lines disappearing.

## 0. How it fits together now

```
Basic.World (exe) ──► World.Buildings ──► World.Core        (simulation: no GraphicsDevice)
      │                     │  └────────► MeshCore.Library  (MeshBuilder, MeshData = GPU buffers)
      │                     └───────────► MeshLoader        (MeshCache, MeshInstance = the renderer)
      ├──► MeshRawData (procedural props) ──► MeshCore.Library
      └──► MeshLoader
Basic.Levels (exe) ──► World.Buildings, MeshLoader, MeshRawData
World.Core.Tests ──► World.Core, World.Buildings
```

What already works well and should stay:
- **The `IGround` decorator chain** (`PhysicsWorld` → `BuildingGround` → `Terrain`). Each layer adds its own surfaces and passes everything else down, and the default interface methods keep simple grounds short. It's easy to test and easy to extend.
- **World.Core has no graphics dependency**, and the tests (79 of them) cover the simulation well.
- **Meshes are shared through the cache.** Palettes are per instance and geometry is position-only, so one buffer serves every sofa whatever its colour.
- **Terrain is chunked and lazy**, and `TerrainView` builds chunks nearest-first with a per-frame budget.

---

## 1. Rendering speed (ordered by payoff for the effort)

### 1.1 One draw range per colour slot, not per box (high payoff, small change)
`MeshBuilder.AddBox` opens **3 draw ranges per box** ([MeshBuilder.cs:48-57](MeshCore.Library/MeshBuilder.cs#L48-L57)), and `AddFrustum` and `AddTube` open 1–3 each. `MeshInstance.DrawRange` does a `pass.Apply()` plus a `DrawPrimitives` for every range. So:
- `PlayerMesh`: 7 boxes = 21 ranges, so 22 draw calls including edges. `DroneMesh` is about 21 ranges.
- Every `SeatingBuilder` sofa, sideboard, TV and so on gets the same multiplication.

Five builders already work around this by hand, collecting faces per slot and emitting them at the end: `TerrainMesh`, `BuildingMesh`, `FenceMesh`, `BillboardMesh`, and parts of `RoomMesh` and `StaircaseBuilder`.

**Change:** `MeshBuilder` keeps one triangle list per colour slot internally, and `Build()` joins them into one range per slot that is actually used. The API becomes `AddTri(slot, a, b, c)`, `AddQuad(slot, …)` and `AddPolygon(slot, …)`, and `AddSolidRange` goes away. That also removes a fragile contract ("add exactly N primitives after it", which is only checked at runtime in the `MeshData` constructor). Then delete the hand-written bucketing in the five builders.

Expected: draw calls per instance fall to about `PaletteSize + 1`. The player goes from 22 calls to 10, a crate from 4 to 4, and furniture drops a lot.

### 1.2 Culling for everything, not just terrain (high payoff)
Only terrain chunks are frustum-culled ([TerrainView.cs:93-101](Basic.World/TerrainView.cs#L93-L101)). Every building shell, **every room interior with all its props**, every door, fixture and scenery body is drawn every frame, including those beyond `FogEnd` (95 m), where fog has already turned them fully into background colour.

**Change:**
- Give `MeshData` a local `BoundingBox`. `MeshBuilder.Build` already sees every vertex, so this costs nothing to compute.
- `MeshInstance` exposes world bounds (the local box transformed by `World`, cached while the transform hasn't changed).
- Skip an instance if its bounds are outside the frustum **or** further than `FogEnd` from the eye. Share one `BoundingFrustum` per frame instead of `TerrainView` making its own.
- Room interiors: also skip them when the eye isn't inside the building's outer outline and every door into it is shut. That makes almost all interiors free when you're outdoors. A cheap first version: draw a building's `RoomView`s only when the eye is within about 20 m of it.

### 1.3 Two passes: all faces, then all edges (medium payoff, small change)
`MeshInstance.Draw` swaps the rasterizer state twice per instance and saves and restores `World`, `DiffuseColor` and `VertexColorEnabled` for each one ([MeshInstance.cs:65-90](MeshLoader/MeshInstance.cs#L65-L90)). Edges are depth-tested against faces, so the order doesn't affect correctness. Draw every visible instance's solids with `_faceRasterizer` set once, then every instance's edges with the normal state. Set `VertexColorEnabled = false` once per frame, not per instance.

### 1.4 A palette shader: one draw call per mesh (bigger change, later)
Colour comes from `DiffuseColor`, one draw per range. A small custom effect could take the slot index as a vertex attribute (a `byte` next to the position) and the palette as a constant array (for example `float4 Palette[32]`). Then every mesh is **one** solids draw plus one edges draw, the palette is still per instance (so the player's wet darkening still works), and hardware instancing of repeated props (fence runs, road straights, crates) becomes possible later. Do 1.1–1.3 first. This is only worth it if the draw call count still matters after those.

### 1.5 Terrain chunk build cost (medium payoff, removes stutter)
`TerrainMesh.Build` repeats a lot of work per chunk ([TerrainMesh.cs:58-115](Basic.World/TerrainMesh.cs#L58-L115)):
- `foreach (var southWest in new[] { false, true })` allocates an array **per cell** (1,024 per chunk).
- `Band()` calls `IsWalkable`, which calls `NormalAt` again straight after `NormalAt` was called for the shade. The edge pass then calls `IsRock` about 3 more times per cell through `Rock(...)`, and each call rebuilds the triangle and recomputes the normal.
- The normal can come from the triangle's own cross product instead of `NormalAt`, which searches for the cell again.

**Change:** compute each triangle's normal, band and rock flag once, into a `(cellsX + 2) × (cellsZ + 2) × 2` array with a one-cell border for the neighbour tests, and use that array in both passes. That's probably 3–5× faster per chunk, which leaves room to raise `BuildsPerFrame` or to move building off the main thread (see 3.3).

### 1.6 Smaller items
- **Far plane**: projection is `0.1 … 300` but nothing past about 110 m can be seen through the fog. Setting the far plane to `FogEnd + 20` gives a little more depth precision for free. The near plane of 0.1 is what really costs precision, and so decides how much `DepthBias` is needed. Try 0.15–0.2 and see whether first-person clipping still looks right.
- **Timestep**: MonoGame's default `IsFixedTimeStep = true` already calls `Update` at 60 Hz, so the accumulator in `Game1.Update` rarely does anything, and on a 144 Hz display you still only see 60 frames. Option: `IsFixedTimeStep = false`, keep the accumulator, and interpolate poses (player, drone, bodies, doors) by `_pending / StepTime` when drawing.
- **Per-frame allocations** (GC churn, not a hotspot yet):
  - `DampenPlayer`'s tuple array
  - the walker array passed to `StepDoors` every tick
  - `BuildingGround.AllWalls()`, an iterator allocated on every `KeepOut`, `Obstructs` and `ClearLine` call
  - `PhysicsWorld.MoveAcross`'s `new[] { UnitX, UnitZ }` per body per tick
  - `Settle`'s `new List<Body>` per tick
  - `TrySlide`'s candidates array
  - `TerrainView.Update`'s `wanted` and `gone` lists per frame

  Hoist them into fields or static readonly arrays, or iterate the two lists directly.
- **Outlines (`OutlineData`)**: none of the meshes that use them (trees, bush, space plane) are in Basic.World yet. When trees go in, note that every instance recomputes its outline **every frame the camera moves**: edges × 8 pieces × ray tests. With a forest that would be the biggest CPU cost in the frame. Before adding trees in number, either skip the recompute until the eye has moved more than about 1% of the distance to the tree, or cap recomputes per frame (round-robin), or fall back to fixed edges beyond some distance.

---

## 2. Architecture and extensibility

### 2.1 `Game1` is doing five jobs
[Basic.World/Game1.cs](Basic.World/Game1.cs) is the composition root (which districts exist, how their pads, pools, walls and buildings are joined), the input mapper, the renderer (with a separate list per kind of drawable), the low-res presenter, and the screenshot/test harness. Adding a third district means editing `LoadContent` in about five places (pads, pools, buildings, walls, things) plus the `Starts` table.

**Change: bring in a district abstraction.**
```csharp
public interface IDistrict
{
    IEnumerable<TerrainGenerator.Pad> Pads { get; }
    IEnumerable<Pool> Pools(Terrain terrain);          // Neighbourhood.SwimmingPool
    IEnumerable<Building> Buildings(Terrain terrain);
    IEnumerable<WallSegment> Walls(Terrain terrain);
    IEnumerable<Placement> Fixtures(Terrain terrain);  // road, fences, billboard (see 4.1)
    IEnumerable<Scenery.Thing> Things(PhysicsWorld world, Terrain terrain);
    IReadOnlyDictionary<string, (Vector2 at, float yaw, float dropFrom)> Starts { get; }
}
```
`Town`, `Neighbourhood` and the current `Scenery` each implement it. A `WorldBuilder.Build(IEnumerable<IDistrict>)` returns the terrain, ground, physics world and a render list. `Game1` then just holds `IDistrict[] { new Town(), new Neighbourhood(), new Scenery() }`. (They're static classes now, so they need to become instances or singletons.)

### 2.2 A render list instead of separate lists
`_waterViews`, `_buildingShells`, `_fixtures`, `_rooms`, `_doors` and `_things` are drawn by near-identical loops. Replace them with:
- a list of **static** `MeshInstance`s (water, shells, fixtures, room shells and props), whose transforms are set once, and
- a list of **dynamic** drawables, whose transform comes from a delegate each frame (doors from `DoorMesh.Transform(door)`, bodies from `Rotation(turn) * body.Pose`, the player and the drone).

Culling (1.2) and the two-pass draw (1.3) then live in one place, a small `SceneRenderer`, instead of being copied into `TerrainView.Draw`, `RoomView.Draw` and `Game1.Draw`.

### 2.3 A shared game shell for the four executables
`Basic.World.Game1`, `Basic.World.Game2`, `Basic.Levels.Game1` and `Basic.Models` each repeat:
- the low-res target and integer-scale present (the same 15 lines)
- `BackgroundColor`, the window and low-res sizes
- the borderless-fullscreen workaround
- the C (or Space), L, F11 and Escape toggles
- `Pressed(keyboard, key)` edge detection and `Axis(...)`
- the `BASIC_WORLD_SHOT` screenshot harness (two versions with different key sets)

Pull these out into a `RetroGame : Game` base class, or into small parts (`LowResPresenter`, `KeyEdges`, `ShotHarness`), in a shared project. That could be `MeshLoader` renamed, see 5.1. The HUD milestone on the roadmap will also need somewhere to draw text over the low-res frame, and the presenter is the natural place.

### 2.4 World.Buildings mixes simulation and rendering
The project comment says "the walking needs no GraphicsDevice, so it's testable", but:
- `PropSpec` holds a `Func<GraphicsDevice, MeshData>` and a `Color[]`, so a simulation-side spec depends on graphics types
- `WallStair` is both simulation (`Ramps()`, `Hatch()`) and mesh (`Build`)
- `RoomMesh`, `BuildingMesh`, `DoorMesh` and `RoomView` live next to `BuildingGround`
- so `World.Core.Tests` pulls in MonoGame graphics and `MeshLoader` through `World.Buildings`

**Change:** split it into `World.Buildings` (specs, `Building`, `BuildingGround`, `Door`, `WallStair`'s geometry) and `World.Buildings.Rendering` (the meshes and `RoomView`). `PropSpec` then carries a **mesh reference** (see 4.1) instead of a build delegate. The same applies to `TerrainMesh`, `WaterMesh`, `CrateMesh`, `PlayerMesh` and `DroneMesh` in Basic.World (see 2.5).

### 2.5 Mesh builders are spread across four projects
Procedural meshes live in:
- `MeshRawData`: furniture, road, fence, billboard, trees
- `World.Buildings`: room, building shell, door, stair
- `Basic.World`: terrain, water, crate, player, drone
- `Basic.Models`: whatever it builds itself

`CrateMesh`, `PlayerMesh` and `DroneMesh` are generic enough that other games would want them. Rule to adopt: **generic props go in `MeshRawData`; meshes derived from a simulation type (`Terrain`, `Pool`, `Building`, `RoomSpec`) go in a rendering project for that type**. Game projects then hold no mesh builders.

### 2.6 Two walking models share `RoomSpec`
`Basic.Levels` walks with its own code: `RoomSpec.KeepInside`, `RoomView.PushOutOfProps`, teleporting `DoorSpec`s, and its own `WalkSpeed`, `TurnSpeed` and `PlayerRadius` constants. `Basic.World` uses `CharacterController` + `BuildingGround`. `RoomSpec` carries both APIs: `Doors`, `FindDoor`, `KeepInside`, `DistanceToWall` and `AlongWall` are used only by Basic.Levels.

Decide which way to go:
- **(a)** Move Basic.Levels onto `BuildingGround` + `CharacterController`, with its rooms as buildings floating in the void. Then drop `DoorSpec` teleports, or turn them into a `Portal` that `BuildingGround` understands. After that, `KeepInside`, `PushOutOfProps` and the Basic.Levels walking code can all be deleted.
- **(b)** Freeze Basic.Levels as a legacy demo and move its legacy-only `RoomSpec` members into an extension class in Basic.Levels, so `RoomSpec` stops growing two ways.

(a) is better if Basic.Levels is still meant to be played. Otherwise (b).

---

## 3. Instabilities and latent bugs

1. **Empty buffers throw.** `MeshBuilder.Build` always creates both buffers, and a zero-length `VertexBuffer` fails. `TerrainMesh` works around it with a degenerate line ([TerrainMesh.cs:117-119](Basic.World/TerrainMesh.cs#L117-L119)). Any mesh with no edges, such as a future edge-less water or glass mesh, or no solids, would crash. **Fix:** make `MeshData.Edges` (and `Solids`) nullable and skip drawing them when null. Then remove the terrain workaround.
2. **Mesh cache keys are chosen by hand and never checked.** `MeshCache.GetOrAdd(key, build)` silently returns whatever was stored under `key` first. `"atticladder"` and `"barnladder"` are safe only because their names differ. Two `LadderMesh.Build(height: …)` calls under one key would share the wrong mesh without any error. `"water" + i` depends on list order. **Fix:** derive keys from the build parameters (as `CrateMesh.Key` and `DoorMesh.Key` already do), or use typed record keys (for example `record LadderKey(float Height, float Lean)`). In debug builds, check that a hit's key came from the same builder.
3. **`MeshInstance` defaults are left over from the spinning-ingot demo.** Position is `(0.7, 0, 0)`, pitch 20°, yaw 35°, and there are `YawSpeed` and `PitchSpeed` values plus an empty `Update()`. That's why `Game1.Placed`, `Game2.Placed` and `RoomView.Place` all exist: to undo them. Forgetting to call one gives a tilted mesh. **Fix:** make the defaults identity, delete `YawSpeed`, `PitchSpeed` and `Update`, and delete the three `Placed` helpers. Move anything in Basic.Models that relies on the spin into Basic.Models.
4. **Palettes are shared by reference.** `MeshInstance` keeps the caller's `Color[]` without copying it. `DampenPlayer` relies on that (it mutates `_playerPalette` in place), but it also means two instances given the same `XMesh.Palette(...)` array change together. Make it explicit: either copy the array in the constructor and add `SetColor(slot, color)`, or document that the palette is shared on purpose.
5. **Lazy terrain chunks aren't thread-safe.** `_chunks[i] ??= FillChunk(...)` and `ChunksMade++` in [Terrain.cs:79-85](World.Core/Terrain/Terrain.cs#L79-L85) race if chunk meshes are ever built on a worker thread while physics queries the terrain. Before moving `TerrainView` building off the main thread, use `Interlocked.CompareExchange` for the chunk slot and `Interlocked.Increment` for the counter. GPU upload (`SetData`) must still happen on the main thread: build the vertex arrays on the worker and create the `MeshData` on the main thread.
6. **Two ownership models for GPU meshes.** `MeshCache` owns shared meshes, while `TerrainView` owns and disposes its chunk meshes itself. That's fine, but `MeshInstance` doesn't know which kind it has borrowed. If anything ever keeps a chunk's `MeshInstance` after `TerrainView` drops it, it draws a disposed buffer. Leave this alone unless a third owner appears. If one does, add an `IsDisposed` guard in `MeshInstance.Draw` (debug only).
7. **Only the player blocks doors.** `StepDoors` is given only the player as a walker. Future NPCs need a registry of walkers (see 4.3), or a door will swing through them.
8. **Stale comment**: the low-res target is 640 × 256 (2.5 : 1), but the comment says "16:9, so 3 x fits the window" ([Game1.cs:39-40](Basic.World/Game1.cs#L39-L40)). Either the comment or the size is wrong. 3 × 256 = 768, which leaves bars in the 810 window.
9. **Game2 clears the depth buffer after drawing the ground** to stop grid lines showing through the road. It works only because Game2's ground is flat. Basic.World avoids the problem with `Neighbourhood.Paved` instead, which is the approach that generalises. If Game2 is kept, pass it the same `bare` predicate and drop the depth clear.

---

## 4. Code that can be merged or shared

### 4.1 One "placed mesh" record
These four records are almost the same (key + build + palette, plus a placement):
- `Scenery.Thing` (Key, Build, Palette, Turn, plus Body)
- `Neighbourhood.Thing` (Key, Build, Palette, Transform)
- `PropSpec` (Key, Build, Palette, Position, YawDegrees, Half)
- `Game2.Piece` (Key, Build, X, Z, Turn)

**Change:** in `MeshCore.Library`, add
```csharp
public sealed record MeshSource(string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette);
```
and have each of these hold a `MeshSource` plus its own placement. `MeshCache.GetOrAdd(device, source)` and `new MeshInstance(cache, source)` then shorten every call site. After 2.4, the simulation side holds only the key and the rendering side resolves it.

### 4.2 Shared 2-D geometry helpers
Copies of the same maths:
- `PushOutOfBox` is written twice: `PhysicsWorld.PushOutOfBox` and `RoomSpec.PushOutOfBox`. The comment even says "The same rule as RoomSpec.PushOutOfBox".
- Nearest point on a segment is written twice: `BuildingGround.Nearest` and `RoomSpec.NearestOnSegment`.
- The outward normal of an edge (`Building.Outward`), the inward normal (`RoomSpec.Inward`, and again inline in `WallStair`), point-in-polygon (`RoomSpec.InPolygon`), the 2-D cross product (`BuildingGround.Cross`), segment against box (`SegmentHitsBox`).
- Polygon code that both `RoomMesh` and `BuildingMesh` use: `RoomMesh.Triangulate` (internal, but `BuildingMesh` calls it), `ClipToHalfPlane`, `SignedArea`, `CutOut`.

**Change:** add a `World.Core.Geometry` static class (`Plan2D`, or similar) and move them all there. Rendering-only polygon work (triangulate, clip, cut out) can go in `MeshCore.Library` as `Polygon2D` if you'd rather keep it out of World.Core.

### 4.3 Shared constants
- `CharacterController.MaxStepUp = 0.3` and `RoomSpec.DefaultMaxStepUp = 0.3` ("the same as").
- `Gravity` is defined in both `PhysicsWorld` and `CharacterController`.
- `Player.Height` just repeats `CharacterController.Height`, and `Player.EyeHeight` is repeated in Basic.Levels.
- `BackgroundColor (27, 13, 120)` is in every game.
- Basic.Levels has its own `WalkSpeed`, `RunMultiplier` and `TurnSpeed`.

**Change:** add a `WorldConstants` class (or `Walker` for body sizes, `Physics` for gravity) in World.Core, and make the others refer to it.

### 4.4 A "walkers" concept
`PhysicsWorld.PushWalker`, `BuildingGround.StepDoors` and `Player.Step` each take one controller. For NPCs, have the world keep an `IReadOnlyList<CharacterController>` (or an `IWalker` interface: feet, radius, height, velocity). Then door blocking, pushing and "who's stood on this body" all work over the list.

### 4.5 Bucketing by colour slot
This is covered by 1.1. Once `MeshBuilder` buckets by slot itself, about 60 lines of hand-written `faces[slot]` code in five builders can go.

---

## 5. Housekeeping

1. **Project names don't say what the projects do.** `MeshLoader` loads nothing. It's the renderer (`MeshCache`, `MeshInstance`), so something like `MeshRender` would fit. `MeshRawData` isn't raw data; it's procedural prop builders, so something like `Props`. Rename these when convenient, since renaming touches every `using`.
2. **Add a `Directory.Build.props`.** Target frameworks are mixed: `VectorViktor` is `net10.0-windows7.0` and the rest are `net9.0-windows`. `Nullable` and `ImplicitUsings` are on only in `MeshCore.Library` and `MeshRawData`. MonoGame is `3.8.*` (floating). One props file could pin the TFM, enable nullable (as warnings to begin with), and pin the MonoGame version, so a restore can't change the build on its own.
3. **Unused package**: `MonoGame.Extended 6.1.1` is referenced by `MeshRawData` and `VectorViktor`, but no `.cs` file uses it. Remove it from `MeshRawData` at least.
4. **Stale plan documents**: `MeshRefactorPlan.md` and `MeshGeneralisationPlan.md` refer to a `BasicTests` project and an `IngotFrustrum` class that no longer exist. Their work is done (it became `MeshCache`/`MeshData`/`DrawRange`). Delete them, or move them to a `docs/history/` folder.
5. **Tests for the rendering-side builders.** `MeshData`'s range check already runs at construction. A test project that builds each mesh against a headless or `null`-device builder, and checks range tiling and `PaletteSize` against each mesh's `Palette()` length, would catch palette/slot mismatches before running the game. That needs `MeshBuilder` to be able to output CPU-side arrays without a device, which fits with 3.5's worker-thread change.

---

## 6. Suggested order

| Phase | Items | Why this order |
|---|---|---|
| 1. Quick wins | 3.3 (instance defaults), 3.1 (nullable edges), 3.8, 5.3, 5.4 | Small and low-risk, and they remove workarounds other changes would otherwise have to carry |
| 2. Draw-call cut | 1.1 (slot bucketing in MeshBuilder) + 4.5, 1.3 (two-pass draw) | Largest render saving with no change in how things look. Take a before/after screenshot with `BASIC_WORLD_SHOT` to compare |
| 3. Culling | 1.2 (bounds, frustum and fog-distance culling, then interiors), 2.2 (render list) | Needs MeshData bounds; best done together with the render list so culling lives in one place |
| 4. Structure | 2.1 (`IDistrict` + `WorldBuilder`), 4.1 (`MeshSource`), 4.2, 4.3 | Makes the next district or level a matter of adding, not editing |
| 5. Split sim and render | 2.4, 2.5, 5.1 | Mostly moving files around. Do it once the pieces above have settled |
| 6. Terrain throughput | 1.5, then 3.5 + background chunk building | Only if walking through the far country stutters |
| 7. Later / as needed | 1.4 (palette shader and instancing), 1.6 (timestep interpolation), outline throttling (before adding trees), 4.4 (walkers), 2.3 (shared game shell), 2.6 (Basic.Levels decision) | Each is tied to a feature: NPCs, trees, HUD, high-refresh displays |
