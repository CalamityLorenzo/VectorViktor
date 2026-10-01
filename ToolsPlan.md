# Maps, tools and the playtest harness: plan

Written 2026-09-27. Steps 1 and 2 were done the same day, and steps 3 to 6 on 2026-10-01 (see 4), which finishes the plan. It follows on from [AnimationPlan.md](AnimationPlan.md) and comes before most of [GameDesign.md](GameDesign.md)'s features: they need these tools to be built and tried.

## 1. What's wanted

In Paul's words: *"Whilst I want to get on with building a game there are lots and lots of other parts of this project to do first."*

- **A teaching experience.** The project is also a way of learning how the parts of a game are put together.
- **Shared map design.** Maps must be shared between projects: one project to build maps, and a way to view them.
- **A playtest harness**: drop into a map as the droid and try out new behaviour.

## 2. Decisions

| Question | Decision |
|---|---|
| How to teach | **Written lessons in the repo, tied to the code** ([Lessons/](Lessons/README.md)), each with **an exercise to write yourself**. Exercises are optional: nothing later depends on them, so skipping one when short of time costs nothing. |
| Map format | **Data files for layout** (JSON: readable, diffable, editable by a tool), with **C# generators referred to by name** for whatever is really code (houses, stairs, roads). Code-built districts keep working beside maps loaded from files. |
| Map builder | **In MonoGame**, sharing the game's rendering, with **snap views**: straight down overhead, front, side, and back to a free camera. |
| Playtest harness | **A separate app** for trying game experiences. It's a workbench, changed and extended as each experiment needs, so the game itself stays clean. |
| Panels (UI) | **Hexa.NET.ImGui** (Dear ImGui's docking branch, kept current with mainline; ImGui.NET has stopped at 1.91.6) with **our own MonoGame renderer**. Proved by a spike on 2026-09-27 (see 7). |
| Map files | **One file per district, and a map file that collects them** (by file, or a code district by name). |
| Where they live | **A `Maps/` folder in the repo**, copied beside each app when it's built. |

## 3. The shape it has

```
                        ┌─────────────── Basic.World (exe): the game so far
                        ├─────────────── Droid.Playground (exe): the playtest harness          (step 2)
                        ├─────────────── Map.Studio (exe): map viewer and editor               (steps 4-6)
                        ├─────────────── Basic.World.Benchmark (exe), Meshes.Tests
                        │                  Tools.DearImGui (lib): Dear ImGui drawn by MonoGame, for the harness and the studio
                        ▼
Maps.Home, Maps.Coast, Maps.Pass (libs): the maps' code districts, terrains and houses, by name     Maps/ (files): the maps, and district files
   ▼
World.Maps (lib): what a map is (IDistrict and its pieces), building one (WorldBuilder),
                  drawing a built one (WorldView, WorldRenderer), and map files (Files: MapLibrary, FileDistrict)
   ▼
World.Rendering ─► MeshRendering ─► MeshCore.Library      MeshProps (props)
   ▼
World.Buildings ─► World.Core   (simulation: no GraphicsDevice)
```

Every app gets maps the same way: reference `World.Maps` for the machinery and the map libraries for their names (`HomeMap.AddTo(library)`), then open a map file by name (`library.Open("home")`).

## 4. Steps

### Step 1: the world as shared libraries (done 2026-09-27)
Before this, everything that defined the world (the districts, `WorldBuilder`, starts, things, fixtures, portals) and drew it (`WorldView`, `WorldRenderer`) was compiled into the game's executable. The benchmark and mesh tests could only reach them by referencing the game. Now:
- **`World.Maps`**: `IDistrict`, `WorldBuilder`/`BuiltWorld`, `Start`, `Thing`, `Fixture`, `Portal`, `Scenery`, `WorldView`, `WorldRenderer`. It knows how a map is put together and drawn, and nothing about any particular map.
- **`Maps.Home`**: the districts (`Countryside`, `Town`, `Street`, `Lane` and what they're made from) and **`HomeMap`**, which lists them and names the default start.
- **`Basic.World`**: just the game (`Game1`), the road demo (`Game2`) and `Program`.
- The benchmark and mesh tests reference the libraries, not the game.
- Lesson: [01: Sharing the world between projects](Lessons/01-sharing-the-world.md).

### Step 2: the playtest harness, first version (done 2026-09-27)
A new app, **`Droid.Playground`**, that loads a map (the home map to begin with) and drops you in as the droid. It overlaps [AnimationPlan.md](AnimationPlan.md) step 2 (the droid as the player). Run `Droid.Playground [experiment] [start]`.

**What was built:**
- **`Tools.DearImGui`**: `ImGuiRenderer`, Dear ImGui 1.92.9b (Hexa.NET.ImGui 3.1.0-experimental, pinned) drawn by MonoGame, with the 1.92 texture protocol, mouse, keyboard and text input, and `WantsMouse`/`WantsKeyboard`. Lesson [02](Lessons/02-drawing-dear-imgui.md).
- **`RetroGame`** got `DrawOverlay` (drawn after the scale-up, at the window's resolution), `KeyboardCaptured`, `PictureArea` (for the mouse) and whole-window screenshots (`ShotOfWholeWindow`).
- **`WorldRenderer.DrawFrom`**: the map from any camera, with extras added to the batch; the game's `Draw` now uses it too. **`BuildingGround.Walls`** is public, for the overlay.
- **`World.Core/Characters/DroidMotion`**: each wheel rolled by its own distance (opposite ways turning on the spot, via `DroidRig.Roll(left, right)`), and a lean into changes of speed; 6 tests. **`World.Rendering/RigView`**: a live rig drawn, following part swaps and cables.
- **`Droid.Playground`**: fixed 60 Hz ticks with pause (P), step (N) and slow motion ([ ]); head (F1), drone (F2) and free (F3) cameras, the head camera run round its visor (Q/E, R/F); drop in at any start, under the free camera (T) or where you Ctrl+click; overlays of walls, bodies, joints and the capsule; the droid's state and frame stats; and the **experiments**: `drive` (the walker's movement, the baseline) and `segway` (lean to go, lean back to stop, wobble; eight live settings and three plots). Lesson [03](Lessons/03-the-playground.md).

**Known gaps, for later:** the droid still has the walker's capsule (1.8 m tall; the droid is 1.39 m) and eye-height logic for ceilings; picking sees only the terrain, not buildings; the drone's own mesh isn't drawn in the harness; the head camera has no mouse control yet.

**As planned:**
- **The droid as the player**: a live `RigView` (it follows `RigNode.Part` swaps), driven by the character controller: `Roll` from distance moved, `Tilt` from acceleration, the view from `DroidRig.CameraView`. The head camera is steered round its rail.
- **`Tools.DearImGui`**: the MonoGame renderer for Dear ImGui (see 7), so experiments can have panels of live values from the start.
- **Experiments**: each thing being tried is a class (an `IExperiment`: set up, step, draw, keys), picked by name on the command line. The first: *Segway movement* (lean in to go, lean back to stop, wobble). The harness is a place to write experiments, not a finished tool.
- **Developer tools**, all toggled by keys and shown on a small text overlay:
  - pause, single-frame step, slow motion (half and quarter speed)
  - a free camera, detached from the droid, and teleport to where it's looking
  - drop in at any start, or at the point under the mouse
  - overlays: collision walls, bodies' boxes, the rig's joints and cables, the ground under the droid
  - live values: named numbers an experiment exposes (speed, lean, rail speed), nudged up and down with keys
- Lessons: the game loop and fixed timestep; the simulation/drawing split; test harnesses and experiments.

### Step 3: the map file format (done 2026-10-01)
Two kinds of file, in the repo's [Maps/](Maps) folder: a **map file** (`Maps/home.map.json`: a version, the terrain by name and its seed, the default start, and its districts in order, each `{"code": name}` or `{"file": path}`) and a **district file** (`Maps/districts/yard.district.json`: pads, pools, buildings, props, things, starts and portals, every one by name and where). Lesson [09](Lessons/09-map-files.md).

**What was built** (`World.Maps/Files`):
- **`MapJson`**: System.Text.Json set up for files people read and write. Names are camelCase, points `[x, z]`, colours `"#rrggbb"`. Each entry goes on a line of its own, and values at their defaults are left out. Comments and trailing commas are allowed; unknown names are an error, giving the file and which entry.
- **`DistrictFile`** and **`MapFile`**: the files' contents, with entries as records that can't change (`PadEntry`, `PoolEntry`, `BuildingEntry`, `PropEntry`, `ThingEntry`, `StartEntry`, `PortalEntry`). A version is required, and a newer one, or none, is refused. `Load` has the place for upgrades.
- **`MapLibrary`**: every name a file can use: terrains, code districts, kinds of building, the catalogue, and the code maps. Each map library adds its own (`HomeMap.AddTo`, `CoastMap.AddTo`, `PassMap.AddTo`): terrains `home`/`coast`/`pass`, districts `home.town` and so on, buildings `house.two-storey`, `house.bungalow` and `house.lane-cottage` (a kind can put up more than a `Building`: the cottage is walls, a mesh and a window, a small `IDistrict` of its own).
- **`Catalogue`**: the standard things to put about (crates, a locker, a pallet, furniture, plants, a Trabant), each with a mesh and a mass. Sizes are measured from the meshes, built headless.
- **`FileDistrict`**: a district file as an `IDistrict`, so `WorldBuilder` didn't change. Every name is looked up when it's made. Props are fixtures with walls round their boxes, things are bodies, and a building gets a pad under it.
- **`MapDocument`**: a map file and its district files, to build a `Map` from, edit and save.
- **`MapFolder`**: the repository's Maps folder when run from inside it, so saves land where git sees them; otherwise the copy beside the app.
- **`MapWatcher`**: a `FileSystemWatcher`, debounced: it reports a change once the files have been quiet for 0.25 s.
- **The maps as files**: `home.map.json` (the four code districts, plus the **yard**, a district file with a crate stack, a box tower, a pallet, a bungalow and an oak; start `yard`), `coast.map.json` and `pass.map.json`. The apps open maps by name through the library (the file if there is one, else the code map), or a `.map.json` by its path.
- **Reloading**: `Basic.World` and `Droid.Playground` build the map again when its files are saved, keeping you where you were. Each built world has a mesh cache of its own. A fence's mesh is built from the ground it captures, so a world built again needs its meshes built again (the cache's debug check caught this).
- **Tests** (`MapFileTests`, 22). Round trips: every Maps/ file saves exactly as read, and every kind of entry survives writing and reading. The maps: the home map's file has every start and building of the code map, unchanged, and the yard; the coast's file is the coast. Building: the yard's crates stack, a file building stands on levelled ground, a solid prop has walls round its box, things turned a quarter turn their boxes, and catalogue sizes are right. Mistakes: unknown names (saying what there is), a misspelt key, a missing `at`, newer or missing versions, bad numbers and colours, two starts of one name. And the watcher.

**Departures from the plan:** the yard is the worked example rather than a copy of lesson 01's exercise. Props got walls round their boxes (a sofa you walk through looks wrong). `Thing` gained `From`, the district it came from, for the studio.

### Step 4: Map Studio, the viewer (done 2026-10-01)
A new app, **`Map.Studio`**, that opens a map file with no player in it. Lesson [10](Lessons/10-cameras-and-views.md).
- **`StudioCamera`**: a target, a yaw and pitch, and a distance. Orbit (middle button, or Alt + left), look about (right button), fly (W, A, S, D, R, F), zoom (wheel), pan (Shift + middle). F frames what's selected.
- **Snap views**: 1 overhead (orthographic, north up), 2 front and 3 side (orthographic), 4 three-quarter (perspective), 0 free. Each is a smooth 0.4 s move: eased, the short way round, zooming by ratio, and flat from the moment it arrives. A flat view shows what the perspective did at the target, so it doesn't jump.
- **`WorldRenderer`** gained what a tool needs, each defaulting to the game's: `Fog`, `FarPlane`, `ProjectionOverride`, `TerrainCentre`, a draw distance, and taking over another renderer's terrain meshes. **`WorldView`** gained layers (`ShowTerrain`, `ShowWater`, `ShowBuildings`, `ShowFixtures`, `ShowThings`).
- **Markers**: pads (yellow, or olive in files not being edited), pools, every start (a post, a ring and an arrow, with its name beside it) and portals. The snapping grid (G) is draped over the ground. Layers can be hidden.
- **Click to see what it is and where**: an entry in a district file, a thing or building built in code (its name, mass, rooms, bounds), or the ground (its height).
- `FreeCamera` and `DebugLines` moved from the playground to MeshRendering, to be shared. `RetroGame` gained `EscapeExits` and a settable `LowResOn`: the studio starts sharp, and L gives the game's look.

### Step 5: Map Studio, the editor (done 2026-10-01)
Lesson [11](Lessons/11-picking-and-undo.md).
- **Picking** (`Picking`): a ray through the mouse (`Viewport.Unproject`). Boxes, turned ones too, are tested in their own frame; the ground by stepping along the ray and halving. The nearest hit wins.
- **Placing**: from the catalogue (loose, as a thing, or fixed, as a prop), kinds of building, pads, pools and starts, each snapped to the grid. New district files can be added to the map.
- **Moving and changing**: select, then drag over the ground, or along a red (east) or blue (south) **arrow** (`MoveGizmo`: worked out on the screen, and a fixed size whatever the zoom). Turn with Q and E (things a quarter at a time). Copy (Ctrl+D), remove (Delete). Every value of the selected entry is in a panel. Code districts are shown and can be clicked on, but not edited.
- **Undo and redo** (`Edits`): every edit is a command (`AddEntry`, `RemoveEntry`, `ChangeEntry`) in an `EditHistory`. Entries being records, a change is just the entry before and after. A drag, or a value dragged in a panel, shows at once and is recorded once, when let go.
- **Building again after each edit**, from what's in memory. While the pads are the same it keeps the same terrain (`Terrain.Drain`), and while its water is too, the same terrain meshes. That's about 60 ms for an edit that doesn't change the ground, against 1.9 s to build everything. When a pad changes, its terrain is drawn again a few chunks a frame.
- **Saving** (Ctrl+S) writes only the files that changed. Files changed on disk by something else are read again; with edits not saved, it says so and offers to reopen.
- **Panels** (Dear ImGui): Map (files, districts, undo, views, layers, snap, play), Add, Selected, and a status line. They float rather than dock round the view; docking can come when the panels grow.
- **Tests** (`Map.Studio.Tests`, 13): undo and redo in order, unsaved either side of a save, and the selection following an edit; entries moved and turned, with boxes where the game builds them; the camera's views (north up overhead, flat views matching perspective, turning the short way); picking turned boxes and the ground; and dragging along an arrow.

**Not done:** a rotate handle (Q and E turn instead); editing portals by dragging their ends (their values can be set in the panel); a schema for the files.

### Step 6: from the Studio to the harness (done 2026-10-01)
**Play here** (P, under the mouse; or the button, where the camera's looking). It saves, then starts `Droid.Playground` as a program of its own on the map file, with the droid dropped at that point facing the camera's way (`at=x,z yaw=degrees`, new arguments for the playground). The playground watches the map's files, so each save in the studio is in the running playground a moment later, the droid where it was: edit, save, look, edit again. Lesson [11](Lessons/11-picking-and-undo.md).

## 5. Lessons

Each lesson is a page in [Lessons/](Lessons/README.md): what it's about, the idea in general, how this project does it (with links into the code), why that way and what else could be done, and **an exercise** (optional) with a way to check it. Lessons are written for step 1 onwards as each step is built. Lessons on what's already built can be written when wanted: meshes, palettes and outlines; the physics; the terrain's chunks; rigs and clips.

## 6. Questions

1. **Which Hexa.NET.ImGui**: `3.1.0-experimental` (Dear ImGui 1.92.9b, built for .NET 10, July 2026) or the last stable `2.2.9` (1.92.2b, October 2025)? Recommended: **3.1.0-experimental, pinned** in `Directory.Packages.props`, so it only changes when chosen; the renderer uses a small part of the API, so moving between them is cheap. Answered: 1.92's new texture protocol works (see 7).

## 7. The Dear ImGui spike (2026-09-27)

A throwaway MonoGame app (kept out of the repo) using `Hexa.NET.ImGui 3.1.0-experimental`, drawing Dear ImGui's demo window and a mock "Map Studio" panel (view: free / overhead / front / side, a grid-snap slider, "Play here"). It worked: text, widgets, clipping and the font atlas all drew correctly.

What a MonoGame renderer for Dear ImGui has to do, about 250 lines (`Tools.DearImGui` is this, cleaned up, with keyboard input added):
- **Each frame, in**: the window size and frame time, mouse position, buttons and wheel, keys and typed characters, as events on `ImGuiIO`; then `ImGui.NewFrame()`.
- **Textures (new in 1.92)**: the renderer says it handles them (`RendererHasTextures`), and each frame ImGui lists textures it wants created, updated (a rectangle of new glyphs, as the font atlas grows) or destroyed; the renderer does it with `Texture2D` and hands back an ID.
- **Each frame, out**: `ImGui.Render()` gives draw lists: vertices (position, texture coordinate, colour: 20 bytes, which `ImDrawVert` and a MonoGame `VertexDeclaration` agree on), 16-bit indices, and commands (a clip rectangle, a texture, a range of indices). The renderer copies them into dynamic buffers and draws each command with a `BasicEffect` (texture and vertex colour on, an orthographic projection in pixels), a scissor rectangle, alpha blending and no depth.
- Hexa's API is generated from Dear ImGui's C API, so it follows the C++ one closely: the Dear ImGui documentation and examples apply almost as written.

It's a good lesson in itself: immediate-mode UI (the interface is rebuilt every frame from the program's state, rather than kept as objects), vertex and index buffers, scissor clipping, and a C library behind a .NET wrapper.
