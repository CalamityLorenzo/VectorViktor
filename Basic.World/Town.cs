using MeshRawData;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using static World.Buildings.Walls;

namespace Basic.World
{
    // The buildings, south-east of where you start, each on its own levelled pad of ground, all with their
    // doorways facing north:
    //  - a cottage: one room, a sofa facing a television on a sideboard
    //  - a two-storey house: a stair climbs the east and south walls of the room downstairs, up through a
    //    hatch into the bedroom above
    //  - a barn: tall and bare, with a pair of wide doors, and a ladder up onto a loft across its south end
    public static class Town
    {
        private const float FloorLift = 0.15f;   // a floor stands a step up from the ground round it
        private const float Slab = 0.3f;         // between storeys

        public static readonly Vector2 CottageCentre = new Vector2(14f, 16f);
        public static readonly Vector2 HouseCentre = new Vector2(32f, 18f);
        public static readonly Vector2 BarnCentre = new Vector2(22f, 36f);

        private const float CottageWidth = 8f, CottageDepth = 6f;
        private const float HouseSize = 7f;
        private const float BarnWidth = 10f, BarnDepth = 8f, BarnHeight = 5.5f;
        private const float Wall = 0.3f;         // the outer wall's thickness, and a little to spare

        // The ground to level for them (see TerrainGenerator.Pad) - pass these to TerrainGenerator.Create
        public static readonly TerrainGenerator.Pad[] Pads =
        {
            new(CottageCentre, new Vector2(CottageWidth / 2f + Wall, CottageDepth / 2f + Wall)),
            new(HouseCentre, new Vector2(HouseSize / 2f + Wall, HouseSize / 2f + Wall)),
            new(BarnCentre, new Vector2(BarnWidth / 2f + Wall, BarnDepth / 2f + Wall)),
        };

        // The buildings, standing on the terrain made with Pads.
        public static List<Building> Build(Terrain terrain)
        {
            Vector3 On(Vector2 centre) => new Vector3(centre.X, terrain.HeightAt(centre.X, centre.Y) + FloorLift, centre.Y);
            return new List<Building> { Cottage(On(CottageCentre)), House(On(HouseCentre)), Barn(On(BarnCentre)) };
        }

        // Every doorway has a door, shut to begin with (see Door): E opens it
        private static OpeningSpec Doorway(float offset, float width = 1f, float height = 2.1f) => new OpeningSpec(North, offset, width, height, null, Door: true);

