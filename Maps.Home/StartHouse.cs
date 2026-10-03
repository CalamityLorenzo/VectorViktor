using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Core;
using World.Core.Characters;
using World.Maps;
using World.Rendering;

namespace Maps.Home
{
    // The house the game starts in (see GameDesign.md, 3.9): where you meet the droid, and learn to move it and handle
    // things. It's on no map but its own (see Map), which is nothing but the house and the country round it, seen from
    // its windows: there's no way out but the front door, a portal onto another map (FrontDoorLeadsTo).
    //
    // Upstairs, from Paul's plan: a narrow hall along the south side, its stairwell along its south wall behind a fence of
    // posts, the stairs coming up from the east to the top at its west end. Off the hall, north, bedroom 1 (an L, round the
    // bathroom's north end) and bedroom 2; west, the bathroom, down the south half of the house's west side. In the
    // south-east corner, east of the hall, the locked room: no door in, and nothing to say what's in it. A hatch in the
    // hall's ceiling at its east end into the attic under the pitched roof: shut for now (see AtticOpen).
    //
    // Bedroom 1's window is an open gap; every other window is glazed, its glass tinted blue and crossed by glazing bars,
    // a window onto the world (see Window.Onto: here, onto the country round the house as it is).
    //
    // Downstairs, under the same outline: the hall under the upstairs one, the stairs climbing west along its south wall,
    // running on east into the porch, where the front door is, in the south wall; off it, north of the porch, the living
    // room (under bedroom 2); off the hall north, the kitchen (under bedroom 1); off it west, a small library (under the
    // bathroom).
    //
    // The plan's on the house's own X (east) and Z (south), its origin the middle of the footprint inside its outer walls,
    // on the ground floor; every room shares that origin, so the plan below reads as one drawing. Partitions are 0.3 thick
    // (Building.PartitionThickness), the rooms either side standing that far apart.
    public sealed class StartHouse : IDistrict
    {
        public const string MapName = "house";
        public const string DefaultStart = "house";

        // The front door's portal: onto the home map, at its hills, for now (to be decided: see GameDesign.md, 3.9)
        public const string FrontDoorLeadsTo = "home", FrontDoorStart = "hills";

        // The house's own map, its own terrain round it: hills like the home map's (another seed), the house on a levelled
        // plot in them
        public static readonly Map Map = new Map(MapName, pads => TerrainGenerator.Create(5, pads), () => new IDistrict[] { new StartHouse() }, DefaultStart);

        public const float Width = 14.7f, Depth = 9.6f;   // inside the outer walls
        public const float DownHeight = 2.6f, UpHeight = 2.4f;
        private const float Slab = Houses.Slab;

        // Where it stands: clear of the terrain's plateau, basin and pond
        public static readonly Vector2 At = new Vector2(45f, 45f);

        private const float E = Width / 2f, N = -Depth / 2f, S = Depth / 2f, W = -Width / 2f;

        // The partitions' middles: west of the hall (the bathroom, and the library under it), the bedrooms' between them,
        // east of the hall; the library's north end, north of the downstairs hall, and north of the locked room and the
        // porch. Upstairs, the hall's narrower than the one under it, its north side in line with the locked room's
        // (UpHallZ), so the bedrooms reach further south; and the bathroom's shorter than the library under it, its north
        // end (UpBathZ) further south, so bedroom 1 reaches further west.
        private const float BathX = -4.1f, BedsX = 0.25f, HallX = 3.75f, BathZ = -2.25f, HallZ = 0.25f, LockedZ = 2.1f;
        private const float UpHallZ = LockedZ, UpBathZ = 0.3f;
        private const float Half = 0.15f;   // half a partition

        // The stairs: one flight along the south wall, climbing west from its foot at StairFoot to its top at StairTop,
        // StairWidth wide; the hole they climb through, StairMargin wider
        private const float StairFoot = 3.3f, StairTop = -1.0f, StairWidth = 1.0f, StairMargin = 0.1f;

        // The ladder to the attic, at the east end of the upstairs hall, along its north side: from its foot at LadderAt,
        // leaning east as it climbs, clear of the stairwell, its head as near the ridge as the narrow hall goes, where
        // there's headroom to stand up in the attic
        private static readonly Vector2 LadderAt = new Vector2(2.4f, 2.65f);
        private const float LadderLean = 0.6f;

        // Whether the attic's hatch is open, with the ladder up through it: for now it's shut, a hatch door in the landing's
        // ceiling and no way up
        public static readonly bool AtticOpen = false;

        // An outline from its corners, going round as a rectangle's do: north-west first, then east along the north side
        private static Vector2[] Outline(params float[] xz)
        {
            var points = new Vector2[xz.Length / 2];
            for (var i = 0; i < points.Length; i++)
                points[i] = new Vector2(xz[2 * i], xz[2 * i + 1]);
            return points;
        }

        // How far along edge `edge` of `outline` a point on the plan is, from the edge's middle (see OpeningSpec.Offset)
        private static float Along(Vector2[] outline, int edge, Vector2 at)
        {
            var (a, b) = (outline[edge], outline[(edge + 1) % outline.Length]);
            return Vector2.Dot(at - (a + b) / 2f, Vector2.Normalize(b - a));
        }

        // A doorway in edge `edge` of `outline`, its middle at `at` on the plan, into `target`; a door hung in it if it's
        // `hung` (on one side only of a doorway between rooms: it swings into this one)
        private static OpeningSpec Doorway(Vector2[] outline, int edge, Vector2 at, string target, bool hung, float width = 0.85f, float height = 2.05f) =>
            new OpeningSpec(edge, Along(outline, edge, at), width, height, target, Door: hung);

        // A window in outer edge `edge` of `outline`, its middle at `at` on the plan
        private static WindowSpec Window(Vector2[] outline, int edge, Vector2 at, float width, float sill, float height, bool glazed = true) =>
            new WindowSpec(edge, Along(outline, edge, at), width, sill, height, glazed);

