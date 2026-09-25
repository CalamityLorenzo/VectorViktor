using System;
using System.Collections.Generic;

using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData.Helpers
{
    // Shared by RoadMesh: flat tarmac, raised pavements behind kerbs, and white paint, laid out in plan.
    // Plan points are Vector2 (X, Z) - X is the world's X, Y is the world's Z - and angles go from +X
    // (0) towards +Z (a quarter turn). The tarmac sits a little above y = 0, so it doesn't fight flat
    // ground under it; the pavements stand a kerb higher, with a face down to y = 0 at the back.
    //
    // Slots: Tarmac, Kerb (the kerb faces and the backs of the pavements), Pavement (the walked-on top, of
    // pavements and islands), Paint (road markings, and bollards) and Verge (the grass on a roundabout's island).
    public sealed class RoadBuilder
    {
        public const int Tarmac = 0, Kerb = 1, Pavement = 2, Paint = 3, Verge = 4;
        public const int PaletteSize = 5;

        public const float LaneWidth = 3.5f;        // two lanes, so the kerb is one lane out from the centre line
        public const float PavementWidth = 2f;
        public const float KerbHeight = 0.12f;
        public const float SurfaceHeight = 0.02f;   // the tarmac
        private const float PaintLift = 0.005f;
        private const float LineWidth = 0.15f;

        private readonly MeshBuilder _mesh = new MeshBuilder();

        public static Color[] Palette(Color tarmac, Color pavement, Color paint, Color verge)
        {
            var kerb = new Color((int)(pavement.R * 0.7f), (int)(pavement.G * 0.7f), (int)(pavement.B * 0.7f));
            return new[] { tarmac, kerb, pavement, paint, verge };
        }

        // For parts added straight to the mesh (a roundabout's island); they must use this builder's slots.
        public MeshBuilder Mesh => _mesh;

        public static Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);

        // Points round an arc about centre from angle `from` to `to`, both ends included.
        public static List<Vector2> Arc(Vector2 centre, float radius, float from, float to, int segments)
        {
            var points = new List<Vector2>(segments + 1);
            for (var i = 0; i <= segments; i++)
            {
                var a = MathHelper.Lerp(from, to, i / (float)segments);
                points.Add(centre + radius * new Vector2(MathF.Cos(a), MathF.Sin(a)));
            }
            return points;
        }

        // The shorter way round centre from one point to another, both on the same circle about it.
        public static List<Vector2> Fillet(Vector2 centre, Vector2 from, Vector2 to, int segments)
        {
            var a0 = MathF.Atan2(from.Y - centre.Y, from.X - centre.X);
            var a1 = MathF.Atan2(to.Y - centre.Y, to.X - centre.X);
            var turn = MathHelper.WrapAngle(a1 - a0);
            return Arc(centre, Vector2.Distance(from, centre), a0, a0 + turn, segments);
        }

        // Runs of points joined into one path, dropping a point that repeats the one before it.
        public static List<Vector2> Path(params IEnumerable<Vector2>[] parts)
        {
            var path = new List<Vector2>();
            foreach (var part in parts)
                foreach (var p in part)
                    if (path.Count == 0 || Vector2.DistanceSquared(path[^1], p) > 1e-6f)
                        path.Add(p);
            return path;
        }

        // A path to paint along: the point `s` metres along it.
        public static Func<float, Vector2> Line(Vector2 from, Vector2 to)
        {
            var direction = Vector2.Normalize(to - from);
            return s => from + direction * s;
        }

        public static Func<float, Vector2> Circle(Vector2 centre, float radius, float fromAngle, float direction) =>
            s => centre + radius * new Vector2(MathF.Cos(fromAngle + direction * s / radius), MathF.Sin(fromAngle + direction * s / radius));

        // A convex patch of tarmac, its corners in order round it.
        public void AddTarmac(params Vector2[] corners)
        {
            for (var i = 1; i < corners.Length - 1; i++)
                Tri(Tarmac, At(corners[0], SurfaceHeight), At(corners[i], SurfaceHeight), At(corners[i + 1], SurfaceHeight));
        }

        // Tarmac from a hub out to every point of a rim that it can see all of.
        public void AddTarmacFan(Vector2 hub, IReadOnlyList<Vector2> rim, bool closed = false)
        {
            var count = closed ? rim.Count : rim.Count - 1;
            for (var i = 0; i < count; i++)
                Tri(Tarmac, At(hub, SurfaceHeight), At(rim[i], SurfaceHeight), At(rim[(i + 1) % rim.Count], SurfaceHeight));
        }

        // Tarmac between two paths of as many points, point for point.
        public void AddTarmacStrip(IReadOnlyList<Vector2> a, IReadOnlyList<Vector2> b)
        {
            for (var i = 0; i < a.Count - 1; i++)
                Quad(Tarmac, At(a[i], SurfaceHeight), At(a[i + 1], SurfaceHeight), At(b[i + 1], SurfaceHeight), At(b[i], SurfaceHeight));
        }

        // A pavement behind the kerb line `kerb`, on the (dz, -dx) side of the way the path runs: the +X side
        // of a kerb running +Z, the outside of one running anticlockwise (+X towards +Z) round a curve.
        // Its ends are left open, where they meet the next piece's.
        public void AddPavement(IReadOnlyList<Vector2> kerb, bool closed = false)
        {
            var n = kerb.Count;
            var back = new Vector2[n];
            static Vector2 Side(Vector2 from, Vector2 to)
            {
                var t = Vector2.Normalize(to - from);
                return new Vector2(t.Y, -t.X);
            }
            for (var i = 0; i < n; i++)
            {
                var hasPrev = closed || i > 0;
                var hasNext = closed || i < n - 1;
                Vector2 normal;
                if (!hasPrev)
                    normal = Side(kerb[i], kerb[i + 1]);
                else if (!hasNext)
                    normal = Side(kerb[i - 1], kerb[i]);
                else
                {
                    // mitred, so the pavement keeps its width round a bend
                    var a = Side(kerb[(i - 1 + n) % n], kerb[i]);
                    var b = Side(kerb[i], kerb[(i + 1) % n]);
                    var mitre = Vector2.Normalize(a + b);
                    normal = mitre / Vector2.Dot(mitre, a);
                }
                back[i] = kerb[i] + normal * PavementWidth;
            }

            var top = SurfaceHeight + KerbHeight;
            var segments = closed ? n : n - 1;
            for (var i = 0; i < segments; i++)
            {
                var j = (i + 1) % n;
                Quad(Pavement, At(kerb[i], top), At(kerb[j], top), At(back[j], top), At(back[i], top));
                Quad(Kerb, At(kerb[i], SurfaceHeight), At(kerb[j], SurfaceHeight), At(kerb[j], top), At(kerb[i], top));
                Quad(Kerb, At(back[i], 0f), At(back[j], 0f), At(back[j], top), At(back[i], top));
                _mesh.AddLine(At(kerb[i], SurfaceHeight), At(kerb[j], SurfaceHeight));
                _mesh.AddLine(At(kerb[i], top), At(kerb[j], top));
                _mesh.AddLine(At(back[i], top), At(back[j], top));
            }
        }

        // The broken white line down the middle of a road `length` long: a whole number of 6 m periods, each
        // half line and half gap with the line in the middle, so pieces laid end to end keep the rhythm.
        public void AddCentreLine(Func<float, Vector2> at, float length) => AddDashes(at, length, 6f, 0.5f, LineWidth);

        // An unbroken white line, `length` long.
        public void AddSolidLine(Func<float, Vector2> at, float length) => AddPaint(at, 0f, length, LineWidth);

        // A raised island standing on the tarmac: kerbs all round a convex outline (in order round it), paved on top.
        public void AddIsland(IReadOnlyList<Vector2> outline)
        {
            var top = SurfaceHeight + KerbHeight;
            var n = outline.Count;
            for (var i = 0; i < n; i++)
            {
                var j = (i + 1) % n;
                Quad(Kerb, At(outline[i], SurfaceHeight), At(outline[j], SurfaceHeight), At(outline[j], top), At(outline[i], top));
                _mesh.AddLine(At(outline[i], SurfaceHeight), At(outline[j], SurfaceHeight));
                _mesh.AddLine(At(outline[i], top), At(outline[j], top));
            }
            for (var i = 1; i < n - 1; i++)
                Tri(Pavement, At(outline[0], top), At(outline[i], top), At(outline[i + 1], top));
        }

        // Give way: two broken lines across the lane coming in, the first at `mouth`; `back` points up the
        // road, away from the junction, and `across` from the centre line towards that lane's kerb.
        public void AddGiveWay(Vector2 mouth, Vector2 back, Vector2 across)
        {
            const float inset = 0.15f;
            var length = LaneWidth - 2f * inset;
            for (var k = 0; k < 2; k++)
            {
                var start = mouth + back * (k * 0.6f) + across * inset;
                AddDashes(Line(start, start + across * length), length, 0.9f, 0.6f, 0.2f);
            }
        }

        // Dashes along a path, whole periods fitting its length, each dash `ratio` of its period and centred in it.
        public void AddDashes(Func<float, Vector2> at, float length, float period, float ratio, float width)
        {
            var count = Math.Max(1, (int)MathF.Round(length / period));
            var step = length / count;
            for (var k = 0; k < count; k++)
            {
                var middle = (k + 0.5f) * step;
                AddPaint(at, middle - step * ratio / 2f, middle + step * ratio / 2f, width);
            }
        }

        // A solid line from `from` to `to` metres along a path, in pieces of a metre or less so it follows a curve,
        // outlined so it still shows with the colours off.
        public void AddPaint(Func<float, Vector2> at, float from, float to, float width)
        {
            var y = SurfaceHeight + PaintLift;
            Vector2 Across(float s)
            {
                var t = Vector2.Normalize(at(s + 0.01f) - at(s - 0.01f));
                return new Vector2(t.Y, -t.X) * (width / 2f);
            }
            var pieces = Math.Max(1, (int)MathF.Ceiling(to - from));
            for (var i = 0; i < pieces; i++)
            {
                var s0 = MathHelper.Lerp(from, to, i / (float)pieces);
                var s1 = MathHelper.Lerp(from, to, (i + 1) / (float)pieces);
                Vector2 p0 = at(s0), p1 = at(s1), a0 = Across(s0), a1 = Across(s1);
                Quad(Paint, At(p0 - a0, y), At(p1 - a1, y), At(p1 + a1, y), At(p0 + a0, y));
                _mesh.AddLine(At(p0 - a0, y), At(p1 - a1, y));
                _mesh.AddLine(At(p0 + a0, y), At(p1 + a1, y));
                if (i == 0)
                    _mesh.AddLine(At(p0 - a0, y), At(p0 + a0, y));
                if (i == pieces - 1)
                    _mesh.AddLine(At(p1 - a1, y), At(p1 + a1, y));
            }
        }

        // Each slot's triangles as one draw range (see MeshBuilder).
        public MeshData Build(GraphicsDevice device) => _mesh.Build(device);

        private void Tri(int slot, Vector3 a, Vector3 b, Vector3 c) => _mesh.AddTri(slot, a, b, c);

        private void Quad(int slot, Vector3 a, Vector3 b, Vector3 c, Vector3 d) => _mesh.AddQuad(slot, a, b, c, d);
    }
}
