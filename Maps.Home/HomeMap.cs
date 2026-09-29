using World.Maps;

namespace Maps.Home
{
    // The home map: the country round where you start, the town, the street and the lane, built in code. Any app
    // that wants it - the game, the benchmark, the tests, and later the harness and the map studio - builds it the
    // same way: WorldBuilder.Build(HomeMap.Districts()).
    public static class HomeMap
    {
        // Its districts, in the order they're put together: each district's pads are levelled over the ones before
        // it. Where you can start (see Start) is theirs.
        public static IDistrict[] Districts() => new IDistrict[] { new Countryside(), new Town(), new Street(), new Lane() };

        public const string DefaultStart = "hills";
    }
}