        private static PropSpec Prop(string key, Func<Microsoft.Xna.Framework.Graphics.GraphicsDevice, MeshData> build, Color[] palette,
                                     float x, float y, float z, float yaw, float halfX = 0f, float halfZ = 0f) =>
            new PropSpec(new MeshSource(key, build, palette), new Vector3(x, y, z), yaw, new Vector2(halfX, halfZ));

        // Its rooms' ids
        private const string Hall = "house.hall", Kitchen = "house.kitchen", Living = "house.living", Library = "house.library";
        private const string Landing = "house.landing", Bedroom1 = "house.bedroom1", Bedroom2 = "house.bedroom2", Bathroom = "house.bathroom";
        private const string Locked = "house.locked", Attic = "house.attic";

        // The downstairs hall's outline, and its front door, painted on its south wall: walked into, it's the portal
        private static readonly Vector2[] HallOutline = Outline(BathX + Half, HallZ + Half, HallX - Half, HallZ + Half, HallX - Half, LockedZ + Half,
            E, LockedZ + Half, E, S, BathX + Half, S);   // N to the kitchen, E to the living room, N to it again (the porch's), E, S, W to the library
        private static readonly DoorSpec FrontDoor = new DoorSpec("front", 4, Along(HallOutline, 4, new Vector2(5.5f, S)), "", "");

        // The floor of the ground floor: the levelled ground, and a step up
        private static float Floor(Terrain terrain) => terrain.HeightAt(At.X, At.Y) + Houses.FloorLift;

        private static Vector3 Origin(Terrain terrain) => new Vector3(At.X, Floor(terrain), At.Y);

        public static Building Build(Vector3 at) => Build(at, AtticOpen);

