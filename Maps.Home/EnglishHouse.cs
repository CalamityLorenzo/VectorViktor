using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using System;
using World.Buildings;
using World.Rendering;

namespace Maps.Home
{
    // An English house: a two-storey front with a single-storey kitchen and dining room built on at the back under a
    // flat roof, every room its own, partitions between them (see RoomSpec.InnerWalls) and a door in every doorway.
    //
    // Downstairs, the front door opens into the hall, down its east side, with the stairs climbing its east wall from
    // the front towards the back. Off it to the west, the living room across the rest of the front: a sofa facing the
    // television on a sideboard, an armchair, a coffee table, the telephone. From the living room, double doors through
    // to the dining room behind it: a table with four chairs and a sideboard. From the back of the hall, the kitchen:
    // a cooker, a sink unit and cupboards, a larder, a door through to the dining room and the back door out.
    //
    // Upstairs, the stairs come up into the landing, over the hall, with a railing round the stairwell; off it, the
    // bedroom across the front (a double bed, a chest of drawers, a desk with a lamp, the telephone and a book on it,
    // a shelf of books over it) and the bathroom behind it (a roll-top bath and a wash basin). The front is under a
    // pitched roof (`pitched`), its ridge along the front.
    //
    // `at` is the middle of its footprint, on the ground floor; the front faces north (-Z). Every room shares that
    // origin, its outline wherever the room is in the house, so the plan below reads as one drawing.
    public static class EnglishHouse
    {
        public const float Width = 9f, Depth = 9f;            // inside the outer walls
        public const float DownHeight = 2.6f, UpHeight = 2.4f;
        public static readonly Vector2 FrontDoor = new Vector2(2.95f, -Depth / 2f);   // the middle of the front doorway, on the floor plan

        // The plan, on the house's own X (east) and Z (south). Partitions are 0.3 thick (Building.PartitionThickness): the
        // hall and the living room meet at x 2.0 to 2.3, the front and back at z 0.9 to 1.2, the kitchen and dining room
        // at x -0.15 to 0.15; upstairs, the bedroom and bathroom at z -1.5 to -1.2.
        private const float HallWest = 2.3f, LivingEast = 2.0f, FrontBack = 0.9f, BackFront = 1.2f, KitchenWest = 0.15f, DiningEast = -0.15f;
        private const float BedroomSouth = -1.5f, BathroomNorth = -1.2f;
        private const float E = Width / 2f, N = -Depth / 2f, S = Depth / 2f, W = -Width / 2f;

        // An outline from its corners, going round as a rectangle's do: north-west first, then east along the north side
        private static Vector2[] Outline(params float[] xz)
        {
            var points = new Vector2[xz.Length / 2];
            for (var i = 0; i < points.Length; i++)
                points[i] = new Vector2(xz[2 * i], xz[2 * i + 1]);
            return points;
        }

        // A doorway in edge `edge` of `outline`, its middle at `at` on the plan, `target` the room through it (or none:
        // outside); a door hung in it if it's `hung` (on one side only of a doorway between rooms: it swings into this one)
        private static OpeningSpec Doorway(Vector2[] outline, int edge, Vector2 at, string? target, bool hung, float width = 0.85f, float height = 2.05f)
        {
            var (a, b) = (outline[edge], outline[(edge + 1) % outline.Length]);
            var along = Vector2.Dot(at - (a + b) / 2f, Vector2.Normalize(b - a));
            return new OpeningSpec(edge, along, width, height, target!, Door: hung);
        }

        private static PropSpec Prop(string key, Func<Microsoft.Xna.Framework.Graphics.GraphicsDevice, MeshData> build, Color[] palette,
                                     float x, float y, float z, float yaw, float halfX = 0f, float halfZ = 0f) =>
            new PropSpec(new MeshSource(key, build, palette), new Vector3(x, y, z), yaw, new Vector2(halfX, halfZ));

