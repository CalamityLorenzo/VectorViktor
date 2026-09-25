using MeshCore.Library;
using MeshProps;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using static World.Buildings.Walls;

namespace Basic.World
{
    // A street east of the town: a straight road running east-west, a pair of two-storey houses on its
    // south side and a pair of bungalows on its north, all facing it, under pitched roofs. Each house stands GardenRise above
    // the road, its front garden sloping gently down to the pavement, and behind it a back garden with a
    // white picket fence round it and a gateway in its west side. The second house on the south side has
    // a swimming pool in its back garden, deep enough to swim in, with a shallow end to walk out at. On
    // the empty lot at the north-east end, a billboard for the Commodore 64 looks out over the road.
    //
    // It's all levelled out of the hills by pads (see TerrainGenerator.Pad), every one of them level with
    // the road's middle: the gardens GardenRise above it, the pool dug into one of them, and the road
    // itself last, so the front gardens' slopes run down onto it.
    public sealed class Neighbourhood : IDistrict
    {
        public static readonly Vector2 RoadCentre = new Vector2(80f, 30f);
        public const float RoadLength = 60f;                   // six 10 m straights
        public const float GardenRise = 0.6f;
        private static readonly Gable RoofPitch = Gable.Pitched(35f, alongX: true);   // every house's ridge running along the street
        private const float FrontGarden = 7f, BackGarden = 12f, PlotHalfWidth = 8f;
        private const float WallThickness = 0.25f;             // Building's default
        private static readonly float RoadHalf = RoadBuilder.LaneWidth + RoadBuilder.PavementWidth;   // centre line to the back of the pavement

        // Just short of the road's west end, on its centre line
        public static readonly Vector2 StreetStart = RoadCentre - new Vector2(RoadLength / 2f + 2f, 0f);

        // A house's plot: which side of the road (+1 south, -1 north), where along it, and what stands on it
        private enum Kind { TwoStorey, Bungalow }
        private readonly record struct Plot(string Id, Kind Kind, int Side, float X, Color Outside, Color Roof)
        {
            public float Width => Kind == Kind.TwoStorey ? Houses.TwoStoreySize : Houses.BungalowWidth;
            public float Depth => Kind == Kind.TwoStorey ? Houses.TwoStoreySize : Houses.BungalowDepth;
            public float Front => RoadCentre.Y + Side * (RoadHalf + FrontGarden);                  // the house's front wall, outside
            public float Middle => Front + Side * (WallThickness + Depth / 2f);
            public float Back => Middle + Side * (Depth / 2f + WallThickness);                      // its back wall, outside
            public float Rear => Back + Side * BackGarden;                                          // the end of the back garden
        }

        private static readonly Plot[] Plots =
        {
            new("street1", Kind.TwoStorey, +1, 64f, new Color(200, 120, 90), new Color(90, 60, 55)),
            new("street2", Kind.TwoStorey, +1, 88f, new Color(170, 190, 210), new Color(70, 75, 90)),
            new("street3", Kind.Bungalow, -1, 64f, new Color(230, 215, 150), new Color(150, 70, 50)),
            new("street4", Kind.Bungalow, -1, 88f, new Color(180, 210, 170), new Color(100, 60, 50)),
        };

        // The swimming pool, in street2's back garden: the water's edge (a rectangle on the grid, so the ground
        // round it stays level right up to it), and inside that, dug out, a deep part and a shallow end
        private static readonly Vector2 PoolCentre = new Vector2(87f, 55.5f), PoolHalf = new Vector2(4f, 2.5f);
        private const float PoolDepth = 1.6f, ShallowDepth = 0.7f;   // below the garden
        private const float PoolFreeboard = 0.1f;                     // water below the garden

        // On the empty lot at the north-east end, turned to face down the street, towards the town: its face
        // (the mesh's +Z) looking west-south-west
        private static readonly Vector2 BillboardAt = new Vector2(106f, 20f);
        private static readonly float BillboardYaw = MathF.Atan2(-0.9f, 0.45f);
        private static Vector2 OnBillboard(float x, float z)
        {
            var turned = Vector3.Transform(new Vector3(x, 0f, z), Matrix.CreateRotationY(BillboardYaw));
            return BillboardAt + new Vector2(turned.X, turned.Z);
        }

