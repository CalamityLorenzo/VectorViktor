using MeshCore.Library;
using MeshRawData;
using MeshRawData.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core;
using World.Core.Physics;

namespace Basic.World
{
    // The things lying about the world to push, stack, climb, drop and knock over, each with what it's
    // made of (for how it's drawn). Most are in view from the start, ahead of you to the north-east; two
    // more wait near the plateau's sheer south edge, to be pushed off it, and the COLA crate and an
    // armchair stand on top of it.
    public static class Scenery
    {
        // A body and how it's drawn: the mesh (built once per key), its palette, and how far it's turned
        // about the vertical inside its box - only by a half turn, so the box it fills is the same.
        public record Thing(Body Body, string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette, float Turn = 0f);

        public static List<Thing> Populate(PhysicsWorld world, Terrain terrain)
        {
            var things = new List<Thing>();

            // Dropped from just above the ground (or the thing under it); the world settles them.
            Body Drop(string name, Vector3 size, float mass, float x, float z, float above) =>
                world.Add(new Body(name, size, mass, new Vector3(x, terrain.HeightAt(x, z) + above + 0.3f, z)));

            void Place(string name, CrateKind kind, Vector3 size, float mass, float x, float z, float above = 0f) =>
                things.Add(new Thing(Drop(name, size, mass, x, z, above), CrateMesh.Key(kind, size),
                    d => CrateMesh.Build(d, kind, size), CrateMesh.Palette(kind)));

            // Something from MeshRawData, in a box that fits round it (its foot on y = 0, centred, like a Body)
            void PlaceProp(string name, string key, Func<GraphicsDevice, MeshData> build, Color[] palette,
                           Vector3 size, float mass, float x, float z, float turn = 0f) =>
                things.Add(new Thing(Drop(name, size, mass, x, z, 0f), key, build, palette, turn));

            var cardboard = new Vector3(0.5f, 0.5f, 0.5f);
            Place("box 1", CrateKind.Cardboard, cardboard, 4f, 2.0f, -3.0f);
            Place("box 2", CrateKind.Cardboard, cardboard, 4f, 2.7f, -3.2f);
            Place("box 3", CrateKind.Cardboard, cardboard, 4f, 2.3f, -2.3f);

            // A stack of three wooden crates, each light enough to push at a walk
            var crate = new Vector3(0.8f, 0.8f, 0.8f);
            Place("crate 1", CrateKind.Wood, crate, 25f, 5f, -4f);
            Place("crate 2", CrateKind.Wood, crate, 25f, 5f, -4f, above: 1f);
            Place("crate 3", CrateKind.Wood, crate, 25f, 5f, -4f, above: 2f);

            // Two tall lockers side by side: push one and it goes over, into the other if it's in the way
            var locker = new Vector3(0.5f, 1.8f, 0.5f);
            Place("locker 1", CrateKind.Steel, locker, 30f, -1f, -3f);
            Place("locker 2", CrateKind.Steel, locker, 30f, -1f, -2.2f);

            // A tower of cardboard boxes, a little askew, to knock down
            Place("tower 1", CrateKind.Cardboard, cardboard, 4f, 7.5f, -1f);
            Place("tower 2", CrateKind.Cardboard, cardboard, 4f, 7.55f, -1.03f, above: 0.7f);
            Place("tower 3", CrateKind.Cardboard, cardboard, 4f, 7.48f, -0.98f, above: 1.4f);
            Place("tower 4", CrateKind.Cardboard, cardboard, 4f, 7.52f, -1.02f, above: 2.1f);

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

            // Also on the plateau, a few metres south of its middle, turned to face you as you come up
            // onto it or stand in the middle: the COLA crate (3.6 wide, 2.0 deep, 2.4 tall - far too heavy
            // to shift) and, beside it, an armchair light enough to push about and climb onto.
            PlaceProp("cola crate", "colacrate", ColaCrateMesh.Build,
                ColaCrateMesh.Palette(new Color(170, 120, 60), new Color(240, 240, 240)),
                new Vector3(3.6f, 2.4f, 2.0f), 1000f, plateau.X - 2f, plateau.Y + 6f, turn: MathHelper.Pi);
            PlaceProp("chair", "armchair",
                d => SeatingBuilder.Build(d, seats: 1, armWidth: 0.10f, armRise: 0.12f, legHeight: 0.16f, backCushionHeight: 0.40f),
                SeatingBuilder.Palette(new Color(120, 80, 50), new Color(160, 120, 80), new Color(60, 40, 25)),
                new Vector3(0.75f, 0.8f, 0.75f), 12f, plateau.X + 2f, plateau.Y + 5f, turn: MathHelper.Pi);

            return things;
        }
    }
}
