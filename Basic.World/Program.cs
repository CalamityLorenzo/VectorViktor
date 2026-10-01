using Maps.Coast;
using Maps.Home;
using Maps.Pass;
using System.Linq;
using World.Maps.Files;

// Optional arguments: 2 for the road layout (Game2); otherwise which map (home, coast or pass, or any other map file in the
// Maps folder by its name, or by its path; home if none), where to start on it (any key of a district's Starts on that map;
// its DefaultStart if none - README.md lists them all), and bird to watch it from the camera chasing the bird roaming round
// there - in any order: "town bird", "bird", "coast beach". A map with a file is read from it (see MapFolder), and read again
// whenever it's saved.
if (args.Length > 0 && args[0] == "2")
{
    using var roads = new Basic.World.Game2();
    roads.Run();
}
else
{
    var library = new MapLibrary();
    HomeMap.AddTo(library);
    CoastMap.AddTo(library);
    PassMap.AddTo(library);
    var followBird = args.Contains("bird");
    var name = args.FirstOrDefault(library.IsMap) ?? "home";
    using var game = new Basic.World.Game1(args.FirstOrDefault(a => a != "bird" && !library.IsMap(a)), followBird,
                                           library.Open(name), () => library.Open(name));
    game.Run();
}
