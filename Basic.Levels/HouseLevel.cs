using MeshRawData;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace Basic.Levels
{
    // The level. On the ground floor, a long corridor with a door halfway along each side: the west door
    // leads to the lounge (a coffee table), the east door to the TV room (a television on a sideboard).
    // At the north end of the corridor a staircase climbs to an upper corridor that runs east-west, a
    // little longer than the first. Its west door leads to a long, empty room, its east door to the entrance
    // of a very large hangar with a crate stencilled COLA standing in it, and a sign beside it that reads
    // 12939 from the front and PEPSI from behind.
    //
    // The corridor, stairs and upper corridor are joined by openings, so they sit edge to edge in the
    // world and you walk straight from one to the next. Every other room is reached by a door, and sits
    // at the world origin like a separate scene.
    public sealed class HouseLevel
    {
        public IReadOnlyDictionary<string, RoomSpec> Rooms { get; }
        public string StartRoom { get; }
        public Vector3 StartPosition { get; }
        public float StartYaw { get; }   // radians; 0 looks north (-Z), increasing turns right

        private HouseLevel(IReadOnlyDictionary<string, RoomSpec> rooms, string startRoom, Vector3 startPosition, float startYaw)
        {
            Rooms = rooms;
            StartRoom = startRoom;
            StartPosition = startPosition;
            StartYaw = startYaw;
        }

        public static HouseLevel Create()
        {
            // The ground floor corridor and the way up. Going north from the corridor's centre the rooms
            // follow each other: the corridor, then the stairwell, then the upper corridor across the top.
            const float corridorWidth = 2.4f, corridorDepth = 16f, corridorHeight = 2.6f;
            const float stairRise = 3.0f, stairDepth = 4.5f;
            const float upperLength = 20f;

            var corridor = new RoomSpec
            {
                Id = "corridor", Name = "Corridor",
                Width = corridorWidth, Depth = corridorDepth, Height = corridorHeight,
                Floor = new Color(70, 70, 70), WallNorthSouth = new Color(0, 150, 150), WallEastWest = new Color(0, 105, 105), Ceiling = new Color(40, 40, 40),
                Doors = new[]
                {
                    new DoorSpec("west", Wall.West, 0f, "lounge", "corridor"),
                    new DoorSpec("east", Wall.East, 0f, "tvroom", "corridor"),
                },
                Openings = new[] { new OpeningSpec(Wall.North, 0f, corridorWidth, corridorHeight, "stairs") },
            };

            // Fifteen steps of 20 cm; the stairwell's south end butts onto the corridor's north end
            var stairs = new RoomSpec
            {
                Id = "stairs", Name = "Stairs",
                Width = corridorWidth, Depth = stairDepth, Height = corridorHeight,
                Floor = new Color(110, 110, 110), WallNorthSouth = new Color(0, 150, 150), WallEastWest = new Color(0, 105, 105), Ceiling = new Color(40, 40, 40),
                WorldOffset = new Vector3(0f, 0f, -(corridorDepth + stairDepth) / 2f),
                Stairs = new StairSpec(stairRise, 15),
                Openings = new[]
                {
                    new OpeningSpec(Wall.South, 0f, corridorWidth, corridorHeight, "corridor"),
                    new OpeningSpec(Wall.North, 0f, corridorWidth, corridorHeight + stairRise, "upper"),
                },
            };

            // Runs east-west across the top of the stairs, which come up into the middle of its south wall
            var upper = new RoomSpec
            {
                Id = "upper", Name = "Upper corridor",
                Width = upperLength, Depth = corridorWidth, Height = corridorHeight,
                Floor = new Color(70, 70, 70), WallNorthSouth = new Color(150, 0, 150), WallEastWest = new Color(105, 0, 105), Ceiling = new Color(40, 40, 40),
                WorldOffset = new Vector3(0f, stairRise, -(corridorDepth / 2f + stairDepth + corridorWidth / 2f)),
                Doors = new[]
                {
                    new DoorSpec("west", Wall.West, 0f, "cola", "upper"),
                    new DoorSpec("east", Wall.East, 0f, "hangar", "upper"),
                },
                Openings = new[] { new OpeningSpec(Wall.South, 0f, corridorWidth, corridorHeight, "stairs") },
            };

            // Bigger than the TV room; the door is off-centre in its east wall.
            var lounge = new RoomSpec
            {
                Id = "lounge", Name = "Lounge",
                Width = 5f, Depth = 4f, Height = 2.6f,
                Floor = new Color(150, 90, 40), WallNorthSouth = new Color(170, 50, 50), WallEastWest = new Color(120, 35, 35), Ceiling = new Color(60, 60, 60),
                Doors = new[] { new DoorSpec("corridor", Wall.East, 1f, "corridor", "west") },
                Props = new[]
                {
                    // 1.0 along X, 0.5 along Z
                    new PropSpec("coffeetable", CoffeeTableMesh.Build,
                        CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40)),
                        new Vector3(-1.2f, 0f, 0.6f), 0f, new Vector2(0.5f, 0.25f)),
                },
            };

            // Small and squarer. The sideboard stands against the east wall facing the door, the TV on top of it.
            var tvRoom = new RoomSpec
            {
                Id = "tvroom", Name = "TV room",
                Width = 3.5f, Depth = 3.5f, Height = 2.4f,
                Floor = new Color(50, 50, 50), WallNorthSouth = new Color(60, 60, 200), WallEastWest = new Color(40, 40, 150), Ceiling = new Color(30, 30, 30),
                Doors = new[] { new DoorSpec("corridor", Wall.West, 0f, "corridor", "east") },
                Props = new[]
                {
                    // 1.46 wide, 0.46 deep, 0.64 tall; turned to face west, so it is 0.46 along X and 1.46 along Z
                    new PropSpec("sideboard", SideboardMesh.Build,
                        SideboardMesh.Palette(new Color(150, 90, 40), new Color(190, 120, 50), new Color(100, 60, 30), new Color(255, 220, 0)),
                        new Vector3(1.51f, 0f, 0f), -90f, new Vector2(0.23f, 0.73f)),
                    new PropSpec("television", TelevisionMesh.Build,
                        TelevisionMesh.Palette(new Color(130, 80, 40), new Color(0, 200, 200), new Color(220, 220, 220),
                            new Color(90, 90, 90), new Color(60, 40, 20), new Color(200, 200, 200)),
                        new Vector3(1.51f, 0.64f, 0f), -90f),
                },
            };

            // Long and narrow: you come in at the south end
            var cola = new RoomSpec
            {
                Id = "cola", Name = "Long room",
                Width = 5f, Depth = 16f, Height = 3f,
                Floor = new Color(90, 70, 50), WallNorthSouth = new Color(130, 130, 130), WallEastWest = new Color(95, 95, 95), Ceiling = new Color(50, 50, 50),
                Doors = new[] { new DoorSpec("upper", Wall.South, 0f, "upper", "west") },
            };

            // The entrance to a hangar: floor, walls and a lot of air, with the crate standing well out from the
            // door, its stencilled face towards you and room to walk right round it. You come in at the middle
            // of the south wall; the grid is every 5 m, which is all there is to judge the size by.
            var hangar = new RoomSpec
            {
                Id = "hangar", Name = "Hangar",
                Width = 100f, Depth = 140f, Height = 25f,
                GridSpacing = 5f,
                Floor = new Color(110, 110, 110), WallNorthSouth = new Color(100, 120, 100), WallEastWest = new Color(75, 90, 75), Ceiling = new Color(60, 60, 70),
                Doors = new[] { new DoorSpec("upper", Wall.South, 0f, "upper", "east") },
                Props = new[]
                {
                    // 3.6 wide, 2.0 deep
                    new PropSpec("colacrate", ColaCrateMesh.Build,
                        ColaCrateMesh.Palette(new Color(170, 120, 60), new Color(240, 240, 240)),
                        new Vector3(0f, 0f, 50f), 0f, new Vector2(1.8f, 1.0f)),
                    // 4.5 wide: 12939 from the door side; walk round behind it and it reads PEPSI. Its frame stands
                    // clear of the crate, and blocks like a solid slab, so you look at it rather than through it.
                    new PropSpec("numbersign", NumberSignMesh.Build,
                        NumberSignMesh.Palette(new Color(150, 150, 150), new Color(240, 240, 240)),
                        new Vector3(11f, 0f, 50f), 0f, new Vector2(2.25f, 0.1f)),
                },
            };

            var rooms = new Dictionary<string, RoomSpec>();
            foreach (var room in new[] { corridor, stairs, upper, lounge, tvRoom, cola, hangar })
                rooms[room.Id] = room;

            // Start at the south end of the corridor, looking up it.
            return new HouseLevel(rooms, corridor.Id, new Vector3(0f, 0f, 6.5f), 0f);
        }
    }
}