        // The house, with its attic's hatch open or shut (see AtticOpen)
        public static Building Build(Vector3 at, bool atticOpen)
        {
            var upstairs = at + Vector3.Up * (DownHeight + Slab);
            var attic = upstairs + Vector3.Up * (UpHeight + Slab);

            // The stairs, along the south wall of an outline of their own (just round them): every step of the flight fills it
            var stairOutline = Outline(StairTop - StairWidth, S - 2f, StairFoot + StairWidth, S - 2f, StairFoot + StairWidth, S, StairTop - StairWidth, S);
            var stair = new WallStair(stairOutline, firstWall: 2, steps: new[] { 16 }, stepsPerWall: 16, height: DownHeight + Slab, width: StairWidth);
            var stairwell = stair.Hatch(margin: StairMargin);
            var stairPalette = WallStairMesh.Palette(new Color(150, 105, 60), new Color(110, 75, 40), new Color(60, 90, 140), new Color(90, 60, 35));

            // The ladder up into the attic, and the hatch round its head
            var ladderRise = UpHeight + Slab;
            var ladderFoot = new Vector3(LadderAt.X, 0f, LadderAt.Y);
            var ladderHead = new Vector3(LadderAt.X + LadderLean, ladderRise, LadderAt.Y);
            var loftHatch = Outline(ladderFoot.X - 0.05f, LadderAt.Y - 0.38f, ladderHead.X + 0.15f, LadderAt.Y - 0.38f,
                ladderHead.X + 0.15f, LadderAt.Y + 0.38f, ladderFoot.X - 0.05f, LadderAt.Y + 0.38f);

            var teak = (carcass: new Color(140, 85, 45), front: new Color(175, 115, 65), leg: new Color(90, 55, 30), brass: new Color(215, 180, 90));
            var books = BookshelfMesh.Palette(new Color(150, 95, 50), new Color(45, 45, 50), new Color(140, 40, 40), new Color(40, 70, 120),
                new Color(50, 100, 60), new Color(200, 160, 70), new Color(220, 190, 90));
            var phone = RotaryPhoneMesh.Palette(new Color(35, 35, 38), new Color(200, 200, 205), new Color(240, 235, 220));
            var plant = PlantPotMesh.Palette(new Color(190, 95, 60), new Color(50, 130, 60));
            var manual = BookMesh.Source(BookCover.RobotManual, false, new Color(40, 70, 120), new Color(235, 230, 215), new Color(220, 190, 90));
            var chair = DiningChairMesh.Palette(teak.carcass, new Color(160, 60, 55), teak.leg);
            var white = new Color(240, 238, 230);

            // ---- Downstairs

            var kitchenOutline = Outline(W, N, BedsX - Half, N, BedsX - Half, HallZ - Half, BathX + Half, HallZ - Half, BathX + Half, BathZ - Half, W, BathZ - Half);   // N, E to the living room, S to the hall, W and S to the library, W
            var livingOutline = Outline(BedsX + Half, N, E, N, E, LockedZ - Half, HallX + Half, LockedZ - Half, HallX + Half, HallZ - Half, BedsX + Half, HallZ - Half);   // N, E, S and W to the hall, S to it again, W to the kitchen
            var libraryOutline = Outline(W, BathZ + Half, BathX - Half, BathZ + Half, BathX - Half, S, W, S);   // N to the kitchen, E to it and the hall, S, W

            var hall = new RoomSpec
            {
                Id = Hall, Name = "Hall",
                Outline = HallOutline, Height = DownHeight, WorldOffset = at,
                Floor = new Color(130, 45, 40), WallA = new Color(225, 215, 185), WallB = new Color(205, 195, 165), Ceiling = white,
                InnerWalls = new[] { 0, 1, 2, 5 },
                Openings = new[]
                {
                    Doorway(HallOutline, 0, new Vector2(-3.2f, HallZ + Half), Kitchen, hung: true),
                    Doorway(HallOutline, 2, new Vector2(5.5f, LockedZ + Half), Living, hung: true),
                    Doorway(HallOutline, 5, new Vector2(BathX + Half, 1.8f), Library, hung: true),
                },
                Doors = new[] { FrontDoor },
                CeilingHatches = new[] { new HatchSpec(stairwell, Landing, Slab, WallStair.HatchRails) { Balusters = true } },
                Ramps = stair.Ramps(),
                Props = new[]
                {
                    new PropSpec(WallStairMesh.Source(stair, stairPalette), Vector3.Zero, 0f),
                    Prop("plantpot", PlantPotMesh.Build, plant, E - 0.35f, 0f, S - 0.35f, 0f, 0.2f, 0.2f),
                    Prop("bookshelf", BookshelfMesh.Build, books, -0.6f, 1.4f, HallZ + Half + 0.11f, 0f),
                },
            };

            var kitchen = new RoomSpec
            {
                Id = Kitchen, Name = "Kitchen",
                Outline = kitchenOutline, Height = DownHeight, WorldOffset = at,
                GridSpacing = 0.5f,   // tiles
                Floor = new Color(200, 200, 190), WallA = new Color(230, 225, 160), WallB = new Color(210, 205, 140), Ceiling = white,
                InnerWalls = new[] { 1, 2, 3, 4 },
                Openings = new[]
                {
                    Doorway(kitchenOutline, 1, new Vector2(BedsX - Half, -2.4f), Living, hung: true),
                    Doorway(kitchenOutline, 2, new Vector2(-3.2f, HallZ - Half), Hall, hung: false),
                },
                Windows = new[]
                {
                    Window(kitchenOutline, 0, new Vector2(-3.1f, N), 1.8f, 1.0f, 1.1f),
                    Window(kitchenOutline, 5, new Vector2(W, -3.6f), 1.2f, 1.0f, 1.1f),
                },
                Cabinets = new[] { new CabinetSpec(CabinetKind.Cupboard, new Vector3(-5.4f, 0f, N + 0.28f), 0f, 0.9f, 0.5f, 0.9f, 2, new Color(140, 170, 130)) },
                Props = new[]
                {
                    // Along the north wall, facing south: the sink under the window, the cooker
                    Prop("kitchensink", KitchenSinkMesh.Build, KitchenSinkMesh.Palette(new Color(140, 170, 130), new Color(220, 215, 200), new Color(190, 190, 195), new Color(60, 60, 65)),
                        -3.1f, 0f, N + 0.31f, 0f, 0.52f, 0.31f),
                    Prop("cooker", CookerMesh.Build, CookerMesh.Palette(new Color(235, 230, 215), new Color(40, 40, 45), new Color(200, 200, 205), new Color(50, 60, 70)),
                        -1.1f, 0f, N + 0.32f, 0f, 0.3f, 0.28f),
                    // The kitchen table, a chair on each side
                    Prop("diningtable", DiningTableMesh.Build, DiningTableMesh.Palette(teak.front, teak.leg), -1.85f, 0f, -1.2f, 0f, 0.75f, 0.43f),
                    Prop("diningchair", DiningChairMesh.Build, chair, -1.85f, 0f, -1.86f, 0f, 0.22f, 0.22f),
                    Prop("diningchair", DiningChairMesh.Build, chair, -1.85f, 0f, -0.54f, 180f, 0.22f, 0.22f),
                    Prop("diningchair", DiningChairMesh.Build, chair, -2.83f, 0f, -1.2f, 90f, 0.22f, 0.22f),
                    Prop("diningchair", DiningChairMesh.Build, chair, -0.87f, 0f, -1.2f, -90f, 0.22f, 0.22f),
                },
            };

            var living = new RoomSpec
            {
                Id = Living, Name = "Living room",
                Outline = livingOutline, Height = DownHeight, WorldOffset = at,
                Floor = new Color(110, 75, 55), WallA = new Color(200, 210, 170), WallB = new Color(180, 190, 150), Ceiling = white,
                InnerWalls = new[] { 2, 3, 4, 5 },
                Openings = new[]
                {
                    Doorway(livingOutline, 2, new Vector2(5.5f, LockedZ - Half), Hall, hung: false),
                    Doorway(livingOutline, 5, new Vector2(BedsX + Half, -2.4f), Kitchen, hung: false),
                },
                Windows = new[]
                {
                    Window(livingOutline, 0, new Vector2(5.0f, N), 2.4f, 0.9f, 1.3f),
                    Window(livingOutline, 1, new Vector2(E, -2.45f), 2.8f, 0.9f, 1.3f),
                },
                Props = new[]
                {
                    // The television on the sideboard against the north wall, the sofa facing it across the coffee table
                    Prop("sideboard", SideboardMesh.Build, SideboardMesh.Palette(teak.carcass, teak.front, teak.leg, teak.brass),
                        2.2f, 0f, N + 0.25f, 0f, 0.73f, 0.23f),
                    Prop("television", TelevisionMesh.Build, TelevisionMesh.Palette(new Color(110, 70, 40), new Color(120, 140, 130), new Color(235, 225, 200),
                        new Color(60, 45, 35), new Color(90, 55, 30), new Color(190, 190, 195)), 2.1f, 0.64f, N + 0.25f, 0f),
                    Prop("rotaryphone", RotaryPhoneMesh.Build, phone, 2.7f, 0.64f, N + 0.25f, 0f),
                    Prop("coffeetable", CoffeeTableMesh.Build, CoffeeTableMesh.Palette(new Color(190, 140, 80), new Color(110, 75, 40)),
                        2.2f, 0f, -2.7f, 0f, 0.5f, 0.25f),
                    Prop("sofa", SofaMesh.Build, SofaMesh.Palette(new Color(120, 60, 50), new Color(160, 90, 75), new Color(90, 60, 40)),
                        2.2f, 0f, -1.1f, 180f, 0.99f, 0.4f),
                    Prop("armchair", ArmchairMesh.Build, ArmchairMesh.Palette(new Color(120, 60, 50), new Color(160, 90, 75), new Color(90, 60, 40)),
                        4.9f, 0f, -0.6f, 200f, 0.4f, 0.4f),
                    Prop("fern", FernMesh.Build, FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60)), E - 0.4f, 0f, LockedZ - Half - 0.4f, 0f, 0.2f, 0.2f),
                    Prop("plantpot", PlantPotMesh.Build, plant, E - 0.35f, 0f, N + 0.35f, 0f, 0.2f, 0.2f),
                    Prop("bookshelf", BookshelfMesh.Build, books, BedsX + Half + 0.11f, 1.4f, -1.0f, 90f),
                },
            };

