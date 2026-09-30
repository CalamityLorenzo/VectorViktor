# VectorViktor: command-line switches and controls

Every runnable project in the solution that reads arguments, and what it accepts; for `Droid.Playground` and
`Basic.Levels`, their keyboard and controller controls too. Arguments go after `--` when run
through `dotnet run`, for example:

```
dotnet run --project Basic.World -- coast beach bird
```

Projects not listed here (`VectorViktor`, `LoadingModelMeshes`, `Tools.DearImGui`, the test projects) take no
arguments.

---

## Basic.World

```
Basic.World [2] | [map] [start] [bird]
```

| Argument | Meaning |
|---|---|
| `2` | Runs the road-layout showcase (`Game2`) instead of the world. Only counts as the **first** argument; everything else is ignored. |
| `home` / `coast` / `pass` | Which map to load. Default `home`. |
| *start name* | Where to start on that map (see [Maps and starts](#maps-and-starts)). Default is the map's own default start. An unknown name falls back to the default. |
| `bird` | Begin with the camera chasing the bird that roams round the start (toggle in game with `B`). |

Apart from `2`, the arguments can be in any order:

```
Basic.World town bird
Basic.World bird
Basic.World coast beach
Basic.World pass lake
Basic.World 2
```

## Droid.Playground

```
Droid.Playground [experiment] [map] [start]
```

All three optional, in any order.

| Argument | Meaning |
|---|---|
| `drive` | Experiment: plain driving of the droid. **Default.** |
| `segway` | Experiment: the segway droid. |
| `home` / `coast` / `pass` | Which map. Default `home`. |
| *start name* | Where to start on that map (see [Maps and starts](#maps-and-starts)). Unknown names fall back to the map's default. |

The experiment and start can also be changed from the playground's ImGui panel once it's running. New experiments are
registered in `Experiments.All` in [Droid.Playground/Experiment.cs](Droid.Playground/Experiment.cs).

```
Droid.Playground segway coast clifftop
Droid.Playground pass trailhead
```

### Controls

Keys are ignored while an ImGui panel has the keyboard. The controller is the first one plugged in (Xbox layout).

| Action | Keyboard | Controller |
|---|---|---|
| Drive forward / back | Up / Down (also W / S, except in the free camera) | Left stick |
| Sidestep | A / D (except in the free camera) | Left stick |
| Turn | Left / Right | Right stick left / right |
| Go faster | Shift | Right trigger |
| Head camera round its visor | Q / E | LB / RB |
| Tilt the head camera | R / F | Right stick up / down |
| Put the head camera back | Home | Click the right stick |
| Open or shut a door | Enter | A |
| Head / drone / free camera | F1 / F2 / F3 | B (cycles through them) |
| Pause | P | Start |
| Step one tick (while paused) | N | D-pad down |
| Slow down / speed up time | `[` / `]` | D-pad left / right |
| Drop the droid under the free camera | T | D-pad up |
| Drop the droid on the ground clicked | Ctrl + left click | — |
| Colours on / off | C | Y |
| Low-resolution look on / off | L | — |
| Full screen | F11 | — |
| Exit | Escape | Back |

In the free camera the controls move the camera, not the droid (the arrow keys still drive it):

| Action | Keyboard / mouse | Controller |
|---|---|---|
| Fly | W / A / S / D | Left stick |
| Look | Mouse, right button held | Right stick |
| Rise / sink | Space / Ctrl | RB / LB |
| Faster | Shift | Right trigger |

## Basic.Levels

```
Basic.Levels [room]
```

| Argument | Meaning |
|---|---|
| *room id* | Start at that room's first door instead of the end of the corridor. Unknown ids are ignored. |

Room ids (from [Basic.Levels/HouseLevel.cs](Basic.Levels/HouseLevel.cs)):

`corridor`, `stairs`, `upper`, `lounge`, `tvroom`, `cola`, `hangar`, `backroom`, `octagon`, `octagonupper`,
`octagontop`

### Controls

Walk into a door to go through it.

| Action | Keyboard | Controller |
|---|---|---|
| Walk forward / back | Up / Down, W / S | Left stick, D-pad up / down |
| Sidestep | A / D | Left stick |
| Turn | Left / Right | Right stick, D-pad left / right |
| Run | Shift (hold) | Right trigger or left stick click (hold) |
| Colours on / off | Space | Y |
| Low-resolution look on / off | L | — |
| Full screen | F11 | — |
| Exit | Escape | Back |

## Basic.Models

```
Basic.Models [1|2|3]
```

| Argument | Meaning |
|---|---|
| `1` | `Game1`: the original spinning-mesh scene. |
| `2` | `Game2`: showcase of every mesh in a 5 x 3 grid, each turning slowly. **Default**, also used for no argument or anything else. |
| `3` | `Game3`: three staircases (straight, quarter turn each way) under an orbit camera. |

## Basic.World.Benchmark

```
dotnet run -c Release --project Basic.World.Benchmark [-- frames [output.md]]
```

| Argument | Meaning |
|---|---|
| `frames` | Frames drawn per start. Default `300`. |
| `output.md` | Also write the results table to this file (only read if `frames` is given too). |

It always benchmarks every start on the **home** map, with no window. Run in Release for timings; a Debug build also
runs the debug-only mesh checks.

---

## Maps and starts

Used by `Basic.World` and `Droid.Playground`. Pick the map by name, then the start by name. A start only means
something on its own map, and with no map named it's the `home` map: `town` on its own is the home map's town, and
`pass town` is the one by the lake on the pass.

### `home` (default start: `hills`)

| Start | Where |
|---|---|
| `hills` | Looking north-east to the plateau |
| `plateau` | On top, facing its sheer south side |
| `causeway` | At the foot of the causeway, facing up it |
| `basin` | In the basin (in the lake) |
| `lockers` | Facing the first locker, to push it over |
| `far` | Out in the far country, looking back towards home |
| `pond` | East of the pond, facing it |
| `droid` | Facing the droid on show |
| `lane` | On the lane behind the plateau, heading west |
| `cottage` | Up the lane, looking down it at the cottage |
| `window` | In the cottage's front garden, looking in at its window |
| `hangar` | In the next cottage's garden, looking in at its window |
| `corridor` | Just inside the long corridor, looking up it |
| `hangarfloor` | In the hangar, just in from its door, looking at the trees |
| `hangarwindow` | In the hangar, looking out of its window |
| `street` | West end of the street, looking down it |
| `billboard` | In front of the Commodore billboard |
| `atari` | In front of the Atari billboard, across the road |
| `pool` | In a back garden, by its swimming pool |
| `junction` | On the street, looking up the lane |
| `town` | North of the buildings, facing them |
| `house` | Outside the house's doorway |
| `bedroom` | In the house's bedroom, facing the ladder to the attic |
| `attic` | Up in the house's attic, facing its east end |
| `barn` | Outside the barn |

### `coast` (default start: `station`)

| Start | Where |
|---|---|
| `station` | On the platform, in front of the ticket hall's door, looking east down the line |
| `hall` | In the ticket hall, facing its door |
| `river` | Near the gorge's west edge, south of the bridge, looking across |
| `clifftop` | A few steps from the edge, looking out to sea |
| `beach` | On the beach, facing the cliff |
| `bridge` | On the beam, facing the bridge's gate |
| `coastline` | On the beam along the cliff top, facing north |

### `pass` (default start: `trailhead`)

| Start | Where |
|---|---|
| `trailhead` | In the car, at the start of the road |
| `firstpass` | In the car, at the first pass |
| `lake` | In the car, on the shelf above the lake |
| `secondpass` | In the car, at the second pass |
| `town` | On foot in the basin, north of the town's lake, looking over it |

Starts are defined per district, in each district's `Starts` dictionary under `Maps.Home`, `Maps.Coast` and
`Maps.Pass`; a new entry there is picked up by both programs automatically.

---

## Environment variable: `BASIC_WORLD_SHOT`

Not a switch, but it works like one. Any game built on `RetroGame` (`Basic.World`, `Basic.Levels`,
`Droid.Playground`) reads it, saves one frame to a file, then exits without needing anyone at the screen:

```
BASIC_WORLD_SHOT="file.png;seconds;keys"
```

| Part | Meaning |
|---|---|
| `file.png` | Where to save the frame (the low-res picture; for `Droid.Playground`, the whole window with its panels). |
| `seconds` | How long after starting to take it. Default `3`. |
| `keys` | Letters for what to do first, from the table below. Optional. |

| Key | Games | Effect |
|---|---|---|
| `c` | all | Colours off (the wireframe look) |
| `w` | Basic.World, Droid.Playground | Walk forward |
| `r` | Basic.World, Droid.Playground | Run (with `w`) |
| `v` | Basic.World | Switch view (first person / drone) |
| `b` | Basic.World | Camera chasing the bird |
| `e` | Basic.World | Press E (get in/out of a car, open a door) half way to the shot |
| `f` | Droid.Playground | Free camera |
| `d` | Droid.Playground | Drone camera (default is head) |
| `o` | Droid.Playground | Show the overlays: walls, bodies, joints, capsule |

PowerShell example:

```powershell
$env:BASIC_WORLD_SHOT = "shot.png;5;wc"; dotnet run --project Basic.World -- coast beach; Remove-Item Env:BASIC_WORLD_SHOT
```
