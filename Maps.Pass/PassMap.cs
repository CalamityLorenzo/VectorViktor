using World.Maps;
using World.Maps.Files;

namespace Maps.Pass
{
    // The pass map: one way through the mountains, for a road, winding up and down over two passes, past a lake under
    // cliffs and a tarn, down into a flat basin with a lake in its middle where a town will go (see PassTerrain,
    // PassRoute); the road along it, from the trailhead to the basin (see Road); and where it can be driven but not
    // walked, a ford and a jump (see Crossings). Built with WorldBuilder.Build(PassMap.Map).
    public static class PassMap
    {
        // Its districts, in the order they're put together (see HomeMap.Districts).
        public static IDistrict[] Districts() => new IDistrict[] { new Pass(), new Road(), new Crossings() };

        public const string DefaultStart = "trailhead";

        public static readonly Map Map = new Map("pass", pads => PassTerrain.Create(1, pads), Districts, DefaultStart);

        // What a map file can name of it (see MapLibrary): the terrain "pass"; the districts "pass.pass", "pass.road" and
        // "pass.crossings"; and the map itself.
        public static void AddTo(MapLibrary library)
        {
            library.AddTerrain("pass", (seed, pads) => PassTerrain.Create(seed, pads));
            library.AddDistrict("pass.pass", () => new Pass());
            library.AddDistrict("pass.road", () => new Road());
            library.AddDistrict("pass.crossings", () => new Crossings());
            library.Add(Map);
        }
    }
}
