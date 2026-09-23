using MeshRawData;
using MeshRawData.Helpers;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using static Basic.Levels.Walls;

namespace Basic.Levels
{
    // The level. On the ground floor, a long corridor with a door halfway along each side: the west door
    // leads to the lounge (a coffee table), the east door to the TV room (a television on a sideboard).
    // A third door, at the corridor's south (back) end, leads to an L-shaped back room, whose notch
    // corner has a door of its own through to a yellow octagonal room, from which a staircase round its
    // walls climbs through the ceiling to a green octagonal room above, and a ladder in the middle of that
    // climbs on up to a purple one. At the north end of the corridor
    // a staircase climbs to an upper corridor that runs east-west, a little longer than the first. Its
    // west door leads to a long, empty room, its east door to the entrance of a very large
    // hangar with a crate stencilled COLA standing in it, and a sign beside it that reads 12939 from the
    // front and PEPSI from behind. Off to one side, clear of the crate, a 1960s retro-futurist space
    // plane stands parked on its undercarriage. Against the hangar's west wall, a break room deck sits
    // 4.5 m up, climbable via a quarter-turn staircase at its south end (flush against the deck's open
    // edge, then turning to face into the hangar for the rest of the descent) and a fixed ladder at its
    // north end.
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
                Outline = RoomSpec.Rectangle(corridorWidth, corridorDepth), Height = corridorHeight,
                Floor = new Color(70, 70, 70), WallA = new Color(0, 150, 150), WallB = new Color(0, 105, 105), Ceiling = new Color(210, 140, 80),
                Doors = new[]
                {
                    new DoorSpec("west", West, 0f, "lounge", "corridor"),
                    new DoorSpec("east", East, 0f, "tvroom", "corridor"),
                    new DoorSpec("south", South, 0f, "backroom", "corridor"),
                },
                Openings = new[] { new OpeningSpec(North, 0f, corridorWidth, corridorHeight, "stairs") },
            };

            // Fifteen steps of 20 cm; the stairwell's south end butts onto the corridor's north end
            var stairs = new RoomSpec
            {
                Id = "stairs", Name = "Stairs",
                Outline = RoomSpec.Rectangle(corridorWidth, stairDepth), Height = corridorHeight,
                Floor = new Color(110, 110, 110), WallA = new Color(0, 150, 150), WallB = new Color(0, 105, 105), Ceiling = new Color(40, 40, 40),
                WorldOffset = new Vector3(0f, 0f, -(corridorDepth + stairDepth) / 2f),
                Ramps = new[] { new RampSpec(new Vector3(0f, 0f, stairDepth / 2f), new Vector3(0f, stairRise, -stairDepth / 2f), corridorWidth, Steps: 15) },
                Openings = new[]
                {
                    new OpeningSpec(South, 0f, corridorWidth, corridorHeight, "corridor"),
                    new OpeningSpec(North, 0f, corridorWidth, corridorHeight + stairRise, "upper"),
                },
            };

            // Runs east-west across the top of the stairs, which come up into the middle of its south wall
            var upper = new RoomSpec
            {
                Id = "upper", Name = "Upper corridor",
                Outline = RoomSpec.Rectangle(upperLength, corridorWidth), Height = corridorHeight,
                Floor = new Color(70, 70, 70), WallA = new Color(150, 0, 150), WallB = new Color(105, 0, 105), Ceiling = new Color(40, 40, 40),
                WorldOffset = new Vector3(0f, stairRise, -(corridorDepth / 2f + stairDepth + corridorWidth / 2f)),
                Doors = new[]
                {
                    new DoorSpec("west", West, 0f, "cola", "upper"),
                    new DoorSpec("east", East, 0f, "hangar", "upper"),
                },
                Openings = new[] { new OpeningSpec(South, 0f, corridorWidth, corridorHeight, "stairs") },
            };

            // Bigger than the TV room; the door is off-centre in its east wall.
            var lounge = new RoomSpec
            {
                Id = "lounge", Name = "Lounge",
                Outline = RoomSpec.Rectangle(5f, 4f), Height = 2.6f,
                Floor = new Color(150, 90, 40), WallA = new Color(170, 50, 50), WallB = new Color(120, 35, 35), Ceiling = new Color(60, 60, 60),
                Doors = new[] { new DoorSpec("corridor", East, 1f, "corridor", "west") },
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
                Outline = RoomSpec.Rectangle(3.5f, 3.5f), Height = 2.4f,
                Floor = new Color(50, 50, 50), WallA = new Color(60, 60, 200), WallB = new Color(40, 40, 150), Ceiling = new Color(30, 30, 30),
                Doors = new[] { new DoorSpec("corridor", West, 0f, "corridor", "east") },
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
                Outline = RoomSpec.Rectangle(5f, 16f), Height = 3f,
                Floor = new Color(90, 70, 50), WallA = new Color(130, 130, 130), WallB = new Color(95, 95, 95), Ceiling = new Color(50, 50, 50),
                Doors = new[] { new DoorSpec("upper", South, 0f, "upper", "west") },
            };

            // The entrance to a hangar: floor, walls and a lot of air, with the crate standing well out from the
            // door, its stencilled face towards you and room to walk right round it. You come in at the middle
            // of the south wall; the grid is every 5 m, which is all there is to judge the size by.
            var hangar = new RoomSpec
            {
                Id = "hangar", Name = "Hangar",
                Outline = RoomSpec.Rectangle(100f, 140f), Height = 25f,
                GridSpacing = 5f,
                Floor = new Color(110, 110, 110), WallA = new Color(100, 120, 100), WallB = new Color(75, 90, 75), Ceiling = new Color(60, 60, 70),
                Doors = new[] { new DoorSpec("upper", South, 0f, "upper", "east") },
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
                    // Parked broadside off to the west, well clear of the crate: 6.9 long, 5 across the
                    // wingtips. A 1960s retro-futurist space plane, wireframe-built like everything else here.
                    new PropSpec("spaceplane", SpacePlaneMesh.Build,
                        SpacePlaneMesh.Palette(new Color(200, 205, 210), new Color(220, 60, 40), new Color(120, 220, 220), new Color(40, 40, 45)),
                        new Vector3(-30f, 0f, 10f), 90f, new Vector2(3.5f, 2.7f)),

                    // A break room mezzanine bolted to the west wall, well clear of the crate and the
                    // plane: a 10 x 6 deck 4.5 m up, with a sofa and coffee table (a fern on top) looking
                    // out over the hangar floor. Both the staircase (south end) and the ladder (north
                    // end) stay entirely outside the deck's own footprint, meeting it flush at its open
                    // (east) edge - a walker never has to pass underneath it to reach either one.
                    new PropSpec("balconydeck", d => PlatformMesh.Build(d, length: 10f, width: 6f, thickness: 0.2f),
                        PlatformMesh.Palette(new Color(130, 130, 140)),
                        new Vector3(-47f, 4.3f, -40f), 0f),
                    new PropSpec("balconysofa", SofaMesh.Build,
                        SofaMesh.Palette(new Color(120, 80, 50), new Color(160, 120, 80), new Color(60, 40, 25)),
                        new Vector3(-48.8f, 4.5f, -40f), 90f),
                    new PropSpec("balconytable", CoffeeTableMesh.Build,
                        CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40)),
                        new Vector3(-47.6f, 4.5f, -40f), 90f),
                    new PropSpec("balconyfern", FernMesh.Build,
                        FernMesh.Palette(new Color(150, 90, 40), new Color(70, 150, 60)),
                        new Vector3(-47.6f, 4.78f, -40f), 0f),
                    // Rises flush against the open (east) edge of the deck from its south (door-side)
                    // corner - never passing under the deck itself - then halfway down turns 90 degrees
                    // onto a landing and faces into the hangar for the rest of the descent to the floor.
                    new PropSpec("balconystairs",
                        d => StaircaseMesh.BuildQuarterTurn(d, stepsBeforeTurn: 12, stepsAfterTurn: 13, turn: StairTurn.Right, carpet: false),
                        StaircaseMesh.Palette(new Color(120, 120, 125), new Color(90, 90, 95)),
                        new Vector3(-39.98f, 0f, -38.83f), -90f),
                    // Non-blocking (see RampSpec below): a Half here would push a walker back off the
                    // ladder's own footprint before they could ever climb it.
                    new PropSpec("balconyladder", d => LadderMesh.Build(d, height: 4.5f, lean: 1.0f),
                        LadderMesh.Palette(new Color(180, 180, 185), new Color(60, 60, 65)),
                        new Vector3(-43f, 0f, -43f), -90f),
                },
                // Lets a walker actually climb the balcony: the deck itself (flat), the staircase's two
                // flights and landing (see PropSpec("balconystairs") above for how these points were
                // worked out - foot, pre-landing, landing and top), and the ladder. Each is a bit wider
                // than its mesh so walking it doesn't feel like balancing on a rail.
                Ramps = new[]
                {
                    new RampSpec(new Vector3(-47f, 4.5f, -45f), new Vector3(-47f, 4.5f, -35f), 6f),
                    new RampSpec(new Vector3(-39.98f, 0f, -38.83f), new Vector3(-43.10f, 2.16f, -38.83f), 0.9f),
                    new RampSpec(new Vector3(-43.55f, 2.16f, -39.28f), new Vector3(-43.55f, 2.16f, -38.38f), 0.9f),
                    new RampSpec(new Vector3(-43.55f, 2.16f, -38.38f), new Vector3(-43.55f, 4.5f, -35f), 0.9f),
                    // 4.5 m of rise over 1 m of horizontal run: at running speed a single frame's
                    // approach can outrun the default MaxStepUp, so this one gets its own, generous
                    // override rather than being dropped back to the floor partway up.
                    new RampSpec(new Vector3(-43f, 0f, -43f), new Vector3(-44f, 4.5f, -43f), 1.0f, MaxStepUp: 5f),
                },
            };

            // L-shaped, off the back (south end) of the corridor: a 6 x 8 room with a 3 x 3 bite taken out
            // of its north-west corner. Six edges instead of a rectangle's four - 0: shortened north wall,
            // 1: east, 2: south, 3: shortened west wall (the door back to the corridor sits on this one,
            // just clear of the notch), 4 and 5: the notch's own two inner faces. See RoomSpec.Outline.
            var backRoomHalfWidth = 3f; var backRoomHalfDepth = 4f;
            var notchWidth = 3f; var notchDepth = 3f;
            var backRoom = new RoomSpec
            {
                Id = "backroom", Name = "Back Room",
                Outline = new[]
                {
                    new Vector2(-backRoomHalfWidth + notchWidth, -backRoomHalfDepth),   // 0->1: north (shortened)
                    new Vector2(backRoomHalfWidth, -backRoomHalfDepth),
                    new Vector2(backRoomHalfWidth, backRoomHalfDepth),                  // 1->2: east
                    new Vector2(-backRoomHalfWidth, backRoomHalfDepth),                 // 2->3: south
                    new Vector2(-backRoomHalfWidth, -backRoomHalfDepth + notchDepth),   // 3->4: west (shortened)
                    new Vector2(-backRoomHalfWidth + notchWidth, -backRoomHalfDepth + notchDepth), // 4->5: notch inner faces
                },
                Height = corridorHeight,
                Floor = new Color(90, 100, 70), WallA = new Color(80, 140, 90), WallB = new Color(55, 100, 65), Ceiling = new Color(50, 50, 50),
                Doors = new[]
                {
                    new DoorSpec("corridor", 3, 1.8f, "corridor", "south"),
                    new DoorSpec("octagon", 4, 0f, "octagon", "backroom"),
                },
            };

            // A yellow octagonal room, reached through the notch in the back room's corner, twice the size
            // of a "small" one.
            const float octagonRadius = 5.6f;
            var octagonHeight = corridorHeight * 2f;
            var octagonOutline = RoomSpec.RegularPolygon(8, octagonRadius);

            // Directly above it, a green octagon of the same shape, on a 30 cm slab (see RoomSpec.CeilingHatches
            // for why it can't just sit on the yellow room's ceiling). A staircase hugs the yellow room's walls
            // round its south side: three steps up the east-south-east wall (2) to a corner landing, a full
            // flight along the next wall, another landing, then a last flight along the wall opposite the door
            // (4) up through a hatch in the ceiling. Only that last flight is under the hatch, so the landing
            // before it has to leave headroom: at 3.0 m, the eye (1.6 m above it) is still under the 5.2 m ceiling.
            const float slab = 0.3f;
            var stair = new WallStair(octagonOutline, firstWall: 2, steps: new[] { 3, 14, 14 }, stepsPerWall: 14,
                                      height: octagonHeight + slab, width: 1.0f);
            var hatch = stair.Hatch(margin: 0.2f);

            var octagon = new RoomSpec
            {
                Id = "octagon", Name = "Octagon Room",
                Outline = octagonOutline, Height = octagonHeight,
                Floor = new Color(200, 170, 30), WallA = new Color(230, 200, 50), WallB = new Color(190, 160, 20), Ceiling = new Color(140, 120, 30),
                Doors = new[] { new DoorSpec("backroom", 0, 0f, "backroom", "octagon") },
                CeilingHatches = new[] { new HatchSpec(hatch, "octagonupper", slab) },
                Props = new[]
                {
                    new PropSpec("octagonstair", stair.Build,
                        WallStair.Palette(new Color(150, 105, 60), new Color(110, 75, 40), new Color(130, 90, 50), new Color(90, 60, 35)),
                        Vector3.Zero, 0f),
                },
                Ramps = stair.Ramps(),
            };

            // In the middle of the green room, a fixed ladder climbs south through a small hatch to a purple
            // octagon above that. The ladder's head is 0.6 m south of its foot. Past the head, a short
            // invisible strip at the purple floor's height sticks out under the hatch's south edge: the
            // ladder is so steep that a running climber could pass the last step's worth of it in one
            // frame, and without the strip would drop back to the green floor instead of stepping off.
            const float ladderLean = 0.6f;
            var ladderRise = corridorHeight + slab;
            var ladderFoot = new Vector3(0f, 0f, -ladderLean / 2f);
            var ladderHead = new Vector3(0f, ladderRise, ladderLean / 2f);
            var ladderHatch = new[]
            {
                new Vector2(-0.5f, ladderFoot.Z - 0.05f), new Vector2(0.5f, ladderFoot.Z - 0.05f),
                new Vector2(0.5f, ladderHead.Z + 0.15f), new Vector2(-0.5f, ladderHead.Z + 0.15f),
            };

            var octagonUpper = new RoomSpec
            {
                Id = "octagonupper", Name = "Green Octagon Room",
                Outline = octagonOutline, Height = corridorHeight,
                Floor = new Color(40, 130, 60), WallA = new Color(70, 190, 90), WallB = new Color(50, 150, 70), Ceiling = new Color(30, 80, 40),
                WorldOffset = octagon.WorldOffset + Vector3.Up * (octagonHeight + slab),
                FloorHatches = new[] { new HatchSpec(hatch, "octagon") },
                CeilingHatches = new[] { new HatchSpec(ladderHatch, "octagontop", slab) },
                // Non-blocking, like the hangar's: a Half would push you off the ladder before you could climb it
                Props = new[]
                {
                    new PropSpec("octagonladder", d => LadderMesh.Build(d, height: ladderRise, lean: ladderLean),
                        LadderMesh.Palette(new Color(180, 180, 185), new Color(60, 60, 65)),
                        ladderFoot, 0f),
                },
                Ramps = new[]
                {
                    new RampSpec(ladderFoot, ladderHead, 1.0f, MaxStepUp: 5f),   // steep: see the hangar's ladder
                    new RampSpec(ladderHead, ladderHead + Vector3.UnitZ * 0.3f, 1.0f),
                },
            };

            var octagonTop = new RoomSpec
            {
                Id = "octagontop", Name = "Purple Octagon Room",
                Outline = octagonOutline, Height = corridorHeight,
                Floor = new Color(110, 40, 150), WallA = new Color(160, 80, 200), WallB = new Color(130, 60, 170), Ceiling = new Color(70, 30, 90),
                WorldOffset = octagonUpper.WorldOffset + Vector3.Up * (corridorHeight + slab),
                FloorHatches = new[] { new HatchSpec(ladderHatch, "octagonupper") },
            };

            var rooms = new Dictionary<string, RoomSpec>();
            foreach (var room in new[] { corridor, stairs, upper, lounge, tvRoom, cola, hangar, backRoom, octagon, octagonUpper, octagonTop })
                rooms[room.Id] = room;

            // Start at the south end of the corridor, looking up it.
            return new HouseLevel(rooms, corridor.Id, new Vector3(0f, 0f, 6.5f), 0f);
        }
    }
}
