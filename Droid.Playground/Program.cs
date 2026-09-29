using Droid.Playground;
using System.Linq;

// Droid.Playground [experiment] [start]: an experiment by name (see Experiments.All; "drive" if none) and a start on
// the home map (see HomeMap; its default if none), in either order.
var experiment = args.FirstOrDefault(Experiments.Has);
var start = args.FirstOrDefault(a => !Experiments.Has(a));
using var playground = new Playground(experiment ?? Experiments.Default, start);
playground.Run();
