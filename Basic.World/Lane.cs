using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Core;
using static World.Buildings.Walls;

namespace Basic.World
{
    // A lane from the street's T-junction (see Street) out past the bungalows' back gardens and round the plateau
    // to the causeway's foot. Down it, on its west side, an old cottage (VectorViktor's house, see HouseMesh) looks
    // out over it. Walk into its front door and you're in a long corridor, off in a scene of its own the way
    // Basic.Levels' rooms are; walk into the door you came in by and you're back outside the cottage; walk up it to
    // the door at its far end and you're in the hangar the next cottage's window looks into (see Hangar), and back
    // again. Next door to the old cottage, another. Come up to either's front window and it thins away to show what's
    // inside - in the old one a parlour, in the next one the hangar, far bigger than the cottage, three trees going
    // round on turntables in it (see Window).
    public sealed class Lane : IDistrict
    {
        // The lane's corners, levelled a little above the hills there, and the foot of the causeway it ends at
        private static readonly RoadNetwork.Level FirstBend = new(new Vector2(76f, -65f), 0.3f);
        private static readonly RoadNetwork.Level SecondBend = new(new Vector2(-30f, -65f), 0.5f);
        private static readonly RoadNetwork.Level ThirdBend = new(new Vector2(-30f, -30f), 0.4f);
        private static readonly RoadNetwork.Level CausewayFoot = new(new Vector2(-9f, -30f));

        // The lane leaves the street northwards, level between the bungalows' gardens, then runs down past the backs
        // of them to a bend onto the plateau's north side, west along it, round and down a cutting through the ridge
        // west of the plateau, and round again onto the causeway's foot, heading up it: every straight sloping
        // evenly from the level of the piece before it to the level of the next.
        private static readonly RoadNetwork Road = new(
            new RoadNetwork.Straight(new(76f, 23f), new(76f, -3f), Street.Level, Street.Level) { Blend = 2f },   // between the gardens: they stand close
            new RoadNetwork.Straight(new(76f, -3f), new(76f, -55f), Street.Level, FirstBend),
            new RoadNetwork.Bend(FirstBend.With, MathHelper.Pi, FirstBend),
            new RoadNetwork.Straight(new(66f, -65f), new(-20f, -65f), FirstBend, SecondBend),
            new RoadNetwork.Bend(SecondBend.With, -MathHelper.PiOver2, SecondBend),
            new RoadNetwork.Straight(new(-30f, -55f), new(-30f, -40f), SecondBend, ThirdBend),
            new RoadNetwork.Bend(ThirdBend.With, 0f, ThirdBend),
            new RoadNetwork.Straight(new(-20f, -30f), new(-9f, -30f), ThirdBend, CausewayFoot));

        // The old cottage, and another next door to it, south down the lane. The old one's window looks onto a parlour,
        // as if behind it, the next one's onto the hangar.
        private static readonly LaneCottage OldCottage = new("cottage", new Vector2(64.25f, -35f),
            HouseMesh.Palette(new Color(235, 225, 205), new Color(150, 60, 45), new Color(60, 90, 60), new Color(90, 130, 190), new Color(130, 75, 55)),
            Parlour.Parts);
        private static readonly LaneCottage NextDoor = new("nextdoor", new Vector2(64.25f, -21f),
            HouseMesh.Palette(new Color(195, 205, 215), new Color(80, 80, 95), new Color(150, 40, 40), new Color(90, 130, 190), new Color(120, 70, 50)),
            Hangar.SeenFromCottage);
        private static readonly LaneCottage[] Cottages = { OldCottage, NextDoor };

