using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Core;
using World.Core.Physics;

namespace Basic.World
{
    // The things lying about the world to push, stack, climb and drop, each with what it's made of (for
    // how it's drawn). Most are in view from the start, ahead of you to the north-east; two wooden crates
    // wait near the plateau's sheer south edge, to be pushed off it.
    public static class Scenery
    {
        public static List<(Body body, CrateKind kind)> Populate(PhysicsWorld world, Terrain terrain)
        {
            var things = new List<(Body, CrateKind)>();

            // Dropped from just above the ground (or the thing under it); the world settles them.
            void Place(string name, CrateKind kind, Vector3 size, float mass, float x, float z, float above = 0f)
            {
                var ground = terrain.HeightAt(x, z);
                things.Add((world.Add(new Body(name, size, mass, new Vector3(x, ground + above + 0.3f, z))), kind));
            }

            var cardboard = new Vector3(0.5f, 0.5f, 0.5f);
            Place("box 1", CrateKind.Cardboard, cardboard, 4f, 2.0f, -3.0f);
            Place("box 2", CrateKind.Cardboard, cardboard, 4f, 2.7f, -3.2f);
            Place("box 3", CrateKind.Cardboard, cardboard, 4f, 2.3f, -2.3f);

            // A stack of three wooden crates, each light enough to push at a walk
            var crate = new Vector3(0.8f, 0.8f, 0.8f);
            Place("crate 1", CrateKind.Wood, crate, 25f, 5f, -4f);
            Place("crate 2", CrateKind.Wood, crate, 25f, 5f, -4f, above: 1f);
            Place("crate 3", CrateKind.Wood, crate, 25f, 5f, -4f, above: 2f);

            // A heavy one: you can shift it, slowly
            Place("big crate", CrateKind.Wood, new Vector3(1.0f, 0.8f, 1.0f), 60f, -2f, -5f);

            // A pallet low enough to step up onto
            Place("pallet", CrateKind.Wood, new Vector3(1.2f, 0.15f, 1.0f), 20f, 0.5f, -7f);

            // Steel: too heavy to move at all, but you can jump onto it (it's under a metre) and stand on it
            Place("steel crate", CrateKind.Steel, new Vector3(1.2f, 0.9f, 1.2f), 300f, 4f, -8f);

            // On the plateau, near its sheer south edge
            var plateau = TerrainGenerator.PlateauCentre;
            var south = plateau.Y + TerrainGenerator.PlateauRadius;
            Place("edge crate 1", CrateKind.Wood, crate, 25f, plateau.X, south - 2.5f);
            Place("edge crate 2", CrateKind.Cardboard, cardboard, 4f, plateau.X + 1.5f, south - 3f);

            return things;
        }
    }
}
