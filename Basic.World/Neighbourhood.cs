using MeshCore.Library;
using MeshProps;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Core;
using static World.Buildings.Walls;

namespace Basic.World
{
    // A street east of the town: a straight road running east-west, a pair of two-storey houses on its
    // south side and a pair of bungalows on its north, all facing it, under pitched roofs. Between the bungalows a
    // T-junction, and a lane from it out past their back gardens and round the plateau to the causeway's foot (see
    // Roads). Each house stands GardenRise above
    // the road, its front garden sloping gently down to the pavement, and behind it a back garden with a
    // white picket fence round it and a gateway in its west side. The second house on the south side has
    // a swimming pool in its back garden, deep enough to swim in, with a shallow end to walk out at. On
    // the empty lots at the north-east end, a billboard for the Commodore 64 on the north side of the road and
    // one for Atari opposite it, on the south, look out over it, down the street. Down the
    // lane, on its west side, an old cottage (VectorViktor's house, see HouseMesh) looks out over it. Walk into
    // its front door and you're in a long corridor, off in a scene of its own the way Basic.Levels' rooms are;
    // walk into the door you came in by and you're back outside the cottage; walk up it to the door at its far end
    // and you're in the hangar the next cottage's window looks into, and back again. Next door to it, another cottage. Come up
    // to either's front window and it thins away to show what's inside - in the old one a parlour, in the next one a
    // hangar far bigger than the cottage, three trees going round on turntables in it (see Window).
    //
    // It's all levelled out of the hills by pads (see TerrainGenerator.Pad), every one of them level with
    // the road's middle: the gardens GardenRise above it, the pool dug into one of them, and the road
    // itself last, so the front gardens' slopes run down onto it.
    public sealed class Neighbourhood : IDistrict
    {
        public static readonly Vector2 RoadCentre = new Vector2(80f, 30f);
        public const float RoadLength = 60f;                   // the street, x 50 to 110
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
        // town, and across the road: its face (the mesh's +Z) looking west and a little towards the road. Commodore's
        // is on the north side, Atari's opposite it on the south, its mirror image.
        private sealed record Billboard(string Id, BillboardDesign Design, Vector2 At, float Yaw, Vector2 View, float ViewHeading)
        {
            // A point in its own (X, Z), in the world
            public Vector2 InWorld(float x, float z)
            {
                var turned = Vector3.Transform(new Vector3(x, 0f, z), Matrix.CreateRotationY(Yaw));
                return At + new Vector2(turned.X, turned.Z);
            }
        }

        private static readonly Billboard[] Billboards =
        {
            new("billboard", BillboardDesign.Commodore64, new Vector2(106f, 20f), MathF.Atan2(-0.9f, 0.45f), new Vector2(101f, 25f), MathHelper.PiOver4),
            new("atari", BillboardDesign.Atari, new Vector2(106f, 40f), MathF.Atan2(-0.9f, -0.45f), new Vector2(101f, 35f), MathHelper.Pi * 0.75f),
        };

        // The old cottage by the lane, and another next door to it, south down the lane: HouseMesh scaled up to 9 m
        // along its front, which faces east, onto the lane, a small front garden back from the pavement. Each has a
        // window onto somewhere else (see Window): the old one's onto a parlour, as if behind it, the next one's onto a
        // hangar far bigger than it, with three trees going round on turntables in it.
        private const float CottageScale = 4.5f;
        private static readonly Vector2 CottageHalf = new Vector2(HouseMesh.Depth, HouseMesh.Width) * (CottageScale / 2f);   // turned: its front along Z

        private sealed record Cottage(string Id, Vector2 At, Color[] Palette)
        {
            public float Front => At.X + CottageHalf.X;
            public float WindowZ => At.Y + HouseMesh.WindowOffset * CottageScale;   // south of its door: the mesh's +X end of the front, turned
            public float Ground(Terrain terrain) => terrain.HeightAt(At.X, At.Y);
        }

        private static readonly Cottage OldCottage = new("cottage", new Vector2(64.25f, -35f),
            HouseMesh.Palette(new Color(235, 225, 205), new Color(150, 60, 45), new Color(60, 90, 60), new Color(90, 130, 190), new Color(130, 75, 55)));
        private static readonly Cottage NextDoor = new("nextdoor", new Vector2(64.25f, -21f),
            HouseMesh.Palette(new Color(195, 205, 215), new Color(80, 80, 95), new Color(150, 40, 40), new Color(90, 130, 190), new Color(120, 70, 50)));
        private static readonly Cottage[] Cottages = { OldCottage, NextDoor };

