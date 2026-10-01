using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace World.Maps.Files
{
    // A map file and the district files it names, loaded together: what the map studio edits and saves, and what an app
    // builds a Map from (ToMap). District files are found by their paths from the map file's folder.
    public sealed class MapDocument
    {
        private readonly Dictionary<string, DistrictFile> _districts = new Dictionary<string, DistrictFile>(StringComparer.OrdinalIgnoreCase);

        private MapDocument(string path, MapFile file)
        {
            Path = System.IO.Path.GetFullPath(path);
            File = file;
        }

        public string Path { get; }
        public MapFile File { get; }

        // The map's name: its file's, without the extension
        public string Name => MapFolder.NameOf(Path);
        public string Folder => System.IO.Path.GetDirectoryName(Path);

        public static MapDocument Open(string path)
        {
            var document = new MapDocument(path, MapFile.Load(path));
            foreach (var district in document.File.Districts.Where(d => d.File != null))
                document._districts[district.File] = DistrictFile.Load(document.PathOf(district.File));
            return document;
        }

        // A district file's path, from its path relative to the map file
        public string PathOf(string districtFile) => System.IO.Path.GetFullPath(System.IO.Path.Combine(Folder, districtFile));

        // Each district file named by the map, by its path from the map file, loaded
        public IReadOnlyDictionary<string, DistrictFile> Districts => _districts;

        // Every file it's made from: the map's own, then its districts'.
        public IEnumerable<string> Files => new[] { Path }.Concat(_districts.Keys.Select(PathOf));

        // A district file of its own, new: added last to the map's districts, at `path` from the map file.
        public DistrictFile AddDistrict(string path, DistrictFile district)
        {
            if (_districts.ContainsKey(path))
                throw new InvalidOperationException($"The map already has a district file '{path}'.");
            _districts[path] = district;
            File.Districts.Add(DistrictRef.OfFile(path));
            return district;
        }

        // The map file and each district file written out, where that changes them (see MapJson.Save): which it wrote.
        public IReadOnlyList<string> Save()
        {
            var written = new List<string>();
            if (File.Save(Path))
                written.Add(Path);
            foreach (var (path, district) in _districts)
                if (district.Save(PathOf(path)))
                    written.Add(PathOf(path));
            return written;
        }

        // The map, to build (see WorldBuilder.Build): its districts in order, those from files as they are in memory now.
        // Each district's made again for each build, as a code-built map's are.
        public Map ToMap(MapLibrary library)
        {
            var terrain = library.Terrain(File.Terrain);
            var seed = File.Seed;
            var districts = File.Districts.ToArray();
            var files = districts.Where(d => d.File != null).ToDictionary(d => d.File, d => _districts[d.File].Copy());
            foreach (var district in districts.Where(d => d.Code != null))
                library.District(district.Code);   // so a name that isn't there fails now, not when it's built
            IDistrict[] Make() => districts.Select(d => d.Code != null ? library.District(d.Code)
                : new FileDistrict(MapFolder.NameOf(d.File), files[d.File], library)).ToArray();
            Make();   // likewise for what the district files name
            return new Map(Name, pads => terrain(seed, pads), Make, File.DefaultStart) { Files = Files.ToArray() };
        }
    }
}
