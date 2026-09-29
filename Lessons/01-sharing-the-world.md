# 01: Sharing the world between projects

**What you'll learn:** what projects, assemblies and references are in .NET; why references must all point one way; how to decide what goes in which library; and how the world was moved out of the game so that a map builder, a viewer and a playtest harness can all use it.

**Built in:** [ToolsPlan.md](../ToolsPlan.md) step 1.

## The problem

The game needed three new apps: a map builder, a map viewer and a playtest harness. All of them need the world: its terrain, buildings and things, where you can start, and how it's drawn.

But everything that defined the world lived *inside the game*, in the `Basic.World` project, next to the code that runs the game. The only way for another project to use it was to reference the game itself, which is what the benchmark and the mesh tests were doing.

## The idea: projects, assemblies and references

- A **solution** (`VectorViktor.slnx`) is a list of **projects**.
- Each project (`.csproj`) compiles to one **assembly**: an **executable** (`.exe`, something you run, with a `Program.cs`) or a **library** (`.dll`, code for other projects to use).
- A **project reference** says "this project may use that one's public types". `Basic.World.csproj` references `World.Core.csproj`, so the game can use `Terrain` and `Player`.

References have a direction, and **they can't go round in a circle**: if A references B, B can't reference A (the compiler needs B built before A). So the projects form **layers**, and the rule that keeps them healthy is:

> **Depend towards what's more general.** Content depends on machinery; machinery never depends on a particular piece of content. Things used by many sit low; things that change often sit high.

## How it's done here

Before, one project did three jobs:

```
Basic.World (exe)
  ├ the game itself            Game1, Game2, Program
  ├ what a map is              IDistrict, WorldBuilder, BuiltWorld, Start, Thing, Fixture, Portal, Scenery
  │   and how one is drawn     WorldView, WorldRenderer
  └ one particular map         Countryside, Town, Street, Lane, Hangar, Houses, ... DroidDisplay
```

After, each job has its own project:

```
Basic.World (exe)      the game                     ─┐
Maps.Home (lib)        one particular map           ─┤ each references the ones below it
World.Maps (lib)       what a map is, and drawing it ─┘
World.Rendering, World.Buildings, World.Core, ...   (as before)
```

| Project | Holds | References |
|---|---|---|
| [World.Maps](../World.Maps/World.Maps.csproj) | the *machinery*: [IDistrict](../World.Maps/District.cs), [WorldBuilder](../World.Maps/WorldBuilder.cs), the pieces a district hands over, [WorldView](../World.Maps/WorldView.cs), [WorldRenderer](../World.Maps/WorldRenderer.cs) | the simulation and rendering libraries |
| [Maps.Home](../Maps.Home/Maps.Home.csproj) | the *content*: the home map's districts, and [HomeMap](../Maps.Home/HomeMap.cs) listing them | World.Maps, and the rest |
| [Basic.World](../Basic.World/Basic.World.csproj) | the game only | both |

Any app that wants the home map now builds it the same way:

```csharp
using Maps.Home;
using World.Maps;

var world = WorldBuilder.Build(HomeMap.Districts());   // the machinery, given the content
var renderer = new WorldRenderer(world, device, cache);
```

### The one real design change

`WorldBuilder` used to have a method `Standard()` returning `new Countryside(), new Town(), new Street(), new Lane()`. That's the machinery naming a particular map, which is the direction the rule forbids. Left there, `World.Maps` would have to reference `Maps.Home` while `Maps.Home` references `World.Maps`: a circle, which won't compile.

So the list moved to the content, as [HomeMap.Districts()](../Maps.Home/HomeMap.cs). `WorldBuilder.Build` already took *any* list of `IDistrict`s, and that's what makes the split work: the machinery defines an **interface** (`IDistrict`) and works with anything that implements it; the content implements it. A map from a file (step 3) will be one more `IDistrict`, and `WorldBuilder` won't need to change.

### Why not just keep referencing the game?

.NET does let you reference an `.exe`; the tests did. But it's the wrong way round:

- **Every tool depends on the game.** The map studio would pull in the game's `Program`, its content pipeline and its window setup, and rebuild whenever the game changes.
- **It hides what's shared.** Nothing in `Basic.World` said which parts other projects rely on. Now anything in `World.Maps` is shared by definition, and anything in `Basic.World` is the game's own business.
- **It blocks the next steps.** The harness and the studio are *also* executables. Three executables sharing code need it in a library.

### Doing the move safely

A move like this should change *where* code is, not *what* it does. Here's how that was checked:

