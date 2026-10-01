# 09: Map files: serialisation, names and versions

**What you'll learn:** what serialisation is and why a game's maps want to be data rather than code; why a file should hold *names* of things, never the things; how this project's map and district files are laid out, and why that way; how a file format is kept working as it changes (versions); how round-trip tests catch what goes wrong; and how a running program notices a file has been saved and builds the map again.

**Built in:** [ToolsPlan.md](../ToolsPlan.md) step 3: [World.Maps/Files](../World.Maps/Files), the [Maps/](../Maps) folder, and [MapFileTests](../World.Core.Tests/MapFileTests.cs). Lesson 01 comes first.

Run it: `Basic.World.exe yard` starts you in the yard, a district that comes from [a file](../Maps/districts/yard.district.json), not from code.

## The problem

Until now every map was C#. The home map is [Countryside](../Maps.Home/Countryside.cs), [Town](../Maps.Home/Town.cs) and the rest: classes that hand `WorldBuilder` their pads, buildings and crates. That's fine for what really is code, such as a house generator working out where its stair goes. But a stack of three crates at (-15, -10) isn't code. It's a *fact about the map*. Writing it in C# means:

- **A map editor can't make one.** The studio (lessons 10 and 11) would have to write C# and recompile the game.
- **Only programmers can change one.** Moving a crate means a code change and a build.
- **It can't be shared** with anything that isn't .NET: a web viewer, a Python script that checks maps.

So the layout moves into files, and the code that's really code stays code.

## The idea: serialisation

**Serialisation** turns objects in memory into something that can be stored or sent: bytes, or text. **Deserialisation** turns that back into objects. The format here is **JSON**: text, easy to read, easy to diff in git, and every language reads it.

Not everything should go in the file. The rule this project follows:

> **A file holds names and numbers, never objects.** "A `crate.wood` at (-15, -10)", not the crate's mesh. Whatever the name stands for is looked up when the file is read.

Why names?

- **A mesh is code.** [CrateMesh](../MeshProps/CrateMesh.cs) builds a crate from a size. Putting the vertices in the file would freeze today's crate; a name picks up tomorrow's.
- **Files stay small and readable.** `{"item": "crate.wood", "at": [-15, -10]}` is one line anyone can read.
- **Mistakes can be caught.** A name that isn't known is a clear error ("there's no catalogue item called 'crate.wod'"); a wrong number in a vertex list is just a broken shape.

The same rule decides how code districts get into map files: the map file says `{"code": "home.town"}`, a name, and a registry turns the name into `new Town()`.

## How it's done here

### Two kinds of file

A **map file**, [Maps/home.map.json](../Maps/home.map.json), says which terrain the map's on, where to start, and lists its districts in the order they're built:

```json
{
  "version": 1,
  "about": "The home map: ...",
  "terrain": "home",
  "seed": 1,
  "defaultStart": "hills",
  "districts": [
    {"code": "home.countryside"},
    {"code": "home.town"},
    {"code": "home.street"},
    {"code": "home.lane"},
    {"file": "districts/yard.district.json"}
  ]
}
```

A **district file**, [Maps/districts/yard.district.json](../Maps/districts/yard.district.json), says what's in one district:

```json
{
  "version": 1,
  "about": "A yard west of the start: ...",
  "pads": [
    {"centre": [-15, -11], "half": [6, 5]}
  ],
  "buildings": [
    {"kind": "house.bungalow", "id": "yardbungalow", "at": [-15, -21], "name": "Yard bungalow", "door": "south", ...}
  ],
  "things": [
    {"item": "crate.wood", "at": [-15, -10]},
    {"item": "crate.wood", "at": [-15, -10], "above": 1},
    ...
  ],
  "starts": [
    {"name": "yard", "at": [-15, -4]}
  ]
}
```

