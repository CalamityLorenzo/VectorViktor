using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using World.Core;
using World.Core.Physics;
using static Basic.World.Scenery;

namespace Basic.World
{
    // The country round the middle (see TerrainGenerator): the hills, the plateau and its causeway, the basin's
    // lake and the pond. Its ground is the terrain's own; what it adds are the things lying about to push, stack,
    // climb, drop and knock over. Most are in view from the start, ahead of you to the north-east; two more wait
    // near the plateau's sheer south edge, to be pushed off it, and the COLA crate and an armchair stand on top of
    // it. Two float on the pond.
    public sealed class Countryside : IDistrict
    {
        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["hills"] = new(Vector2.Zero, MathHelper.PiOver4),                                // looking north-east to the plateau
            ["plateau"] = new(TerrainGenerator.PlateauCentre, MathHelper.Pi),                 // on top, facing its sheer south side
            ["causeway"] = new(new Vector2(TerrainGenerator.PlateauCentre.X - TerrainGenerator.PlateauRadius - TerrainGenerator.RampLength,
                                           TerrainGenerator.PlateauCentre.Y), MathHelper.PiOver2),   // at its foot, facing up it
            ["basin"] = new(TerrainGenerator.BasinCentre, MathHelper.PiOver4),
            ["lockers"] = new(new Vector2(-3f, -3f), MathHelper.PiOver2),                   // facing the first locker, to push it over
            ["far"] = new(new Vector2(300f, 300f), -MathHelper.PiOver4),                      // out in the far country, looking back towards home
            ["pond"] = new(TerrainGenerator.PondCentre + new Vector2(TerrainGenerator.PondRadius + 3f, 0f), -MathHelper.PiOver2),   // east of it, facing it
        };

        public IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain)
        {
            Thing Place(string name, CrateKind kind, Vector3 size, float mass, float x, float z, float above = 0f) =>
                Crate(world, terrain, name, kind, size, mass, x, z, above);

            var cardboard = new Vector3(0.5f, 0.5f, 0.5f);
            yield return Place("box 1", CrateKind.Cardboard, cardboard, 4f, 2.0f, -3.0f);
            yield return Place("box 2", CrateKind.Cardboard, cardboard, 4f, 2.7f, -3.2f);
            yield return Place("box 3", CrateKind.Cardboard, cardboard, 4f, 2.3f, -2.3f);

            // A stack of three wooden crates, each light enough to push at a walk
            var crate = new Vector3(0.8f, 0.8f, 0.8f);
            yield return Place("crate 1", CrateKind.Wood, crate, 25f, 5f, -4f);
            yield return Place("crate 2", CrateKind.Wood, crate, 25f, 5f, -4f, above: 1f);
            yield return Place("crate 3", CrateKind.Wood, crate, 25f, 5f, -4f, above: 2f);

            // Two tall lockers side by side: push one and it goes over, into the other if it's in the way
            var locker = new Vector3(0.5f, 1.8f, 0.5f);
            yield return Place("locker 1", CrateKind.Steel, locker, 30f, -1f, -3f);
            yield return Place("locker 2", CrateKind.Steel, locker, 30f, -1f, -2.2f);

            // A tower of cardboard boxes, a little askew, to knock down
            yield return Place("tower 1", CrateKind.Cardboard, cardboard, 4f, 7.5f, -1f);
            yield return Place("tower 2", CrateKind.Cardboard, cardboard, 4f, 7.55f, -1.03f, above: 0.7f);
            yield return Place("tower 3", CrateKind.Cardboard, cardboard, 4f, 7.48f, -0.98f, above: 1.4f);
            yield return Place("tower 4", CrateKind.Cardboard, cardboard, 4f, 7.52f, -1.02f, above: 2.1f);

            // A heavy one: you can shift it, slowly
            yield return Place("big crate", CrateKind.Wood, new Vector3(1.0f, 0.8f, 1.0f), 60f, -2f, -5f);

            // A pallet low enough to step up onto
            yield return Place("pallet", CrateKind.Wood, new Vector3(1.2f, 0.15f, 1.0f), 20f, 0.5f, -7f);

            // Steel: too heavy to move at all, but you can jump onto it (it's under a metre) and stand on it
            yield return Place("steel crate", CrateKind.Steel, new Vector3(1.2f, 0.9f, 1.2f), 300f, 4f, -8f);

            // On the plateau, near its sheer south edge
            var plateau = TerrainGenerator.PlateauCentre;
            var south = plateau.Y + TerrainGenerator.PlateauRadius;
            yield return Place("edge crate 1", CrateKind.Wood, crate, 25f, plateau.X, south - 2.5f);
            yield return Place("edge crate 2", CrateKind.Cardboard, cardboard, 4f, plateau.X + 1.5f, south - 3f);

            // Afloat on the pond (see PhysicsWorld): a cardboard box riding high, and a heavy wooden crate, about
            // half under - too heavy to budge on land, but afloat, there's nothing to grip, so you can push it
            var pond = TerrainGenerator.PondCentre;
            yield return Place("pond box", CrateKind.Cardboard, cardboard, 4f, pond.X + 1.5f, pond.Y - 1f);
            yield return Place("pond crate", CrateKind.Wood, crate, 300f, pond.X - 1f, pond.Y + 1.5f);

            // Also on the plateau, a few metres south of its middle, turned to face you as you come up
            // onto it or stand in the middle: the COLA crate (3.6 wide, 2.0 deep, 2.4 tall - far too heavy
            // to shift) and, beside it, an armchair light enough to push about and climb onto.
            yield return Prop(world, terrain, "cola crate", new MeshSource("colacrate", ColaCrateMesh.Build,
                ColaCrateMesh.Palette(new Color(170, 120, 60), new Color(240, 240, 240))),
                new Vector3(3.6f, 2.4f, 2.0f), 1000f, plateau.X - 2f, plateau.Y + 6f, turn: MathHelper.Pi);
            yield return Prop(world, terrain, "chair", new MeshSource("armchair", ArmchairMesh.Build,
                ArmchairMesh.Palette(new Color(120, 80, 50), new Color(160, 120, 80), new Color(60, 40, 25))),
                new Vector3(0.75f, 0.8f, 0.75f), 12f, plateau.X + 2f, plateau.Y + 5f, turn: MathHelper.Pi);
        }
    }
}
