using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MeshProps
{
    // The white face of a billboard, for a BillboardDesign to paint its art on. Positions are in the face's own
    // units: X across from its middle, Y up from the board's bottom edge, and the art lies on the face, a touch
    // in front of it.
    public sealed class BillboardFace
    {
        // Block characters in a cell 0.6 wide and 1 high: `Fills` are convex pieces (x, y pairs) that meet without
        // overlapping and are filled without edges; `Outlines` are the loops (x, y pairs) of the character's true
        // outline, holes included, the only edges drawn - so the wireframe shows one clean outline per character,
        // not its strokes.
        private static float[] Rect(float x0, float y0, float x1, float y1) => new[] { x0, y0, x1, y0, x1, y1, x0, y1 };

        private static readonly Dictionary<char, (float[][] Fills, float[][] Outlines)> Glyphs = new Dictionary<char, (float[][], float[][])>
        {
            ['C'] = (
                new[] { Rect(0f, 0f, 0.2f, 1f), Rect(0.2f, 0.8f, 0.6f, 1f), Rect(0.2f, 0f, 0.6f, 0.2f) },
                new[] { new[] { 0f, 0f, 0.6f, 0f, 0.6f, 0.2f, 0.2f, 0.2f, 0.2f, 0.8f, 0.6f, 0.8f, 0.6f, 1f, 0f, 1f } }),
            ['6'] = (
                new[] { Rect(0f, 0f, 0.2f, 1f), Rect(0.2f, 0.8f, 0.6f, 1f), Rect(0.2f, 0.4f, 0.6f, 0.6f), Rect(0.2f, 0f, 0.6f, 0.2f), Rect(0.4f, 0.2f, 0.6f, 0.4f) },
                new[] { new[] { 0f, 0f, 0.6f, 0f, 0.6f, 0.6f, 0.2f, 0.6f, 0.2f, 0.8f, 0.6f, 0.8f, 0.6f, 1f, 0f, 1f },
                        new[] { 0.2f, 0.2f, 0.4f, 0.2f, 0.4f, 0.4f, 0.2f, 0.4f } }),
            ['4'] = (
                new[] { Rect(0f, 0.4f, 0.2f, 1f), Rect(0.2f, 0.4f, 0.4f, 0.6f), Rect(0.4f, 0f, 0.6f, 1f) },
                new[] { new[] { 0.4f, 0f, 0.6f, 0f, 0.6f, 1f, 0.4f, 1f, 0.4f, 0.6f, 0.2f, 0.6f, 0.2f, 1f, 0f, 1f, 0f, 0.4f, 0.4f, 0.4f } }),
            ['A'] = (
                new[] { Rect(0f, 0f, 0.2f, 1f), Rect(0.4f, 0f, 0.6f, 1f), Rect(0.2f, 0.8f, 0.4f, 1f), Rect(0.2f, 0.4f, 0.4f, 0.6f) },
                new[] { new[] { 0f, 0f, 0.2f, 0f, 0.2f, 0.4f, 0.4f, 0.4f, 0.4f, 0f, 0.6f, 0f, 0.6f, 1f, 0f, 1f },
                        new[] { 0.2f, 0.6f, 0.4f, 0.6f, 0.4f, 0.8f, 0.2f, 0.8f } }),
            ['T'] = (
                new[] { Rect(0f, 0.8f, 0.6f, 1f), Rect(0.2f, 0f, 0.4f, 0.8f) },
                new[] { new[] { 0.2f, 0f, 0.4f, 0f, 0.4f, 0.8f, 0.6f, 0.8f, 0.6f, 1f, 0f, 1f, 0f, 0.8f, 0.2f, 0.8f } }),
            ['R'] = (
                new[] { Rect(0f, 0f, 0.2f, 1f), Rect(0.2f, 0.8f, 0.6f, 1f), Rect(0.4f, 0.6f, 0.6f, 0.8f), Rect(0.2f, 0.4f, 0.6f, 0.6f),
                        new[] { 0.4f, 0f, 0.6f, 0f, 0.5f, 0.4f, 0.3f, 0.4f } },
                new[] { new[] { 0f, 0f, 0.2f, 0f, 0.2f, 0.4f, 0.3f, 0.4f, 0.4f, 0f, 0.6f, 0f, 0.5f, 0.4f, 0.6f, 0.4f, 0.6f, 1f, 0f, 1f },
                        new[] { 0.2f, 0.6f, 0.4f, 0.6f, 0.4f, 0.8f, 0.2f, 0.8f } }),
            ['I'] = (
                new[] { Rect(0.2f, 0f, 0.4f, 1f) },
                new[] { new[] { 0.2f, 0f, 0.4f, 0f, 0.4f, 1f, 0.2f, 1f } }),
        };

        private readonly MeshBuilder _mesh;
        private readonly float _z;

        internal BillboardFace(MeshBuilder mesh, float z)
        {
            _mesh = mesh;
            _z = z;
        }

        private Vector3 At(Vector2 p) => new Vector3(p.X, BillboardMesh.Clearance + p.Y, _z);

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

        // Block capitals (C, 6, 4, A, T, R and I so far), each `size` high, with the first's bottom left corner at (left, bottom)
        public void Write(string text, float left, float bottom, float size, int slot)
        {
            for (var i = 0; i < text.Length; i++)
            {
                var x = left + i * 0.8f * size;
                var (fills, outlines) = Glyphs[text[i]];
                foreach (var fill in fills)
                {
                    var corners = new Vector3[fill.Length / 2];
                    for (var p = 0; p < corners.Length; p++)
                        corners[p] = At(new Vector2(x + fill[2 * p] * size, bottom + fill[2 * p + 1] * size));
                    _mesh.AddPolygon(slot, corners);
                }
                foreach (var loop in outlines)
                {
                    var points = new Vector3[loop.Length / 2];
                    for (var p = 0; p < points.Length; p++)
                        points[p] = At(new Vector2(x + loop[2 * p] * size, bottom + loop[2 * p + 1] * size));
                    _mesh.AddLineLoop(points);
                }
            }
        }
    }
}
