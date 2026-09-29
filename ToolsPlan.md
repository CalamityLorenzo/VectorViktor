# Maps, tools and the playtest harness: plan

Written 2026-09-27. Steps 1 and 2 were done the same day (see 4). It follows on from [AnimationPlan.md](AnimationPlan.md) and comes before most of [GameDesign.md](GameDesign.md)'s features: they need these tools to be built and tried.

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

## 3. The shape it's heading for

```
                        ┌─────────────── Basic.World (exe): the game so far
                        ├─────────────── Droid.Playground (exe): the playtest harness          (step 2)
                        ├─────────────── Map.Studio (exe): map viewer and editor               (steps 4-5)
                        ├─────────────── Basic.World.Benchmark (exe), Meshes.Tests
                        │                  Tools.DearImGui (lib): Dear ImGui drawn by MonoGame, for the harness and the studio
                        ▼
Maps.Home (lib): the home map, built in code (later also map files)
   ▼
World.Maps (lib): what a map is (IDistrict and its pieces), building one (WorldBuilder),
                  drawing a built one (WorldView, WorldRenderer); later, loading one from a file
   ▼
World.Rendering ─► MeshRendering ─► MeshCore.Library      MeshProps (props)
   ▼
World.Buildings ─► World.Core   (simulation: no GraphicsDevice)
```

Every app gets maps the same way: reference `World.Maps` for the machinery, and a map library (`Maps.Home` today) or a map file for the content.

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

### Step 3: the map file format
Two kinds of file, in the repo's `Maps/` folder (copied beside each app when it's built):
- **A district file** (`Maps/districts/yard.district.json`): a version number; pads; water; buildings (rooms, doors and roofs are already data in `RoomSpec`, or a generator by name with its parameters, such as `house.two-storey`); **props** (a catalogue name, position, turn); **things** (a catalogue name, mass, position); starts; portals.
- **A map file** (`Maps/home.map.json`): a version number, the terrain's seed, the default start, and its **districts in order**, each either a district file or a code district by name:

```json
{
  "version": 1,
  "name": "Home",
  "seed": 1,
  "defaultStart": "hills",
  "districts": [
    { "code": "home.countryside" },
    { "code": "home.town" },
    { "file": "districts/yard.district.json" }
  ]
}
```

Per district means a map is put together from pieces, and two people (or the studio and a text editor) can work on different pieces at once.
- **A catalogue**: names to `MeshSource`s and their sizes (`crate.wood`, `armchair`, `television`), so a file never holds a mesh, only a name.
- **`DistrictFile` → `IDistrict`**, so a file-built district joins the code-built ones through the same `WorldBuilder`; **`MapFile` → the list of districts** (a registry names the code districts: `home.countryside` is `new Countryside()`).
- **Round-trip tests**: load, save and load again gives the same world; unknown names fail with a clear message; old versions still load.
- **Reloading**: a running app watches its map file and rebuilds when it's saved.
- Lesson: serialisation and file formats (JSON, versioning, round trips, why files hold names, not objects).

### Step 4: Map Studio, the viewer
A new app, **`Map.Studio`**, that opens a map (a code map by name, or a file) with no player in it.
- **Cameras**: a free flying camera; orbit round a point; **snap views**: overhead (straight down, orthographic, with the grid), front, side and a three-quarter view, each a key, with a smooth move between them.
- Overlays as in the harness, plus pads, starts and portals drawn as markers; layers can be hidden (terrain, buildings, props).
- Click something to see what it is and where.
- Lesson: cameras (view and projection matrices, perspective against orthographic, moving smoothly between views).

### Step 5: Map Studio, the editor
- Place, move, turn and delete props and things from the catalogue, snapped to a grid in the overhead view; level pads; set starts; save to the map file.
- Undo and redo (every edit a command that can be undone).
- Panels for the catalogue and the selected object's values, with Dear ImGui (see 7), docked round the 3D view.
- Lessons: picking (a ray from the mouse into the world), gizmos, the command pattern for undo.

### Step 6: from the Studio to the harness
"Play here": start the harness on the open map, at the point under the mouse, as the droid. Edit, play and edit again in seconds.

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
