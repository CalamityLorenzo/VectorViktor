using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace World.Maps.Files
{
    // A map as a file (Maps/*.map.json; the map's name is the file's): the terrain it's on, by name (see MapLibrary),
    // with its seed; where to start when no start is asked for; and its districts, in the order they're put together
    // (see WorldBuilder), each a district built in code, by the name it's registered with ("home.town"), or a district
    // file (see DistrictFile), by its path from the map file's folder.
    public sealed class MapFile
    {
        public const int CurrentVersion = 1;

        [JsonRequired]
        public int Version { get; set; } = CurrentVersion;
        public string About { get; set; }
        public string Terrain { get; set; }
        public int Seed { get; set; }
        public string DefaultStart { get; set; }
        public List<DistrictRef> Districts { get; set; } = new List<DistrictRef>();

        public static MapFile Load(string path)
        {
            var file = MapJson.Read<MapFile>(path);
            Versions.Check(path, file.Version, CurrentVersion);
            file.Districts ??= new List<DistrictRef>();
            if (string.IsNullOrEmpty(file.Terrain))
                throw new InvalidDataException($"{path}: it doesn't say which \"terrain\" it's on.");
            if (string.IsNullOrEmpty(file.DefaultStart))
                throw new InvalidDataException($"{path}: it has no \"defaultStart\".");
            foreach (var district in file.Districts)
                if ((district.Code == null) == (district.File == null))
                    throw new InvalidDataException($"{path}: each of its districts is either {{\"code\": name}} or {{\"file\": path}}.");
            return file;
        }

        public bool Save(string path) => MapJson.Save(path, this);
    }

    // One of a map's districts: built in code (Code, its registered name), or from a file (File, its path from the map's).
    public sealed record DistrictRef(string Code = null, string File = null)
    {
        public static DistrictRef OfCode(string name) => new DistrictRef(Code: name);
        public static DistrictRef OfFile(string path) => new DistrictRef(File: path);
    }
}