            var library = new RoomSpec
            {
                Id = Library, Name = "Library",
                Outline = libraryOutline, Height = DownHeight, WorldOffset = at,
                Floor = new Color(90, 60, 45), WallA = new Color(120, 60, 55), WallB = new Color(100, 50, 45), Ceiling = white,
                InnerWalls = new[] { 0, 1 },
                Openings = new[] { Doorway(libraryOutline, 1, new Vector2(BathX - Half, 1.8f), Hall, hung: false) },
                Windows = new[] { Window(libraryOutline, 3, new Vector2(W, 0.85f), 1.6f, 0.9f, 1.3f) },
                Props = LibraryProps(teak, books, manual),
            };

            // ---- Upstairs

            // The landing's north wall is two edges, split where the bedrooms' partition meets it, so each has its doorway
            var landingOutline = Outline(BathX + Half, UpHallZ + Half, BedsX, UpHallZ + Half, HallX - Half, UpHallZ + Half, HallX - Half, S, BathX + Half, S);   // N to bedroom 1, N to bedroom 2, E, S, W to the bathroom
            var bedroom1Outline = Outline(W, N, BedsX - Half, N, BedsX - Half, UpHallZ - Half, BathX + Half, UpHallZ - Half, BathX + Half, UpBathZ - Half, W, UpBathZ - Half);   // N, E to bedroom 2, S to the hall, W and S to the bathroom, W
            var bedroom2Outline = Outline(BedsX + Half, N, E, N, E, UpHallZ - Half, BedsX + Half, UpHallZ - Half);   // N, E, S to the locked room and the hall, W to bedroom 1
            var bathroomOutline = Outline(W, UpBathZ + Half, BathX - Half, UpBathZ + Half, BathX - Half, S, W, S);   // N to bedroom 1, E to it and the hall, S, W
            var lockedOutline = Outline(HallX + Half, LockedZ + Half, E, LockedZ + Half, E, S, HallX + Half, S);

            var landing = new RoomSpec
            {
                Id = Landing, Name = "Landing",
                Outline = landingOutline, Height = UpHeight, WorldOffset = upstairs,
                Floor = new Color(130, 45, 40), WallA = new Color(225, 215, 185), WallB = new Color(205, 195, 165), Ceiling = white,
                InnerWalls = new[] { 0, 1, 2, 4 },
                Openings = new[]
                {
                    Doorway(landingOutline, 0, new Vector2(-2.7f, UpHallZ + Half), Bedroom1, hung: false),
                    Doorway(landingOutline, 1, new Vector2(1.85f, UpHallZ + Half), Bedroom2, hung: false),
                    Doorway(landingOutline, 4, new Vector2(BathX + Half, 3.0f), Bathroom, hung: false, width: 0.8f),
                },
                FloorHatches = new[] { new HatchSpec(stairwell, Hall, Railed: WallStair.HatchRails) { Balusters = true } },
                CeilingHatches = atticOpen ? new[] { new HatchSpec(loftHatch, Attic, Slab) } : Array.Empty<HatchSpec>(),
                Ramps = atticOpen
                    ? new[]
                    {
                        new RampSpec(ladderFoot, ladderHead, 0.7f, MaxStepUp: 5f),   // steep: see RampSpec.MaxStepUp
                        new RampSpec(ladderHead, ladderHead + Vector3.UnitX * 0.3f, 0.7f),
                    }
                    : Array.Empty<RampSpec>(),
                Props = new[]
                {
                    atticOpen
                        ? new PropSpec(LadderMesh.Source(ladderRise, LadderLean, LadderMesh.Palette(new Color(180, 180, 185), new Color(60, 60, 65))), ladderFoot, 90f)   // turned to lean east
                        : ShutHatch(loftHatch, UpHeight),
                    Prop("bookshelf", BookshelfMesh.Build, books, BathX + Half + 0.11f, 1.3f, 4.2f, 90f),
                },
            };

            var bedroom1 = new RoomSpec
            {
                Id = Bedroom1, Name = "Bedroom 1",
                Outline = bedroom1Outline, Height = UpHeight, WorldOffset = upstairs,
                Floor = new Color(90, 80, 110), WallA = new Color(190, 200, 225), WallB = new Color(165, 175, 205), Ceiling = white,
                InnerWalls = new[] { 1, 2, 3, 4 },
                Openings = new[] { Doorway(bedroom1Outline, 2, new Vector2(-2.7f, UpHallZ - Half), Landing, hung: true) },
                Windows = new[] { Window(bedroom1Outline, 0, new Vector2(-5.3f, N), 2.0f, 0.8f, 1.3f, glazed: false) },   // open: a gap in the wall
                Cabinets = new[] { Wardrobe },
                Props = Bedroom1Props(teak, books, manual),
            };

