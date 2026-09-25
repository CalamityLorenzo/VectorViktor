using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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

        // What's built into it and never moves: roads, fences, paving, signs.
        IEnumerable<Fixture> Fixtures(Terrain terrain) => Array.Empty<Fixture>();

        // Things lying about, to push, stack and knock over: each a body added to the world.
        IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain) => Array.Empty<Thing>();

        // Named places to start (see Program), each unique across all the districts.
        IReadOnlyDictionary<string, Start> Starts => new Dictionary<string, Start>();

        // Where the terrain gets no grid lines, because something's laid over it (see TerrainMesh).
        bool Bare(float x, float z) => false;
    }

    // Where to start, and which way to face. With Above, you're dropped from that far above the ground and land
    // on the highest floor below that: to start up in a building rather than on the ground under it.
    public readonly record struct Start(Vector2 At, float Yaw, float Above = 0f);

    // Something built into the world: a mesh (built once per key), how it's coloured, and where it goes.
    public readonly record struct Fixture(string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette, Matrix Transform);

    // A body and how it's drawn: the mesh (built once per key), its palette, and how far it's turned about the
    // vertical inside its box - only by a half turn, so the box it fills is the same.
    public record Thing(Body Body, string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette, float Turn = 0f);
}
