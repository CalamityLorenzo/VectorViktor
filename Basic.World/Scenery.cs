using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using World.Core;
using World.Core.Physics;

namespace Basic.World
{
    // Making the things that lie about the world (see Thing): each a body, dropped from just above the ground
    // (or the thing under it) for the world to settle, and what it's made of, for how it's drawn.
    public static class Scenery
    {
        private static Body Drop(PhysicsWorld world, Terrain terrain, string name, Vector3 size, float mass, float x, float z, float above) =>
            world.Add(new Body(name, size, mass, new Vector3(x, terrain.HeightAt(x, z) + above + 0.3f, z)));

        // A crate of any size, dressed as what it's made of (see CrateMesh). `above` stacks it on another.
        public static Thing Crate(PhysicsWorld world, Terrain terrain, string name, CrateKind kind, Vector3 size, float mass,
                                  float x, float z, float above = 0f) =>
            new Thing(Drop(world, terrain, name, size, mass, x, z, above), CrateMesh.Source(kind, size));

        // Something from MeshProps, in a box that fits round it (its foot on y = 0, centred, like a Body).
        public static Thing Prop(PhysicsWorld world, Terrain terrain, string name, MeshSource mesh,
                                 Vector3 size, float mass, float x, float z, float turn = 0f) =>
            new Thing(Drop(world, terrain, name, size, mass, x, z, 0f), mesh, turn);
    }
}