            var bedroom2 = new RoomSpec
            {
                Id = Bedroom2, Name = "Bedroom 2",
                Outline = bedroom2Outline, Height = UpHeight, WorldOffset = upstairs,
                Floor = new Color(70, 95, 90), WallA = new Color(215, 205, 175), WallB = new Color(195, 185, 155), Ceiling = white,
                InnerWalls = new[] { 2, 3 },
                Openings = new[] { Doorway(bedroom2Outline, 2, new Vector2(1.85f, UpHallZ - Half), Landing, hung: true) },
                Windows = new[]
                {
                    Window(bedroom2Outline, 0, new Vector2(5.0f, N), 2.4f, 0.8f, 1.3f),
                    Window(bedroom2Outline, 1, new Vector2(E, -2.45f), 3.0f, 0.8f, 1.3f),
                },
                Props = new[]
                {
                    // The bed's head against the west wall; the desk against the south wall at the east end, the lamp, the
                    // telephone and the droid's manual on it
                    Prop("doublebed", DoubleBedMesh.Build, DoubleBedMesh.Palette(new Color(150, 95, 50), new Color(185, 150, 100), new Color(235, 232, 220),
                        new Color(70, 110, 150), new Color(90, 55, 30)), BedsX + Half + 1.04f, 0f, -2.8f, 90f, 1.0f, 0.71f),
                    Prop("officedesk", OfficeDeskMesh.Build, OfficeDeskMesh.Palette(teak.carcass, teak.front, teak.leg, teak.brass),
                        5.6f, 0f, LockedZ - Half - 0.37f, 180f, 0.7f, 0.35f),
                    Prop("desklamp", DeskLampMesh.Build, DeskLampMesh.Palette(new Color(40, 90, 70), new Color(190, 190, 195), new Color(255, 240, 180)),
                        5.1f, OfficeDeskMesh.Height, LockedZ - Half - 0.25f, 160f),
                    Prop("rotaryphone", RotaryPhoneMesh.Build, phone, 6.1f, OfficeDeskMesh.Height, LockedZ - Half - 0.3f, 195f),
                    new PropSpec(manual, new Vector3(5.6f, OfficeDeskMesh.Height, LockedZ - Half - 0.45f), 190f),
                },
            };

            var bathroom = new RoomSpec
            {
                Id = Bathroom, Name = "Bathroom",
                Outline = bathroomOutline, Height = UpHeight, WorldOffset = upstairs,
                GridSpacing = 0.5f,   // tiles
                Floor = new Color(60, 110, 110), WallA = new Color(200, 230, 225), WallB = new Color(180, 215, 210), Ceiling = white,
                InnerWalls = new[] { 0, 1 },
                Openings = new[] { Doorway(bathroomOutline, 1, new Vector2(BathX - Half, 3.0f), Landing, hung: true, width: 0.8f) },
                Windows = new[] { Window(bathroomOutline, 3, new Vector2(W, 2.6f), 1.6f, 1.1f, 1.0f) },
                Props = new[]
                {
                    // The bath along the south wall, the basin on the north
                    Prop("bath", BathMesh.Build, BathMesh.Palette(new Color(40, 80, 70), new Color(240, 240, 235), new Color(200, 170, 80), new Color(200, 200, 205)),
                        -5.8f, 0f, S - 0.39f, 180f, 0.86f, 0.38f),
                    Prop("washbasin", WashBasinMesh.Build, WashBasinMesh.Palette(new Color(240, 240, 235), new Color(200, 200, 205)),
                        -5.8f, 0f, UpBathZ + Half + 0.22f, 0f, 0.28f, 0.21f),
                },
            };

            // No way in, and nothing yet to say what's in it
            var locked = new RoomSpec
            {
                Id = Locked, Name = "Locked room",
                Outline = lockedOutline, Height = UpHeight, WorldOffset = upstairs,
                Floor = new Color(40, 40, 45), WallA = new Color(70, 70, 75), WallB = new Color(60, 60, 65), Ceiling = new Color(80, 80, 85),
                InnerWalls = new[] { 0, 3 },
            };

            // Under the roof: bare boards, the rafters' slopes for a ceiling, and things put away up here
            var loft = new RoomSpec
            {
                Id = Attic, Name = "Attic",
                Outline = RoomSpec.Rectangle(Width, Depth), Height = 0.5f, WorldOffset = attic,
                Pitched = Gable.Pitched(35f, alongX: true),
                GridSpacing = 0.5f,
                Floor = new Color(150, 115, 75), WallA = new Color(170, 135, 95), WallB = new Color(150, 115, 80), Ceiling = new Color(125, 95, 65),
                FloorHatches = atticOpen ? new[] { new HatchSpec(loftHatch, Landing) } : Array.Empty<HatchSpec>(),
                Screens = new[] { new ScreenSpec(new Vector3(-2.0f, 0.64f, -0.6f), 0f, ScreenSpec.Static) },   // the old set, put away up here: static
                Props = new[]
                {
                    Prop("sideboard", SideboardMesh.Build, SideboardMesh.Palette(new Color(150, 90, 40), new Color(190, 120, 50), new Color(100, 60, 30), new Color(255, 220, 0)),
                        -2.0f, 0f, -0.6f, 0f, 0.73f, 0.23f),
                    Prop("television", TelevisionMesh.Build, TelevisionMesh.Palette(new Color(130, 80, 40), new Color(0, 200, 200), new Color(220, 220, 220),
                        new Color(90, 90, 90), new Color(60, 40, 20), new Color(200, 200, 200)), -2.0f, 0.64f, -0.6f, 0f),
                    new PropSpec(CrateMesh.Source(CrateKind.Cardboard, new Vector3(0.6f, 0.45f, 0.5f)), new Vector3(3.2f, 0f, -0.3f), 15f, new Vector2(0.35f, 0.35f)),
                    new PropSpec(CrateMesh.Source(CrateKind.Wood, new Vector3(0.7f, 0.6f, 0.6f)), new Vector3(-3.8f, 0f, 0.2f), -10f, new Vector2(0.4f, 0.4f)),
                },
            };

