using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Core.Physics;
using static World.Buildings.Walls;

namespace Basic.World
{
    // The buildings, south-east of where you start, each on its own levelled pad of ground, all with their
    // doorways facing north:
    //  - a cottage: one room, a sofa facing a television on a sideboard, under a pitched roof
    //  - a two-storey house: a stair climbs the east and south walls of the room downstairs, up through a
    //    hatch into the bedroom above, and from there a ladder climbs through another into the attic,
    //    under its pitched roof
    //  - a barn: tall and bare, with a pair of wide doors, a ladder up onto a loft across its south end,
    //    and a pitched roof, and two bales of straw (as wooden crates) inside the doorway, to shove about
    public sealed class Town : IDistrict
    {
        private const float FloorLift = Houses.FloorLift;
        private const float RoofPitch = 35f;     // degrees, for every building's pitched roof

        public static readonly Vector2 CottageCentre = new Vector2(14f, 16f);
        public static readonly Vector2 HouseCentre = new Vector2(32f, 18f);
        public static readonly Vector2 BarnCentre = new Vector2(22f, 36f);

        private const float CottageWidth = 8f, CottageDepth = 6f;
        private const float BarnWidth = 10f, BarnDepth = 8f, BarnHeight = 5.5f;
        private const float Wall = 0.3f;         // the outer wall's thickness, and a little to spare

        // The ground to level for them (see TerrainGenerator.Pad) - pass these to TerrainGenerator.Create
        public static readonly TerrainGenerator.Pad[] Pads =
        {
            new(CottageCentre, new Vector2(CottageWidth / 2f + Wall, CottageDepth / 2f + Wall)),
            new(HouseCentre, new Vector2(Houses.TwoStoreySize / 2f + Wall, Houses.TwoStoreySize / 2f + Wall)),
            new(BarnCentre, new Vector2(BarnWidth / 2f + Wall, BarnDepth / 2f + Wall)),
        };

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["town"] = new(new Vector2(22f, 6f), MathHelper.Pi),                           // north of the buildings, facing them
            ["house"] = new(HouseCentre + new Vector2(-1.5f, -7f), MathHelper.Pi),         // outside the house's doorway
            ["bedroom"] = new(HouseCentre + new Vector2(-2f, -2f), MathHelper.Pi, Above: 4.5f),   // in the house's bedroom, facing the ladder up to the attic
            ["attic"] = new(HouseCentre, MathHelper.PiOver2, Above: 100f),                 // up in the house's attic, facing its east end
            ["barn"] = new(BarnCentre + new Vector2(0f, -8f), MathHelper.Pi),              // outside the barn's
        };

        IEnumerable<TerrainGenerator.Pad> IDistrict.Pads => Pads;
        IEnumerable<Building> IDistrict.Buildings(Terrain terrain) => Build(terrain);

        public IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain)
        {
            var bale = new Vector3(0.8f, 0.8f, 0.8f);
            yield return Scenery.Crate(world, terrain, "barn crate 1", CrateKind.Wood, bale, 25f, BarnCentre.X - 3f, BarnCentre.Y - 1.5f);
            yield return Scenery.Crate(world, terrain, "barn crate 2", CrateKind.Wood, bale, 25f, BarnCentre.X - 3f, BarnCentre.Y - 1.5f, above: 1f);
        }

        // The buildings, standing on the terrain made with Pads.
        public static List<Building> Build(Terrain terrain)
        {
            Vector3 On(Vector2 centre) => new Vector3(centre.X, terrain.HeightAt(centre.X, centre.Y) + FloorLift, centre.Y);
            return new List<Building>
            {
                Cottage(On(CottageCentre)),
                Houses.TwoStorey("house", "House", On(HouseCentre), new Color(215, 190, 120), new Color(80, 85, 95),
                    pitched: Gable.Pitched(RoofPitch, alongX: true), attic: true),
                Barn(On(BarnCentre)),
            };
        }

        // Every doorway has a door, shut to begin with (see Door): E opens it
        private static OpeningSpec Doorway(float offset, float width = 1f, float height = 2.1f) => Houses.Doorway(North, offset, width, height);

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
                    new PropSpec(new MeshSource("sideboard", SideboardMesh.Build,
                        SideboardMesh.Palette(new Color(150, 90, 40), new Color(190, 120, 50), new Color(100, 60, 30), new Color(255, 220, 0))),
                        new Vector3(3.7f, 0f, 0f), -90f, new Vector2(0.23f, 0.73f)),
                    new PropSpec(new MeshSource("television", TelevisionMesh.Build,
                        TelevisionMesh.Palette(new Color(130, 80, 40), new Color(0, 200, 200), new Color(220, 220, 220),
                            new Color(90, 90, 90), new Color(60, 40, 20), new Color(200, 200, 200))),
                        new Vector3(3.7f, 0.64f, 0f), -90f),
                    // Facing it across a coffee table: 1.97 wide, turned to face east, so along Z
                    new PropSpec(new MeshSource("sofa", SofaMesh.Build,
                        SofaMesh.Palette(new Color(60, 125, 125), new Color(100, 170, 160), new Color(150, 100, 60))),
                        new Vector3(0.6f, 0f, 0f), 90f, new Vector2(0.4f, 0.99f)),
                    new PropSpec(new MeshSource("coffeetable", CoffeeTableMesh.Build,
                        CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40))),
                        new Vector3(2.1f, 0f, 0f), 90f, new Vector2(0.25f, 0.5f)),
                    new PropSpec(new MeshSource("fern", FernMesh.Build,
                        FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60))),
                        new Vector3(-3.5f, 0f, 2.5f), 0f, new Vector2(0.2f, 0.2f)),
                },
            };
            return new Building("Cottage", lounge)
            {
                WallColor = new Color(235, 230, 215), RoofColor = new Color(160, 60, 45),
                Roof = Gable.Pitched(RoofPitch, alongX: true),   // the ridge along its length
            };
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
                    new PropSpec(PlatformMesh.Source(loftDepth, BarnWidth, 0.2f, PlatformMesh.Palette(new Color(150, 115, 70))),
                        new Vector3(0f, loft - 0.2f, loftFrom + loftDepth / 2f), 0f),
                    new PropSpec(LadderMesh.Source(loft, ladderLean, LadderMesh.Palette(new Color(180, 180, 185), new Color(60, 60, 65))),
                        ladderFoot, 0f),
                },
                Ramps = new[]
                {
                    new RampSpec(new Vector3(-BarnWidth / 2f, loft, loftFrom + loftDepth / 2f), new Vector3(BarnWidth / 2f, loft, loftFrom + loftDepth / 2f), loftDepth),
                    new RampSpec(ladderFoot, ladderHead, 1f, MaxStepUp: 5f),   // steep: see RampSpec.MaxStepUp
                },
            };
            return new Building("Barn", barn)
            {
                WallColor = new Color(170, 55, 40), RoofColor = new Color(70, 70, 75), WallThickness = Wall,
                Roof = Gable.Pitched(RoofPitch, alongX: true),   // the ridge along its length
            };
        }
    }
}
