using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using World.Buildings;
using World.Core;

namespace World.Maps.Files
{
    // Everything a map file can name, by name: the terrains, the districts built in code, the kinds of building and the
    // catalogue of things to put about. A file only ever holds names; this is where they're looked up. World.Maps can't
    // know what's in Maps.Home or Maps.Coast (they're built on it, not the other way round: see lesson 01), so each map
    // library adds its own (HomeMap.AddTo(library)), and an app makes a library of what it has.
    public sealed class MapLibrary
    {
        private readonly Dictionary<string, Func<int, IReadOnlyList<TerrainGenerator.Pad>, Terrain>> _terrains =
            new Dictionary<string, Func<int, IReadOnlyList<TerrainGenerator.Pad>, Terrain>>();
        private readonly Dictionary<string, Func<IDistrict>> _districts = new Dictionary<string, Func<IDistrict>>();
        private readonly Dictionary<string, BuildingKind> _buildings = new Dictionary<string, BuildingKind>();
        private readonly Dictionary<string, CatalogueItem> _items = new Dictionary<string, CatalogueItem>();
        private readonly Dictionary<string, Map> _maps = new Dictionary<string, Map>();

        // A library with the standard catalogue in it (see Catalogue), and nothing else yet.
        public MapLibrary()
        {
            foreach (var item in Catalogue.Standard())
                Add(item);
        }

        // A terrain, made from a seed and the pads to level into it.
        public void AddTerrain(string name, Func<int, IReadOnlyList<TerrainGenerator.Pad>, Terrain> make) => _terrains.Add(name, make);

        // A district built in code, made afresh each time a map's built.
        public void AddDistrict(string name, Func<IDistrict> make) => _districts.Add(name, make);

        public void Add(BuildingKind kind) => _buildings.Add(kind.Name, kind);
        public void Add(CatalogueItem item) => _items.Add(item.Name, item);

        // A map built in code: to open by name when there's no file for it.
        public void Add(Map map) => _maps.Add(map.Name, map);

        public IReadOnlyCollection<string> Terrains => _terrains.Keys;
        public IReadOnlyCollection<string> Districts => _districts.Keys;
        public IEnumerable<BuildingKind> BuildingKinds => _buildings.Values.OrderBy(k => k.Name, StringComparer.Ordinal);
        public IEnumerable<CatalogueItem> Items => _items.Values.OrderBy(i => i.Name, StringComparer.Ordinal);
        public IReadOnlyCollection<string> CodeMaps => _maps.Keys;

        public Func<int, IReadOnlyList<TerrainGenerator.Pad>, Terrain> Terrain(string name) => Find(_terrains, "terrain", name);
        public IDistrict District(string name) => Find(_districts, "district built in code", name)();
        public BuildingKind BuildingKind(string name) => Find(_buildings, "kind of building", name);
        public CatalogueItem Item(string name) => Find(_items, "catalogue item", name);
        public bool HasItem(string name) => _items.ContainsKey(name);

        private static T Find<T>(Dictionary<string, T> all, string what, string name) =>
            name != null && all.TryGetValue(name, out var found) ? found : throw new InvalidDataException(MapJson.Unknown(what, name, all.Keys));

        // A map by name: its file, if the maps folder has one (see MapFolder), else the one built in code; or a map file
        // by its path.
        public Map Open(string nameOrPath)
        {
            if (nameOrPath.EndsWith(MapFolder.MapExtension, StringComparison.OrdinalIgnoreCase))
                return MapDocument.Open(nameOrPath).ToMap(this);
            var path = MapFolder.PathOf(nameOrPath);
            if (File.Exists(path))
                return MapDocument.Open(path).ToMap(this);
            return Find(_maps, "map", nameOrPath);
        }

        // Every map there is by name: the files in the maps folder, and those built in code.
        public IEnumerable<string> MapNames() =>
            MapFolder.MapNames().Concat(_maps.Keys).Distinct().OrderBy(n => n, StringComparer.Ordinal);

        // Whether `name` is a map's name (see MapNames), or a map file's path.
        public bool IsMap(string name) =>
            name.EndsWith(MapFolder.MapExtension, StringComparison.OrdinalIgnoreCase) || _maps.ContainsKey(name) || File.Exists(MapFolder.PathOf(name));
    }

    // A kind of building a file can put up by name (see BuildingEntry): `Half` is how far its ground floor's footprint
    // reaches either side of its middle, walls and all, for the ground to be levelled under it; `Make` puts one up,
    // given where its ground floor is (the levelled ground's height, and a step up).
    public sealed record BuildingKind(string Name, Vector2 Half, Func<BuildingEntry, Vector3, Building> Make, string About = "")
    {
        public const float Step = 0.15f;   // its floor above the ground round it
    }

    // Something to put about a map, by name: its mesh (standing on y = 0, centred, facing +Z as all meshes do), how heavy
    // it is when it's loose (see ThingEntry), and whether you walk into it when it's fixed (see PropEntry). Its box is
    // measured from its mesh, once, the first time it's asked for.
    public sealed record CatalogueItem(string Name, MeshSource Mesh, float Mass, bool Solid = true, string About = "")
    {
        private Vector3? _size;

        // Its box: as wide and deep as the mesh reaches either side of its middle, twice over, and as tall as it stands.
        public Vector3 Size => _size ??= Measure(Mesh);

        private static Vector3 Measure(MeshSource mesh)
        {
            using var built = mesh.Build(null);   // on the CPU only (see MeshData.Headless): no device needed
            var (min, max) = (built.Bounds.Min, built.Bounds.Max);
            return new Vector3(2f * MathF.Max(-min.X, max.X), max.Y, 2f * MathF.Max(-min.Z, max.Z));
        }
    }
}
