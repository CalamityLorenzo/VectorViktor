using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MeshProps
{
    // A flat face for art to be painted on: a billboard's (see BillboardDesign) or a book's cover (see BookCover). Positions
    // are in the face's own units: X across from its middle, Y up from its bottom edge, and the art lies on the face, a
    // touch in front of it.
    public sealed class PaintedFace
    {
        // Block characters in a cell 0.6 wide and 1 high: `Fills` are convex pieces (x, y pairs) that meet without
        // overlapping and are filled without edges; `Edges` are the lines (x0, y0, x1, y1) of the character's true
        // outline, holes included, the only edges drawn - so the wireframe shows one clean outline per character,
        // not its strokes.
        private sealed record Glyph(float[][] Fills, float[] Edges);

        private static float[] Rect(float x0, float y0, float x1, float y1) => new[] { x0, y0, x1, y0, x1, y1, x0, y1 };

        // A character drawn on a grid 3 squares wide and 5 high, its rows from the top down, '#' for a square filled:
        // its fills are each row's runs of squares, its edges every side of a square with no square beyond it
        private static Glyph Pixels(params string[] rows)
        {
            const float square = 0.2f;
            bool Lit(int column, int row) => row >= 0 && row < 5 && column >= 0 && column < 3 && rows[row][column] == '#';
            float Y(int row) => 1f - row * square;   // the top of a row

            var fills = new List<float[]>();
            for (var row = 0; row < 5; row++)
                for (var column = 0; column < 3; column++)
                    if (Lit(column, row) && !Lit(column - 1, row))
                    {
                        var end = column;
                        while (Lit(end + 1, row))
                            end++;
                        fills.Add(Rect(column * square, Y(row + 1), (end + 1) * square, Y(row)));
                    }

            // Each line between squares, where one side is filled and the other isn't, run on along the line as far as it goes
            var edges = new List<float>();
            for (var line = 0; line <= 5; line++)
                for (var column = 0; column < 3; column++)
                {
                    bool Across(int c) => c < 3 && Lit(c, line - 1) != Lit(c, line);
                    if (!Across(column) || (column > 0 && Across(column - 1)))
                        continue;
                    var end = column;
                    while (Across(end + 1))
                        end++;
                    edges.AddRange(new[] { column * square, Y(line), (end + 1) * square, Y(line) });
                }
            for (var line = 0; line <= 3; line++)
                for (var row = 0; row < 5; row++)
                {
                    bool Down(int r) => r < 5 && Lit(line - 1, r) != Lit(line, r);
                    if (!Down(row) || (row > 0 && Down(row - 1)))
                        continue;
                    var end = row;
                    while (Down(end + 1))
                        end++;
                    edges.AddRange(new[] { line * square, Y(row), line * square, Y(end + 1) });
                }
            return new Glyph(fills.ToArray(), edges.ToArray());
        }

        // A character drawn by hand, its outline as loops (x, y pairs)
        private static Glyph Drawn(float[][] fills, params float[][] outlines)
        {
            var edges = new List<float>();
            foreach (var loop in outlines)
                for (var p = 0; p < loop.Length; p += 2)
                {
                    var q = (p + 2) % loop.Length;
                    edges.AddRange(new[] { loop[p], loop[p + 1], loop[q], loop[q + 1] });
                }
            return new Glyph(fills, edges.ToArray());
        }

        private static readonly Dictionary<char, Glyph> Glyphs = new Dictionary<char, Glyph>
        {
            ['A'] = Pixels("###", "#.#", "###", "#.#", "#.#"),
            ['B'] = Pixels("##.", "#.#", "###", "#.#", "##."),
            ['C'] = Pixels("###", "#..", "#..", "#..", "###"),
            ['D'] = Pixels("##.", "#.#", "#.#", "#.#", "##."),
            ['E'] = Pixels("###", "#..", "###", "#..", "###"),
            ['F'] = Pixels("###", "#..", "###", "#..", "#.."),
            ['G'] = Pixels("###", "#..", "#.#", "#.#", "###"),
            ['H'] = Pixels("#.#", "#.#", "###", "#.#", "#.#"),
            ['I'] = Pixels(".#.", ".#.", ".#.", ".#.", ".#."),
            ['J'] = Pixels("..#", "..#", "..#", "#.#", "###"),
            ['K'] = Pixels("#.#", "#.#", "##.", "#.#", "#.#"),
            ['L'] = Pixels("#..", "#..", "#..", "#..", "###"),
            ['M'] = Pixels("#.#", "###", "###", "#.#", "#.#"),
            ['N'] = Pixels("##.", "#.#", "#.#", "#.#", "#.#"),
            ['O'] = Pixels("###", "#.#", "#.#", "#.#", "###"),
            ['P'] = Pixels("###", "#.#", "###", "#..", "#.."),
            ['Q'] = Pixels("###", "#.#", "#.#", "###", "..#"),
            // R's leg is a slope, which squares can't draw
            ['R'] = Drawn(
                new[] { Rect(0f, 0f, 0.2f, 1f), Rect(0.2f, 0.8f, 0.6f, 1f), Rect(0.4f, 0.6f, 0.6f, 0.8f), Rect(0.2f, 0.4f, 0.6f, 0.6f),
                        new[] { 0.4f, 0f, 0.6f, 0f, 0.5f, 0.4f, 0.3f, 0.4f } },
                new[] { 0f, 0f, 0.2f, 0f, 0.2f, 0.4f, 0.3f, 0.4f, 0.4f, 0f, 0.6f, 0f, 0.5f, 0.4f, 0.6f, 0.4f, 0.6f, 1f, 0f, 1f },
                new[] { 0.2f, 0.6f, 0.4f, 0.6f, 0.4f, 0.8f, 0.2f, 0.8f }),
            ['S'] = Pixels("###", "#..", "###", "..#", "###"),
            ['T'] = Pixels("###", ".#.", ".#.", ".#.", ".#."),
            ['U'] = Pixels("#.#", "#.#", "#.#", "#.#", "###"),
            ['V'] = Pixels("#.#", "#.#", "#.#", "#.#", ".#."),
            ['W'] = Pixels("#.#", "#.#", "###", "###", "#.#"),
            ['X'] = Pixels("#.#", "#.#", ".#.", "#.#", "#.#"),
            ['Y'] = Pixels("#.#", "#.#", ".#.", ".#.", ".#."),
            ['Z'] = Pixels("###", "..#", ".#.", "#..", "###"),
            ['0'] = Pixels("###", "#.#", "#.#", "#.#", "###"),
            ['1'] = Pixels("##.", ".#.", ".#.", ".#.", "###"),
            ['2'] = Pixels("###", "..#", "###", "#..", "###"),
            ['3'] = Pixels("###", "..#", "###", "..#", "###"),
            ['4'] = Pixels("#.#", "#.#", "###", "..#", "..#"),
            ['5'] = Pixels("###", "#..", "###", "..#", "###"),
            ['6'] = Pixels("###", "#..", "###", "#.#", "###"),
            ['7'] = Pixels("###", "..#", "..#", "..#", "..#"),
            ['8'] = Pixels("###", "#.#", "###", "#.#", "###"),
            ['9'] = Pixels("###", "#.#", "###", "..#", "###"),
            ['-'] = Pixels("...", "...", "###", "...", "..."),
            ['.'] = Pixels("...", "...", "...", "...", ".#."),
            ['!'] = Pixels(".#.", ".#.", ".#.", "...", ".#."),
            ['?'] = Pixels("###", "..#", ".##", "...", ".#."),
            ['\''] = Pixels(".#.", ".#.", "...", "...", "..."),
        };

        private const float Advance = 0.8f;   // from one character's left side to the next's, a character being 0.6 wide

        private readonly MeshBuilder _mesh;
        private readonly Vector3 _origin, _across, _up;
        private readonly float _lift;

        // A face whose (0, 0) is at `origin`, X running along `across` and Y along `up`, both a unit long; `lift` is how far
        // in front of what's under it a layer of art has to be not to fight it for the same pixels
        internal PaintedFace(MeshBuilder mesh, Vector3 origin, Vector3 across, Vector3 up, float lift)
        {
            _mesh = mesh;
            _origin = origin;
            _across = across;
            _up = up;
            _lift = lift;
        }

        // The same face a layer further out, to paint over what's painted on this one (eyes on a face)
        public PaintedFace Over() => new PaintedFace(_mesh, _origin + Vector3.Cross(_across, _up) * _lift, _across, _up, _lift);

        private Vector3 At(Vector2 p) => _origin + p.X * _across + p.Y * _up;
        private Vector3 At(float x, float y) => At(new Vector2(x, y));

        // A flat shape (convex), in a palette slot, outlined
        public void Shape(int slot, params Vector2[] points)
        {
            var polygon = Array.ConvertAll(points, At);
            _mesh.AddPolygon(slot, polygon);
            _mesh.AddLineLoop(polygon);
        }

        // A curved band between two lines of matching points, filled a quad at a time, outlined by the two lines
        // and its two ends only - not the joins between quads
        public void Band(int slot, Vector2[] a, Vector2[] b)
        {
            for (var k = 0; k < a.Length - 1; k++)
                _mesh.AddPolygon(slot, At(a[k]), At(a[k + 1]), At(b[k + 1]), At(b[k]));
            for (var k = 0; k < a.Length - 1; k++)
            {
                _mesh.AddLine(At(a[k]), At(a[k + 1]));
                _mesh.AddLine(At(b[k]), At(b[k + 1]));
            }
            _mesh.AddLine(At(a[0]), At(b[0]));
            _mesh.AddLine(At(a[^1]), At(b[^1]));
        }

        // A rectangular border `thickness` wide, inside the rectangle from (left, bottom) to (right, top): a band all the way
        // round, joined at its bottom left corner
        public void Frame(int slot, float left, float bottom, float right, float top, float thickness)
        {
            Vector2[] Round(float inset) => new[]
            {
                new Vector2(left + inset, bottom + inset), new Vector2(right - inset, bottom + inset),
                new Vector2(right - inset, top - inset), new Vector2(left + inset, top - inset), new Vector2(left + inset, bottom + inset),
            };
            Band(slot, Round(0f), Round(thickness));
        }

        // How wide a line of block capitals `size` high is
        public static float WidthOf(string text, float size) => text.Length == 0 ? 0f : (text.Length * Advance - (Advance - 0.6f)) * size;

        // Block capitals (A to Z, 0 to 9, spaces and - . ! ? '), each `size` high, with the first's bottom left corner at
        // (left, bottom). Small letters are written as capitals.
        public void Write(string text, float left, float bottom, float size, int slot)
        {
            text = text.ToUpperInvariant();
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == ' ')
                    continue;
                if (!Glyphs.TryGetValue(text[i], out var glyph))
                    throw new ArgumentException($"There's no block capital for '{text[i]}' (in \"{text}\").", nameof(text));
                var x = left + i * Advance * size;
                foreach (var fill in glyph.Fills)
                {
                    var corners = new Vector3[fill.Length / 2];
                    for (var p = 0; p < corners.Length; p++)
                        corners[p] = At(x + fill[2 * p] * size, bottom + fill[2 * p + 1] * size);
                    _mesh.AddPolygon(slot, corners);
                }
                var e = glyph.Edges;
                for (var p = 0; p < e.Length; p += 4)
                    _mesh.AddLine(At(x + e[p] * size, bottom + e[p + 1] * size), At(x + e[p + 2] * size, bottom + e[p + 3] * size));
            }
        }

        // The same, centred on `middle`
        public void WriteCentred(string text, float middle, float bottom, float size, int slot) =>
            Write(text, middle - WidthOf(text, size) / 2f, bottom, size, slot);
    }
}