| List | Each entry is | Becomes (see [FileDistrict](../World.Maps/Files/FileDistrict.cs)) |
|---|---|---|
| `pads` | ground to level | a `TerrainGenerator.Pad` |
| `pools` | water, at an absolute level | a `Pool` |
| `buildings` | a kind of building, by name, and where | a `Building`, from a generator, with a pad under it |
| `props` | a catalogue item, fixed in place | a `Fixture`, and walls round its box |
| `things` | a catalogue item, loose | a `Thing`: a body to push about |
| `starts` | a named place to start | a `Start` |
| `portals` | a door that takes you elsewhere | a `Portal` |

Why one file per district, and a map file that lists them, rather than one file per map? Two people (or the studio and a text editor) can each work on a district without touching the other's file, and a district can be put into more than one map.

Everything's in metres and degrees, because people write these files too. Angles go clockwise from north, as the walker's yaw does; [FileDistrict](../World.Maps/Files/FileDistrict.cs) turns them into radians, and into the turn a mesh needs (meshes face +Z, south, so a mesh facing `d` degrees is turned `π - d`).

### The types: records that can't change

The entries are C# **records**, declared in [DistrictFile.cs](../World.Maps/Files/DistrictFile.cs):

```csharp
public sealed record PropEntry(string Item, Vector2 At, float Turn = 0f, float Above = 0f);
```

A record is a class whose equality is by value, and whose values can't change once it's made. To move a prop you make a new one: `prop with { At = new Vector2(3, 4) }`. That makes them good for files (an entry is what it says, no more) and, in lesson 11, for undo.

These are **data transfer objects**: plain shapes that match the file, kept apart from the game's own types (`Pad`, `Pool`, `Thing`). That gap is deliberate. The file's shape is a promise to every file already written, while the game's types should be free to change. [FileDistrict](../World.Maps/Files/FileDistrict.cs) is the one place that maps one to the other.

### Reading and writing: System.Text.Json

.NET's own `System.Text.Json` does the work, set up in [MapJson](../World.Maps/Files/MapJson.cs). The options are where the choices are:

- **camelCase names**: C#'s `DefaultStart` is `defaultStart` in the file, as JSON usually has it.
- **Converters** for what JSON has no shape for: a `Vector2` is `[x, z]`, a `Vector3` is `[x, y, z]`, a `Color` is `"#rrggbb"`.
- **Defaults left out**: an entry with no turn doesn't say `"turn": 0`. The file says only what's different, so it reads as what's there. This needs care: a value left out is read back as the record's default, so every default in a record must be the type's own (0, false, null). A pad's apron defaults to 2 m, so it's written `float? Apron = null`, meaning "the usual". Where an entry is must always be written, even at (0, 0), so `At` is marked `JsonIgnoreCondition.Never`.
- **One entry, one line**: a list of entries is written with each on its own line (`OneEach`). Then moving a crate changes one line, and a diff in git shows exactly that.
- **Forgiving where people are, strict where they slip**: comments and trailing commas are allowed, but a name that isn't known (`"trun": 90`) is an error, not quietly ignored. Silently dropping a typo is the worst outcome: the file looks right and does nothing.

Every error says which file, and where in it: `D:\...\yard.district.json: The props, number 2: The JSON property 'trun' could not be mapped to any .NET member contained in type 'World.Maps.Files.PropEntry'.` (Each entry is read on its own, by `OneEach`, so that it can say which one it was.)

### The library of names

A file says `"crate.wood"`, `"house.bungalow"`, `"home.town"`, `"home"` (a terrain). Something has to know what those mean. That's [MapLibrary](../World.Maps/Files/MapLibrary.cs): dictionaries from names to terrains, code districts, kinds of building and catalogue items.

Lesson 01's rule matters here. `World.Maps` can't name `Town`, because `Maps.Home` is built on `World.Maps`, not the other way round. So each map library **adds its own names** ([HomeMap.AddTo](../Maps.Home/HomeMap.cs)), and each app makes a library of what it has:

```csharp
var library = new MapLibrary();   // comes with the standard catalogue
HomeMap.AddTo(library);
CoastMap.AddTo(library);
PassMap.AddTo(library);
var map = library.Open("home");   // Maps/home.map.json, or the map built in code if there's no file
```