        // The old cottage's front door, in the east wall: the mesh's -X end of the front is north, turned
        private static readonly float CottageDoorZ = OldCottage.At.Y + HouseMesh.DoorOffset * CottageScale;
        private const float CottageDoorHalf = HouseMesh.DoorWidth * CottageScale / 2f;

        private const float ParlourStep = 0.3f;   // the parlour's floor above the ground, so you look down into it over the sill
        private const float HangarDrop = 2f;      // the hangar's floor below the ground: you look down into it, as if from a gallery

        // The long corridor behind it: off the map, well out of sight of it (and of the fog, from it), running
        // north from its door in the south wall. So long that its far end, and the door there into the hangar,
        // fades into the fog.
        private const float CorridorWidth = 2.4f, CorridorLength = 80f;
        private static readonly RoomSpec Corridor = new RoomSpec
        {
            Id = "longcorridor", Name = "Long corridor",
            Outline = RoomSpec.Rectangle(CorridorWidth, CorridorLength), Height = 2.6f,
            WorldOffset = new Vector3(0f, 0f, 1500f),
            Floor = new Color(70, 70, 70), WallA = new Color(0, 150, 150), WallB = new Color(0, 105, 105), Ceiling = new Color(210, 140, 80),
            Doors = new[] { new DoorSpec("cottage", South, 0f, "", ""), new DoorSpec("hangar", North, 0f, "", "") },   // see Portals
        };
        private const float ArrivalDistance = 1f;   // how far in from a door you are when you come through it

        // The hangar the next cottage's window looks into (see HangarParts), really there, to walk round: off the map
        // too, well away from the corridor. HangarMesh's roof stands in a plain room its size, whose floor and walls
        // are what you walk on and into, coloured as the hangar's are, so they don't show where they meet it. Its
        // front wall, the one the window looks in from, has a door in the middle, back into the corridor; the
        // turntables are solid, to jump up onto.
        private static readonly Color HangarFloor = new Color(120, 120, 125), HangarRoof = new Color(150, 160, 170), HangarWall = new Color(110, 115, 125);
        // The three trees, each on its turntable: where, across the hangar and back from its front, how big, how fast
        // it goes round (radians a second, the middle one the other way), and how thick its trunk is to walk into
        private readonly record struct HangarTree(MeshSource Mesh, float Scale, float X, float Z, float Turn, float Trunk);
        private static readonly HangarTree[] HangarTrees =
        {
            new(new MeshSource("tree", TreeMesh.Build, TreeMesh.Palette(new Color(100, 70, 40), new Color(60, 150, 60))), 4.5f, -7f, 12f, 0.5f, 0.35f),
            new(new MeshSource("oak", OakMesh.Build, OakMesh.Palette(new Color(90, 65, 40), new Color(70, 130, 50))), 3f, 0f, 20f, -0.3f, 0.6f),
            new(new MeshSource("tree", TreeMesh.Build, TreeMesh.Palette(new Color(100, 70, 40), new Color(150, 170, 50))), 4.5f, 7f, 12f, 0.8f, 0.35f),
        };

        // A point in the hangar, so far across and back from its front, in the room's own (X, Z): the room's centred
        private static Vector2 InHangarRoom(float x, float z) => new Vector2(x, z - HangarMesh.Depth / 2f);

        // The hangar's window, in its front wall beside the door (so far across from it): looking out of it is looking
        // out of the next cottage's front window, from inside it, onto the lane (see HangarWindow)
        private const float HangarWindowAcross = 6f;

        private const float TableSize = 6f;   // the turntables, across
        private static readonly RoomSpec HangarRoom = new RoomSpec
        {
            Id = "hangar", Name = "Hangar",
            Outline = RoomSpec.Rectangle(HangarMesh.Width, HangarMesh.Depth), Height = HangarMesh.Height,
            WorldOffset = new Vector3(300f, 0f, 1500f),
            GridSpacing = 4f,
            Floor = HangarFloor, WallA = HangarWall, WallB = HangarWall, Ceiling = HangarRoof,
            Doors = new[] { new DoorSpec("corridor", North, 0f, "", "") },
            Ramps = Array.ConvertAll(HangarTrees, tree =>
            {
                var middle = InHangarRoom(tree.X, tree.Z);
                var top = TurntableMesh.Height * TableSize;
                var half = TurntableMesh.Radius * TableSize * 0.9f;   // about the octagon's width across its flats
                return new RampSpec(new Vector3(middle.X, top, middle.Y - half), new Vector3(middle.X, top, middle.Y + half), half * 2f, Thickness: top);
            }),
        };

