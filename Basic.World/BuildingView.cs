using MeshLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Buildings;

namespace Basic.World
{
    // A building as drawn: its shell (see BuildingMesh), its doors, and its rooms' insides (see RoomView) with
    // their furniture. Nothing of it is drawn when its shell is out of view. Its insides are left out when they
    // can't be seen: from outside, more than InteriorReach off, with every door shut - and every doorway has
    // one. Nearer than that they're drawn anyway, so nothing's lost through the gap round a shut door.
    public sealed class BuildingView
    {
        public const float InteriorReach = 10f;

        private readonly MeshInstance _shell;
        private readonly List<MeshInstance> _insides = new List<MeshInstance>();
        private readonly List<(Door door, MeshInstance view)> _doors = new List<(Door, MeshInstance)>();
        private readonly bool _doorless;   // a way in with no door in it: always open

        // Round the shell, roof and plinth and all, so round everything inside it too.
        public BoundingBox Bounds => _shell.Mesh.Bounds;   // the shell's mesh is in world coordinates

        public BuildingView(Building building, IReadOnlyList<Door> doors, GraphicsDevice device, MeshCache cache)
        {
            var shell = cache.GetOrAdd(device, "building:" + building.Name, d => BuildingMesh.Build(d, building));
            _shell = new MeshInstance(shell, BuildingMesh.Palette(building));
            foreach (var room in building.Rooms)
            {
                _insides.AddRange(new RoomView(room, device, cache).Instances);
                _doorless |= Array.Exists(room.Openings, o => o.LeadsOutside && !o.Door);
            }
            foreach (var door in doors)
            {
                var leaf = cache.GetOrAdd(device, DoorMesh.Key(door), d => DoorMesh.Build(d, door.Width, door.Height));
                _doors.Add((door, new MeshInstance(leaf, DoorMesh.Palette(door.Color))));
            }
        }

        public void Collect(MeshBatch batch, Vector3 eye)
        {
            if (!batch.InView(Bounds))
                return;
            batch.Add(_shell);
            foreach (var (door, view) in _doors)
            {
                view.Transform = DoorMesh.Transform(door);
                batch.Add(view);
            }
            if (InsideVisible(eye))
                foreach (var inside in _insides)
                    batch.Add(inside);
        }

        private bool InsideVisible(Vector3 eye)
        {
            if (_doorless || Vector3.Distance(eye, Vector3.Clamp(eye, Bounds.Min, Bounds.Max)) <= InteriorReach)
                return true;
            foreach (var (door, _) in _doors)
                if (!door.IsShut)
                    return true;
            return false;
        }
    }
}