The **catalogue** ([Catalogue](../World.Maps/Files/Catalogue.cs)) is the list of things to put about: crates, furniture, plants. Each item has a mesh, a mass and whether it's solid. Its size isn't written down anywhere. It's **measured from its mesh** the first time it's asked for: the mesh is built on the CPU only (`MeshData.Headless`, from the review plan) and its bounds read off. A number that can be worked out shouldn't be copied by hand, because the copy goes stale.

A name that isn't there fails at once, saying what there is:

```
District 'yard': There's no catalogue item called 'crate.wod'. There are: crate.cardboard, crate.locker, crate.pallet, crate.steel, crate.wood, ...
```

`FileDistrict` looks every name up in its constructor, before anything's built, so the mistake shows when the map is opened rather than halfway through building the world.

### A file district is just another district

[FileDistrict](../World.Maps/Files/FileDistrict.cs) implements `IDistrict`, the same interface `Town` does. So `WorldBuilder` didn't change at all: it can't tell a district read from a file from one written in C#. That's lesson 01's interface paying off.

### Where the files are

The files live in the repository's [Maps/](../Maps) folder. Every app gets a copy beside itself when it's built (one `<None ... CopyToOutputDirectory>` line in [World.Maps.csproj](../World.Maps/World.Maps.csproj), which projects that reference it inherit). But run from inside the repository, an app uses the repository's own folder instead ([MapFolder](../World.Maps/Files/MapFolder.cs)). That way the studio saves where git sees it, and a running game sees what the studio has just saved.

### Versions

Every file starts `"version": 1`. When the format has to change (say `turn` becomes a full rotation), the version goes to 2, and `Load` **upgrades** an older file as it reads it, step by step, so every file ever written still loads. [DistrictFile.Load](../World.Maps/Files/DistrictFile.cs) has the place for it:

```csharp
Versions.Check(path, file.Version, CurrentVersion);
// Upgrades from older versions go here, oldest first, each taking a file from one version to the next:
// if (file.Version == 1) { ...; file.Version = 2; }
```

A file with no version, or a version newer than the program knows, is refused with a message saying so. Guessing would be worse: an old program reading a newer file and quietly losing what it doesn't understand, then saving over it.

### Round-trip tests

The surest test of a file format is the **round trip**: read a file, write it, and you should get what you started with. [MapFileTests](../World.Core.Tests/MapFileTests.cs) checks that:

- **every file in Maps/ saves exactly as it was read**, to the character. So saving a map that hasn't changed leaves its files alone, and a change is only the lines that changed.
- **every kind of entry**, with every value set, survives writing and reading.
- **the home map's file builds the home map**: every start and building of the map in code, in the same place, plus the yard. The coast's file builds exactly the coast built in code.
- **mistakes say what's wrong**: unknown names, a misspelt key, a missing `at`, a newer version, a colour written `"red"`.

### Reloading

[MapWatcher](../World.Maps/Files/MapWatcher.cs) uses a `FileSystemWatcher`: the operating system tells it, on a thread of its own, when a file in the folder changes. It only notes the time. The game asks each frame, and builds the map again once the files have been **quiet for a quarter of a second**. One save can be several writes, and reading a half-written file would fail.

Then [Game1](../Basic.World/Game1.cs) and the [Playground](../Droid.Playground/Playground.cs) build the map again and put you back where you were. One trap had to be got round. Meshes are cached by name ([MeshCache](../MeshRendering/MeshCache.cs)), and a fence's mesh is built from the ground it stands on, which it captures. The new world's ground is a new object, so the cache's debug check said "same name, different capture" and stopped the game. The fix: each built world gets **a mesh cache of its own**, thrown away with it.

## Other ways it could have been done

