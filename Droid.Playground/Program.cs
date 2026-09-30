using Droid.Playground;
using Maps.Coast;
using Maps.Home;
using Maps.Pass;
using System.Linq;
using World.Maps;

// Droid.Playground [experiment] [map] [start]: an experiment by name (see Experiments.All; "drive" if none), a map by
// name (home, coast or pass; home if none) and a start on it (its default if none), in any order.
Map[] maps = { HomeMap.Map, CoastMap.Map, PassMap.Map };
var experiment = args.FirstOrDefault(Experiments.Has);
var map = maps.FirstOrDefault(m => args.Contains(m.Name)) ?? HomeMap.Map;
var start = args.FirstOrDefault(a => !Experiments.Has(a) && maps.All(m => m.Name != a));
using var playground = new Playground(experiment ?? Experiments.Default, map, start);
playground.Run();
