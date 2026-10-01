using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace World.Maps.Files
{
    // Where map files live: the repository's Maps folder. Each app gets a copy of it beside itself when it's built (see
    // World.Maps.csproj), but run from inside the repository, an app uses the repository's own instead - so the map
    // studio saves where git sees it, and every app running sees what the studio's just saved.
    public static class MapFolder
    {
        public const string MapExtension = ".map.json";
        public const string DistrictExtension = ".district.json";
        public const string DistrictsFolder = "districts";
        private const string Solution = "VectorViktor.slnx";

        private static string _found;

        public static string Find() => _found ??= Search();

        private static string Search()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, Solution)) && Directory.Exists(Path.Combine(folder.FullName, "Maps")))
                    return Path.Combine(folder.FullName, "Maps");
            return Path.Combine(AppContext.BaseDirectory, "Maps");
        }

        public static string PathOf(string mapName) => Path.Combine(Find(), mapName + MapExtension);

        // A map's or district's name: its file's, without the extension ("districts/yard.district.json" is "yard").
        public static string NameOf(string path)
        {
            var name = Path.GetFileName(path);
            foreach (var extension in new[] { MapExtension, DistrictExtension, ".json" })
                if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return name[..^extension.Length];
            return name;
        }

        public static IEnumerable<string> MapNames() =>
            Directory.Exists(Find()) ? Directory.GetFiles(Find(), "*" + MapExtension).Select(NameOf) : Enumerable.Empty<string>();
    }
}