        public static Building Build(string id, string name, Vector3 at, Color outside, Color roof, Gable pitched)
        {
            string Id(string room) => id + "." + room;
            var upstairs = at + Vector3.Up * (DownHeight + Houses.Slab);

            // ---- Downstairs

            var hallOutline = Outline(HallWest, N, E, N, E, FrontBack, HallWest, FrontBack);                        // N, E, S, W
            var livingOutline = Outline(W, N, LivingEast, N, LivingEast, FrontBack, 0f, FrontBack, W, FrontBack);       // N, E, S to the kitchen, S to the dining room, W
            var kitchenOutline = Outline(KitchenWest, BackFront, 2.15f, BackFront, E, BackFront, E, S, KitchenWest, S); // N to the living room, N to the hall, E, S, W
            var diningOutline = Outline(W, BackFront, DiningEast, BackFront, DiningEast, S, W, S);                     // N, E, S, W

            // The stairs: one flight up the hall's east wall from the front, sixteen steps of 18 cm to the landing
            var stair = new WallStair(hallOutline, firstWall: 1, steps: new[] { 16 }, stepsPerWall: 16, height: DownHeight + Houses.Slab, width: 0.9f);
            var stairwell = stair.Hatch(margin: 0.1f);

            var stairPalette = WallStairMesh.Palette(new Color(150, 105, 60), new Color(110, 75, 40), new Color(150, 40, 45), new Color(90, 60, 35));
            var teak = (carcass: new Color(140, 85, 45), front: new Color(175, 115, 65), leg: new Color(90, 55, 30), brass: new Color(215, 180, 90));
            var books = BookshelfMesh.Palette(new Color(150, 95, 50), new Color(45, 45, 50), new Color(140, 40, 40), new Color(40, 70, 120),
                new Color(50, 100, 60), new Color(200, 160, 70), new Color(220, 190, 90));
            var phone = RotaryPhoneMesh.Palette(new Color(35, 35, 38), new Color(200, 200, 205), new Color(240, 235, 220));
            var plant = PlantPotMesh.Palette(new Color(190, 95, 60), new Color(50, 130, 60));
            var manual = BookMesh.Source(BookCover.RobotManual, false, new Color(40, 70, 120), new Color(235, 230, 215), new Color(220, 190, 90));

            var hall = new RoomSpec
            {
                Id = Id("hall"), Name = name + " hall",
                Outline = hallOutline, Height = DownHeight, WorldOffset = at,
                Floor = new Color(130, 45, 40), WallA = new Color(225, 215, 185), WallB = new Color(205, 195, 165), Ceiling = new Color(240, 238, 230),
                InnerWalls = new[] { 2, 3 },
                Openings = new[]
                {
                    Doorway(hallOutline, 0, FrontDoor, null, hung: true, width: 1f, height: 2.1f),
                    Doorway(hallOutline, 2, new Vector2(3.0f, FrontBack), Id("kitchen"), hung: false),
                    Doorway(hallOutline, 3, new Vector2(HallWest, -3.0f), Id("living"), hung: false),
                },
                CeilingHatches = new[] { new HatchSpec(stairwell, Id("landing"), Houses.Slab, WallStair.HatchRails) },
                Ramps = stair.Ramps(),
                Props = new[]
                {
                    new PropSpec(WallStairMesh.Source(stair, stairPalette), Vector3.Zero, 0f),
                    Prop("plantpot", PlantPotMesh.Build, plant, 2.6f, 0f, -0.5f, 0f, 0.2f, 0.2f),
                    Prop("bookshelf", BookshelfMesh.Build, books, HallWest + 0.11f, 1.3f, -1.6f, 90f),   // on the wall, over your head's height
                },
            };

            var living = new RoomSpec
            {
                Id = Id("living"), Name = name + " living room",
                Outline = livingOutline, Height = DownHeight, WorldOffset = at,
                Floor = new Color(110, 75, 55), WallA = new Color(200, 210, 170), WallB = new Color(180, 190, 150), Ceiling = new Color(240, 238, 230),
                InnerWalls = new[] { 1, 2, 3 },
                Openings = new[]
                {
                    Doorway(livingOutline, 1, new Vector2(LivingEast, -3.0f), Id("hall"), hung: true),
                    Doorway(livingOutline, 3, new Vector2(-2.25f, FrontBack), Id("dining"), hung: true, width: 1.5f),   // swinging into here: the dining table's close
                },
                Props = new[]
                {
                    // The sofa against the west wall, facing east across the coffee table to the television, on the
                    // sideboard against the east wall with the telephone beside it
                    Prop("sofa", SofaMesh.Build, SofaMesh.Palette(new Color(120, 60, 50), new Color(160, 90, 75), new Color(90, 60, 40)),
                        W + 0.43f, 0f, -1.8f, 90f, 0.4f, 0.99f),
                    Prop("coffeetable", CoffeeTableMesh.Build, CoffeeTableMesh.Palette(new Color(190, 140, 80), new Color(110, 75, 40)),
                        -2.8f, 0f, -1.8f, 90f, 0.25f, 0.5f),
                    new PropSpec(manual, new Vector3(-2.8f, 0.28f, -1.5f), 80f),
                    Prop("sideboard", SideboardMesh.Build, SideboardMesh.Palette(teak.carcass, teak.front, teak.leg, teak.brass),
                        LivingEast - 0.25f, 0f, -1.0f, -90f, 0.23f, 0.73f),
                    Prop("television", TelevisionMesh.Build, TelevisionMesh.Palette(new Color(110, 70, 40), new Color(120, 140, 130), new Color(235, 225, 200),
                        new Color(60, 45, 35), new Color(90, 55, 30), new Color(190, 190, 195)), LivingEast - 0.25f, 0.64f, -1.1f, -90f),
                    Prop("rotaryphone", RotaryPhoneMesh.Build, phone, LivingEast - 0.25f, 0.64f, -0.45f, -90f),
                    Prop("armchair", ArmchairMesh.Build, ArmchairMesh.Palette(new Color(120, 60, 50), new Color(160, 90, 75), new Color(90, 60, 40)),
                        -1.0f, 0f, -3.6f, 150f, 0.4f, 0.4f),
                    Prop("fern", FernMesh.Build, FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60)), W + 0.4f, 0f, N + 0.4f, 0f, 0.2f, 0.2f),
                    Prop("plantpot", PlantPotMesh.Build, plant, LivingEast - 0.35f, 0f, FrontBack - 0.35f, 0f, 0.2f, 0.2f),
                    Prop("bookshelf", BookshelfMesh.Build, books, -1.5f, 1.4f, N + 0.11f, 0f),
                },
            };

