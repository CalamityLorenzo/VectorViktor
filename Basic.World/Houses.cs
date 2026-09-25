using MeshRawData;
using Microsoft.Xna.Framework;
using World.Buildings;
using static World.Buildings.Walls;

namespace Basic.World
{
    // House designs, to put up anywhere (see Town and Neighbourhood). `at` is the middle of the ground
    // floor, a step (FloorLift) up from the levelled ground round it; `id` names the rooms, so every house
    // needs its own. Every doorway has a door, shut to begin with (see Door).
    public static class Houses
    {
        public const float FloorLift = 0.15f;   // a floor stands a step up from the ground round it
        public const float Slab = 0.3f;         // between storeys

        public const float TwoStoreySize = 7f;
        public const float BungalowWidth = 9f, BungalowDepth = 6f;

        public static OpeningSpec Doorway(int wall, float offset, float width = 1f, float height = 2.1f) =>
            new OpeningSpec(wall, offset, width, height, null, Door: true);

        // Two storeys, 7 m square, the front door in the north wall: four steps up the east wall downstairs,
        // a landing in the south-east corner, then twelve along the south wall, up through a hatch into the
        // bedroom above - 16 rises of 18 cm in all. A settee and a coffee table downstairs, a sofa up.
        public static Building TwoStorey(string id, string name, Vector3 at, Color outside, Color roof)
        {
            var outline = RoomSpec.Rectangle(TwoStoreySize, TwoStoreySize);
            const float downHeight = 2.6f;
            var stair = new WallStair(outline, firstWall: East, steps: new[] { 4, 12 }, stepsPerWall: 12, height: downHeight + Slab, width: 1f);
            var hatch = stair.Hatch(margin: 0.05f);
            var upId = id + "upstairs";

            var down = new RoomSpec
            {
                Id = id, Name = name,
                Outline = outline, Height = downHeight, WorldOffset = at,
                Floor = new Color(120, 80, 50), WallA = new Color(200, 170, 90), WallB = new Color(170, 140, 70), Ceiling = new Color(235, 230, 210),
                Openings = new[] { Doorway(North, -1.5f) },
                CeilingHatches = new[] { new HatchSpec(hatch, upId, Slab) },
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
                Id = upId, Name = name + " bedroom",
                Outline = outline, Height = 2.4f, WorldOffset = at + Vector3.Up * (downHeight + Slab),
                Floor = new Color(90, 70, 110), WallA = new Color(150, 170, 210), WallB = new Color(120, 140, 180), Ceiling = new Color(235, 235, 240),
                FloorHatches = new[] { new HatchSpec(hatch, id) },
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
            return new Building(name, down, up) { WallColor = outside, RoofColor = roof };
        }

        // One storey, 9 m across and 6 deep, its front door in `doorWall` (North or South), off to the west
        // of the middle: a sofa against the wall opposite it, facing a coffee table and the door.
        public static Building Bungalow(string id, string name, Vector3 at, int doorWall, Color outside, Color roof)
        {
            var back = doorWall == North ? 1f : -1f;   // which way (along Z) the back wall is
            var room = new RoomSpec
            {
                Id = id, Name = name,
                Outline = RoomSpec.Rectangle(BungalowWidth, BungalowDepth), Height = 2.6f, WorldOffset = at,
                Floor = new Color(140, 110, 80), WallA = new Color(225, 215, 180), WallB = new Color(195, 185, 150), Ceiling = new Color(240, 240, 235),
                Openings = new[] { Doorway(doorWall, doorWall == North ? -2f : 2f) },
                Props = new[]
                {
                    new PropSpec("sofa", SofaMesh.Build,
                        SofaMesh.Palette(new Color(150, 60, 60), new Color(190, 90, 80), new Color(90, 60, 40)),
                        new Vector3(1.5f, 0f, back * 2.4f), back > 0f ? 180f : 0f, new Vector2(0.99f, 0.4f)),
                    new PropSpec("coffeetable", CoffeeTableMesh.Build,
                        CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40)),
                        new Vector3(1.5f, 0f, back * 1.2f), 0f, new Vector2(0.5f, 0.25f)),
                    new PropSpec("fern", FernMesh.Build,
                        FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60)),
                        new Vector3(-4f, 0f, back * 2.5f), 0f, new Vector2(0.2f, 0.2f)),
                },
            };
            return new Building(name, room) { WallColor = outside, RoofColor = roof };
        }
    }
}