        // The long corridor behind the old cottage: off the map, well out of sight of it (and of the fog, from it),
        // running north from its door in the south wall. So long that its far end, and the door there into the
        // hangar, fades into the fog.
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

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["lane"] = new(new Vector2(40f, -65f), -MathHelper.PiOver2),         // on the lane behind the plateau, heading west
            ["cottage"] = new(new Vector2(77f, -21f), -0.75f),                   // up the lane, looking down it at the cottage
            ["window"] = new(new Vector2(OldCottage.Front + 3f, OldCottage.WindowZ), -MathHelper.PiOver2),   // in the cottage's front garden, looking in at its window
            ["hangar"] = new(new Vector2(NextDoor.Front + 3f, NextDoor.WindowZ), -MathHelper.PiOver2),       // in the next cottage's, looking in at its
            ["corridor"] = new(new Vector2(Corridor.WorldOffset.X, Corridor.WorldOffset.Z + CorridorLength / 2f - ArrivalDistance), 0f),   // just inside, looking up it
            ["hangarfloor"] = ArrivingBy(Hangar.Room, Hangar.Room.Doors[0]),     // in the hangar, just in from its door, looking at the trees
            ["hangarwindow"] = new(Hangar.WindowStart, 0f),                      // in the hangar, looking out of its window
        };

        // The cottages' plots, both level with the hills where the old one stands - which is as high as the lane out
        // in front of the next one, where the hills are lower - and the lane's own ground, level or sloping with it.
        public IEnumerable<TerrainGenerator.Pad> Pads =>
            Cottages.Select(cottage => cottage.Pad(OldCottage.At)).Concat(Road.Pads());

        public IEnumerable<Building> Buildings(Terrain terrain)
        {
            yield return new Building(Corridor.Name, Corridor) { WallColor = new Color(120, 120, 120), RoofColor = new Color(80, 80, 80) };
            yield return Hangar.Shell();
        }

        // The old cottage's front door takes you just inside the corridor's, looking up it; the corridor's takes you
        // back out, a step in front of the cottage's, facing away from it down its garden. The door at the corridor's
        // far end takes you just inside the hangar's, and the hangar's back to it, looking back down the corridor.
        public IEnumerable<Portal> Portals(Terrain terrain)
        {
            var toCottage = Corridor.Doors[0];
            var toHangar = Corridor.Doors[1];
            var toCorridor = Hangar.Room.Doors[0];
            yield return new Portal(new Vector2(OldCottage.Front, OldCottage.DoorZ - LaneCottage.DoorHalf), new Vector2(OldCottage.Front, OldCottage.DoorZ + LaneCottage.DoorHalf),
                OldCottage.Ground(terrain), ArrivalBy(Corridor, toCottage), ArrivingBy(Corridor, toCottage).Yaw);
            yield return Through(Corridor, toCottage, new Vector3(OldCottage.Front + ArrivalDistance, OldCottage.Ground(terrain), OldCottage.DoorZ), MathHelper.PiOver2);
            yield return Through(Corridor, toHangar, ArrivalBy(Hangar.Room, toCorridor), ArrivingBy(Hangar.Room, toCorridor).Yaw);
            yield return Through(Hangar.Room, toCorridor, ArrivalBy(Corridor, toHangar), ArrivingBy(Corridor, toHangar).Yaw);
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

        // The hangar's trees' trunks and the cottages' walls, to walk into (see BuildingGround)
        public IEnumerable<WallSegment> Walls(Terrain terrain) =>
            Hangar.Walls().Concat(Cottages.SelectMany(cottage => cottage.Walls(terrain)));

        // Everything to draw: the lane's road, the cottages, and the hangar's window.
        public IEnumerable<Fixture> Fixtures(Terrain terrain) =>
            Road.Fixtures(terrain)
                .Concat(Cottages.SelectMany(cottage => cottage.Fixtures(terrain)))
                .Concat(Hangar.WindowFixtures(terrain, NextDoor));

        public IEnumerable<Window> Windows(Terrain terrain) =>
            Cottages.Select(cottage => cottage.FrontWindow(terrain)).Append(Hangar.LookingOut(terrain, NextDoor));

        public IEnumerable<ScenePart> Moving(Terrain terrain) => Hangar.InTheWorld();

        public bool Bare(float x, float z) => Road.Paved(x, z);
    }
}
