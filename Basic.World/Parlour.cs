using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;

namespace Basic.World
{
    // A parlour, as if it were behind the old cottage's window, inside it: its floor a step up from the ground so
    // you look down into it, over the sill, rather than across at its ceiling. A settee against the back wall facing
    // the window, a coffee table in front of it, an armchair turned to it, and a pot plant in the corner.
    public static class Parlour
    {
        private const float Step = 0.3f;   // the floor above the ground

        public static ScenePart[] Parts()
        {
            var room = Matrix.CreateTranslation(0f, Step, 0f);
            Matrix In(float x, float z, float facing) => Matrix.CreateRotationY(facing) * Matrix.CreateTranslation(x, 0f, z) * room;
            const float back = ParlourMesh.Depth;
            var towardsWindow = MathHelper.Pi;   // the furniture faces +Z

            return new[]
            {
                ScenePart.Still(new MeshSource("parlour", ParlourMesh.Build,
                    ParlourMesh.Palette(new Color(150, 100, 60), new Color(200, 185, 140), new Color(240, 235, 220), new Color(110, 160, 110), new Color(120, 80, 40))),
                    room),
                ScenePart.Still(new MeshSource("settee", SetteeMesh.Build,
                    SetteeMesh.Palette(new Color(150, 50, 60), new Color(190, 80, 85), new Color(90, 60, 35))),
                    In(0f, back - 0.45f, towardsWindow)),
                ScenePart.Still(new MeshSource("coffeetable", CoffeeTableMesh.Build,
                    CoffeeTableMesh.Palette(new Color(200, 150, 80), new Color(120, 80, 40))),
                    In(0f, back - 1.45f, 0f)),
                ScenePart.Still(new MeshSource("armchair", ArmchairMesh.Build,
                    ArmchairMesh.Palette(new Color(70, 110, 140), new Color(110, 150, 175), new Color(90, 60, 35))),
                    In(1.45f, back - 1.3f, MathF.Atan2(-1f, -0.5f))),   // looking across at the table, and a little towards the window
                ScenePart.Still(new MeshSource("plantpot", PlantPotMesh.Build,
                    PlantPotMesh.Palette(new Color(190, 95, 60), new Color(50, 130, 60))),
                    In(-1.7f, back - 0.45f, 0.3f)),
            };
        }
    }
}
