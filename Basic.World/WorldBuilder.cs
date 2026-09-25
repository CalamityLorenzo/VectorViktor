using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Core;
using World.Core.Physics;

namespace Basic.World
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
        public required IReadOnlyList<Thing> Things { get; init; }
        public required IReadOnlyDictionary<string, Start> Starts { get; init; }

        // Where the terrain gets no grid lines (see IDistrict.Bare).
        public required Func<float, float, bool> Bare { get; init; }
    }

    public static class WorldBuilder
    {
        // Every district's pads levelled into one terrain, in the order the districts are listed, then its water,
        // its buildings and walls as one ground, and its things in one world of bodies.
        public static BuiltWorld Build(IReadOnlyList<IDistrict> districts, int seed = 1)
        {
            var terrain = TerrainGenerator.Create(seed, districts.SelectMany(d => d.Pads).ToList());
            terrain.Flood(districts.SelectMany(d => d.Pools(terrain)).ToArray());

            var buildings = districts.SelectMany(d => d.Buildings(terrain)).ToList();
            var ground = new BuildingGround(terrain, buildings, districts.SelectMany(d => d.Walls(terrain)).ToList());
            var physics = new PhysicsWorld(ground);

            var all = districts.ToArray();
            bool Bare(float x, float z)
            {
                foreach (var district in all)
                    if (district.Bare(x, z))
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
                Things = districts.SelectMany(d => d.Things(physics, terrain)).ToList(),
                Starts = starts,
                Bare = Bare,
            };
        }
    }
}
