using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Maps;
using static Maps.Coast.CoastTerrain;

namespace Maps.Coast
{
    // The coast's ground (see CoastTerrain) and nothing more: places to start by the river and the sea. The station
    // and the railway are districts of their own.
    public sealed class Coast : IDistrict
    {
        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["river"] = new(new Vector2(RiverAt(20f) - GorgeHalfWidth - 4f, 20f), MathHelper.PiOver2),       // near its gorge's west edge, south of the bridge, looking across
            ["clifftop"] = new(new Vector2(CliffAt(0f) - 6f, 0f), MathHelper.PiOver2),                       // a few steps from the edge, looking out to sea
            ["beach"] = new(new Vector2((CliffAt(0f) + CliffFace + ShoreAt(0f)) / 2f, 0f), -MathHelper.PiOver2),   // facing the cliff
        };
    }
}
