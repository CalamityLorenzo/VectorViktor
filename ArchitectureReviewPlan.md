# Architecture, design and speed: review plan

Reviewed 2026-09-26, when phases 1 to 5 of section 6 were done; 2.1, 5.1 (the palette shader) and 5.3 were done on 2026-10-02, and the rest of section 5 reviewed then (5.7). What each found and changed is marked in its section. At the first review there were 139 tests (98 in `World.Core.Tests`, 41 in `Meshes.Tests`). Frame times come from `Basic.World.Benchmark` (see 2), which draws with no window; the game itself was not run. It replaces `BasicWorldReviewPlan.md` (in git history), whose finished work is listed under "Already done" and whose unfinished items are carried into section 5.

The aim: a codebase a team of three can work in without treading on each other. A sensible split is **world/simulation** (`World.Core`, `World.Buildings`), **rendering** (`MeshCore.Library`, `MeshRendering`, `World.Rendering`) and **content** (`MeshProps`, the districts in `Basic.World`).

## 0. How it fits together

Since 2026-09-27 the districts, `WorldBuilder` and the world's renderers are in two libraries, `World.Maps` (what a map is,
building and drawing one) and `Maps.Home` (the home map's districts), so every app can share them: see
[ToolsPlan.md](ToolsPlan.md) step 1. `Basic.World` below now holds only the game.

```
Basic.World (exe) ──► Maps.Home ──► World.Maps ──► World.Rendering ──► MeshRendering ──► MeshCore.Library
      │                                              │                                    ▲
      ├──► World.Buildings ──────────────────────────┴──► World.Core   (simulation: no GraphicsDevice)
      └──► MeshProps (procedural props) ──────────────────────────────────────────────────┘
Basic.World.Benchmark (exe) ──► Maps.Home, World.Maps: draws and steps every start with no window (see 2)
Basic.Levels (exe), Basic.Models (exe): the same libraries, smaller games
World.Core.Tests ──► World.Core, World.Buildings
Meshes.Tests ──► MeshCore.Library, MeshProps, World.Rendering, World.Maps, Maps.Home: every mesh, built with no device
VectorViktor, LoadingModelMeshes: the original prototypes, self-contained (see 1.5)
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

### 1.5 Old prototypes in the solution — decided: kept, isolated
`VectorViktor` is where the whole project started, so it stays. It (and `LoadingModelMeshes`) references no other project and nothing references it; they share only `Directory.Build.props` and the package versions. They now sit in the solution's `Prototypes` folder, and [VectorViktor/README.md](VectorViktor/README.md) says why they are kept as they were and that shared code isn't to be refactored into them. The CI build still builds them, so they can't rot unnoticed.

### 1.6 Comment clean-up — done
The duplicated `Window` paragraph and the repeated sentence in the `TerrainView` header are gone.

## 2. Render speed

### Measured: `Basic.World.Benchmark`
`dotnet run -c Release --project Basic.World.Benchmark -- 300 results/name.md` draws 300 frames at each of the 24 starts into an off-screen 640 x 256 target (a hidden window's swap chain, nothing on screen), steps 600 ticks of the world walking straight ahead, and writes a table: meshes drawn and culled, draw calls, chunks, CPU milliseconds per frame, milliseconds including the GPU finishing, garbage per frame and per tick, and microseconds per tick. Run it in Release for timings and in Debug to exercise the debug-only checks. Results are in `Basic.World.Benchmark/results/`; the numbers are this machine's (an RTX 3050), for comparing before and after.

`WorldRenderer` (now in `World.Maps`) is the frame the game draws, pulled out of `Game1` so the benchmark draws exactly the same one; `HomeMap.Districts()` (in `Maps.Home`, formerly `WorldBuilder.Standard()`) is the list of districts.

What it found (average of the 24 starts, Release):

| | before | after 3.1, 3.2 |
|---|---:|---:|
| draw calls per frame | 284 | 284 |
| draw, CPU ms per frame | 0.13 | 0.14 |
| frame including GPU, ms | 0.14 | 0.14 |
| garbage per frame | not measured | 5 B |
| simulation, µs per tick | 70 | 59 |
| garbage per tick | about 21.7 KB | 130 B |

(The first baseline file predates the garbage columns; the 21.7 KB is from the run just after the wall grid and before the closure fixes.)

- **Drawing isn't the bottleneck.** Even the busiest views (basin, plateau, 500+ draw calls) cost 0.16 ms of CPU a frame, and the GPU keeps up. The palette shader (5.1) is not needed for speed at this scale.
- **The hangar is the slowest view, at 0.37 to 0.49 ms** (three to five times any other), because its trees recompute their outlines every frame (see 5.3). That is the only view where the draw cost stands out.
- **The simulation was the noisier cost**: about 21.7 KB of garbage every tick (1.3 MB a second), which is what 3.2 removed.

### 2.1 Draw calls: the effect re-apply per colour — done 2026-10-02 (with 5.1)
`MeshInstance.DrawRange` called `pass.Apply()` for every colour range, which re-uploads the whole `BasicEffect`. The world did get busier (the droids on show in the workshop yard, each about a hundred parts), and each call was costing 10 to 25 µs: the yard's view made 843 calls and took 13 to 16 ms of CPU a frame. Fixed by 5.1: a mesh's faces are now one draw whatever its colours.

### 2.2 Static matrices rebuilt every frame — small, open
`MeshInstance.World` builds four matrices per call, and `MeshBatch.Add` calls it for every instance every frame. Terrain chunks and fixtures never move: set `Transform` once when they're created (chunks: identity). The measured frame doesn't miss it.

### 2.3 Window portals draw the world twice — watch
While a window is open, `WorldView.Collect` runs again from the window's side and the whole scene is drawn into the portal. In the benchmark, `window` (0.18 ms) and `hangar` (0.49 ms) are the two views that do it.

## 3. Efficiency and run time

### 3.1 Collision cost grows with the size of the world — done
`WallGrid` (`World.Buildings/WallGrid.cs`) sorts the free-standing walls (fences, posts, cottage walls, buildings' walls) into 8 m cells. `KeepOut`, `Obstructs` and `ClearLine` now look only at the walls in the cells the walker or the line touches, in the order the walls were given, so a walker in a corner is pushed out exactly as before. Doors still swing, so their leaves are checked directly. Six tests compare the grid with the old look-at-every-wall loops over 2,000 random cases each (`WallGridTests`): same pushes, same obstruction answers, same points where a line of sight stops.
At today's size (about 60 walls) it saves little (tick 70 to 59 µs); it's the cost that would have grown with every fence added.

### 3.2 Per-frame and per-tick garbage — done
Found with an allocation listener rather than by reading, which showed the real sources were not the ones first suspected. In order of size:
- **`BuildingGround.ClearLine`** (called twice a tick by the drone, once per room per step): `Array.Exists(..., h => h.Contains(local))` made a closure for every room at every step of the line, 5.8 KB a call. Now `HatchSpec.AnyContain`, a plain loop. This was about 11.9 KB of the 12 KB a tick the drone cost.
- **`Terrain.WaterLevelAt`** and `TerrainMesh.Band` looped `foreach` over `IReadOnlyList<Pool>`, boxing an enumerator each time: every body's water test every tick, and every triangle of every terrain chunk built.
- **`StepDoors`**: a closure per door per tick even with the door shut. Now the moving door's work is its own method, called only for a door that isn't at rest.
- Smaller: `TerrainView`'s per-cell closure and interface enumerators, the per-tick walker array in `Game1`, `PhysicsWorld`'s per-tick lists and axis array (now fields), `PushWalker`'s `FindAll`, `CharacterController.TrySlide`'s candidates (now `stackalloc`), and `Window`'s matrices, corners and bounds (now worked out once, when it is made).
After: 130 B a tick, of which about 48 is the benchmark's own walker array.

### 3.3 `AddPolygon` trusts its input — done
In a debug build `MeshBuilder.AddPolygon` and `AddQuad` throw if the fan from the first point folds back (which is when a concave polygon draws wrongly), naming the point. It doesn't refuse concave polygons the first point can still see all of. Every mesh in the game passes (`MeshCatalogueTests`), and the check compiles out in Release.

## 4. Testing and safeguards

- **Rendering-side tests — done.** `MeshBuilder.Build(null)` builds a mesh on the CPU without GPU buffers (`MeshData.Headless`: it can be looked at, and throws if drawn). `Meshes.Tests` uses that to check, for every mesh: whole triangles and lines, finite vertices, every vertex inside the bounds that culling uses, some face with area, and a palette with a colour for each slot its faces use. It covers every `MeshProps` builder (by reflection, so a new one is picked up), every mesh the world draws (fixtures, things, moving parts, window scenes, buildings, rooms, props, doors, water, terrain chunks round the start), and the builder's own rules. `IngotMesh` and `PyramidMesh`, the last two built by hand with buffers, now use `MeshBuilder`.
- **CI — added, not yet run.** `.github/workflows/ci.yml` restores, builds `VectorViktor.slnx` and runs all the tests on every push and pull request, on `windows-latest` with .NET 10. It's the first time this runs anywhere but this machine, so expect to fix a first-run problem (the content pipeline tool is the likely one).
- **Nullable checks are off** in the games, `VectorViktor`, `LoadingModelMeshes` and the tests. Turn them on for `Basic.World` next, since it's now mostly plumbing. Open.
- **Shot regression.** The benchmark's draw-call and mesh counts already act as a rough one (a change to what's drawn shows up in them); a comparison of saved images at a handful of starts is still open, and worth it only once the look has settled.

## 5. Carried over from the old plan (unfinished)

Each is tied to a feature or a measured problem; do it when that arrives.

### 5.1 Palette shader and instancing (old 1.4) — palette shader done 2026-10-02; instancing measured, not worth it yet
- **The palette shader.** `MeshRendering/Shaders/PaletteEffect.fx`: every face vertex carries its colour slot (`VertexPositionSlot`, a float beside the position, built by `MeshBuilder`), and the instance's palette goes to the shader as an array of up to 64 colours (`PaletteEffect`, one per device). Fogged as `BasicEffect` fogs, from whose view, projection and fog it takes its own, so nothing that calls `MeshBatch` or `MeshInstance` changed. Edges and outlines are still drawn with `BasicEffect`. The palette stays per instance (`SetColor`, the walker's wet darkening, colours off and `ColorsOffTint` all work). The shader is compiled by MonoGame's content builder into `Shaders/PaletteEffect.xnb`, kept in the repository and built into `MeshRendering`; `MeshRendering.csproj` compiles it again whenever the `.fx` is newer, if the content builder has been restored (any of the games restores it). Tests: the compiled shader is built in, and every mesh's palette fits it.
- **Measured** (this machine, Radeon 780M, Release, every start): 352 → 125 draw calls a frame on average, 4.38 → 0.54 ms of draw CPU, 5.28 → 0.81 ms a frame including the GPU. The workshop yard standing still: 8.7 → 1.7 ms (Debug 13.2 → 2.4).
- **Instancing: not done.** Counted at every start: a view repeats a mesh about 13 times in 80 (most at the workshop, 48 of 116, where the three droids on show share parts). Instancing those would save about 26 calls a frame, now about 0.1 ms, and needs instanced variants of both the face shader and an edge shader (`BasicEffect` can't instance) and an instance buffer gathered every frame. Worth it when a view repeats a mesh hundreds of times: a forest, a crowd, a car park.

### 5.2 Fixed timestep and interpolation (old 1.6)
`IsFixedTimeStep` is never set, so MonoGame already calls `Update` at 60 Hz and the accumulator in `Game1.UpdateWorld` rarely does anything; on a 144 Hz display you still see 60. Option: set `IsFixedTimeStep = false`, keep the accumulator, and interpolate poses (player, drone, bodies, doors) by `_pending / StepTime` when drawing. Also try a near plane of 0.15–0.2 (now 0.1): it decides how much depth bias the faces need.

### 5.3 Outline throttling, before trees go in (old 1.6) — done 2026-10-02
Every instance with an outline (trees, bush, space plane, and now the droids' rounded parts) recomputed it whenever the camera moved, and testing each piece of each outline edge for being hidden (a ray against the mesh) was most of the cost: in a Debug build, 18 ms a frame with the droids on show. Now (`MeshCore.Library/OutlineData.cs`):
- **Convex surfaces aren't tested against themselves.** Each blob is checked for being convex when the mesh is built (`Blobs`, `ConvexBlobs`); a convex blob can't hide its own outline, so its edges are tested only against the other blobs, and a mesh that's one convex blob isn't tested at all. The droid's rounded parts are all built convex (a test holds them to it).
- **Each blob has a sphere round it**, and a ray that misses it skips the blob's chunks.
- **Recomputed only when it'd show**: when the eye has moved 1% of its distance from the mesh's bounds (half a degree), not on every frame it moves at all (`OutlineView`).
- **Fixed edges far off**: seen smaller than about twenty pixels (`OutlineView.Detailed`), an outline keeps every edge where the surface turns away without the hiding tests. That's what keeps a forest cheap.
- **Sent to the GPU only when it changes** (a dynamic buffer per instance, not `DrawUserPrimitives` every draw), and the eye is worked out once a view, not two matrix inverses per outline.
- With `MeshBatch.SmallestSeen` (anything narrower than a pixel isn't drawn) and `MeshInstance.SeenWithin` (cables only within 6 m), far droids cost next to nothing.
Measured turning on the spot (`Basic.World.Benchmark -- turn <start>`): outlines are now at most 0.1 ms a frame in Release; in Debug 0.6 ms in the hangar (three trees close to, which still need the tests), 0.2 ms or less elsewhere. Still possible if a forest needs it: a cap on recomputes a frame, round robin.

### 5.4 Terrain chunks off the main thread (old 3.5, second half)
The chunk lookup is already thread-safe. Still to do: build the vertex arrays on a worker and create the `MeshData` on the main thread (GPU upload must stay there), so `BuildsPerFrame` (4) stops being a hitch risk. The device-free `MeshBuilder` step it needed now exists (section 4). The benchmark builds 4 chunks a frame without trouble, so do this only if walking into new country stutters in the game.

### 5.5 A registry of walkers (old 3.7 and 4.4)
Only the player blocks doors (`StepDoors` is given one walker), and `PhysicsWorld.PushWalker` and `Player.Step` each take one controller. For NPCs, keep an `IReadOnlyList<CharacterController>` (or an `IWalker`: feet, radius, height, velocity) in the world, so door blocking, pushing and "who's stood on this body" all work over the list.

### 5.6 Tests for the rendering-side builders (old 5.5) — done
See section 4.

## 5.7 Review of what's left in section 5 (2026-10-02)

- **5.2 Fixed timestep and interpolation: not yet.** The juddering seen in the playground was the frame running over 16.7 ms (fixed by 2.1, 5.1 and 5.3) and the stepped ramp, not the timestep. A 60 Hz update looks smooth on a 60 Hz display; this only shows on a faster one (120 or 144 Hz), where it would mean interpolating every moving thing's drawn pose (the player, the drone, bodies, doors, rigs) between ticks. Do it when the game's played on such a display. The near-plane part is still worth a try on its own if faces start fighting their edges.
- **5.4 Terrain chunks off the main thread: not yet.** Nothing has stuttered walking into new country; the 4 chunks a frame budget hasn't shown in any measurement. Do it if that changes (a faster vehicle on the pass road is the likely first case).
- **5.5 A registry of walkers: when the first NPC or enemy arrives** (GameDesign 3.6's vacuum-cleaner tanks). Each walker now carries its own `Gait` (size, strength, mass), so the registry is mostly the plumbing: `StepDoors`, `PushWalker` and `BodyUnder` over a list instead of the one player.
- **Instancing (rest of 5.1): when a view repeats a mesh hundreds of times** (see 5.1).

## 6. Suggested order

| Phase | Items | State |
|---|---|---|
| 1. Quick wins | 3.3, CI (4) | done; CI not yet run on GitHub |
| 2. Baseline | Measure draw calls and frame times at every start | done: `Basic.World.Benchmark`, results in `Basic.World.Benchmark/results/` |
| 3. Collision | 3.1 (wall grid), then 3.2 | done, with tests and measured |
| 4. Structure | 1.1 to 1.6 | done; 1.5 decided (keep `VectorViktor`, isolated) |
| 5. Testability | the device-free `MeshBuilder` step, then the mesh tests | done |
| 6. As needed | 2.1, 5.1 (shader), 5.3 done 2026-10-02; 2.2, instancing, 5.2, 5.4, 5.5, nullable checks, shot regression | each tied to a feature (a forest, NPCs, high-refresh displays) or a measurement: see 5.7 |
