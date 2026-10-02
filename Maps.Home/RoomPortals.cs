using Microsoft.Xna.Framework;
using System;
using World.Buildings;
using World.Maps;

namespace Maps.Home
{
    // Doors in rooms off the map that take you somewhere else (see Portal), the way Basic.Levels' doors do: the lane's
    // long corridor and hangar (see Lane), and Basic.Levels' own house (see HouseRooms).
    public static class RoomPortals
    {
        public const float ArrivalDistance = 1f;   // how far in from a door you are when you come through it

        // Walking into a door in a room, to be taken `to`, facing `yaw`
        public static Portal Through(RoomSpec room, DoorSpec door, Vector3 to, float yaw)
        {
            Vector2 Flat(Vector3 v) => new Vector2(v.X, v.Z);
            return new Portal(Flat(room.WorldOffset + room.WallPoint(door.WallIndex, door.Offset - RoomSpec.DoorWidth / 2f)),
                Flat(room.WorldOffset + room.WallPoint(door.WallIndex, door.Offset + RoomSpec.DoorWidth / 2f)), room.WorldOffset.Y, to, yaw);
        }

        // Where you are when you come into a room by one of its doors: a step in from it, facing into the room
        public static Vector3 ArrivalBy(RoomSpec room, DoorSpec door) =>
            room.WorldOffset + room.WallPoint(door.WallIndex, door.Offset) + room.Inward(door.WallIndex) * ArrivalDistance;

        public static Start ArrivingBy(RoomSpec room, DoorSpec door)
        {
            var at = ArrivalBy(room, door);
            var inward = room.Inward(door.WallIndex);
            return new Start(new Vector2(at.X, at.Z), MathF.Atan2(inward.X, -inward.Z));
        }

        // Walking into one door of a room, to come in by another (in that room or another one), facing into the room
        public static Portal Between(RoomSpec room, DoorSpec door, RoomSpec target, DoorSpec targetDoor) =>
            Through(room, door, ArrivalBy(target, targetDoor), ArrivingBy(target, targetDoor).Yaw);
    }
}