        // Where to stand to see it all from behind: in the pool's garden, facing the pool
        public static readonly Vector2 PoolSide = new Vector2(84f, 51.5f);

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["street"] = new(StreetStart, MathHelper.PiOver2),                  // at the west end of the street, looking down it
            ["billboard"] = new(Billboards[0].View, Billboards[0].ViewHeading),  // in front of the Commodore billboard, looking at it
            ["atari"] = new(Billboards[1].View, Billboards[1].ViewHeading),      // in front of the Atari one, across the road
            ["pool"] = new(PoolSide, MathHelper.Pi * 0.75f),                    // in a back garden, by its swimming pool
            ["junction"] = new(new Vector2(76f, 36f), 0f),                       // on the street, looking up the lane
            ["lane"] = new(new Vector2(40f, -65f), -MathHelper.PiOver2),         // on the lane behind the plateau, heading west
            ["cottage"] = new(new Vector2(77f, -21f), -0.75f),                   // up the lane, looking down it at the cottage
            ["window"] = new(new Vector2(OldCottage.Front + 3f, OldCottage.WindowZ), -MathHelper.PiOver2),   // in the cottage's front garden, looking in at its window
            ["hangar"] = new(new Vector2(NextDoor.Front + 3f, NextDoor.WindowZ), -MathHelper.PiOver2),       // in the next cottage's, looking in at its
            ["corridor"] = new(new Vector2(Corridor.WorldOffset.X, Corridor.WorldOffset.Z + CorridorLength / 2f - ArrivalDistance), 0f),   // just inside, looking up it
            ["hangarfloor"] = ArrivingBy(HangarRoom, HangarRoom.Doors[0]),       // in the hangar, just in from its door, looking at the trees
            ["hangarwindow"] = new(new Vector2(HangarRoom.WorldOffset.X + HangarWindowAcross, HangarRoom.WorldOffset.Z - HangarMesh.Depth / 2f + 3f), 0f),   // in the hangar, looking out of its window
        };

