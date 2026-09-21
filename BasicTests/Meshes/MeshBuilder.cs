using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace BasicTests.Meshes
{
    // Collects position-only solid triangles (with colour-slot draw ranges) and edge lines, then
    // turns them into a MeshData. Shared by meshes that are assembled from boxes and extra faces.
    //
    // Boxes have three shades like the original VectorViktor box builder: Side (flanks),
    // Dim (nose + tail + optional bottom) and Top. A part's palette uses 3 consecutive slots
    // starting at its baseSlot, in that order.
    sealed class MeshBuilder
    {
        public const int Side = 0, Dim = 1, Top = 2;

        private readonly List<VertexPosition> _solids = new();
        private readonly List<DrawRange> _solidRanges = new();
        private readonly List<VertexPosition> _edges = new();

        // Fills the 3 shade slots of a box part from one base colour, with the same formulas as the original.
        public static void SetBoxShades(Color[] palette, int baseSlot, Color color)
        {
            palette[baseSlot + Side] = color;
            palette[baseSlot + Dim] = new Color((int)(color.R * 0.55f), (int)(color.G * 0.55f), (int)(color.B * 0.55f));
            palette[baseSlot + Top] = Color.Lerp(color, Color.White, 0.35f);
        }

        // Axis-aligned box (forward = +Z, right = +X) whose position argument is the centre of its bottom face.
        // Adds 5 quads, grouped by shade so each shade is one contiguous range: 2 flanks (Side),
        // nose + tail (Dim), top (Top); plus its 12 edges. With sealBottom the underside is a sixth
        // quad (Dim); leave it off where the underside is never visible.
        public void AddBox(int baseSlot, Vector3 bottomCenter, float length, float width, float height, bool sealBottom = false)
        {
            var hf = Vector3.UnitZ * (length * 0.5f);
            var hr = Vector3.UnitX * (width * 0.5f);
            var hu = Vector3.Up * height;

            var a = bottomCenter - hf - hr;
            var b = bottomCenter + hf - hr;
            var c = bottomCenter + hf + hr;
            var d = bottomCenter - hf + hr;
            var e = a + hu;
            var f = b + hu;
            var g = c + hu;
            var h = d + hu;

            AddSolidRange(4, baseSlot + Side);
            AddQuad(a, b, f, e);   // flank (-right)
            AddQuad(c, d, h, g);   // flank (+right)
            AddSolidRange(sealBottom ? 6 : 4, baseSlot + Dim);
            AddQuad(b, c, g, f);   // nose (+forward)
            AddQuad(d, a, e, h);   // tail (-forward)
            if (sealBottom)
                AddQuad(a, b, c, d);   // bottom
            AddSolidRange(2, baseSlot + Top);
            AddQuad(e, f, g, h);   // top

            AddLine(a, b); AddLine(b, c); AddLine(c, d); AddLine(d, a);   // base
            AddLine(e, f); AddLine(f, g); AddLine(g, h); AddLine(h, e);   // top rim
            AddLine(a, e); AddLine(b, f); AddLine(c, g); AddLine(d, h);   // verticals
        }

        // Starts a draw range at the current end of the solids; add exactly `primitives` triangles after it.
        public void AddSolidRange(int primitives, int colorSlot) =>
            _solidRanges.Add(new DrawRange(_solids.Count, primitives, colorSlot));

        public void AddTri(Vector3 a, Vector3 b, Vector3 c)
        {
            _solids.Add(new VertexPosition(a));
            _solids.Add(new VertexPosition(b));
            _solids.Add(new VertexPosition(c));
        }

        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            AddTri(a, b, c);
            AddTri(a, c, d);
        }

        // A convex polygon (points in order round its edge) as a triangle fan: points.Length - 2 triangles.
        public void AddPolygon(params Vector3[] points)
        {
            for (var i = 1; i < points.Length - 1; i++)
                AddTri(points[0], points[i], points[i + 1]);
        }

        // A closed loop of lines round the given points, e.g. a polygon's outline.
        public void AddLineLoop(params Vector3[] points)
        {
            for (var i = 0; i < points.Length; i++)
                AddLine(points[i], points[(i + 1) % points.Length]);
        }

        public void AddLine(Vector3 a, Vector3 b)
        {
            _edges.Add(new VertexPosition(a));
            _edges.Add(new VertexPosition(b));
        }

        public MeshData Build(GraphicsDevice device) =>
            new MeshData(ToBuffer(device, _solids), _solidRanges.ToArray(), ToBuffer(device, _edges));

        private static VertexBuffer ToBuffer(GraphicsDevice device, List<VertexPosition> vertices)
        {
            var buffer = new VertexBuffer(device, typeof(VertexPosition), vertices.Count, BufferUsage.WriteOnly);
            buffer.SetData(vertices.ToArray());
            return buffer;
        }
    }
}