- **Binary files**: smaller and faster to read, but no diffs, no hand-editing, and a version change is harder to see. Worth it for big data (the terrain's heights), not for a list of crates.
- **YAML or TOML**: friendlier to write by hand (no quotes, comments built in), but .NET doesn't read them without a package, and YAML's rules have surprises (`no` is `false`).
- **One file per map**: simpler, but two people can't edit one map without merging.
- **The MonoGame content pipeline**: it can turn files into game data at build time. That gives faster loading, but you'd have to rebuild to see a change, which is the opposite of what the studio wants.
- **A schema** (JSON Schema): a file describing the format, so editors like VS Code can check map files and suggest names as you type. A good next step once the format settles.
- **Ready-made editors** such as Tiled (2D) or Blender with custom properties: lots of editing for free, but their files are shaped for them, and you'd still write the importer.

## Exercise (optional): a garden of your own

Write a district file by hand and add it to the home map.

1. Make `Maps/districts/garden.district.json`, version 1, with:
   - a pad at `[-40, -25]`, half `[6, 5]`, to level the garden
   - a `house.two-storey` building, id `gardenhouse`, at `[-40, -35]`: its door's in its north wall, so it faces the garden
   - an oak and two ferns as props (`plant.oak`, `plant.fern`), wherever you like on the pad
   - two `crate.wood` things stacked (the second with `"above": 1`), and a `furniture.armchair` turned 180
   - a start called `garden` at `[-40, -17]`, facing north (a yaw of 0)
2. Add `{"file": "districts/garden.district.json"}` to the end of `districts` in [Maps/home.map.json](../Maps/home.map.json).
3. Write it however you like: spaces, line breaks, entries over several lines.

**Hints:** the yard's file has one of nearly everything to copy from. Keep clear of the yard: pads that overlap are levelled by the later one, and a building standing on the earlier pad would be left on a slope.

**Check it:**
- `Basic.World.exe garden` starts you in your garden, looking at the house, with the oak and the crates in front of it. Walk into the house: its door opens with E.
- Run `dotnet test`. One test fails: `AMapFolderFileSavesAsItWasRead(districts\garden.district.json)`. It shows where your layout and the saved layout part. That's the round trip doing its job: your file reads fine, but saving it would change it. Open the home map in the studio (`Map.Studio.exe`) and save it (Ctrl+S). The studio rewrites only your file, in its own layout, and the test passes. Every file in Maps/ is kept in that layout, so the next change to it is a one-line diff.
- Misspell something on purpose (`"crate.wod"`, or `"trun"`), run the game, and read the message. Then put it right.

**Stretch:** with `Basic.World.exe garden` running, move the armchair in the file and save. A moment later, the game builds the map again with it moved, and you where you were.

## Thinking questions

1. `PadEntry`'s apron is `float? Apron = null` rather than `float Apron = 2f`. What would go wrong, given that values at their defaults aren't written?
2. Why does `FileDistrict` look up every name in its constructor rather than when `Things` or `Buildings` is called?
3. The catalogue measures each item's size from its mesh. A thing's box is that size. What happens to a map file if someone redraws the armchair 10 cm wider? What doesn't happen?

<details>
<summary>Answers</summary>

1. A pad with an apron of exactly 2 would be written without its apron (2 is its default), and read back as 2: fine. But a pad with an apron of 0 would also be left out, since 0 is a `float`'s default and `WhenWritingDefault` compares with that, not with the record's `2f`. It would then be read back as 2: the file changes meaning by being saved. Making the type's default (null) mean "the usual" keeps the two kinds of default the same.
2. So a mistake shows as soon as the map's opened, with the file's name, before any terrain or building is made. Called later, the error would come from deep inside `WorldBuilder.Build`, perhaps after a second of terrain generation, and with less to say about where it came from.
3. Nothing in the file changes: it says `furniture.armchair` and where, not how big. Next time the map's built, the chair's box is 10 cm wider, so it might now touch something next to it. Nothing has to be found and edited by hand, and no stale size is left in a file to disagree with the mesh.

</details>

## Further reading

- Microsoft Learn: *JSON serialization and deserialization in .NET* (System.Text.Json), especially "custom converters" and "ignore properties".
- *Designing Data-Intensive Applications* by Martin Kleppmann, chapter 4, "Encoding and Evolution": versions, and keeping old and new readers and writers working together.
- *Game Programming Patterns* by Robert Nystrom, "Type Object": things defined by data (a catalogue item) rather than by a class each.