        private static Building Cottage(Vector3 at)
        {
            var lounge = new RoomSpec
            {
                Id = "cottage", Name = "Cottage",
                Outline = RoomSpec.Rectangle(CottageWidth, CottageDepth), Height = 2.6f, WorldOffset = at,
                Floor = new Color(150, 95, 50), WallA = new Color(230, 220, 190), WallB = new Color(200, 190, 160), Ceiling = new Color(240, 240, 235),
                Openings = new[] { Doorway(-2f) },
                Props = new[]
                {
                    // Against the east wall facing west: 0.46 along X, 1.46 along Z; the television on top of it
                    new PropSpec("sideboard", SideboardMesh.Build,
                        SideboardMesh.Palette(new Color(150, 90, 40), new Color(190, 120, 50), new Color(100, 60, 30), new Color(255, 220, 0)),
                        new Vector3(3.7f, 0f, 0f), -90f, new Vector2(0.23f, 0.73f)),
                    new PropSpec("television", TelevisionMesh.Build,
                        TelevisionMesh.Palette(new Color(130, 80, 40), new Color(0, 200, 200), new Color(220, 220, 220),
                            new Color(90, 90, 90), new Color(60, 40, 20), new Color(200, 200, 200)),
                        new Vector3(3.7f, 0.64f, 0f), -90f),
                    // Facing it across a coffee table: 1.97 wide, turned to face east, so along Z
                    new PropSpec("sofa", SofaMesh.Build,
                        SofaMesh.Palette(new Color(60, 125, 125), new Color(100, 170, 160), new Color(150, 100, 60)),
                        new Vector3(0.6f, 0f, 0f), 90f, new Vector2(0.4f, 0.99f)),
                    new PropSpec("coffeetable", CoffeeTableMesh.Build,
                        CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40)),
                        new Vector3(2.1f, 0f, 0f), 90f, new Vector2(0.25f, 0.5f)),
                    new PropSpec("fern", FernMesh.Build,
                        FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60)),
                        new Vector3(-3.5f, 0f, 2.5f), 0f, new Vector2(0.2f, 0.2f)),
                },
            };
            return new Building("Cottage", lounge) { WallColor = new Color(235, 230, 215), RoofColor = new Color(160, 60, 45) };
        }

        private static Building House(Vector3 at)
        {
            // Four steps up the east wall, a landing in the south-east corner, then twelve along the south
            // wall, up through the hatch; 16 rises of 18 cm in all
            var outline = RoomSpec.Rectangle(HouseSize, HouseSize);
            const float downHeight = 2.6f;
            var stair = new WallStair(outline, firstWall: East, steps: new[] { 4, 12 }, stepsPerWall: 12, height: downHeight + Slab, width: 1f);
            var hatch = stair.Hatch(margin: 0.05f);

            var down = new RoomSpec
            {
                Id = "house", Name = "House",
                Outline = outline, Height = downHeight, WorldOffset = at,
                Floor = new Color(120, 80, 50), WallA = new Color(200, 170, 90), WallB = new Color(170, 140, 70), Ceiling = new Color(235, 230, 210),
                Openings = new[] { Doorway(-1.5f) },
                CeilingHatches = new[] { new HatchSpec(hatch, "houseupstairs", Slab) },
                Ramps = stair.Ramps(),
                Props = new[]
                {
                    new PropSpec("housestair", stair.Build,
                        WallStair.Palette(new Color(150, 105, 60), new Color(110, 75, 40), new Color(130, 90, 50), new Color(90, 60, 35)),
                        Vector3.Zero, 0f),
                    // Against the west wall, facing east: 1.30 wide, so along Z
                    new PropSpec("settee", SetteeMesh.Build,
                        SetteeMesh.Palette(new Color(195, 145, 45), new Color(225, 180, 85), new Color(150, 100, 60)),
                        new Vector3(-3.0f, 0f, 0.5f), 90f, new Vector2(0.4f, 0.65f)),
                    new PropSpec("coffeetable", CoffeeTableMesh.Build,
                        CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40)),
                        new Vector3(-1.8f, 0f, 0.5f), 90f, new Vector2(0.25f, 0.5f)),
                },
            };
            var up = new RoomSpec
            {
                Id = "houseupstairs", Name = "Bedroom",
                Outline = outline, Height = 2.4f, WorldOffset = at + Vector3.Up * (downHeight + Slab),
                Floor = new Color(90, 70, 110), WallA = new Color(150, 170, 210), WallB = new Color(120, 140, 180), Ceiling = new Color(235, 235, 240),
                FloorHatches = new[] { new HatchSpec(hatch, "house") },
                Props = new[]
                {
                    // Against the north wall, facing south
                    new PropSpec("sofa", SofaMesh.Build,
                        SofaMesh.Palette(new Color(60, 125, 125), new Color(100, 170, 160), new Color(150, 100, 60)),
                        new Vector3(0f, 0f, -3.0f), 0f, new Vector2(0.99f, 0.4f)),
                    new PropSpec("fern", FernMesh.Build,
                        FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60)),
                        new Vector3(-3.0f, 0f, -3.0f), 0f, new Vector2(0.2f, 0.2f)),
                },
            };
            return new Building("House", down, up) { WallColor = new Color(215, 190, 120), RoofColor = new Color(80, 85, 95) };
        }

        private static Building Barn(Vector3 at)
        {
            // The loft: a deck 3 m up across the south end (z 0.5 to 4), and a ladder up to its north edge.
            // The deck isn't solid to walk into: its underside is well over your head, and a solid edge
            // would stop a climber's head at the top of the ladder.
            const float loft = 3f, loftFrom = 0.5f, ladderLean = 0.8f;
            var loftDepth = BarnDepth / 2f - loftFrom;
            var ladderFoot = new Vector3(3.5f, 0f, loftFrom - ladderLean);
            var ladderHead = new Vector3(3.5f, loft, loftFrom);

            var barn = new RoomSpec
            {
                Id = "barn", Name = "Barn",
                Outline = RoomSpec.Rectangle(BarnWidth, BarnDepth), Height = BarnHeight, WorldOffset = at,
                GridSpacing = 2f,
                Floor = new Color(110, 90, 60), WallA = new Color(140, 60, 45), WallB = new Color(115, 50, 38), Ceiling = new Color(70, 55, 45),
                Openings = new[] { Doorway(0f, width: 3f, height: 3.2f) },
                Props = new[]
                {
                    new PropSpec("barnloft", d => PlatformMesh.Build(d, length: loftDepth, width: BarnWidth, thickness: 0.2f),
                        PlatformMesh.Palette(new Color(150, 115, 70)),
                        new Vector3(0f, loft - 0.2f, loftFrom + loftDepth / 2f), 0f),
                    new PropSpec("barnladder", d => LadderMesh.Build(d, height: loft, lean: ladderLean),
                        LadderMesh.Palette(new Color(180, 180, 185), new Color(60, 60, 65)),
                        ladderFoot, 0f),
                },
                Ramps = new[]
                {
                    new RampSpec(new Vector3(-BarnWidth / 2f, loft, loftFrom + loftDepth / 2f), new Vector3(BarnWidth / 2f, loft, loftFrom + loftDepth / 2f), loftDepth),
                    new RampSpec(ladderFoot, ladderHead, 1f, MaxStepUp: 5f),   // steep: see RampSpec.MaxStepUp
                },
            };
            return new Building("Barn", barn) { WallColor = new Color(170, 55, 40), RoofColor = new Color(70, 70, 75), WallThickness = Wall };
        }
    }
}
