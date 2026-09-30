using Maps.Coast;
using Maps.Home;
using Maps.Pass;
using System.Linq;
using World.Maps;

// Optional arguments: 2 for the road layout (Game2); otherwise which map (home, coast or pass; home if none), where to start
// on it (any key of a district's Starts on that map; its DefaultStart if none - README.md lists them all), and bird to
// watch it from the camera chasing the bird roaming round there - in any order: "town bird", "bird", "coast beach".
Map[] maps = { HomeMap.Map, CoastMap.Map, PassMap.Map };
if (args.Length > 0 && args[0] == "2")
{
    using var roads = new Basic.World.Game2();
    roads.Run();
}
else
{
    var followBird = args.Contains("bird");
    var map = maps.FirstOrDefault(m => args.Contains(m.Name));
    using var game = new Basic.World.Game1(args.FirstOrDefault(a => a != "bird" && maps.All(m => m.Name != a)), followBird, map);
    game.Run();
}
