using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using static World.Buildings.Walls;

namespace Basic.World
{
    // A street east of the town: a straight road running east-west, a pair of two-storey houses on its
    // south side and a pair of bungalows on its north, all facing it, under pitched roofs. Between the bungalows a
    // T-junction, where the lane begins (see Lane). Each house stands GardenRise above
    // the road, its front garden sloping gently down to the pavement, and behind it a back garden with a
    // white picket fence round it and a gateway in its west side. The second house on the south side has
    // a swimming pool in its back garden, deep enough to swim in, with a shallow end to walk out at. On
    // the empty lots at the north-east end, a billboard for the Commodore 64 on the north side of the road and
    // one for Atari opposite it, on the south, look out over it, down the street.
    //
    // It's all levelled out of the hills by pads (see TerrainGenerator.Pad), every one of them level with
    // the road's middle: the gardens GardenRise above it, the pool dug into one of them, and the road
    // itself last, so the front gardens' slopes run down onto it.
    public sealed class Street : IDistrict
    {
        public static readonly Vector2 RoadCentre = new Vector2(80f, 30f);
        public const float RoadLength = 60f;                   // the street, x 50 to 110
        public const float GardenRise = 0.6f;
        private static readonly Gable RoofPitch = Gable.Pitched(35f, alongX: true);   // every house's ridge running along the street
        private const float FrontGarden = 7f, BackGarden = 12f, PlotHalfWidth = 8f;
        private const float WallThickness = 0.25f;             // Building's default

        // How high the road is: level with the hills at its middle. The lane starts from it.
        public static readonly RoadNetwork.Level Level = new(RoadCentre);

        // The road: the T-junction at x 76 between the north side's two bungalows, and a straight either side of it
        // each way along the street, all level.
        private static readonly RoadNetwork Road = new(
            new RoadNetwork.Straight(new(50f, 30f), new(60f, 30f), Level, Level),
            new RoadNetwork.Straight(new(60f, 30f), new(69f, 30f), Level, Level),
            new RoadNetwork.Junction(new(76f, 30f), MathHelper.PiOver2, Level),
            new RoadNetwork.Straight(new(83f, 30f), new(90f, 30f), Level, Level),
            new RoadNetwork.Straight(new(90f, 30f), new(100f, 30f), Level, Level),
            new RoadNetwork.Straight(new(100f, 30f), new(110f, 30f), Level, Level));

        // A house's plot: which side of the road (+1 south, -1 north), where along it, and what stands on it
        private enum Kind { TwoStorey, Bungalow }
        private readonly record struct Plot(string Id, Kind Kind, int Side, float X, Color Outside, Color Roof)
        {
            public float Width => Kind == Kind.TwoStorey ? Houses.TwoStoreySize : Houses.BungalowWidth;
            public float Depth => Kind == Kind.TwoStorey ? Houses.TwoStoreySize : Houses.BungalowDepth;
            public float Front => RoadCentre.Y + Side * (RoadNetwork.Half + FrontGarden);           // the house's front wall, outside
            public float Middle => Front + Side * (WallThickness + Depth / 2f);
            public float Back => Middle + Side * (Depth / 2f + WallThickness);                      // its back wall, outside
            public float Rear => Back + Side * BackGarden;                                          // the end of the back garden
        }

        private static readonly Plot[] Plots =
        {
            new("street1", Kind.TwoStorey, +1, 64f, new Color(200, 120, 90), new Color(90, 60, 55)),
            new("street2", Kind.TwoStorey, +1, 88f, new Color(170, 190, 210), new Color(70, 75, 90)),
            // Set wide apart, for the lane between them
            new("street3", Kind.Bungalow, -1, 61f, new Color(230, 215, 150), new Color(150, 70, 50)),
            new("street4", Kind.Bungalow, -1, 91f, new Color(180, 210, 170), new Color(100, 60, 50)),
        };

        // The swimming pool, in street2's back garden: the water's edge (a rectangle on the grid, so the ground
        // round it stays level right up to it), and inside that, dug out, a deep part and a shallow end
        private static readonly Vector2 PoolCentre = new Vector2(87f, 55.5f), PoolHalf = new Vector2(4f, 2.5f);
        private const float PoolDepth = 1.6f, ShallowDepth = 0.7f;   // below the garden
        private const float PoolFreeboard = 0.1f;                     // water below the garden

        // The billboards on the empty lots at the north-east end, each turned to face down the street, towards the
        // town, and across the road: its face looking west and a little towards the road. Commodore's is on the
        // north side, Atari's opposite it on the south, its mirror image.
        private static readonly Billboard[] Billboards =
        {
            new(BillboardDesign.Commodore64, new Vector2(106f, 20f), MathF.Atan2(-0.9f, 0.45f), new Vector2(101f, 25f), MathHelper.PiOver4),
            new(BillboardDesign.Atari, new Vector2(106f, 40f), MathF.Atan2(-0.9f, -0.45f), new Vector2(101f, 35f), MathHelper.Pi * 0.75f),
        };

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["street"] = new(RoadCentre - new Vector2(RoadLength / 2f + 2f, 0f), MathHelper.PiOver2),   // at the west end of the street, looking down it
            ["billboard"] = new(Billboards[0].View, Billboards[0].ViewHeading),  // in front of the Commodore billboard, looking at it
            ["atari"] = new(Billboards[1].View, Billboards[1].ViewHeading),      // in front of the Atari one, across the road
            ["pool"] = new(new Vector2(84f, 51.5f), MathHelper.Pi * 0.75f),      // in a back garden, by its swimming pool
            ["junction"] = new(new Vector2(76f, 36f), 0f),                       // on the street, looking up the lane
        };

