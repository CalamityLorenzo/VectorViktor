using World.Maps;
using World.Maps.Files;

namespace Maps.Coast
{
    // The coast map: a station on a plateau at the west end, high land crossed by a river in a gorge far to the east,
    // and a sheer cliff down to a beach and the sea (see CoastTerrain); and a railway from the station, over the gorge
    // and north along the cliff top (see Railway). Built with WorldBuilder.Build(CoastMap.Map).
    public static class CoastMap
    {
        // Its districts, in the order they're put together (see HomeMap.Districts).
        public static IDistrict[] Districts() => new IDistrict[] { new Coast(), new Railway(), new Station() };

        public const string DefaultStart = "station";

        public static readonly Map Map = new Map("coast", pads => CoastTerrain.Create(1, pads), Districts, DefaultStart);

        // What a map file can name of it (see MapLibrary): the terrain "coast", the districts "coast.coast",
        // "coast.railway" and "coast.station", and the map itself.
        public static void AddTo(MapLibrary library)
        {
            library.AddTerrain("coast", (seed, pads) => CoastTerrain.Create(seed, pads));
            library.AddDistrict("coast.coast", () => new Coast());
            library.AddDistrict("coast.railway", () => new Railway());
            library.AddDistrict("coast.station", () => new Station());
            library.Add(Map);
        }
    }
}
