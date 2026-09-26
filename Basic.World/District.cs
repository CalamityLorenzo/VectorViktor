using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Core.Physics;

namespace Basic.World
{
    // A part of the world - the country round the middle, the town, the street - and everything it puts in
    // it. WorldBuilder asks each district in turn, in the order they're listed, so adding another one is a
    // matter of writing it and adding it to the list, not of threading its pieces through the game.
    //
    // Everything has a default of nothing, so a district only says what it has.
    public interface IDistrict
    {
        // Ground to level for it (see TerrainGenerator.Pad), before the terrain's made. Later pads, this
        // district's or a later one's, are levelled over earlier ones.
        IEnumerable<TerrainGenerator.Pad> Pads => Array.Empty<TerrainGenerator.Pad>();

        // Water of its own, on top of the terrain's lakes and ponds.
        IEnumerable<Pool> Pools(Terrain terrain) => Array.Empty<Pool>();

        IEnumerable<Building> Buildings(Terrain terrain) => Array.Empty<Building>();

        // Free-standing walls to walk into: fences, posts.
        IEnumerable<WallSegment> Walls(Terrain terrain) => Array.Empty<WallSegment>();

        // Doors that take you somewhere else when you walk into them (see Portal).
        IEnumerable<Portal> Portals(Terrain terrain) => Array.Empty<Portal>();

        // What's built into it and never moves: roads, fences, paving, signs.
        IEnumerable<Fixture> Fixtures(Terrain terrain) => Array.Empty<Fixture>();

        // Windows that look onto somewhere else (see Window).
        IEnumerable<Window> Windows(Terrain terrain) => Array.Empty<Window>();

        // What's drawn that moves by itself, never touched (something going round), placed in the world.
        IEnumerable<ScenePart> Moving(Terrain terrain) => Array.Empty<ScenePart>();

        // Things lying about, to push, stack and knock over: each a body added to the world.
        IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain) => Array.Empty<Thing>();

        // Named places to start (see Program), each unique across all the districts.
        IReadOnlyDictionary<string, Start> Starts => new Dictionary<string, Start>();

        // Where the terrain gets no grid lines, because something's laid over it (see TerrainMesh).
        bool Bare(float x, float z) => false;
    }
}
