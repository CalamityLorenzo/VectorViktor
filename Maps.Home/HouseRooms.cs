using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Maps;
using static World.Buildings.Walls;

namespace Maps.Home
{
    // Basic.Levels' house (see HouseLevel) in the home map: off the map, like the lane's long corridor (see Lane), each of
    // its scenes somewhere of its own, well apart - the corridor with the stairs and upper corridor it opens into, the
    // three octagons stacked one on another, and each other room by itself. Its doors take you into each other, as in
    // Basic.Levels (see RoomPortals). Its rooms' ids are the level's with "level." in front, so they don't clash with the
    // map's own (its hangar with the lane's). And it has a front door the level hasn't: in the corridor's west wall, near
    // its south end, out to the cottage whose window looks into the lane's hangar (see Lane).
    public static class HouseRooms
    {
        private const string Prefix = "level.";
        private static readonly Vector3 First = new Vector3(0f, 0f, 2000f);   // where the first scene is: the corridor
        private const float Apart = 150f;                                     // and each next one, so much further east

        public static readonly DoorSpec FrontDoor = new DoorSpec("outside", West, -5.5f, "", "");   // see Lane.Portals

        private static readonly HouseLevel Level = HouseLevel.Create();

        // Each scene, its rooms moved to where it is in the map
        private static readonly RoomSpec[][] Scenes = Place();

        public static IEnumerable<RoomSpec> Rooms => Scenes.SelectMany(scene => scene);
        public static RoomSpec Corridor => Rooms.Single(room => room.Id == Prefix + Level.StartRoom);

        // Where Basic.Levels starts you: at the south end of the corridor, looking up it
        public static Start Start
        {
            get
            {
                var at = Level.StartPosition + Corridor.WorldOffset - Level.Rooms[Level.StartRoom].WorldOffset;
                return new Start(new Vector2(at.X, at.Z), Level.StartYaw);
            }
        }

        // The level's rooms, joined into scenes by their openings and hatches, in the level's order, each scene moved on
        // from the last; the corridor with its front door
        private static RoomSpec[][] Place()
        {
            var placed = new HashSet<string>();
            var scenes = new List<RoomSpec[]>();
            foreach (var id in Level.Rooms.Keys)
            {
                if (!placed.Add(id))
                    continue;
                var scene = new List<RoomSpec> { Level.Rooms[id] };
                for (var i = 0; i < scene.Count; i++)
                    foreach (var neighbour in scene[i].Neighbours())
                        if (placed.Add(neighbour))
                            scene.Add(Level.Rooms[neighbour]);
                var by = First + Vector3.UnitX * Apart * scenes.Count;
                scenes.Add(scene.Select(room => room.Id == Level.StartRoom ? room.Moved(by, name => Prefix + name, FrontDoor)
                                                                           : room.Moved(by, name => Prefix + name)).ToArray());
            }
            return scenes.ToArray();
        }

        // A building round each scene, its shell out of sight in the fog
        public static IEnumerable<Building> Buildings() =>
            Scenes.Select(scene => new Building("House level " + scene[0].Name, scene));

        // Each of the level's doors takes you in at the one it leads to (not the front door: see Lane.Portals)
        public static IEnumerable<Portal> Portals()
        {
            var byId = Rooms.ToDictionary(room => room.Id);
            foreach (var room in Rooms)
                foreach (var door in room.Doors)
                {
                    if (string.IsNullOrEmpty(door.TargetRoom))
                        continue;
                    var target = byId[door.TargetRoom];
                    yield return RoomPortals.Between(room, door, target, target.Doors.Single(d => d.Id == door.TargetDoor));
                }
        }
    }
}