        IEnumerable<TerrainGenerator.Pad> IDistrict.Pads => Pads;
        IEnumerable<Pool> IDistrict.Pools(Terrain terrain) => new[] { SwimmingPool(terrain) };
        IEnumerable<Building> IDistrict.Buildings(Terrain terrain) => Buildings(terrain);
        IEnumerable<WallSegment> IDistrict.Walls(Terrain terrain) => Walls(terrain);
        IEnumerable<Fixture> IDistrict.Fixtures(Terrain terrain) => Fixtures(terrain);
        IEnumerable<Window> IDistrict.Windows(Terrain terrain) => Windows(terrain);
        IEnumerable<ScenePart> IDistrict.Moving(Terrain terrain) => Moving();
        IEnumerable<Portal> IDistrict.Portals(Terrain terrain) => Portals(terrain);
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
            // Level ground under each billboard, so both its posts stand in it alike - level with the road, since
            // the road's slope reaches it, and would tip one end of it otherwise
            foreach (var billboard in Billboards)
                pads.Add(new TerrainGenerator.Pad(billboard.At, new Vector2(4f, 2.5f), Apron: 1f, Blend: 4f, LevelWith: RoadCentre));
            // The cottages' plots, a metre round each, both level with the hills where the old one stands - which
            // is as high as the lane out in front of the next one, where the hills are lower
            foreach (var cottage in Cottages)
                pads.Add(new TerrainGenerator.Pad(cottage.At, CottageHalf + new Vector2(1f), Apron: 1f, Blend: 4f, LevelWith: OldCottage.At));
            // The street, over the gardens' slopes, and the lane from it
            pads.Add(new TerrainGenerator.Pad(RoadCentre, new Vector2(RoadLength / 2f, RoadHalf), Apron: 0.5f, Blend: 6f));
            pads.AddRange(LanePads());
            return pads.ToArray();
        }

        // ---- The roads: the street, and the lane from its T-junction to the causeway

        // How high a road is somewhere: level with the hills at With, and Raise above them.
        private readonly record struct Level(Vector2 With, float Raise = 0f);

        private static readonly Level Street = new(RoadCentre);

        // A piece of road (see RoadMesh), laid on its own pad of levelled ground.
        private abstract record Piece
        {
            // How far its pad blends back into the hills
            public float Blend { get; init; } = 6f;
        }

        // A straight from A to B, sloping evenly between the levels at its two ends.
        private sealed record Straight(Vector2 A, Vector2 B, Level AtA, Level AtB) : Piece;

        // A T-junction, the through road along Z turned by Turn, its side road off to the turned +X (see RoadMesh.TJunction).
        private sealed record Junction(Vector2 Centre, float Turn, Level At) : Piece;

        // A quarter-turn bend of radius BendRadius, turned by Turn (see RoadMesh.Corner): in heading the turned +Z, out heading the turned +X.
        private sealed record Bend(Vector2 Centre, float Turn, Level At) : Piece;

        private const float JunctionSize = 14f, JunctionCorner = 3f, BendRadius = 10f;
        private const float RoadLift = 0.03f;

        // The lane's corners, levelled a little above the hills there, and the foot of the causeway it ends at
        private static readonly Level FirstBend = new(new Vector2(76f, -65f), 0.3f);
        private static readonly Level SecondBend = new(new Vector2(-30f, -65f), 0.5f);
        private static readonly Level ThirdBend = new(new Vector2(-30f, -30f), 0.4f);
        private static readonly Level CausewayFoot = new(new Vector2(-9f, -30f));

        // The street runs east-west, level all along, with the T-junction at x 76 between the north side's two
        // bungalows. The lane leaves it northwards, level between their gardens, then runs down past the backs of
        // them to a bend onto the plateau's north side, west along it, round and down a cutting through the ridge
        // west of the plateau, and round again onto the causeway's foot, heading up it: every straight sloping
        // evenly from the level of the piece before it to the level of the next.
        private static readonly Piece[] Roads =
        {
            new Straight(new(50f, 30f), new(60f, 30f), Street, Street),
            new Straight(new(60f, 30f), new(69f, 30f), Street, Street),
            new Junction(new(76f, 30f), MathHelper.PiOver2, Street),
            new Straight(new(83f, 30f), new(90f, 30f), Street, Street),
            new Straight(new(90f, 30f), new(100f, 30f), Street, Street),
            new Straight(new(100f, 30f), new(110f, 30f), Street, Street),

            new Straight(new(76f, 23f), new(76f, -3f), Street, Street) { Blend = 2f },   // between the gardens: they stand close
            new Straight(new(76f, -3f), new(76f, -55f), Street, FirstBend),
            new Bend(FirstBend.With, MathHelper.Pi, FirstBend),
            new Straight(new(66f, -65f), new(-20f, -65f), FirstBend, SecondBend),
            new Bend(SecondBend.With, -MathHelper.PiOver2, SecondBend),
            new Straight(new(-30f, -55f), new(-30f, -40f), SecondBend, ThirdBend),
            new Bend(ThirdBend.With, 0f, ThirdBend),
            new Straight(new(-20f, -30f), new(-9f, -30f), ThirdBend, CausewayFoot),
        };

        // After Roads, which they're made from
        public static readonly TerrainGenerator.Pad[] Pads = MakePads();

        // The street's pieces lie on its one long pad (see MakePads); the lane's each on its own.
        private static bool OnTheStreet(Piece piece) =>
            piece is Junction || piece is Straight { A.Y: 30f, B.Y: 30f };

        // A point in a piece's own plan: turned back by `turn`, about its centre.
        private static Vector2 Local(Vector2 p, Vector2 centre, float turn)
        {
            var d = p - centre;
            var (sin, cos) = MathF.SinCos(turn);
            return new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
        }

        // Whether a piece's tarmac and pavements are over (x, z)
        private static bool Covers(Piece piece, float x, float z)
        {
            var p = new Vector2(x, z);
            switch (piece)
            {
                case Straight s:
                    var along = s.B - s.A;
                    var local = Local(p, (s.A + s.B) / 2f, MathF.Atan2(along.X, along.Y));
                    return MathF.Abs(local.X) <= RoadHalf && MathF.Abs(local.Y) <= along.Length() / 2f;
                case Junction j:
                    var inJunction = Local(p, j.Centre, j.Turn);
                    return inJunction.X >= -RoadHalf && inJunction.X <= JunctionSize / 2f && MathF.Abs(inJunction.Y) <= JunctionSize / 2f;
                case Bend b:
                    var inBend = Local(p, b.Centre, b.Turn);
                    var fromMiddle = Vector2.Distance(inBend, new Vector2(BendRadius, -BendRadius));   // the centre line curves round this
                    return MathF.Abs(inBend.X) <= BendRadius && MathF.Abs(inBend.Y) <= BendRadius && MathF.Abs(fromMiddle - BendRadius) <= RoadHalf;
                default:
                    return false;
            }
        }

        // Round each piece, in the map, for ruling most of them out at once: the square its turned footprint fits in
        private static readonly (float minX, float maxX, float minZ, float maxZ)[] RoadBounds = Array.ConvertAll(Roads, piece =>
        {
            var (centre, reach) = piece switch
            {
                Straight s => ((s.A + s.B) / 2f, (s.B - s.A).Length() / 2f + RoadHalf),
                Junction j => (j.Centre, JunctionSize),
                Bend b => (b.Centre, BendRadius * 1.5f),
                _ => (Vector2.Zero, 0f),
            };
            return (centre.X - reach, centre.X + reach, centre.Y - reach, centre.Y + reach);
        });

        // Under a road: no terrain grid there (it'd show through the tarmac, 2 cm above it)
        public static bool Paved(float x, float z)
        {
            for (var k = 0; k < Roads.Length; k++)
            {
                var (minX, maxX, minZ, maxZ) = RoadBounds[k];
                if (x >= minX && x <= maxX && z >= minZ && z <= maxZ && Covers(Roads[k], x, z))
                    return true;
            }
            return false;
        }

        // The lane's pads: each piece's ground, level or sloping with it.
        private static IEnumerable<TerrainGenerator.Pad> LanePads()
        {
            foreach (var piece in Roads)
            {
                if (OnTheStreet(piece))
                    continue;
                switch (piece)
                {
                    case Straight s:
                        var half = s.A.X == s.B.X
                            ? new Vector2(RoadHalf, MathF.Abs(s.B.Y - s.A.Y) / 2f)
                            : new Vector2(MathF.Abs(s.B.X - s.A.X) / 2f, RoadHalf);
                        yield return new TerrainGenerator.Pad((s.A + s.B) / 2f, half, Apron: 1f, Blend: s.Blend, Raise: s.AtA.Raise, LevelWith: s.AtA.With,
                            Slope: new TerrainGenerator.PadSlope(s.A, s.B, s.AtB.With, s.AtB.Raise));
                        break;
                    case Bend b:
                        yield return new TerrainGenerator.Pad(b.Centre, new Vector2(BendRadius), Apron: 0.5f, Blend: b.Blend, Raise: b.At.Raise, LevelWith: b.At.With);
                        break;
                }
            }
        }

        private static IEnumerable<Fixture> RoadFixtures(Terrain terrain)
        {
            var palette = RoadMesh.Palette(new Color(70, 70, 75), new Color(170, 165, 155), Color.White, new Color(60, 140, 50));
            // A little above its pad, besides the tarmac's own 2 cm: seen far off and edge on, the faces' depth bias
            // (see MeshInstance) would otherwise let the ground show through along the joins
            float Height(Vector2 p) => terrain.HeightAt(p.X, p.Y) + RoadLift;
            foreach (var piece in Roads)
                switch (piece)
                {
                    case Straight s:
                    {
                        // Sheared up or down to lie on its pad - every point lifted by the rise over its distance along, not
                        // turned, so its kerbs stay upright and its ends stand exactly over the next pieces' ends
                        var along = s.B - s.A;
                        var length = along.Length();
                        var shear = Matrix.Identity;
                        shear.M32 = (Height(s.B) - Height(s.A)) / length;   // y += rise * z, z along it from A to B
                        var middle = (s.A + s.B) / 2f;
                        yield return new Fixture(new MeshSource($"road-straight:{length:F3}", d => RoadMesh.Straight(d, length), palette),
                            shear * Matrix.CreateRotationY(MathF.Atan2(along.X, along.Y)) *
                            Matrix.CreateTranslation(middle.X, (Height(s.A) + Height(s.B)) / 2f, middle.Y));
                        break;
                    }
                    case Junction j:
                        yield return new Fixture(new MeshSource($"road-tjunction:{JunctionSize}:{JunctionCorner}",
                                d => RoadMesh.TJunction(d, JunctionSize, JunctionCorner), palette),
                            Matrix.CreateRotationY(j.Turn) * Matrix.CreateTranslation(j.Centre.X, Height(j.Centre), j.Centre.Y));
                        break;
                    case Bend b:
                        yield return new Fixture(new MeshSource($"road-corner:{BendRadius}", d => RoadMesh.Corner(d, BendRadius), palette),
                            Matrix.CreateRotationY(b.Turn) * Matrix.CreateTranslation(b.Centre.X, Height(b.Centre), b.Centre.Y));
                        break;
                }
        }


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
            buildings.Add(new Building(Corridor.Name, Corridor) { WallColor = new Color(120, 120, 120), RoofColor = new Color(80, 80, 80) });
            buildings.Add(new Building(HangarRoom.Name, HangarRoom) { WallColor = HangarWall, RoofColor = HangarRoof });
            return buildings;
        }

        // The old cottage's front door takes you just inside the corridor's, looking up it; the corridor's takes you
        // back out, a step in front of the cottage's, facing away from it down its garden. The door at the corridor's
        // far end takes you just inside the hangar's, and the hangar's back to it, looking back down the corridor.
        public static IEnumerable<Portal> Portals(Terrain terrain)
        {
            var toCottage = Corridor.Doors[0];
            var toHangar = Corridor.Doors[1];
            var toCorridor = HangarRoom.Doors[0];
            yield return new Portal(new Vector2(OldCottage.Front, CottageDoorZ - CottageDoorHalf), new Vector2(OldCottage.Front, CottageDoorZ + CottageDoorHalf),
                OldCottage.Ground(terrain), ArrivalBy(Corridor, toCottage), ArrivingBy(Corridor, toCottage).Yaw);
            yield return Through(Corridor, toCottage, new Vector3(OldCottage.Front + ArrivalDistance, OldCottage.Ground(terrain), CottageDoorZ), MathHelper.PiOver2);
            yield return Through(Corridor, toHangar, ArrivalBy(HangarRoom, toCorridor), ArrivingBy(HangarRoom, toCorridor).Yaw);
            yield return Through(HangarRoom, toCorridor, ArrivalBy(Corridor, toHangar), ArrivingBy(Corridor, toHangar).Yaw);
        }

        // Walking into a door in a room, to be taken `to`, facing `yaw`
        private static Portal Through(RoomSpec room, DoorSpec door, Vector3 to, float yaw)
        {
            Vector2 Flat(Vector3 v) => new Vector2(v.X, v.Z);
            return new Portal(Flat(room.WorldOffset + room.WallPoint(door.WallIndex, door.Offset - RoomSpec.DoorWidth / 2f)),
                Flat(room.WorldOffset + room.WallPoint(door.WallIndex, door.Offset + RoomSpec.DoorWidth / 2f)), room.WorldOffset.Y, to, yaw);
        }

        // Where you are when you come into a room by one of its doors: a step in from it, facing into the room
        private static Vector3 ArrivalBy(RoomSpec room, DoorSpec door) =>
            room.WorldOffset + room.WallPoint(door.WallIndex, door.Offset) + room.Inward(door.WallIndex) * ArrivalDistance;

        private static Start ArrivingBy(RoomSpec room, DoorSpec door)
        {
            var at = ArrivalBy(room, door);
            var inward = room.Inward(door.WallIndex);
            return new Start(new Vector2(at.X, at.Z), MathF.Atan2(inward.X, -inward.Z));
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

        // The fences, the billboards' posts and the cottage's walls, to walk into (see BuildingGround)
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

            foreach (var billboard in Billboards)
            {
                var foot = terrain.HeightAt(billboard.At.X, billboard.At.Y);
                foreach (var x in BillboardMesh.PostsAt)
                {
                    // Each post, as its four sides
                    var half = BillboardMesh.PostSize / 2f;
                    var corners = new[]
                    {
                        billboard.InWorld(x - half, BillboardMesh.PostZ - half), billboard.InWorld(x + half, BillboardMesh.PostZ - half),
                        billboard.InWorld(x + half, BillboardMesh.PostZ + half), billboard.InWorld(x - half, BillboardMesh.PostZ + half),
                    };
                    for (var k = 0; k < 4; k++)
                        yield return new WallSegment(corners[k], corners[(k + 1) % 4], foot - 1f, foot + BillboardMesh.Clearance + BillboardMesh.Height);
                }
            }

            // The hangar's trees' trunks, as a square round each
            foreach (var tree in HangarTrees)
            {
                var middle = InHangarRoom(tree.X, tree.Z) + new Vector2(HangarRoom.WorldOffset.X, HangarRoom.WorldOffset.Z);
                var bottom = HangarRoom.WorldOffset.Y;
                var h = tree.Trunk;
                var corners = new[] { middle + new Vector2(-h, -h), middle + new Vector2(h, -h), middle + new Vector2(h, h), middle + new Vector2(-h, h) };
                for (var k = 0; k < 4; k++)
                    yield return new WallSegment(corners[k], corners[(k + 1) % 4], bottom, bottom + 4f);
            }

            // The cottages have no insides: just their four walls, up to the eaves
            foreach (var cottage in Cottages)
            {
                var floor = cottage.Ground(terrain);
                var at = cottage.At;
                var corners = new[]
                {
                    at - CottageHalf, at + new Vector2(CottageHalf.X, -CottageHalf.Y),
                    at + CottageHalf, at + new Vector2(-CottageHalf.X, CottageHalf.Y),
                };
                for (var k = 0; k < 4; k++)
                    yield return new WallSegment(corners[k], corners[(k + 1) % 4], floor - 0.2f, floor + HouseMesh.WallHeight * CottageScale);
            }
        }

        // Everything else to draw: the roads, the fences, the pool's paving, the billboards and the cottages.
        public static List<Fixture> Fixtures(Terrain terrain)
        {
            var things = new List<Fixture>(RoadFixtures(terrain));
            var road = RoadLevel(terrain);

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

            foreach (var billboard in Billboards)
                things.Add(new Fixture(BillboardMesh.Source(billboard.Design, new Color(110, 110, 115), new Color(80, 80, 85)),
                    Matrix.CreateRotationY(billboard.Yaw) * Matrix.CreateTranslation(billboard.At.X, terrain.HeightAt(billboard.At.X, billboard.At.Y), billboard.At.Y)));

            // Each its front (the mesh's -Z) turned to face east, and stood on its floor (the mesh is centred on its
            // height). Near its window, one with a hole for the window, to see through (see Windows).
            foreach (var cottage in Cottages)
            {
                var at = Matrix.CreateScale(CottageScale) * Matrix.CreateRotationY(-MathHelper.PiOver2) *
                    Matrix.CreateTranslation(cottage.At.X, cottage.Ground(terrain) + HouseMesh.Height / 2f * CottageScale, cottage.At.Y);
                var window = CottageWindow(terrain, cottage);
                things.Add(new Fixture(new MeshSource(cottage.Id, HouseMesh.Build, cottage.Palette), at, you => !window.Open(you)));
                things.Add(new Fixture(new MeshSource(cottage.Id + "-open", d => HouseMesh.Build(d, openWindow: true), cottage.Palette), at, window.Open));
            }
            things.AddRange(HangarWindowFixtures(terrain));
            return things;
        }

        public static IEnumerable<Window> Windows(Terrain terrain) =>
            Array.ConvertAll(Cottages, cottage => CottageWindow(terrain, cottage)).Append(HangarWindow(terrain));

        // The hangar's window, the size the cottages' are and as high up its front wall (a couple of centimetres in
        // front of it, so the wall's behind it), looking out onto the world from the next cottage's front window, as if
        // you were standing in the cottage. Seen from further in than the cottage is deep, you'd be out through the
        // back of it, so it's shut sooner than the others.
        private static Window HangarWindow(Terrain terrain)
        {
            var wall = HangarRoom.WorldOffset + new Vector3(HangarWindowAcross, 0f, -HangarMesh.Depth / 2f + 0.02f);
            var outOf = CottageWindow(terrain, NextDoor);
            return new Window(Matrix.CreateRotationY(MathHelper.Pi) * Matrix.CreateTranslation(wall),   // looking north, out of the front wall
                outOf.Width, outOf.Sill, outOf.Height, outOf.Pane, Array.Empty<ScenePart>())
            {
                Onto = Matrix.CreateRotationY(MathHelper.Pi) * outOf.Frame,   // the cottage's window, turned to look out of it
                Reach = CottageHalf.X * 2f - 0.3f,
            };
        }

        // The hangar window's frame, always, and its pane, cross and all, while it's shut
        private static IEnumerable<Fixture> HangarWindowFixtures(Terrain terrain)
        {
            var window = HangarWindow(terrain);
            var (width, sill, height) = (window.Width, window.Sill, window.Height);
            var palette = new Color[3];
            MeshBuilder.SetBoxShades(palette, 0, new Color(90, 130, 190));   // as the cottages' windows: its face, the dim shade, is the pane
            Vector3[] Rim(float z) => new[] { new Vector3(-width / 2f, sill, z), new Vector3(width / 2f, sill, z), new Vector3(width / 2f, sill + height, z), new Vector3(-width / 2f, sill + height, z) };
            yield return new Fixture(new MeshSource("hangar-window-frame", d =>
            {
                var mesh = new MeshBuilder();
                mesh.AddLineLoop(Rim(-0.04f));
                return mesh.Build(d);
            }, Array.Empty<Color>()), window.Frame);
            yield return new Fixture(new MeshSource("hangar-window-pane", d =>
            {
                var mesh = new MeshBuilder();
                mesh.AddBox(0, new Vector3(0f, sill, -0.015f), 0.03f, width, height);
                var middle = sill + height / 2f;
                mesh.AddLine(new Vector3(-width / 2f, middle, -0.04f), new Vector3(width / 2f, middle, -0.04f));
                mesh.AddLine(new Vector3(0f, sill, -0.04f), new Vector3(0f, sill + height, -0.04f));
                return mesh.Build(d);
            }, palette), window.Frame, you => !window.Open(you));
        }

        // A cottage's front window, and what's beyond it in its own space: the window's +Z, back from it, turned west,
        // into the cottage, its +X south, along the front, and its origin on the ground under the window. Its pane's the
        // colour the window's is, drawn shut.
        private static Window CottageWindow(Terrain terrain, Cottage cottage) =>
            new Window(Matrix.CreateRotationY(-MathHelper.PiOver2) * Matrix.CreateTranslation(cottage.Front, cottage.Ground(terrain), cottage.WindowZ),
                HouseMesh.WindowWidth * CottageScale, HouseMesh.WindowSill * CottageScale, HouseMesh.WindowHeight * CottageScale,
                cottage.Palette[HouseMesh.WindowBase + MeshBuilder.Dim], cottage == OldCottage ? Parlour()
                    : HangarParts(Matrix.CreateTranslation(0f, -HangarDrop, 0f), withFloor: true));

        // The hangar you can walk round, in the world: in its room, the roof's front at the room's north wall, and
        // no floor of its own - the room's is walked on.
        public static ScenePart[] Moving()
        {
            var front = InHangarRoom(0f, 0f);
            return HangarParts(Matrix.CreateTranslation(HangarRoom.WorldOffset + new Vector3(front.X, 0f, front.Y)), withFloor: false);
        }

        // A thing seen through a window that stays put
        private static ScenePart Still(MeshSource mesh, Matrix at) => new ScenePart(mesh, _ => at);

        // The parlour, as if it were behind the old cottage's window, inside it: its floor a step up from the ground so
        // you look down into it, over the sill, rather than across at its ceiling. A settee against the back wall facing
        // the window, a coffee table in front of it, an armchair turned to it, and a pot plant in the corner.
        private static ScenePart[] Parlour()
        {
            var room = Matrix.CreateTranslation(0f, ParlourStep, 0f);
            Matrix In(float x, float z, float facing) => Matrix.CreateRotationY(facing) * Matrix.CreateTranslation(x, 0f, z) * room;
            const float back = ParlourMesh.Depth;
            var towardsWindow = MathHelper.Pi;   // the furniture faces +Z

            return new[]
            {
                Still(new MeshSource("parlour", ParlourMesh.Build,
                    ParlourMesh.Palette(new Color(150, 100, 60), new Color(200, 185, 140), new Color(240, 235, 220), new Color(110, 160, 110), new Color(120, 80, 40))),
                    room),
                Still(new MeshSource("settee", SetteeMesh.Build,
                    SetteeMesh.Palette(new Color(150, 50, 60), new Color(190, 80, 85), new Color(90, 60, 35))),
                    In(0f, back - 0.45f, towardsWindow)),
                Still(new MeshSource("coffeetable", CoffeeTableMesh.Build,
                    CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40))),
                    In(0f, back - 1.45f, 0f)),
                Still(new MeshSource("armchair", ArmchairMesh.Build,
                    ArmchairMesh.Palette(new Color(70, 110, 140), new Color(110, 150, 175), new Color(90, 60, 35))),
                    In(1.45f, back - 1.3f, MathF.Atan2(-1f, -0.5f))),   // looking across at the table, and a little towards the window
                Still(new MeshSource("plantpot", PlantPotMesh.Build,
                    PlantPotMesh.Palette(new Color(190, 95, 60), new Color(50, 130, 60))),
                    In(-1.7f, back - 0.45f, 0.3f)),
            };
        }

        // The hangar - 24 m across, 32 m back and 12 m high, where the cottage is 9 by 4.5 - placed by `floor`, the
        // middle of the front of its floor. Seen through the next cottage's window, its floor's well down below the
        // window, as if you're looking in from a gallery; it's really there too, off the map, to walk round (see
        // HangarRoom). Three trees stand in it on turntables, each going round at its own pace, the middle one the
        // other way.
        private static ScenePart[] HangarParts(Matrix floor, bool withFloor)
        {
            var tablePalette = TurntableMesh.Palette(new Color(90, 90, 100), new Color(200, 170, 40));
            var parts = new List<ScenePart>
            {
                Still(new MeshSource(withFloor ? "hangar" : "hangar-roof", d => HangarMesh.Build(d, withFloor),
                    HangarMesh.Palette(HangarFloor, HangarRoof, HangarWall, new Color(160, 60, 40))),
                    floor),
            };
            foreach (var tree in HangarTrees)
            {
                var spot = Matrix.CreateTranslation(tree.X, 0f, tree.Z) * floor;
                var turn = tree.Turn;
                parts.Add(new ScenePart(new MeshSource("turntable", TurntableMesh.Build, tablePalette),
                    t => Matrix.CreateScale(TableSize) * Matrix.CreateRotationY(t * turn) * spot));
                parts.Add(new ScenePart(tree.Mesh,
                    t => Matrix.CreateScale(tree.Scale) * Matrix.CreateRotationY(t * turn) * Matrix.CreateTranslation(0f, TurntableMesh.Height * TableSize, 0f) * spot));
            }
            return parts.ToArray();
        }
    }
}
