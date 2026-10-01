using Maps.Coast;
using Maps.Home;
using Maps.Pass;
using MapStudio;
using System;
using System.IO;
using System.Linq;
using World.Maps.Files;

// Map.Studio [map]: a map file to open, by its name in the Maps folder (home, coast, pass) or by its path; home if none.
var library = new MapLibrary();
HomeMap.AddTo(library);
CoastMap.AddTo(library);
PassMap.AddTo(library);

var name = args.FirstOrDefault() ?? "home";
var path = name.EndsWith(MapFolder.MapExtension, StringComparison.OrdinalIgnoreCase) ? name : MapFolder.PathOf(name);
if (!File.Exists(path))
{
    Console.Error.WriteLine($"There's no map file {path}. The maps folder has: {string.Join(", ", MapFolder.MapNames())}.");
    return 1;
}
using var studio = new Studio(library, path);
studio.Run();
return 0;
