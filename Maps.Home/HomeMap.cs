using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Maps;
using World.Maps.Files;

namespace Maps.Home
{
    // The home map: the country round where you start, the town, the street, the lane and the droid's workshop yard, built in code. Any app
    // that wants it - the game, the benchmark, the tests, the harness and the map studio - builds it the same way:
    // WorldBuilder.Build(HomeMap.Map). Maps/home.map.json is the same map as a file, with a district from a file added
    // (see AddTo).
    public static class HomeMap
    {
        // Its districts, in the order they're put together: each district's pads are levelled over the ones before
        // it. Where you can start (see Start) is theirs.
        public static IDistrict[] Districts() => new IDistrict[] { new Countryside(), new Town(), new Street(), new Lane(), new Workshop() };

        public const string DefaultStart = "hills";

        // All of that as one Map, to pick by name ("home") beside other maps.
        public static readonly Map Map = new Map("home", pads => TerrainGenerator.Create(1, pads), Districts, DefaultStart);

        private static readonly Gable Pitched = Gable.Pitched(35f, alongX: true);

        // The way a wall faces, out from the building, clockwise from north (as a start's yaw)
        private static float Facing(Side side) => side switch
        {
            Side.North => 0f,
            Side.East => MathHelper.PiOver2,
            Side.South => MathHelper.Pi,
            _ => -MathHelper.PiOver2,
        };

        // Everything of the home map's that a map file can name (see MapLibrary): its terrain, "home"; its districts,
        // "home.countryside" and so on; its houses, "house.two-storey" and "house.bungalow" (see Houses), and a cottage
        // like the lane's, "house.lane-cottage" (see LaneCottage); and the map itself.
        public static void AddTo(MapLibrary library)
        {
            library.AddTerrain("home", (seed, pads) => TerrainGenerator.Create(seed, pads));
            library.AddDistrict("home.countryside", () => new Countryside());
            library.AddDistrict("home.town", () => new Town());
            library.AddDistrict("home.street", () => new Street());
            library.AddDistrict("home.lane", () => new Lane());
            library.AddDistrict("home.workshop", () => new Workshop());

            const float wall = 0.3f;   // the outer wall's thickness, and a little to spare
            library.Add(new BuildingKind("house.two-storey", new Vector2(Houses.TwoStoreySize / 2f + wall),
                (b, at) => Houses.TwoStorey(b.Id, b.Name ?? b.Id, at, b.Walls ?? new Color(215, 190, 120), b.Roof ?? new Color(80, 85, 95),
                    b.Flat ? null : Pitched, b.Attic && !b.Flat),
                "two storeys, 7 m square, its front door always in the north wall; with an attic under a pitched roof if asked")
                { CanBeFlat = true, CanHaveAttic = true });
            library.Add(new BuildingKind("house.bungalow", new Vector2(Houses.BungalowWidth / 2f + wall, Houses.BungalowDepth / 2f + wall),
                (b, at) => Houses.Bungalow(b.Id, b.Name ?? b.Id, at, b.Door == Side.South ? Walls.South : Walls.North,
                    b.Walls ?? new Color(230, 215, 150), b.Roof ?? new Color(150, 70, 50), b.Flat ? null : Pitched),
                "one storey, 9 m across and 6 deep, its door in its north or south wall")
                { Doors = new[] { Side.North, Side.South }, CanBeFlat = true });
            library.Add(new BuildingKind("house.lane-cottage", new Vector2(LaneCottage.Half.Y, LaneCottage.Half.X) + new Vector2(wall),
                (b, at) => new LaneCottage(b.Id, b.At, HouseMesh.Palette(b.Walls ?? new Color(235, 225, 205), b.Roof ?? new Color(150, 60, 45),
                    new Color(60, 90, 60), new Color(90, 130, 190), new Color(130, 75, 55)), Beyond(b.View), Facing(b.Door)),
                "a cottage like the lane's, 9 m along its front and 4.5 deep, its front (door and window) in the wall its door's in: " +
                "no insides, but through its window a parlour, or the hangar, or nothing (the window stays shut)", TurnsWithDoor: true)
                {
                    Doors = new[] { Side.North, Side.East, Side.South, Side.West }, Views = new[] { "parlour", "hangar", "none" },
                    Doorway = b => new LaneCottage(b.Id, b.At, Array.Empty<Color>(), null, Facing(b.Door)).Doorway,
                });
            library.Add(Map);
        }

        // What a cottage's window looks onto (see LaneCottage), by name, a parlour if it doesn't say; nothing: it's shut
        private static Func<IReadOnlyList<ScenePart>> Beyond(string view) => view switch
        {
            "hangar" => Hangar.SeenFromCottage,
            "none" => null,
            _ => Parlour.Parts,
        };
    }
}