            var dining = new RoomSpec
            {
                Id = Id("dining"), Name = name + " dining room",
                Outline = diningOutline, Height = DownHeight, WorldOffset = at,
                Floor = new Color(120, 80, 50), WallA = new Color(170, 120, 110), WallB = new Color(150, 100, 95), Ceiling = new Color(240, 238, 230),
                InnerWalls = new[] { 0, 1 },
                Openings = new[]
                {
                    Doorway(diningOutline, 0, new Vector2(-2.25f, BackFront), Id("living"), hung: false, width: 1.5f),
                    Doorway(diningOutline, 1, new Vector2(DiningEast, 2.2f), Id("kitchen"), hung: false),
                },
                Props = DiningRoom(teak),
            };

            var kitchen = new RoomSpec
            {
                Id = Id("kitchen"), Name = name + " kitchen",
                Outline = kitchenOutline, Height = DownHeight, WorldOffset = at,
                GridSpacing = 0.5f,   // tiles
                Floor = new Color(200, 200, 190), WallA = new Color(230, 225, 160), WallB = new Color(210, 205, 140), Ceiling = new Color(245, 245, 240),
                InnerWalls = new[] { 0, 1, 4 },
                Openings = new[]
                {
                    Doorway(kitchenOutline, 1, new Vector2(3.0f, BackFront), Id("hall"), hung: true),
                    Doorway(kitchenOutline, 3, new Vector2(1.2f, S), null, hung: true, width: 0.9f, height: 2.1f),   // the back door
                    Doorway(kitchenOutline, 4, new Vector2(KitchenWest, 2.2f), Id("dining"), hung: true),
                },
                // A cupboard at the end of the run along the east wall, and a tall larder against the north wall: E opens them
                Cabinets = new[]
                {
                    new CabinetSpec(CabinetKind.Cupboard, new Vector3(E - 0.3f, 0f, 4.0f), -90f, 0.9f, 0.55f, 0.9f, 2, new Color(140, 170, 130)),
                    new CabinetSpec(CabinetKind.Cupboard, new Vector3(1.15f, 0f, BackFront + 0.28f), 0f, 0.8f, 0.5f, 2.0f, 2, new Color(140, 170, 130)),
                },
                Props = new[]
                {
                    // Along the east wall, facing west: the cooker, then the sink
                    Prop("cooker", CookerMesh.Build, CookerMesh.Palette(new Color(235, 230, 215), new Color(40, 40, 45), new Color(200, 200, 205), new Color(50, 60, 70)),
                        E - 0.32f, 0f, 1.95f, -90f, 0.3f, 0.28f),
                    Prop("kitchensink", KitchenSinkMesh.Build, KitchenSinkMesh.Palette(new Color(140, 170, 130), new Color(220, 215, 200), new Color(190, 190, 195), new Color(60, 60, 65)),
                        E - 0.31f, 0f, 3.0f, -90f, 0.31f, 0.52f),
                },
            };