        // Where to stand to see it: in front of it, down the street
        public static readonly Vector2 BillboardView = new Vector2(101f, 25f);

        // Where to stand to see it all from behind: in the pool's garden, facing the pool
        public static readonly Vector2 PoolSide = new Vector2(84f, 51.5f);

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["street"] = new(StreetStart, MathHelper.PiOver2),                  // at the west end of the street, looking down it
            ["billboard"] = new(BillboardView, MathHelper.PiOver4),              // in front of the billboard, looking at it
            ["pool"] = new(PoolSide, MathHelper.Pi * 0.75f),                    // in a back garden, by its swimming pool
        };

        public static readonly TerrainGenerator.Pad[] Pads = MakePads();

        IEnumerable<TerrainGenerator.Pad> IDistrict.Pads => Pads;
        IEnumerable<Pool> IDistrict.Pools(Terrain terrain) => new[] { SwimmingPool(terrain) };
        IEnumerable<Building> IDistrict.Buildings(Terrain terrain) => Buildings(terrain);
        IEnumerable<WallSegment> IDistrict.Walls(Terrain terrain) => Walls(terrain);
        IEnumerable<Fixture> IDistrict.Fixtures(Terrain terrain) => Fixtures(terrain);
        bool IDistrict.Bare(float x, float z) => Paved(x, z);

        private static TerrainGenerator.Pad[] MakePads()
        {
            var pads = new List<TerrainGenerator.Pad>();
            foreach (var plot in Plots)
            {
                var near = Math.Min(plot.Front, plot.Rear);
                var far = Math.Max(plot.Front, plot.Rear);
                pads.Add(new TerrainGenerator.Pad(new Vector2(plot.X, (near + far) / 2f), new Vector2(PlotHalfWidth, (far - near) / 2f),
                    Apron: 0.5f, Blend: FrontGarden - 0.5f, Raise: GardenRise, LevelWith: RoadCentre));
            }
            // The pool, dug straight down: its corners inside the water's edge, one cell in all round
            pads.Add(new TerrainGenerator.Pad(PoolCentre - new Vector2(1f, 0f), new Vector2(PoolHalf.X - 2f, PoolHalf.Y - 1f),
                Apron: 0f, Blend: 0f, Raise: GardenRise - PoolDepth, LevelWith: RoadCentre));
            pads.Add(new TerrainGenerator.Pad(new Vector2(PoolCentre.X + PoolHalf.X - 1.5f, PoolCentre.Y), new Vector2(0.5f, PoolHalf.Y - 1f),
                Apron: 0f, Blend: 0f, Raise: GardenRise - ShallowDepth, LevelWith: RoadCentre));
            // Level ground under the billboard, so both its posts stand in it alike - level with the road, since
            // the road's slope reaches it, and would tip one end of it otherwise
            pads.Add(new TerrainGenerator.Pad(BillboardAt, new Vector2(4f, 2.5f), Apron: 1f, Blend: 4f, LevelWith: RoadCentre));
            // The road, last, over the gardens' slopes
            pads.Add(new TerrainGenerator.Pad(RoadCentre, new Vector2(RoadLength / 2f, RoadHalf), Apron: 0.5f, Blend: 6f));
            return pads.ToArray();
        }

        // Under the road: no terrain grid there (it'd show through the tarmac, 2 cm above it)
        public static bool Paved(float x, float z) =>
            MathF.Abs(z - RoadCentre.Y) <= RoadHalf && MathF.Abs(x - RoadCentre.X) <= RoadLength / 2f;

        private static float RoadLevel(Terrain terrain) => terrain.HeightAt(RoadCentre.X, RoadCentre.Y);

        public static Pool SwimmingPool(Terrain terrain) =>
            Pool.Rectangle(PoolCentre, PoolHalf, RoadLevel(terrain) + GardenRise - PoolFreeboard);

