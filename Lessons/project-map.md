# Project map: how the libraries relate

An arrow means "references": `A --> B` means A may use B's public types. Arrows point towards what's more general (see [lesson 01](01-sharing-the-world.md)), so the most general libraries are at the bottom.

Only **direct** arrows that aren't already implied by another path are drawn. For example, [World.Maps](../World.Maps/World.Maps.csproj) also references World.Core in its `.csproj`, but it would get there anyway through World.Rendering and World.Buildings, so that arrow is left out to keep the picture readable.

## The libraries

```mermaid
flowchart BT
    subgraph meshes["Meshes: shapes, knowing nothing of the world"]
        MeshCore["<b>MeshCore.Library</b><br/>MeshBuilder, MeshData,<br/>OutlineData, DrawRange"]
        MeshProps["<b>MeshProps</b><br/>props built from code:<br/>HouseMesh, OakMesh, CarMesh, ..."]
        MeshRendering["<b>MeshRendering</b><br/>drawing meshes: MeshCache,<br/>MeshBatch, RetroGame, FreeCamera"]
    end

    subgraph sim["Simulation: no drawing, testable without a window"]
        WorldCore["<b>World.Core</b><br/>Terrain, Player, PhysicsWorld,<br/>CharacterController, Rig, Clip"]
        Buildings["<b>World.Buildings</b><br/>Building, WallGrid, RoomSpec,<br/>Door, WallStair"]
    end

    subgraph draw["Drawing the world"]
        Rendering["<b>World.Rendering</b><br/>TerrainView, BuildingView,<br/>RoomView, RigView, DroidMesh"]
    end

    subgraph maps["Maps"]
        WorldMaps["<b>World.Maps</b><br/>what a map is: IDistrict, WorldBuilder,<br/>WorldView, WorldRenderer;<br/>map files: MapLibrary, FileDistrict"]
        MapsHome["<b>Maps.Home</b><br/>one map: HomeMap, Town,<br/>Street, Countryside, ..."]
        MapsCoast["<b>Maps.Coast</b><br/>another: CoastMap,<br/>CoastTerrain, Coast"]
        MapsPass["<b>Maps.Pass</b><br/>and another: PassMap,<br/>PassTerrain, PassRoute, Pass"]
    end

    subgraph tools["Tools"]
        DearImGui["<b>Tools.DearImGui</b><br/>ImGuiRenderer"]
    end

    MeshProps --> MeshCore
    MeshRendering --> MeshCore
    Buildings --> WorldCore
    Buildings --> MeshCore
    Rendering --> Buildings
    Rendering --> MeshRendering
    WorldMaps --> Rendering
    WorldMaps --> MeshProps
    MapsHome --> WorldMaps
    MapsCoast --> WorldMaps
    MapsPass --> WorldMaps
```

Things to notice:

- There are **two roots**, World.Core and MeshCore.Library, and neither knows about the other. The simulation has no idea what anything looks like, and meshes have no idea what they're for.
- **World.Buildings is where they first meet.** A [RoomSpec](../World.Buildings/RoomSpec.cs) uses MeshCore so a room can say what it's made of, but it still draws nothing.
- **World.Rendering** is the first place the world is *drawn*: it joins the simulation (through World.Buildings) to MeshRendering.
- **Maps.Home, Maps.Coast and Maps.Pass sit side by side**, each depending only on the machinery below, never on each other. A map brings its own terrain (see [Map](../World.Maps/Map.cs)): the home map's is [TerrainGenerator](../World.Core/Terrain/TerrainGenerator.cs), the coast's is [CoastTerrain](../Maps.Coast/CoastTerrain.cs), the pass's is [PassTerrain](../Maps.Pass/PassTerrain.cs).
- **Tools.DearImGui** stands alone. It only needs MonoGame and Dear ImGui, so any app can pick it up.
- Every library also references the MonoGame package (for types like `Vector3` and `GraphicsDevice`); that isn't drawn.

## The apps and tests on top

The same picture with the executables and test projects added. The libraries are collapsed to keep it small.

```mermaid
flowchart BT
    subgraph libs["Libraries"]
        MeshCore["MeshCore.Library"]
        MeshProps["MeshProps"]
        MeshRendering["MeshRendering"]
        WorldCore["World.Core"]
        Buildings["World.Buildings"]
        Rendering["World.Rendering"]
        WorldMaps["World.Maps"]
        MapsHome["Maps.Home"]
        MapsCoast["Maps.Coast"]
        MapsPass["Maps.Pass"]
        DearImGui["Tools.DearImGui"]
    end

    subgraph apps["Apps"]
        BasicWorld(["<b>Basic.World</b><br/>the game"])
        Playground(["<b>Droid.Playground</b><br/>playtest harness"])
        Studio(["<b>Map.Studio</b><br/>map viewer and editor"])
        Benchmark(["<b>Basic.World.Benchmark</b>"])
        BasicLevels(["<b>Basic.Levels</b>"])
        BasicModels(["<b>Basic.Models</b>"])
    end

    subgraph tests["Tests"]
        CoreTests[["World.Core.Tests"]]
        MeshTests[["Meshes.Tests"]]
        StudioTests[["Map.Studio.Tests"]]
    end

    subgraph protos["Prototypes (stand alone)"]
        VectorViktor(["VectorViktor"])
        LoadingModelMeshes(["LoadingModelMeshes"])
    end

    MeshProps --> MeshCore
    MeshRendering --> MeshCore
    Buildings --> WorldCore
    Buildings --> MeshCore
    Rendering --> Buildings
    Rendering --> MeshRendering
    WorldMaps --> Rendering
    WorldMaps --> MeshProps
    MapsHome --> WorldMaps
    MapsCoast --> WorldMaps
    MapsPass --> WorldMaps

    BasicWorld --> MapsHome
    BasicWorld --> MapsCoast
    BasicWorld --> MapsPass
    Playground --> MapsHome
    Playground --> MapsCoast
    Playground --> MapsPass
    Playground --> DearImGui
    Studio --> MapsHome
    Studio --> MapsCoast
    Studio --> MapsPass
    Studio --> DearImGui
    Studio -. starts .-> Playground
    Benchmark --> MapsHome
    BasicLevels --> Rendering
    BasicLevels --> MeshProps
    BasicModels --> MeshProps
    BasicModels --> MeshRendering

    CoreTests --> Buildings
    CoreTests --> MapsHome
    CoreTests --> MapsCoast
    CoreTests --> MapsPass
    MeshTests --> MapsHome
    MeshTests --> MapsCoast
    MeshTests --> MapsPass
    StudioTests --> Studio
```

- The game, the playground, the studio and the benchmark all reach the world the same way: through a map library (**Maps.Home**, and for all but the benchmark **Maps.Coast** and **Maps.Pass** too), which pulls in everything below it. The game, the playground and the studio pick a map by name on the command line (`coast`, `pass`), home if none: a map file in [Maps/](../Maps) (see [lesson 09](09-map-files.md)), each of whose districts is built in code by a library or read from a district file.
- The dotted arrow isn't a reference: the studio *starts* the playground as a program of its own (**Play here**, [lesson 11](11-picking-and-undo.md)), and they share only the map files.
- **Map.Studio.Tests** references the studio's program itself. Tests may: they're there to reach into what they test.
- **Basic.Levels** and **Basic.Models** are older apps that use the lower libraries directly, without the map machinery.
- **VectorViktor** and **LoadingModelMeshes** reference none of the solution's libraries; they're early prototypes (VectorViktor uses SharpGLTF to load models).