            // ---- Upstairs

            var landingOutline = Outline(HallWest, N, E, N, E, FrontBack, HallWest, FrontBack, HallWest, -1.35f);   // N, E, S, W to the bathroom, W to the bedroom
            var bedroomOutline = Outline(W, N, LivingEast, N, LivingEast, BedroomSouth, W, BedroomSouth);
            var bathroomOutline = Outline(W, BathroomNorth, LivingEast, BathroomNorth, LivingEast, FrontBack, W, FrontBack);

            var landing = new RoomSpec
            {
                Id = Id("landing"), Name = name + " landing",
                Outline = landingOutline, Height = UpHeight, WorldOffset = upstairs,
                Floor = new Color(130, 45, 40), WallA = new Color(225, 215, 185), WallB = new Color(205, 195, 165), Ceiling = new Color(240, 238, 230),
                InnerWalls = new[] { 3, 4 },
                Openings = new[]
                {
                    Doorway(landingOutline, 3, new Vector2(HallWest, -0.15f), Id("bathroom"), hung: false),
                    Doorway(landingOutline, 4, new Vector2(HallWest, -3.0f), Id("bedroom"), hung: false),
                },
                FloorHatches = new[] { new HatchSpec(stairwell, Id("hall"), Railed: WallStair.HatchRails) },
                Props = new[] { Prop("bookshelf", BookshelfMesh.Build, books, 3.3f, 1.2f, N + 0.11f, 0f) },
            };

            var bedroom = new RoomSpec
            {
                Id = Id("bedroom"), Name = name + " bedroom",
                Outline = bedroomOutline, Height = UpHeight, WorldOffset = upstairs,
                Floor = new Color(90, 80, 110), WallA = new Color(190, 200, 225), WallB = new Color(165, 175, 205), Ceiling = new Color(240, 240, 245),
                InnerWalls = new[] { 1, 2 },
                Openings = new[] { Doorway(bedroomOutline, 1, new Vector2(LivingEast, -3.0f), Id("landing"), hung: true) },
                Props = Bedroom(teak, books, phone, manual),
            };

            var bathroom = new RoomSpec
            {
                Id = Id("bathroom"), Name = name + " bathroom",
                Outline = bathroomOutline, Height = UpHeight, WorldOffset = upstairs,
                GridSpacing = 0.5f,   // tiles
                Floor = new Color(60, 110, 110), WallA = new Color(200, 230, 225), WallB = new Color(180, 215, 210), Ceiling = new Color(245, 245, 240),
                InnerWalls = new[] { 0, 1 },
                Openings = new[] { Doorway(bathroomOutline, 1, new Vector2(LivingEast, -0.15f), Id("landing"), hung: true) },
                Props = new[]
                {
                    // Against the back wall, facing into the room: the bath at the west end, the basin
                    Prop("bath", BathMesh.Build, BathMesh.Palette(new Color(40, 80, 70), new Color(240, 240, 235), new Color(200, 170, 80), new Color(200, 200, 205)),
                        W + 0.88f, 0f, FrontBack - 0.39f, 180f, 0.86f, 0.38f),
                    Prop("washbasin", WashBasinMesh.Build, WashBasinMesh.Palette(new Color(240, 240, 235), new Color(200, 200, 205)),
                        -1.6f, 0f, FrontBack - 0.22f, 180f, 0.28f, 0.21f),
                    Prop("plantpot", PlantPotMesh.Build, plant, LivingEast - 0.35f, 0f, FrontBack - 0.35f, 0f, 0.2f, 0.2f),
                },
            };