        public static List<Building> Buildings(Terrain terrain)
        {
            var floor = RoadLevel(terrain) + GardenRise + Houses.FloorLift;
            var buildings = new List<Building>();
            foreach (var plot in Plots)
            {
                var at = new Vector3(plot.X, floor, plot.Middle);
                var name = "Street house " + plot.Id.Substring("street".Length);
                buildings.Add(plot.Kind == Kind.TwoStorey
                    ? Houses.TwoStorey(plot.Id, name, at, plot.Outside, plot.Roof, RoofPitch)
                    : Houses.Bungalow(plot.Id, name, at, plot.Side > 0 ? North : South, plot.Outside, plot.Roof, RoofPitch));
            }
            return buildings;
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

        // The fences, and the billboard's posts, to walk into (see BuildingGround)
        public static IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            foreach (var plot in Plots)
                foreach (var run in FenceRuns(plot))
                    for (var k = 0; k < run.Length - 1; k++)
                    {
                        var middle = (run[k] + run[k + 1]) / 2f;
                        var ground = terrain.HeightAt(middle.X, middle.Y);
                        yield return new WallSegment(run[k], run[k + 1], ground - 0.2f, ground + FenceMesh.Height);
                    }

            var foot = terrain.HeightAt(BillboardAt.X, BillboardAt.Y);
            foreach (var x in BillboardMesh.PostsAt)
            {
                // Each post, as its four sides
                var half = BillboardMesh.PostSize / 2f;
                var corners = new[]
                {
                    OnBillboard(x - half, BillboardMesh.PostZ - half), OnBillboard(x + half, BillboardMesh.PostZ - half),
                    OnBillboard(x + half, BillboardMesh.PostZ + half), OnBillboard(x - half, BillboardMesh.PostZ + half),
                };
                for (var k = 0; k < 4; k++)
                    yield return new WallSegment(corners[k], corners[(k + 1) % 4], foot - 1f, foot + BillboardMesh.Clearance + BillboardMesh.Height);
            }
        }

        // Everything else to draw: the road, the fences, the pool's paving and the billboard.
        public static List<Fixture> Fixtures(Terrain terrain)
        {
            var things = new List<Fixture>();
            var road = RoadLevel(terrain);

            var roadPalette = RoadMesh.Palette(new Color(70, 70, 75), new Color(170, 165, 155), Color.White, new Color(60, 140, 50));
            for (var k = 0; k < (int)(RoadLength / 10f); k++)
            {
                var x = RoadCentre.X - RoadLength / 2f + 5f + k * 10f;
                things.Add(new Fixture(new MeshSource("road-straight", d => RoadMesh.Straight(d), roadPalette),
                    Matrix.CreateRotationY(MathHelper.PiOver2) * Matrix.CreateTranslation(x, road, RoadCentre.Y)));
            }

            var fencePalette = FenceMesh.Palette(new Color(240, 240, 235));
            foreach (var plot in Plots)
            {
                var n = 0;
                foreach (var run in FenceRuns(plot))
                    things.Add(new Fixture(new MeshSource($"fence:{plot.Id}:{n++}", d => FenceMesh.Build(d, terrain.HeightAt, run), fencePalette), Matrix.Identity));
            }

            things.Add(new Fixture(new MeshSource("poolsurround", d => PoolSurroundMesh.Build(d, PoolHalf.X, PoolHalf.Y),
                PoolSurroundMesh.Palette(new Color(215, 205, 185))),
                Matrix.CreateTranslation(PoolCentre.X, road + GardenRise, PoolCentre.Y)));

            things.Add(new Fixture(new MeshSource("billboard", BillboardMesh.Build, BillboardMesh.Palette(new Color(110, 110, 115), new Color(80, 80, 85))),
                Matrix.CreateRotationY(BillboardYaw) * Matrix.CreateTranslation(BillboardAt.X, terrain.HeightAt(BillboardAt.X, BillboardAt.Y), BillboardAt.Y)));
            return things;
        }
    }
}
