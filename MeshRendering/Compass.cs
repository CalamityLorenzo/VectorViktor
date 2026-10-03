using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRendering
{
    // Which way you're facing, as a heading tape across the top of the picture, the way a cockpit shows it: a strip
    // SpanDegrees wide sliding under a fixed pointer, a tick every 15 degrees, a longer one and its letters (N, NE, E...)
    // every 45, and under the pointer the heading in whole degrees (000 to 359, clockwise from north). All of it in
    // hard square pixels, `unit` screen pixels each, letters from a 3 x 5 font: drawn into the low-resolution picture,
    // it's scaled up with everything else.
    public sealed class Compass : IDisposable
    {
        public const int SpanDegrees = 120;   // across the strip, a pixel a degree
        private const int Half = SpanDegrees / 2;
        private const int StripHeight = 13;

        private readonly SpriteBatch _batch;
        private readonly Texture2D _pixel;

        public Compass(GraphicsDevice device)
        {
            _batch = new SpriteBatch(device);
            _pixel = new Texture2D(device, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        // The tape, its middle `centreX` across and its top `top` down whatever's being drawn to, for a heading of
        // `bearing` (radians clockwise from north): `ink` for its marks, on `paper`.
        public void Draw(float bearing, int centreX, int top, int unit, Color ink, Color paper)
        {
            var degrees = MathHelper.ToDegrees(bearing) % 360f;
            if (degrees < 0f)
                degrees += 360f;
            var heading = (int)MathF.Round(degrees) % 360;

            void Block(int x, int y, int w, int h, Color colour) =>
                _batch.Draw(_pixel, new Rectangle(centreX + x * unit, top + y * unit, w * unit, h * unit), colour);

            _batch.Begin(samplerState: SamplerState.PointClamp);

            // The strip: paper, framed
            Block(-Half - 2, 0, SpanDegrees + 5, StripHeight, paper);
            Block(-Half - 2, 0, SpanDegrees + 5, 1, ink);
            Block(-Half - 2, StripHeight - 1, SpanDegrees + 5, 1, ink);
            Block(-Half - 2, 0, 1, StripHeight, ink);
            Block(Half + 2, 0, 1, StripHeight, ink);

            // The ticks and letters in view, each where its bearing is from the heading
            var first = (int)MathF.Ceiling((degrees - Half) / 15f) * 15;
            for (var mark = first; mark <= degrees + Half; mark += 15)
            {
                var x = (int)MathF.Round(mark - degrees);
                var at = ((mark % 360) + 360) % 360;
                var major = at % 45 == 0;
                Block(x, StripHeight - (major ? 5 : 3), 1, major ? 4 : 2, ink);
                if (major)
                {
                    var label = Labels[at / 45];
                    var width = label.Length * 4 - 1;
                    if (x - width / 2 >= -Half && x - width / 2 + width <= Half + 1)
                        Text(label, x - width / 2, 2, ink, Block);
                }
            }

            // The pointer, under the middle, and the heading under it in its own box
            Block(0, StripHeight, 1, 2, ink);
            Block(-1, StripHeight + 1, 3, 1, ink);
            var number = heading.ToString("000");
            Block(-7, StripHeight + 2, 15, 9, paper);
            Block(-7, StripHeight + 2, 15, 1, ink);
            Block(-7, StripHeight + 10, 15, 1, ink);
            Block(-7, StripHeight + 2, 1, 9, ink);
            Block(7, StripHeight + 2, 1, 9, ink);
            Text(number, -5, StripHeight + 4, ink, Block);

            _batch.End();
        }

        private static readonly string[] Labels = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        // Characters 3 wide, a pixel between them, from `x`, `y` down
        private static void Text(string text, int x, int y, Color ink, Action<int, int, int, int, Color> block)
        {
            foreach (var c in text)
            {
                var rows = Glyph(c);
                for (var row = 0; row < 5; row++)
                    for (var col = 0; col < 3; col++)
                        if (rows[row][col] == '#')
                            block(x + col, y + row, 1, 1, ink);
                x += 4;
            }
        }

        // A character as five rows of three: '#' is ink
        private static string[] Glyph(char c) => c switch
        {
            '0' => new[] { "###", "#.#", "#.#", "#.#", "###" },
            '1' => new[] { ".#.", "##.", ".#.", ".#.", "###" },
            '2' => new[] { "###", "..#", "###", "#..", "###" },
            '3' => new[] { "###", "..#", "###", "..#", "###" },
            '4' => new[] { "#.#", "#.#", "###", "..#", "..#" },
            '5' => new[] { "###", "#..", "###", "..#", "###" },
            '6' => new[] { "###", "#..", "###", "#.#", "###" },
            '7' => new[] { "###", "..#", "..#", "..#", "..#" },
            '8' => new[] { "###", "#.#", "###", "#.#", "###" },
            '9' => new[] { "###", "#.#", "###", "..#", "###" },
            'N' => new[] { "#.#", "###", "###", "###", "#.#" },
            'E' => new[] { "###", "#..", "##.", "#..", "###" },
            'S' => new[] { ".##", "#..", ".#.", "..#", "##." },
            'W' => new[] { "#.#", "#.#", "###", "###", "#.#" },
            _ => new[] { "...", "...", "...", "...", "..." },
        };

        public void Dispose()
        {
            _batch.Dispose();
            _pixel.Dispose();
        }
    }
}