1. **`git mv`** moved each file, so its history follows it (`git log --follow Maps.Home/Town.cs` still shows its past).
2. **Namespaces** changed with the projects (`Basic.World` → `World.Maps` / `Maps.Home`), and each file got the `using` lines it now needs. The compiler finds every one you miss.
3. **The whole solution built**, with no new warnings.
4. **The tests passed**: `Meshes.Tests` builds the entire home map and every mesh in it, so a district lost in the move would fail there.
5. **The game ran**: a screenshot from the `street` start looked as before.

One detail: the new libraries have `<Nullable>disable</Nullable>` in their project files. The solution turns null-checking on for libraries ([Directory.Build.props](../Directory.Build.props)), but this code was written in the game, where it was off. Turning it on is a separate job, and mixing it into a move would make the move harder to check.

## Other ways it could have been done

- **One big shared library** holding the machinery and the home map together. Simpler, but then every app gets the home map whether it wants it or not. A test map, or a map from a file, would sit beside it rather than replace it.
- **Linked files** or a shared project (`.shproj`): the same source files compiled into several projects. No DLL, but every project compiles its own copy, and it's easy to lose track of what's shared.
- **NuGet packages**: the way to share a library between *separate repositories*. Overkill inside one solution.
- **Data files**: the real long-term way to share *maps* (ToolsPlan step 3). Code maps stay useful for what's really code (a house generator, a road network); files hold layouts.

## Exercise (optional): a district of your own

Add a small district to the home map: a start called `yard`, with a stack of crates to push over.

1. Make `Maps.Home/Yard.cs`: a `public sealed class Yard : IDistrict`, in namespace `Maps.Home`.
2. Give it a `Starts` property with one start, `"yard"`, at `(-15, -6)`, facing north. A yaw of `0` faces north (towards -Z); `MathHelper.Pi` faces south.
3. Give it a `Things` method that puts three wooden crates in a stack at `(-15, -10)`. Use `Scenery.Crate`, with `above: 1f` and `above: 2f` for the upper two.
4. Add `new Yard()` to the list in `HomeMap.Districts()`.
5. Run it: `Basic.World\bin\Debug\net10.0-windows\Basic.World.exe yard`, or set `yard` as the start argument in Visual Studio.

**Hints:** [Countryside.cs](../Maps.Home/Countryside.cs) has both a `Starts` property and a `Things` method to copy from. `IDistrict`'s other members all have defaults, so you only write the two you need.

**Check it:**
- The game starts facing a stack of three crates, and walking into it knocks them over.
- `dotnet test` still passes. `Meshes.Tests` builds your crates' meshes too.
- Try naming your start `hills` instead. The game should stop as it starts, with `InvalidOperationException: Two districts both have a start called 'hills'.`: `WorldBuilder` refuses, because the countryside already has one. Failing loudly at once beats quietly starting you in the wrong place. Then put the name back.

**Stretch:** make the stack a tower of four cardboard boxes, slightly askew like Countryside's "tower", so it's easier to knock down.

## Thinking questions

1. Why does `IDistrict` live in `World.Maps` and not in `Maps.Home`?
2. A future `Maps.Moon` library holds the moon's map. Which projects must it reference, and which must reference it?
3. `WorldRenderer` draws the player and the drone. Is `World.Maps` really the right home for it, or is it the game's? What would decide it?

<details>
<summary>Answers</summary>

1. Because the machinery (`WorldBuilder`) must use it. If it were in `Maps.Home`, `World.Maps` would have to reference `Maps.Home`, and `Maps.Home` already references `World.Maps`: a circle. Interfaces belong with the code that *consumes* them, so that anyone can provide one.
2. `Maps.Moon` references `World.Maps`, plus whatever simulation and mesh libraries its districts use. Only the apps that want the moon reference `Maps.Moon`. `World.Maps` never does, and neither does `Maps.Home`: maps don't know about each other.
3. It's a judgement call. The harness and the studio both need to draw a built map with the same fog, culling and windows, which argues for sharing it. The player, drone and bird in it are a game's, which argues against. When the harness is built (step 2), the likely split is a map renderer in `World.Maps` and each app drawing its own characters on top. A good rule: split when a *second* user needs the thing, not before, because then you know which half they need.

</details>

## Further reading

- Microsoft Learn: *Manage references in a project* and *.NET project SDK overview*.
- The **Dependency Inversion Principle** (the "D" in SOLID): machinery depending on an interface that content implements, as `WorldBuilder` and `IDistrict` do.
- `git help mv` and `git log --follow`: moving files without losing their history.