        public IEnumerable<TerrainGenerator.Pad> Pads
        {
            get
            {
                foreach (var plot in Plots)
                {
                    var near = Math.Min(plot.Front, plot.Rear);
                    var far = Math.Max(plot.Front, plot.Rear);
                    yield return new TerrainGenerator.Pad(new Vector2(plot.X, (near + far) / 2f), new Vector2(PlotHalfWidth, (far - near) / 2f),
                        Apron: 0.5f, Blend: FrontGarden - 0.5f, Raise: GardenRise, LevelWith: RoadCentre);
                }
                // The pool, dug straight down: its corners inside the water's edge, one cell in all round
                yield return new TerrainGenerator.Pad(PoolCentre - new Vector2(1f, 0f), new Vector2(PoolHalf.X - 2f, PoolHalf.Y - 1f),
                    Apron: 0f, Blend: 0f, Raise: GardenRise - PoolDepth, LevelWith: RoadCentre);
                yield return new TerrainGenerator.Pad(new Vector2(PoolCentre.X + PoolHalf.X - 1.5f, PoolCentre.Y), new Vector2(0.5f, PoolHalf.Y - 1f),
                    Apron: 0f, Blend: 0f, Raise: GardenRise - ShallowDepth, LevelWith: RoadCentre);
                foreach (var billboard in Billboards)
                    yield return billboard.Pad(RoadCentre);
                // The road, over the gardens' slopes: one long pad for all its pieces
                yield return new TerrainGenerator.Pad(RoadCentre, new Vector2(RoadLength / 2f, RoadNetwork.Half), Apron: 0.5f, Blend: 6f);
            }
        }

        private static float RoadLevel(Terrain terrain) => terrain.HeightAt(RoadCentre.X, RoadCentre.Y);

        public IEnumerable<Pool> Pools(Terrain terrain)
        {
            yield return Pool.Rectangle(PoolCentre, PoolHalf, RoadLevel(terrain) + GardenRise - PoolFreeboard);
        }

        public IEnumerable<Building> Buildings(Terrain terrain)
        {
            var floor = RoadLevel(terrain) + GardenRise + Houses.FloorLift;
            foreach (var plot in Plots)
            {
                var at = new Vector3(plot.X, floor, plot.Middle);
                var name = "Street house " + plot.Id.Substring("street".Length);
                yield return plot.Kind == Kind.TwoStorey
                    ? Houses.TwoStorey(plot.Id, name, at, plot.Outside, plot.Roof, RoofPitch)
                    : Houses.Bungalow(plot.Id, name, at, plot.Side > 0 ? North : South, plot.Outside, plot.Roof, RoofPitch);
            }
        }

        // Each back garden's fence: from the house's back corner out to the side of the plot and back to the
        // gateway, then from the gateway round the rest of the garden to the house's other back corner.
        private static IEnumerable<Vector2[]> FenceRuns(Plot plot)
        {
            const float inset = 0.3f, gateFrom = 1f, gateTo = 2.2f;
            var s = plot.Side;
            var corner = plot.Width / 2f + WallThickness;
            var west = plot.X - PlotHalfWidth + inset;
            var east = plot.X + PlotHalfWidth - inset;
            var rear = plot.Rear - s * inset;
            yield return new[] { new Vector2(plot.X - corner, plot.Back), new Vector2(west, plot.Back), new Vector2(west, plot.Back + s * gateFrom) };
            yield return new[]
            {
                new Vector2(west, plot.Back + s * gateTo), new Vector2(west, rear), new Vector2(east, rear),
                new Vector2(east, plot.Back), new Vector2(plot.X + corner, plot.Back),
            };
        }

        // The fences and the billboards' posts, to walk into (see BuildingGround)
        public IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            foreach (var plot in Plots)
                foreach (var run in FenceRuns(plot))
                    for (var k = 0; k < run.Length - 1; k++)
                    {
                        var middle = (run[k] + run[k + 1]) / 2f;
                        var ground = terrain.HeightAt(middle.X, middle.Y);
                        yield return new WallSegment(run[k], run[k + 1], ground - 0.2f, ground + FenceMesh.Height);
                    }
            foreach (var billboard in Billboards)
                foreach (var wall in billboard.Walls(terrain))
                    yield return wall;
        }

        // Everything to draw: the road, the fences, the pool's paving and the billboards.
        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            foreach (var fixture in Road.Fixtures(terrain))
                yield return fixture;

            var fencePalette = FenceMesh.Palette(new Color(240, 240, 235));
            foreach (var plot in Plots)
            {
                var n = 0;
                foreach (var run in FenceRuns(plot))
                    yield return new Fixture(new MeshSource($"fence:{plot.Id}:{n++}", d => FenceMesh.Build(d, terrain.HeightAt, run), fencePalette), Matrix.Identity);
            }

            yield return new Fixture(new MeshSource("poolsurround", d => PoolSurroundMesh.Build(d, PoolHalf.X, PoolHalf.Y),
                PoolSurroundMesh.Palette(new Color(215, 205, 185))),
                Matrix.CreateTranslation(PoolCentre.X, RoadLevel(terrain) + GardenRise, PoolCentre.Y));

            foreach (var billboard in Billboards)
                yield return billboard.Fixture(terrain);
        }

        public bool Bare(float x, float z) => Road.Paved(x, z);
    }
}
