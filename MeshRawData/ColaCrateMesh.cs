using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData
{
    // A big shipping crate, 3.6 wide, 2.0 deep and 2.4 tall, with COLA stencilled across its front (+Z) face
    // in letters a metre high. Built with its foot on y = 0, centred on X and Z.
    // The letters are made of separate strokes with small gaps between them, the way a stencil leaves them.
    public static class ColaCrateMesh
    {
        // The crate has three shades (see MeshBuilder); the paint is one colour.
        public const int CrateBase = 0, Paint = 3;
        public const int PaletteSize = 4;

        public static Color[] Palette(Color wood, Color paint)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, CrateBase, wood);
            palette[Paint] = paint;
            return palette;
        }

        private const float Width = 3.6f, Depth = 2.0f, Height = 2.4f;
        private const float LetterBottom = 0.7f;   // height of the foot of the letters above the floor
        private const float LetterAdvance = 0.8f;  // from the start of one letter to the next
        private const float PaintLift = 0.004f, EdgeLift = 0.008f;

        // Strokes as (x0, y0, x1, y1) in a letter cell 0.6 wide and 1.0 high. Neighbouring strokes stop 0.04 short of each other.
        private static readonly float[][][] Letters =
        {
            // C
            new[] { new[] { 0f, 0.84f, 0.6f, 1f }, new[] { 0f, 0f, 0.6f, 0.16f }, new[] { 0f, 0.20f, 0.16f, 0.80f } },
            // O
            new[] { new[] { 0.04f, 0.84f, 0.56f, 1f }, new[] { 0.04f, 0f, 0.56f, 0.16f },
                    new[] { 0f, 0.20f, 0.16f, 0.80f }, new[] { 0.44f, 0.20f, 0.6f, 0.80f } },
            // L
            new[] { new[] { 0f, 0.20f, 0.16f, 1f }, new[] { 0f, 0f, 0.6f, 0.16f } },
            // A
            new[] { new[] { 0f, 0f, 0.16f, 0.80f }, new[] { 0.44f, 0f, 0.6f, 0.80f },
                    new[] { 0.04f, 0.84f, 0.56f, 1f }, new[] { 0.20f, 0.40f, 0.40f, 0.56f } },
        };

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(CrateBase, Vector3.Zero, Depth, Width, Height);

            var strokeCount = 0;
            foreach (var letter in Letters)
                strokeCount += letter.Length;

            // Text runs left to right as seen from in front of the crate, which is +X
            var left = -(Letters.Length * LetterAdvance - (LetterAdvance - 0.6f)) / 2f;
            Vector3 Point(float cursor, float x, float y, float lift) => new Vector3(left + cursor + x, LetterBottom + y, Depth / 2f + lift);

            mesh.AddSolidRange(strokeCount * 2, Paint);
            for (var i = 0; i < Letters.Length; i++)
                foreach (var s in Letters[i])
                    mesh.AddQuad(Point(i * LetterAdvance, s[0], s[1], PaintLift), Point(i * LetterAdvance, s[2], s[1], PaintLift),
                                 Point(i * LetterAdvance, s[2], s[3], PaintLift), Point(i * LetterAdvance, s[0], s[3], PaintLift));

            // Faces vanish in wireframe, so each stroke is outlined too
            for (var i = 0; i < Letters.Length; i++)
                foreach (var s in Letters[i])
                    mesh.AddLineLoop(Point(i * LetterAdvance, s[0], s[1], EdgeLift), Point(i * LetterAdvance, s[2], s[1], EdgeLift),
                                     Point(i * LetterAdvance, s[2], s[3], EdgeLift), Point(i * LetterAdvance, s[0], s[3], EdgeLift));

            return mesh.Build(device);
        }
    }
}
