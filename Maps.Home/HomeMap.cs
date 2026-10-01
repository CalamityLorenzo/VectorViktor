using Microsoft.Xna.Framework;
using System.IO;
using World.Buildings;
using World.Core;
using World.Maps;
using World.Maps.Files;

namespace Maps.Home
{
    // The home map: the country round where you start, the town, the street and the lane, built in code. Any app
    // that wants it - the game, the benchmark, the tests, the harness and the map studio - builds it the same way:
    // WorldBuilder.Build(HomeMap.Map). Maps/home.map.json is the same map as a file, with a district from a file added
    // (see AddTo).
    public static class HomeMap
    {
        // Its districts, in the order they're put together: each district's pads are levelled over the ones before
        // it. Where you can start (see Start) is theirs.
        public static IDistrict[] Districts() => new IDistrict[] { new Countryside(), new Town(), new Street(), new Lane() };

        public const string DefaultStart = "hills";

        // All of that as one Map, to pick by name ("home") beside other maps.
        public static readonly Map Map = new Map("home", pads => TerrainGenerator.Create(1, pads), Districts, DefaultStart);

        private static readonly Gable Pitched = Gable.Pitched(35f, alongX: true);

        // Everything of the home map's that a map file can name (see MapLibrary): its terrain, "home"; its districts,
        // "home.countryside" and so on; its houses, "house.two-storey" and "house.bungalow" (see Houses); and the map itself.
        public static void AddTo(MapLibrary library)
        {
            library.AddTerrain("home", (seed, pads) => TerrainGenerator.Create(seed, pads));
            library.AddDistrict("home.countryside", () => new Countryside());
            library.AddDistrict("home.town", () => new Town());
            library.AddDistrict("home.street", () => new Street());
            library.AddDistrict("home.lane", () => new Lane());

            const float wall = 0.3f;   // the outer wall's thickness, and a little to spare
            library.Add(new BuildingKind("house.two-storey", new Vector2(Houses.TwoStoreySize / 2f + wall),
                (b, at) => Houses.TwoStorey(b.Id, b.Name ?? b.Id, at, b.Walls ?? new Color(215, 190, 120), b.Roof ?? new Color(80, 85, 95),
                    b.Flat ? null : Pitched, b.Attic && !b.Flat),
                "two storeys, 7 m square, its front door always in the north wall; with an attic under a pitched roof if asked"));
            library.Add(new BuildingKind("house.bungalow", new Vector2(Houses.BungalowWidth / 2f + wall, Houses.BungalowDepth / 2f + wall),
                (b, at) => Houses.Bungalow(b.Id, b.Name ?? b.Id, at, b.Door switch
                    {
                        Side.North => Walls.North,
                        Side.South => Walls.South,
                        _ => throw new InvalidDataException($"Bungalow '{b.Id}': its door can be in its north or south wall, not its {b.Door.ToString().ToLowerInvariant()}."),
                    }, b.Walls ?? new Color(230, 215, 150), b.Roof ?? new Color(150, 70, 50), b.Flat ? null : Pitched),
                "one storey, 9 m across and 6 deep, its door in its north or south wall"));
            library.Add(Map);
        }
    }
}
