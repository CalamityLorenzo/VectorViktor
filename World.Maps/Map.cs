using System;
using System.Collections.Generic;
using World.Core;

namespace World.Maps
{
    // A whole map: its name, to pick it by (see the apps' Program); how its terrain is made, given every district's
    // pads to level into it (see TerrainGenerator.Levelled); its districts, in the order they're put together; and
    // where to start when no start is asked for. WorldBuilder.Build(map) builds it.
    public sealed record Map(string Name, Func<IReadOnlyList<TerrainGenerator.Pad>, Terrain> MakeTerrain, Func<IDistrict[]> Districts,
                             string DefaultStart)
    {
        // The files it was read from, if it was (see Files.MapDocument): the map file, then its district files. None for
        // a map built in code.
        public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
    }
}