            return new Building(name, hall, living, dining, kitchen, landing, bedroom, bathroom)
            {
                WallColor = outside, RoofColor = roof, Roof = pitched,
                DoorColor = new Color(30, 70, 50),
            };
        }

        // The table in the middle, two chairs either side of it along its length, and the sideboard against the west wall
        private static PropSpec[] DiningRoom((Color carcass, Color front, Color leg, Color brass) teak)
        {
            var middle = new Vector2((W + DiningEast) / 2f, (BackFront + S) / 2f + 0.15f);   // a little back from the doorway
            var chair = DiningChairMesh.Palette(teak.carcass, new Color(160, 60, 55), teak.leg);
            var props = new System.Collections.Generic.List<PropSpec>
            {
                Prop("diningtable", DiningTableMesh.Build, DiningTableMesh.Palette(teak.front, teak.leg), middle.X, 0f, middle.Y, 0f, 0.75f, 0.43f),
                Prop("sideboard", SideboardMesh.Build, SideboardMesh.Palette(teak.carcass, teak.front, teak.leg, teak.brass), W + 0.25f, 0f, middle.Y, 90f, 0.23f, 0.73f),
            };
            foreach (var x in new[] { -0.4f, 0.4f })
            {
                props.Add(Prop("diningchair", DiningChairMesh.Build, chair, middle.X + x, 0f, middle.Y - 0.6f, 0f, 0.22f, 0.22f));
                props.Add(Prop("diningchair", DiningChairMesh.Build, chair, middle.X + x, 0f, middle.Y + 0.6f, 180f, 0.22f, 0.22f));
            }
            return props.ToArray();
        }

        // The double bed, its head against the front wall; the chest of drawers against the west wall; the desk against
        // the front wall, the lamp, the telephone and a book on it and a shelf of books on the wall over it
        private static PropSpec[] Bedroom((Color carcass, Color front, Color leg, Color brass) teak, Color[] books, Color[] phone, MeshSource manual)
        {
            var desk = new Vector2(0.4f, N + 0.37f);   // clear of the door's swing
            var top = OfficeDeskMesh.Height;
            return new[]
            {
                Prop("doublebed", DoubleBedMesh.Build, DoubleBedMesh.Palette(new Color(150, 95, 50), new Color(185, 150, 100), new Color(235, 232, 220),
                    new Color(70, 110, 150), new Color(90, 55, 30)), -1.6f, 0f, N + 1.04f, 0f, 0.71f, 1.0f),
                Prop("chestofdrawers", ChestOfDrawersMesh.Build, ChestOfDrawersMesh.Palette(teak.carcass, teak.front, teak.leg, teak.brass),
                    W + 0.25f, 0f, -2.6f, 90f, 0.23f, 0.4f),
                Prop("officedesk", OfficeDeskMesh.Build, OfficeDeskMesh.Palette(teak.carcass, teak.front, teak.leg, teak.brass),
                    desk.X, 0f, desk.Y, 0f, 0.7f, 0.35f),
                Prop("desklamp", DeskLampMesh.Build, DeskLampMesh.Palette(new Color(40, 90, 70), new Color(190, 190, 195), new Color(255, 240, 180)),
                    desk.X + 0.5f, top, desk.Y - 0.12f, -20f),
                Prop("rotaryphone", RotaryPhoneMesh.Build, phone, desk.X - 0.5f, top, desk.Y - 0.05f, 15f),
                new PropSpec(manual, new Vector3(desk.X, top, desk.Y + 0.1f), 10f),
                Prop("bookshelf", BookshelfMesh.Build, books, desk.X, 1.4f, N + 0.11f, 0f),
            };
        }
    }
}