            return new Building("House", hall, kitchen, living, library, landing, bedroom1, bedroom2, bathroom, locked, loft)
            {
                WallColor = new Color(215, 200, 170), RoofColor = new Color(90, 70, 65),
                DoorColor = new Color(30, 70, 50),
            };
        }

        // A hatch door, shut, just under a ceiling `height` up, over the hole `outline` (a rectangle) would be: a thin board
        // in a frame, outlined, with a little handle to pull it down by
        private static PropSpec ShutHatch(Vector2[] outline, float height)
        {
            var (min, max) = (Vector2.Min(outline[0], outline[2]), Vector2.Max(outline[0], outline[2]));
            var size = max - min;
            var palette = new Color[6];
            MeshBuilder.SetBoxShades(palette, 0, new Color(225, 220, 205));   // the frame
            MeshBuilder.SetBoxShades(palette, 3, new Color(175, 140, 95));    // the board
            var mesh = new MeshSource($"shuthatch:{size.X:F2}x{size.Y:F2}", d =>
            {
                const float frame = 0.05f, depth = 0.02f;
                var builder = new MeshBuilder();
                builder.AddBox(0, new Vector3(0f, -depth, 0f), size.Y + 2f * frame, size.X + 2f * frame, depth);
                builder.AddBox(3, new Vector3(0f, -2f * depth, 0f), size.Y, size.X, depth);
                builder.AddBox(0, new Vector3(0f, -2f * depth - 0.03f, 0f), 0.04f, 0.1f, 0.03f);   // the handle
                return builder.Build(d);
            }, palette);
            var middle = (min + max) / 2f;
            return new PropSpec(mesh, new Vector3(middle.X, height, middle.Y), 0f);
        }

        // Bedroom 1's wardrobe, against the west wall facing into the room, two doors (E opens them), so full of clothes
        // they won't stay in (see WardrobeClothesMesh)
        private static readonly CabinetSpec Wardrobe = new CabinetSpec(CabinetKind.Cupboard, new Vector3(W + 0.3f, 0f, -2.6f), 90f, 1.2f, 0.6f, 2.0f, 2,
            new Color(120, 80, 50));

        // Where the droid stands in its milk churn, switched off (see Moving), out in the middle of bedroom 1, on its floor plan
        public static readonly Vector2 DroidAt = new Vector2(-3.8f, -2.2f);

        // Bedroom 1's double bed, in its north-east corner against both walls, head to the north: the middle of its footprint
        private static readonly Vector2 Bed = new Vector2(BedsX - Half - 0.72f, N + 1.02f);
        private const float BlanketTop = 0.49f;   // how high the top of its blanket is (see BedBuilder)

        // The bedside table, west of the bed's head against the north wall, and the alarm clock on it, turned a little towards
        // the bed: its digits flashing, as it's never been set
        private static readonly Vector2 BedsideAt = new Vector2(Bed.X - 0.72f - 0.03f - BedsideTableMesh.Width / 2f, N + BedsideTableMesh.Depth / 2f + 0.02f);
        private const float ClockYaw = 25f;

        // Bedroom 1: the double bed in the north-east corner, against both walls, crockery and sporks left on it and more
        // pushed under it; the bedside table, the alarm clock on it; the wardrobe's clothes; the desk against the bathroom's
        // wall, the lamp, a fern and a book on it, and a shelf of books over it; another shelf on the east wall; books left
        // about on the floor; and a poster, askew, on the north wall between the window and the bed
        private static PropSpec[] Bedroom1Props((Color carcass, Color front, Color leg, Color brass) teak, Color[] books, MeshSource manual)
        {
            var (east, south) = (BedsX - Half, UpBathZ - Half);   // the walls to the bedroom's east, and to the bathroom's north
            var desk = new Vector2(-5.6f, south - 0.37f);
            var top = OfficeDeskMesh.Height;
            var clothes = WardrobeClothesMesh.Palette(new Color(60, 90, 150), new Color(220, 215, 200), new Color(150, 50, 60), new Color(70, 70, 80));
            PropSpec Book(Color cover, float x, float y, float z, float yaw) =>
                new PropSpec(BookMesh.Source(BookCover.Plain, false, cover, new Color(30, 30, 30), new Color(220, 190, 90)), new Vector3(x, y, z), yaw);
            return new[]
            {
                Prop("doublebed", DoubleBedMesh.Build, DoubleBedMesh.Palette(new Color(150, 95, 50), new Color(185, 150, 100), new Color(235, 232, 220),
                    new Color(150, 70, 60), new Color(90, 55, 30)), Bed.X, 0f, Bed.Y, 0f, 0.71f, 1.0f),
                Prop("bedsidetable", BedsideTableMesh.Build, BedsideTableMesh.Palette(teak.carcass, teak.front, teak.brass),
                    BedsideAt.X, 0f, BedsideAt.Y, 0f, BedsideTableMesh.Width / 2f, BedsideTableMesh.Depth / 2f),
                Prop("alarmclock", AlarmClockMesh.Build, AlarmClockMesh.Palette(new Color(40, 40, 45), new Color(150, 150, 155), Digits),
                    BedsideAt.X, BedsideTableMesh.Height, BedsideAt.Y, ClockYaw),
                new PropSpec(WardrobeClothesMesh.Source(Wardrobe.Width, Wardrobe.Depth, Wardrobe.Height, clothes), Wardrobe.Position, Wardrobe.YawDegrees),

                Prop("officedesk", OfficeDeskMesh.Build, OfficeDeskMesh.Palette(teak.carcass, teak.front, teak.leg, teak.brass),
                    desk.X, 0f, desk.Y, 180f, 0.7f, 0.35f),
                Prop("desklamp", DeskLampMesh.Build, DeskLampMesh.Palette(new Color(200, 60, 50), new Color(190, 190, 195), new Color(255, 240, 180)),
                    desk.X - 0.45f, top, desk.Y + 0.12f, 200f),
                Prop("fern", FernMesh.Build, FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60)), desk.X + 0.45f, top, desk.Y + 0.1f, 0f),
                Book(new Color(60, 120, 80), desk.X, top, desk.Y - 0.05f, 165f),
                Prop("bookshelf", BookshelfMesh.Build, books, desk.X, 1.4f, south - 0.11f, 180f),
                Prop("bookshelf", BookshelfMesh.Build, books, east - 0.11f, 1.4f, -1.4f, -90f),

                // On the floor: a pile of three by the desk, and a couple left by the bed
                Book(new Color(140, 40, 40), -4.4f, 0f, -0.6f, 10f),
                Book(new Color(40, 70, 120), -4.41f, BookMesh.Thickness, -0.61f, 35f),
                Book(new Color(200, 160, 70), -4.39f, 2f * BookMesh.Thickness, -0.6f, -15f),
                Book(new Color(50, 100, 60), -1.7f, 0f, -2.3f, 50f),
                Book(new Color(120, 60, 120), -2.1f, 0f, -1.7f, -30f),

                new PropSpec(new MeshSource("poster-unicorn", PosterMesh.Build, PosterMesh.Palette(new Color(230, 190, 215))), new Vector3(-2.6f, 1.45f, N), 0f),
            }.Concat(Crockery()).ToArray();
        }

        private static readonly Color Digits = new Color(255, 40, 30);

        // Crockery and sporks left on the bed (on its blanket, clear of the pillows), and more pushed under it from its
        // west side, half out
        private static IEnumerable<PropSpec> Crockery()
        {
            var palette = CrockeryMesh.Palette(new Color(240, 238, 228), new Color(60, 100, 170), new Color(235, 130, 35));
            var plate = new MeshSource("crockery:plate", CrockeryMesh.Plate, palette);
            var bowl = new MeshSource("crockery:bowl", CrockeryMesh.Bowl, palette);
            var mug = new MeshSource("crockery:mug", CrockeryMesh.Mug, palette);
            var spork = new MeshSource("crockery:spork", CrockeryMesh.Spork, palette);
            PropSpec On(MeshSource mesh, float x, float y, float z, float yaw) => new PropSpec(mesh, new Vector3(Bed.X + x, y, Bed.Y + z), yaw);
            const float top = BlanketTop;

            yield return On(plate, -0.3f, top, 0.1f, 0f);
            yield return On(spork, -0.3f, top + CrockeryMesh.PlateHeight, 0.1f, 30f);
            yield return On(CrockeryMesh.StackSource(2, palette), -0.25f, top, 0.65f, 0f);
            yield return On(spork, -0.22f, top + 2f * CrockeryMesh.PlateHeight, 0.62f, -60f);
            yield return On(bowl, 0.2f, top, 0.3f, 0f);
            yield return On(spork, 0.38f, top, 0.55f, 110f);
            yield return On(mug, 0.1f, top, 0.75f, 200f);

            // Under the bed: a stack of plates and a bowl, half out from under its west side, and a spork dropped by them
            var side = -0.71f;
            yield return On(CrockeryMesh.StackSource(3, palette), side, 0f, -0.15f, 0f);
            yield return On(bowl, side + 0.01f, 0f, 0.4f, 0f);
            yield return On(spork, side - 0.18f, 0f, 0.55f, 70f);
        }

        // A small library: shelves of books on the walls, a desk under the north wall with a lamp and the droid's manual on
        // it, an armchair by the window
        private static PropSpec[] LibraryProps((Color carcass, Color front, Color leg, Color brass) teak, Color[] books, MeshSource manual)
        {
            var props = new List<PropSpec>
            {
                Prop("officedesk", OfficeDeskMesh.Build, OfficeDeskMesh.Palette(teak.carcass, teak.front, teak.leg, teak.brass),
                    -5.8f, 0f, BathZ + Half + 0.37f, 0f, 0.7f, 0.35f),
                Prop("desklamp", DeskLampMesh.Build, DeskLampMesh.Palette(new Color(120, 40, 40), new Color(190, 190, 195), new Color(255, 240, 180)),
                    -5.3f, OfficeDeskMesh.Height, BathZ + Half + 0.25f, -20f),
                new PropSpec(manual, new Vector3(-5.9f, OfficeDeskMesh.Height, BathZ + Half + 0.45f), 10f),
                Prop("armchair", ArmchairMesh.Build, ArmchairMesh.Palette(new Color(60, 90, 60), new Color(90, 130, 90), new Color(70, 45, 30)),
                    -5.6f, 0f, 3.5f, -60f, 0.4f, 0.4f),
            };
            // Three columns up the south wall, one up the east wall south of the door, three shelves high, and up the west wall north of the window
            foreach (var y in new[] { 0.5f, 1.0f, 1.5f })
            {
                foreach (var x in new[] { -6.55f, -5.6f, -4.75f })
                    props.Add(Prop("bookshelf", BookshelfMesh.Build, books, x, y, S - 0.11f, 180f));
                props.Add(Prop("bookshelf", BookshelfMesh.Build, books, BathX - Half - 0.11f, y, 3.6f, -90f));
                props.Add(Prop("bookshelf", BookshelfMesh.Build, books, W + 0.11f, y + 0.4f, -1.05f, 90f));
            }
            return props.ToArray();
        }

        // ---- The district: the house, and the country round it to see from its windows

        public IEnumerable<TerrainGenerator.Pad> Pads => new[]
        {
            new TerrainGenerator.Pad(At, new Vector2(Width / 2f + 1f, Depth / 2f + 1f), Apron: 4f, Blend: 10f),
        };

        public IEnumerable<Building> Buildings(Terrain terrain)
        {
            yield return Build(Origin(terrain));
        }

        // Where to start: inside the front door, looking into the house; on the landing; in bedroom 1, looking at the droid;
        // in the attic
        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["house"] = Arriving(new Vector2(5.5f, S - 1.0f), 0f, 1.0f),
            ["landing"] = Arriving(new Vector2(-2.0f, 2.9f), MathHelper.Pi / 2f, DownHeight + Slab + 1.0f),
            ["bedroom1"] = Arriving(new Vector2(-3.4f, -0.3f), -0.21f, DownHeight + Slab + 1.0f),   // facing the droid, the bed beyond it
            ["attic"] = Arriving(new Vector2(0f, -0.5f), 0f, DownHeight + UpHeight + 2f * Slab + 1.0f),
        };

        private static Start Arriving(Vector2 plan, float yaw, float above) => new Start(At + plan, yaw, above);

        // The front door: onto the map it leads to (see FrontDoorLeadsTo)
        public IEnumerable<Portal> Portals(Terrain terrain)
        {
            var hall = Build(Origin(terrain)).Rooms.Single(room => room.Id == Hall);
            yield return RoomPortals.Through(hall, FrontDoor, Vector3.Zero, 0f) with { ToMap = FrontDoorLeadsTo, ToStart = FrontDoorStart };
        }

        // The droid in its milk churn, out in the middle of bedroom 1, facing the door: switched off, so it stands still,
        // its head camera looking down, and its visor's ruby dark. And the alarm clock's digits, flashing 12:00, on half a
        // second, off half a second, as an unset clock's do.
        public IEnumerable<ScenePart> Moving(Terrain terrain)
        {
            var upstairs = Origin(terrain) + Vector3.Up * (DownHeight + Slab);
            var clock = Matrix.CreateRotationY(MathHelper.ToRadians(ClockYaw)) *
                Matrix.CreateTranslation(upstairs + new Vector3(BedsideAt.X, BedsideTableMesh.Height, BedsideAt.Y));
            var digits = new MeshSource("alarmclock:12:00", d => AlarmClockMesh.Digits(d, "12:00"), AlarmClockMesh.Palette(Color.Black, Color.Black, Digits));
            yield return new ScenePart(digits, t => t % 1f < 0.5f ? clock : RigScene.OutOfSight);

            var palette = DroidMesh.StartingPalette();
            MeshBuilder.SetBoxShades(palette, DroidMesh.Ruby, new Color(55, 6, 14));
            var at = Origin(terrain) + new Vector3(DroidAt.X, DownHeight + Slab, DroidAt.Y);
            void Off(World.Core.Animation.Rig rig, float t) => DroidRig.Look(rig, 0f, up: -0.6f);   // slumped
            foreach (var part in RigScene.Parts(DroidRig.Build(Locomotion.Churn), DroidMesh.Sources(palette), Off,
                Matrix.CreateTranslation(at), DroidMesh.CableSource(palette), DroidMesh.Variants))   // the rig faces +Z: south, to the door
                yield return part;
        }

        // The churn, to walk into: a square round it
        public IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            var floor = Origin(terrain).Y + DownHeight + Slab;
            var centre = At + DroidAt;
            const float r = DroidBases.ChurnRadius + 0.03f;
            var corners = new[] { centre + new Vector2(-r, -r), centre + new Vector2(r, -r), centre + new Vector2(r, r), centre + new Vector2(-r, r) };
            for (var k = 0; k < 4; k++)
                yield return new WallSegment(corners[k], corners[(k + 1) % 4], floor, floor + DroidBases.ChurnHeight);
        }

        // A tree or two in sight of each window, clear of the house and its plot, and the glazing bars of every glazed window
        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var oak = new MeshSource("oak", OakMesh.Build, OakMesh.Palette(new Color(90, 65, 40), new Color(70, 130, 50)));
            var tree = new MeshSource("tree", TreeMesh.Build, TreeMesh.Palette(new Color(100, 70, 40), new Color(60, 150, 60)));
            foreach (var (mesh, x, z, scale, turn) in new[]
            {
                (oak, -7f, -14f, 3f, 0.4f), (oak, 7f, -16f, 3.4f, 1.9f), (tree, 16f, -4f, 4.5f, 0.2f), (oak, 18f, 8f, 3f, 2.6f),
                (tree, -15f, 3f, 4.5f, 1.1f), (oak, -18f, -6f, 2.8f, 0.7f), (oak, -4f, 15f, 3.2f, 2.2f),
            })
            {
                var at = At + new Vector2(x, z);
                yield return new Fixture(mesh, Matrix.CreateScale(scale) * Matrix.CreateRotationY(turn) * Matrix.CreateTranslation(at.X, terrain.HeightAt(at.X, at.Y), at.Y));
            }

            var building = Build(Origin(terrain));
            var palette = new Color[3];
            MeshBuilder.SetBoxShades(palette, 0, new Color(235, 235, 228));
            foreach (var (room, window) in Glazed(building))
            {
                var frame = Frame(building, room, window);
                var key = $"glazing:{window.Width:F2}x{window.Height:F2}:{window.Sill:F2}";
                var middle = -building.WallThickness / 2f;   // halfway through the wall
                yield return new Fixture(new MeshSource(key, d =>
                {
                    const float bar = 0.05f, depth = 0.04f;
                    var mesh = new MeshBuilder();
                    mesh.AddBox(0, new Vector3(0f, window.Sill, middle), depth, bar, window.Height);
                    mesh.AddBox(0, new Vector3(0f, window.Sill + (window.Height - bar) / 2f, middle), depth, window.Width, bar);
                    return mesh.Build(d);
                }, palette), frame);
            }
        }

        // Every glazed window looks out onto the world as it is, through blue-tinted glass: always, from anywhere in the house
        public IEnumerable<MeshRendering.Window> Windows(Terrain terrain)
        {
            var building = Build(Origin(terrain));
            var pane = new Color(90, 130, 190);
            foreach (var (room, window) in Glazed(building))
            {
                var frame = Frame(building, room, window);
                yield return new MeshRendering.Window(frame, window.Width, window.Sill, window.Height, pane, Array.Empty<ScenePart>(), Onto: frame)
                {
                    Reach = 14f, Clear = 13.9f,
                };
            }
        }

        private static IEnumerable<(RoomSpec room, WindowSpec window)> Glazed(Building building) =>
            building.Rooms.SelectMany(room => room.Windows.Where(window => window.Glazed).Select(window => (room, window)));

        // A window's frame (see MeshRendering.Window): on the outer face of the wall, its middle on the room's floor level,
        // its -Z into the room
        private static Matrix Frame(Building building, RoomSpec room, WindowSpec window)
        {
            var inward = room.Inward(window.WallIndex);
            var outer = room.WorldOffset + room.WallPoint(window.WallIndex, window.Offset) - inward * building.WallThickness;
            return Matrix.CreateWorld(outer, inward, Vector3.Up);
        }
    }
}
