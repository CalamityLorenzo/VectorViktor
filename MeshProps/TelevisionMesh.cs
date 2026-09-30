using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A 1950s television set: a deep walnut cabinet with a small, bulging (convex) screen on the left,
    // three round knobs and a speaker grille on the right, short splayed legs, and rabbit-ear aerials on
    // top. 0.50 wide, 0.45 to the top of the cabinet (0.74 to the aerial tips), 0.42 deep.
    // The screen faces +Z, centred on X and Z, standing on y = 0.
    public static class TelevisionMesh
    {
        // The cabinet has three shades (see MeshBuilder); the grille slats also have three; the rest one each.
        public const int CabinetBase = 0, Glass = 3, Knob = 4, GrilleBase = 5, Leg = 8, Metal = 9;
        public const int PaletteSize = 10;

        // The screen: a grid of ScreenCells x ScreenCells quads bulging out towards the middle, like a CRT (see ScreenPoint)
        public const float ScreenCentreX = -0.09f, ScreenCentreY = 0.29f, ScreenWidth = 0.30f, ScreenHeight = 0.26f, ScreenBulge = 0.03f;
        public const int ScreenCells = 4;
        private const float Depth = 0.42f;

        // A point on the screen's glass, `u` (-1 left to 1 right) across it and `v` (-1 bottom to 1 top) up it, standing
        // `proud` in front of it: for a picture laid over it (a ScreenView in World.Maps).
        public static Vector3 ScreenPoint(float u, float v, float proud = 0f) => new Vector3(
            ScreenCentreX + u * ScreenWidth / 2f,
            ScreenCentreY + v * ScreenHeight / 2f,
            Depth / 2f + 0.002f + ScreenBulge * (1f - u * u) * (1f - v * v) + proud);

        public static Color[] Palette(Color cabinet, Color glass, Color knob, Color grille, Color leg, Color metal)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, CabinetBase, cabinet);
            palette[Glass] = glass;
            palette[Knob] = knob;
            MeshBuilder.SetBoxShades(palette, GrilleBase, grille);
            palette[Leg] = leg;
            palette[Metal] = metal;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float width = 0.50f, depth = Depth, cabinetHeight = 0.40f;
            const float legHeight = 0.05f;
            var top = legHeight + cabinetHeight;
            var frontZ = depth / 2f;

            var mesh = new MeshBuilder();

            // Cabinet, sealed underneath
            mesh.AddBox(CabinetBase, new Vector3(0f, legHeight, 0f), depth, width, cabinetHeight, sealBottom: true);

            // Screen: a 4 x 4 grid of quads that bulges out towards the middle, like a CRT. Its border sits
            // 0.002 proud of the cabinet's front so it doesn't z-fight with it. Only its outline is edged.
            const int cells = ScreenCells;
            Vector3 Point(int i, int j) => ScreenPoint(-1f + 2f * i / cells, -1f + 2f * j / cells);
            for (var i = 0; i < cells; i++)
                for (var j = 0; j < cells; j++)
                    mesh.AddQuad(Glass, Point(i, j), Point(i + 1, j), Point(i + 1, j + 1), Point(i, j + 1));

            var outline = new System.Collections.Generic.List<Vector3>();
            for (var i = 0; i < cells; i++) outline.Add(Point(i, 0));            // bottom, left to right
            for (var j = 0; j < cells; j++) outline.Add(Point(cells, j));        // right, bottom to top
            for (var i = cells; i > 0; i--) outline.Add(Point(i, cells));        // top, right to left
            for (var j = cells; j > 0; j--) outline.Add(Point(0, j));            // left, top to bottom
            mesh.AddLineLoop(outline.ToArray());

            // Three knobs stacked on the right, and a speaker grille of four slats under them
            const float controlsX = 0.17f;
            foreach (var y in new[] { 0.41f, 0.33f, 0.25f })
                mesh.AddTube(new Vector3(controlsX, y, frontZ), new Vector3(controlsX, y, frontZ + 0.025f), 0.024f, 0.020f, 8, Knob, ringEdges: true);
            for (var slat = 0; slat < 4; slat++)
                mesh.AddBox(GrilleBase, new Vector3(controlsX, 0.10f + slat * 0.025f, frontZ + 0.003f), 0.006f, 0.13f, 0.008f);

            // Splayed legs
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    mesh.AddTube(
                        new Vector3(sx * 0.21f, legHeight, sz * 0.15f),
                        new Vector3(sx * 0.24f, 0f, sz * 0.18f),
                        0.022f, 0.015f, 4, Leg);

            // Rabbit ears: a small round base on top, and two thin rods splayed out from it
            var earBase = new Vector3(0f, top, -0.08f);
            mesh.AddFrustum(earBase, 0.035f, 0.025f, 0.03f, 8, Metal, topSlot: Metal, verticalEdges: true);
            var earTop = earBase + new Vector3(0f, 0.03f, 0f);
            mesh.AddTube(earTop, earTop + new Vector3(-0.22f, 0.26f, 0f), 0.007f, 0.005f, 3, Metal);
            mesh.AddTube(earTop, earTop + new Vector3(0.22f, 0.26f, 0f), 0.007f, 0.005f, 3, Metal);

            return mesh.Build(device);
        }
    }
}
