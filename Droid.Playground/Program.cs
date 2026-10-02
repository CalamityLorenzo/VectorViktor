using Droid.Playground;
using Maps.Coast;
using Maps.Home;
using Maps.Pass;
using Microsoft.Xna.Framework;
using System.Globalization;
using System.Linq;
using World.Core.Characters;
using World.Maps.Files;

// Droid.Playground [experiment] [map] [start] [base] [at=x,z] [yaw=degrees]: an experiment by name (see Experiments.All; "drive"
// if none), a map by name (home, coast, pass or any other map file in the Maps folder) or a map file's path (home if none),
// a start on it (its default if none), and what the droid goes about on (segway, tracks or tri-star: see Locomotion; segway
// if none), in any order. at= drops the droid at that point on the ground instead, facing yaw=
// (as the map studio's "Play here" does). A map from a file is built again whenever its files are saved.
var library = new MapLibrary();
HomeMap.AddTo(library);
CoastMap.AddTo(library);
PassMap.AddTo(library);

string? Setting(string name) => args.FirstOrDefault(a => a.StartsWith(name + "="))?[(name.Length + 1)..];
float[]? Numbers(string? text) => text?.Split(',').Select(n => float.Parse(n, CultureInfo.InvariantCulture)).ToArray();

var plain = args.Where(a => !a.Contains('=')).ToArray();
var experiment = plain.FirstOrDefault(Experiments.Has);
var mapName = plain.FirstOrDefault(library.IsMap) ?? "home";
var locomotion = plain.Select(Locomotions.Named).FirstOrDefault(l => l != null) ?? Locomotion.Segway;
var start = plain.FirstOrDefault(a => !Experiments.Has(a) && !library.IsMap(a) && Locomotions.Named(a) == null);
var at = Numbers(Setting("at")) is { Length: 2 } xz ? new Vector2(xz[0], xz[1]) : (Vector2?)null;
var yaw = MathHelper.ToRadians(Numbers(Setting("yaw"))?[0] ?? 0f);

using var playground = new Playground(experiment ?? Experiments.Default, library.Open(mapName), start, () => library.Open(mapName),
                                      at is { } point ? (point, yaw) : null, locomotion);
playground.Run();
