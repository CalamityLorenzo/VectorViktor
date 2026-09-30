using World.Maps;

namespace Maps.Pass
{
    // The pass map: one way through the mountains, for a road, winding up and down over two passes, past a lake under
    // cliffs and a tarn, down into a flat basin with a lake in its middle where a town will go (see PassTerrain,
    // PassRoute); and the road along it, from the trailhead to the basin (see Road). Built with WorldBuilder.Build(PassMap.Map).
    public static class PassMap
    {
        // Its districts, in the order they're put together (see HomeMap.Districts).
        public static IDistrict[] Districts() => new IDistrict[] { new Pass(), new Road() };

        public const string DefaultStart = "trailhead";

        public static readonly Map Map = new Map("pass", pads => PassTerrain.Create(1, pads), Districts, DefaultStart);
    }
}
