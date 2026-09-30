using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Core;
using World.Core.Physics;
using World.Core.Vehicles;

namespace World.Maps
{
    // The world, put together from its districts (see IDistrict): the ground to walk on and the bodies in it,
    // and what's to be drawn.
    public sealed class BuiltWorld
    {
        public required Terrain Terrain { get; init; }
        public required BuildingGround Ground { get; init; }
        public required PhysicsWorld Physics { get; init; }
        public required IReadOnlyList<Building> Buildings { get; init; }
        public required IReadOnlyList<Fixture> Fixtures { get; init; }
        public required IReadOnlyList<Window> Windows { get; init; }
        public required IReadOnlyList<ScenePart> Moving { get; init; }
        public required IReadOnlyList<Portal> Portals { get; init; }
        public required IReadOnlyList<Thing> Things { get; init; }

        // The cars, parked or driven: more can be added (see Start.InCar)
        public required List<Car> Cars { get; init; }
        public required IReadOnlyDictionary<string, Start> Starts { get; init; }

        // Where the terrain gets no grid lines (see IDistrict.Bare).
        public required Func<float, float, bool> Bare { get; init; }

        // Where the terrain isn't drawn at all (see IDistrict.Covered).
        public required Func<float, float, bool> Covered { get; init; }
    }

    public static class WorldBuilder
    {
        private static void Unique(IEnumerable<string> names, string what)
        {
            var seen = new HashSet<string>();
            foreach (var name in names)
                if (!seen.Add(name))
                    throw new InvalidOperationException($"Two of the world's buildings have a {what} called '{name}'.");
        }

        // A whole map (see Map): its districts on its own terrain.
        public static BuiltWorld Build(Map map) => Build(map.Districts(), map.MakeTerrain);

        // A map, from its districts (see Maps.Home's HomeMap for one), on the home map's terrain (see TerrainGenerator).
        public static BuiltWorld Build(IReadOnlyList<IDistrict> districts, int seed = 1) =>
            Build(districts, pads => TerrainGenerator.Create(seed, pads));

        // A map, from its districts: every district's pads levelled into one terrain, made by `makeTerrain`, in the
        // order the districts are listed (each district's over the ones before it), then its water, its buildings
        // walls and ledges as one ground, and its things in one world of bodies.
        public static BuiltWorld Build(IReadOnlyList<IDistrict> districts, Func<IReadOnlyList<TerrainGenerator.Pad>, Terrain> makeTerrain)
        {
            var terrain = makeTerrain(districts.SelectMany(d => d.Pads).ToList());
            terrain.Flood(districts.SelectMany(d => d.Pools(terrain)).ToArray());

            var buildings = districts.SelectMany(d => d.Buildings(terrain)).ToList();
            // Their meshes are known by their names (see BuildingMesh.Source, RoomView), so no two may share one
            Unique(buildings.Select(b => b.Name), "building");
            Unique(buildings.SelectMany(b => b.Rooms).Select(r => r.Id), "room");
            var ground = new BuildingGround(terrain, buildings, districts.SelectMany(d => d.Walls(terrain)).ToList(),
                                            districts.SelectMany(d => d.Ledges(terrain)).ToList());
            var physics = new PhysicsWorld(ground);

            var all = districts.ToArray();
            bool Bare(float x, float z)
            {
                foreach (var district in all)
                    if (district.Bare(x, z))
                        return true;
                return false;
            }
            bool Covered(float x, float z)
            {
                foreach (var district in all)
                    if (district.Covered(x, z))
                        return true;
                return false;
            }

            var starts = new Dictionary<string, Start>();
            foreach (var district in districts)
                foreach (var (name, start) in district.Starts)
                    if (!starts.TryAdd(name, start))
                        throw new InvalidOperationException($"Two districts both have a start called '{name}'.");

            return new BuiltWorld
            {
                Terrain = terrain,
                Ground = ground,
                Physics = physics,
                Buildings = buildings,
                Fixtures = districts.SelectMany(d => d.Fixtures(terrain)).ToList(),
                Windows = districts.SelectMany(d => d.Windows(terrain)).ToList(),
                Moving = districts.SelectMany(d => d.Moving(terrain)).ToList(),
                Portals = districts.SelectMany(d => d.Portals(terrain)).ToList(),
                Things = districts.SelectMany(d => d.Things(physics, terrain)).ToList(),
                Cars = districts.SelectMany(d => d.Cars(terrain))
                    .Select(c => new Car(new Vector3(c.At.X, terrain.HeightAt(c.At.X, c.At.Y), c.At.Y), c.Yaw, physics)).ToList(),
                Starts = starts,
                Bare = Bare,
                Covered = Covered,
            };
        }
    }
}
