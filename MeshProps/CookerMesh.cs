using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A 1950s enamel gas cooker: a squarish body on a dark plinth, four burner rings on the hob, two big and two small,
    // a row of chrome knobs along the front under the hob, the oven door below with a dark glass window and a chrome
    // bar handle, and a low splashback along the back of the hob. 0.55 wide, 0.60 deep, 0.90 to the hob (1.05 to the
    // top of the splashback). The oven door faces +Z, centred on X and Z, standing on y = 0.
    public static class CookerMesh
    {
        // The enamel has three shades (see MeshBuilder); the rings and plinth one, the chrome one, the glass one.
        public const int EnamelBase = 0, Ring = 3, Chrome = 4, Glass = 5;
        public const int PaletteSize = 6;

        public const float Height = 0.90f;

        public static Color[] Palette(Color enamel, Color ring, Color chrome, Color glass)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, EnamelBase, enamel);
            palette[Ring] = ring;
            palette[Chrome] = chrome;
            palette[Glass] = glass;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float width = 0.55f, depth = 0.60f, plinth = 0.06f;
            const float front = depth / 2f;
            var mesh = new MeshBuilder();

            // The plinth, set back a little under the body, and the body on it
            mesh.AddBox(EnamelBase, new Vector3(0f, plinth, 0f), depth, width, Height - plinth, sealBottom: true);
            AddBoxIn(mesh, Ring, new Vector3(0f, 0f, -0.02f), depth - 0.06f, width - 0.04f, plinth);

            // The burners: a flat disc and a cap on each, two big at the front left and back right, two small
            foreach (var (x, z, radius) in new[] { (-0.13f, 0.12f, 0.08f), (0.13f, -0.12f, 0.08f), (0.13f, 0.12f, 0.06f), (-0.13f, -0.12f, 0.06f) })
            {
                mesh.AddFrustum(new Vector3(x, Height, z), radius, radius, 0.012f, 10, Ring, topSlot: Ring);
                mesh.AddFrustum(new Vector3(x, Height + 0.012f, z), radius * 0.35f, radius * 0.3f, 0.01f, 8, Chrome, topSlot: Chrome);
            }

            // The splashback along the back of the hob
            mesh.AddBox(EnamelBase, new Vector3(0f, Height, -depth / 2f + 0.015f), 0.03f, width, 0.15f, sealBottom: true);

            // The knobs along the front under the hob, and the oven door below them, proud of the body
            for (var k = 0; k < 4; k++)
                mesh.AddTube(new Vector3(-0.18f + k * 0.12f, Height - 0.07f, front), new Vector3(-0.18f + k * 0.12f, Height - 0.07f, front + 0.025f),
                    0.018f, 0.015f, 8, Chrome, ringEdges: true);
            const float doorBottom = plinth + 0.04f, doorTop = Height - 0.14f;
            mesh.AddBox(EnamelBase, new Vector3(0f, doorBottom, front + 0.008f), 0.016f, width - 0.06f, doorTop - doorBottom);
            var glass = new[]
            {
                new Vector3(-0.15f, doorBottom + 0.20f, front + 0.0175f), new Vector3(0.15f, doorBottom + 0.20f, front + 0.0175f),
                new Vector3(0.15f, doorBottom + 0.42f, front + 0.0175f), new Vector3(-0.15f, doorBottom + 0.42f, front + 0.0175f),
            };
            mesh.AddPolygon(Glass, glass);
            mesh.AddLineLoop(glass);
            mesh.AddTube(new Vector3(-0.18f, doorTop - 0.05f, front + 0.04f), new Vector3(0.18f, doorTop - 0.05f, front + 0.04f), 0.01f, 0.01f, 6, Chrome);
            foreach (var x in new[] { -0.16f, 0.16f })
                mesh.AddTube(new Vector3(x, doorTop - 0.05f, front + 0.016f), new Vector3(x, doorTop - 0.05f, front + 0.04f), 0.008f, 0.008f, 4, Chrome);

            return mesh.Build(device);
        }

        // A box in one colour, its edges and all: for the plinth, which has no shades of its own
        private static void AddBoxIn(MeshBuilder mesh, int slot, Vector3 bottomCentre, float length, float width, float height)
        {
            var (hf, hr, hu) = (Vector3.UnitZ * (length / 2f), Vector3.UnitX * (width / 2f), Vector3.Up * height);
            var (a, b, c, d) = (bottomCentre - hf - hr, bottomCentre + hf - hr, bottomCentre + hf + hr, bottomCentre - hf + hr);
            mesh.AddQuad(slot, a, b, b + hu, a + hu);
            mesh.AddQuad(slot, c, d, d + hu, c + hu);
            mesh.AddQuad(slot, b, c, c + hu, b + hu);
            mesh.AddQuad(slot, d, a, a + hu, d + hu);
            mesh.AddLineLoop(a + hu, b + hu, c + hu, d + hu);
            mesh.AddLine(a, a + hu); mesh.AddLine(b, b + hu); mesh.AddLine(c, c + hu); mesh.AddLine(d, d + hu);
        }
    }
}
