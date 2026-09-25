using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace MeshRawData
{
    // A free-standing sign, 4.5 wide and 2.4 tall, that reads 12939 from the front (+Z). Its number is
    // made of separate strokes floating in an open frame, with no board behind them, so it can be seen
    // from both sides. From behind it is the same strokes mirrored, and the digits are drawn so that
    // mirrored, and in the opposite order, they read PEPSI: 9 becomes P, 3 becomes E, 2 becomes S
    // and 1 becomes I. (After the number on a crate in the 1985 game Mercenary.)
    public static class NumberSignMesh
    {
        // The frame has three shades (see MeshBuilder); the strokes are one colour.
        public const int FrameBase = 0, Paint = 3;
        public const int PaletteSize = 4;

        public static Color[] Palette(Color frame, Color paint)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, FrameBase, frame);
            palette[Paint] = paint;
            return palette;
        }

        private const string Number = "12939";

        private const float PostWidth = 0.15f, PostDepth = 0.15f, Height = 2.4f;
        private const float RailHeight = 0.15f, BottomRail = 0.5f;   // the frame's rails: height above the floor of the lower one
        private const float NumberBottom = 0.95f;                     // height of the foot of the digits above the floor
        private const float Advance = 0.8f;                           // from the start of one digit to the next, digits being 0.6 wide

        // Strokes are (x0, y0, x1, y1) in a cell 0.6 wide and 1.0 high. The seven segments of a calculator
        // digit, with a little gap between them where they would touch, as a stencil leaves them.
        private static readonly Dictionary<char, float[]> Segments = new Dictionary<char, float[]>
        {
            ['a'] = new[] { 0f, 0.86f, 0.6f, 1f },        // top
            ['g'] = new[] { 0f, 0.43f, 0.6f, 0.57f },     // middle
            ['d'] = new[] { 0f, 0f, 0.6f, 0.14f },        // bottom
            ['f'] = new[] { 0f, 0.61f, 0.14f, 0.82f },    // upper left
            ['b'] = new[] { 0.46f, 0.61f, 0.6f, 0.82f },  // upper right
            ['e'] = new[] { 0f, 0.18f, 0.14f, 0.39f },    // lower left
            ['c'] = new[] { 0.46f, 0.18f, 0.6f, 0.39f },  // lower right
            ['|'] = new[] { 0.23f, 0f, 0.37f, 1f },       // the stem of a 1, which is the same mirrored
        };

        // Which segments make each digit. The 9 has no bottom bar, so that mirrored it is a P.
        private static readonly Dictionary<char, string> Digits = new Dictionary<char, string>
        {
            ['1'] = "|",
            ['2'] = "abged",
            ['3'] = "abgcd",
            ['9'] = "abfgc",
        };

        public static MeshData Build(GraphicsDevice device)
        {
            const float width = 4.5f;
            var mesh = new MeshBuilder();

            // Open frame: two posts and a rail top and bottom
            var postX = (width - PostWidth) / 2f;
            mesh.AddBox(FrameBase, new Vector3(-postX, 0f, 0f), PostDepth, PostWidth, Height);
            mesh.AddBox(FrameBase, new Vector3(postX, 0f, 0f), PostDepth, PostWidth, Height);
            var railWidth = width - 2f * PostWidth;
            mesh.AddBox(FrameBase, new Vector3(0f, BottomRail, 0f), PostDepth, railWidth, RailHeight);
            mesh.AddBox(FrameBase, new Vector3(0f, Height - RailHeight, 0f), PostDepth, railWidth, RailHeight);

            var strokes = new List<float[]>();   // x0, y0, x1, y1, in the sign's own coordinates
            var left = -(Number.Length * Advance - (Advance - 0.6f)) / 2f;
            for (var i = 0; i < Number.Length; i++)
                foreach (var segment in Digits[Number[i]])
                {
                    var s = Segments[segment];
                    var x = left + i * Advance;
                    strokes.Add(new[] { x + s[0], NumberBottom + s[1], x + s[2], NumberBottom + s[3] });
                }

            // All in the one plane, z = 0, with nothing in front of or behind them to hide them from either side
            foreach (var s in strokes)
                mesh.AddQuad(Paint, new Vector3(s[0], s[1], 0f), new Vector3(s[2], s[1], 0f), new Vector3(s[2], s[3], 0f), new Vector3(s[0], s[3], 0f));
            foreach (var s in strokes)
                mesh.AddLineLoop(new Vector3(s[0], s[1], 0f), new Vector3(s[2], s[1], 0f), new Vector3(s[2], s[3], 0f), new Vector3(s[0], s[3], 0f));

            return mesh.Build(device);
        }
    }
}
