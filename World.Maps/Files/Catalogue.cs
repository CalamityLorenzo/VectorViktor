using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace World.Maps.Files
{
    // The standard catalogue: what any map file can put about by name (see PropEntry, ThingEntry). Names are a kind and
    // then, after a dot, which one ("crate.wood"), so similar things sort together. A map library can add its own (see
    // MapLibrary.Add). The masses are guesses at what each would weigh, and decide what you can push (see Player):
    // about 60 kg is as much as you can shift, slowly.
    public static class Catalogue
    {
        private static MeshSource Crate(CrateKind kind, float x, float y, float z) => CrateMesh.Source(kind, new Vector3(x, y, z));

        public static IEnumerable<CatalogueItem> Standard() => new[]
        {
            new CatalogueItem("crate.cardboard", Crate(CrateKind.Cardboard, 0.5f, 0.5f, 0.5f), 4f, About: "a cardboard box, half a metre each way"),
            new CatalogueItem("crate.wood", Crate(CrateKind.Wood, 0.8f, 0.8f, 0.8f), 25f, About: "a wooden crate, light enough to push at a walk"),
            new CatalogueItem("crate.wood.big", Crate(CrateKind.Wood, 1f, 0.8f, 1f), 60f, About: "a big wooden crate: you can shift it, slowly"),
            new CatalogueItem("crate.steel", Crate(CrateKind.Steel, 1.2f, 0.9f, 1.2f), 300f, About: "a steel crate, too heavy to move, low enough to jump onto"),
            new CatalogueItem("crate.locker", Crate(CrateKind.Steel, 0.5f, 1.8f, 0.5f), 30f, About: "a tall steel locker, easily pushed over"),
            new CatalogueItem("crate.pallet", Crate(CrateKind.Wood, 1.2f, 0.15f, 1f), 20f, About: "a pallet, low enough to step up onto"),

            new CatalogueItem("furniture.armchair", new MeshSource("armchair", ArmchairMesh.Build,
                ArmchairMesh.Palette(new Color(120, 80, 50), new Color(160, 120, 80), new Color(60, 40, 25))), 12f),
            new CatalogueItem("furniture.settee", new MeshSource("settee", SetteeMesh.Build,
                SetteeMesh.Palette(new Color(150, 50, 60), new Color(190, 80, 85), new Color(90, 60, 35))), 35f),
            new CatalogueItem("furniture.sofa", new MeshSource("sofa", SofaMesh.Build,
                SofaMesh.Palette(new Color(60, 125, 125), new Color(100, 170, 160), new Color(150, 100, 60))), 45f),
            new CatalogueItem("furniture.coffee-table", new MeshSource("coffeetable", CoffeeTableMesh.Build,
                CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40))), 8f),
            new CatalogueItem("furniture.sideboard", new MeshSource("sideboard", SideboardMesh.Build,
                SideboardMesh.Palette(new Color(130, 80, 45), new Color(170, 115, 65), new Color(90, 55, 30), new Color(205, 175, 90))), 40f),
            new CatalogueItem("furniture.television", new MeshSource("television", TelevisionMesh.Build,
                TelevisionMesh.Palette(new Color(110, 70, 40), new Color(120, 140, 130), new Color(235, 225, 200), new Color(60, 45, 35),
                                       new Color(90, 55, 30), new Color(190, 190, 195))), 25f),
            new CatalogueItem("furniture.cola-crate", new MeshSource("colacrate", ColaCrateMesh.Build,
                ColaCrateMesh.Palette(new Color(170, 120, 60), new Color(240, 240, 240))), 1000f, About: "the COLA crate: far too heavy to shift"),

            new CatalogueItem("plant.pot", new MeshSource("plantpot", PlantPotMesh.Build,
                PlantPotMesh.Palette(new Color(190, 95, 60), new Color(50, 130, 60))), 5f),
            new CatalogueItem("plant.fern", new MeshSource("fern", FernMesh.Build,
                FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60))), 5f),
            new CatalogueItem("plant.oak", new MeshSource("oak", OakMesh.Build,
                OakMesh.Palette(new Color(100, 70, 45), new Color(70, 145, 55))), 2000f, About: "an oak tree"),
            new CatalogueItem("plant.spiky-bush", new MeshSource("spikybush", SpikyBushMesh.Build,
                SpikyBushMesh.Palette(new Color(95, 65, 40), new Color(90, 130, 50))), 30f),

            new CatalogueItem("vehicle.trabant", new MeshSource("trabant", TrabantMesh.Build,
                TrabantMesh.Palette(new Color(150, 190, 200), new Color(220, 220, 210), new Color(40, 40, 40))), 600f, About: "a Trabant, to look at, not to drive"),
        };
    }
}
