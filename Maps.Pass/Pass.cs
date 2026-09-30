using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Core;
using World.Maps;
using static Maps.Pass.PassTerrain;

namespace Maps.Pass
{
    // The pass's ground (see PassTerrain): the car, parked at the trailhead to drive the road in; places to start along
    // the road's way, in the car; and on foot in the basin by the town's lake. The road (see Road) is a district of its
    // own, and the town will be.
    public sealed class Pass : IDistrict
    {
        public static readonly float TrailheadAlong = PassRoute.ControlAlong[PassRoute.Trailhead] + 12f;

        public IEnumerable<ParkedCar> Cars(Terrain terrain)
        {
            yield return new ParkedCar(PassRoute.At(TrailheadAlong).at, PassRoute.YawAt(TrailheadAlong));
        }

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["trailhead"] = OnTheRoad(TrailheadAlong),                                   // in the car, at the start of the way
            ["firstpass"] = OnTheRoad(PassRoute.ControlAlong[PassRoute.FirstPass]),
            ["lake"] = OnTheRoad(PassRoute.ControlAlong[PassRoute.LakeShelf]),         // on the shelf above the lake
            ["secondpass"] = OnTheRoad(PassRoute.ControlAlong[PassRoute.SecondPass]),
            // On foot, in the basin, north of the town's lake, looking over it
            ["town"] = new(TownLakeCentre - new Vector2(0f, TownLakeRadius + TownLakeShore + 20f), MathHelper.Pi, Above: 2f),
        };

        // In a car on the road's middle line `s` along it, facing the way to the town
        private static Start OnTheRoad(float s) => new(PassRoute.At(s).at, PassRoute.YawAt(s), Above: 2f, InCar: true);
    }
}
