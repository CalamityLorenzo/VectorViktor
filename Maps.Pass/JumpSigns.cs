using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Maps;

namespace Maps.Pass
{
    // What warns you of the jump (see Crossings): coming up to it, a red triangle at the roadside, a ramp and a gap on
    // it, twice; at the lip, at both edges of the road, a board striped red and white, so there's no mistaking where
    // the road ends; and the same at the top of the wall, for whoever comes down the road the other way. Each mesh
    // faces +Z, standing on its post's foot; placed turned to face whoever's coming.
    public static class JumpSigns
    {
        private const int Post = 0, Red = 3, White = 4, Black = 5;   // the post's three shades, then the paint
        private const float PostHeight = 1.1f, PostSide = 0.1f, Face = 0.07f;   // the face a little in front of the post

        private static Color[] Palette()
        {
            var palette = new Color[6];
            MeshBuilder.SetBoxShades(palette, Post, new Color(150, 150, 155));
            palette[Red] = new Color(200, 30, 30);
            palette[White] = new Color(240, 240, 240);
            palette[Black] = new Color(25, 25, 25);
            return palette;
        }

        // A board 1.2 m wide and 0.5 high on a post, striped red and white at 45 degrees
        public static MeshData Hazard(GraphicsDevice device)
        {
            const float width = 1.2f, height = 0.5f, stripe = 0.2f;
            var mesh = new MeshBuilder();
            mesh.AddBox(Post, Vector3.Zero, PostSide, PostSide, PostHeight + height);
            var (left, right, bottom, top) = (-width / 2f, width / 2f, PostHeight, PostHeight + height);
            mesh.AddQuad(Post + MeshBuilder.Dim, new Vector3(left, bottom, Face - 0.01f), new Vector3(right, bottom, Face - 0.01f),
                         new Vector3(right, top, Face - 0.01f), new Vector3(left, top, Face - 0.01f));   // its back
            // Each stripe a band at 45 degrees, cut to the board
            var n = 0;
            for (var from = left - height; from < right; from += stripe, n++)
            {
                var band = new[] { new Vector2(from, 0f), new Vector2(from + stripe, 0f), new Vector2(from + stripe + height, height), new Vector2(from + height, height) };
                var cut = Clip(band, left, right, 0f, height);
                if (cut.Count >= 3)
                    mesh.AddPolygon(n % 2 == 0 ? Red : White, cut.ConvertAll(p => new Vector3(p.X, bottom + p.Y, Face)).ToArray());
            }
            mesh.AddLineLoop(new Vector3(left, bottom, Face), new Vector3(right, bottom, Face), new Vector3(right, top, Face), new Vector3(left, top, Face));
            return mesh.Build(device);
        }

        // A red triangle a metre across, point up, white inside, and on it in black a ramp, a gap and the road beyond
        public static MeshData Warning(GraphicsDevice device)
        {
            const float side = 1f;
            var mesh = new MeshBuilder();
            var height = side * MathF.Sqrt(3f) / 2f;
            mesh.AddBox(Post, Vector3.Zero, PostSide, PostSide, PostHeight + height * 0.5f);
            Vector3 At(float x, float y, float z = Face) => new Vector3(x, PostHeight + y, z);
            mesh.AddPolygon(Post + MeshBuilder.Dim, At(-side / 2f, 0f, Face - 0.01f), At(side / 2f, 0f, Face - 0.01f), At(0f, height, Face - 0.01f));
            mesh.AddPolygon(Red, At(-side / 2f, 0f), At(side / 2f, 0f), At(0f, height));
            var inset = 0.12f;   // the red border's width, at the sides
            var inner = side - 2f * inset * MathF.Sqrt(3f);
            var innerBottom = inset;
            mesh.AddPolygon(White, At(-inner / 2f, innerBottom, Face + 0.005f), At(inner / 2f, innerBottom, Face + 0.005f),
                            At(0f, innerBottom + inner * MathF.Sqrt(3f) / 2f, Face + 0.005f));
            // The ramp, a wedge rising to the right; then a gap; then the road going on
            var symbol = Face + 0.01f;
            var y0 = innerBottom + 0.06f;
            mesh.AddPolygon(Black, At(-0.3f, y0, symbol), At(-0.05f, y0, symbol), At(-0.05f, y0 + 0.14f, symbol));
            mesh.AddQuad(Black, At(0.08f, y0 + 0.1f, symbol), At(0.3f, y0 + 0.1f, symbol), At(0.3f, y0 + 0.14f, symbol), At(0.08f, y0 + 0.14f, symbol));
            mesh.AddQuad(Black, At(0.08f, y0, symbol), At(0.12f, y0, symbol), At(0.12f, y0 + 0.1f, symbol), At(0.08f, y0 + 0.1f, symbol));
            mesh.AddLineLoop(At(-side / 2f, 0f), At(side / 2f, 0f), At(0f, height));
            return mesh.Build(device);
        }

        // The signs, placed: each on the road's edge `s` along it, `side` of the middle (1 right, -1 left, going towards
        // the town), facing `towards` the town (1) or away from it (-1) - that is, whoever's driving the other way
        public static IEnumerable<Fixture> Fixtures()
        {
            var palette = Palette();
            var hazard = new MeshSource("jump-hazard", Hazard, palette);
            var warning = new MeshSource("jump-warning", Warning, palette);
            var edge = PassRoute.HalfWidth - 0.4f;
            foreach (var side in new[] { -1f, 1f })
            {
                yield return new Fixture(hazard, Place(Crossings.JumpAt, side * edge, -1f));    // at the lip, facing the cars coming up
                yield return new Fixture(hazard, Place(Crossings.JumpFar, side * edge, 1f));    // at the wall's top, facing those coming down
            }
            foreach (var before in new[] { 40f, 100f })
                yield return new Fixture(warning, Place(Crossings.JumpAt - Crossings.RampLength - before, edge, -1f));
        }

        private static Matrix Place(float s, float across, float facing)
        {
            var (at, road, heading) = PassRoute.At(s);
            var p = at + new Vector2(-heading.Y, heading.X) * across;
            var toward = heading * facing;   // the way its face looks
            return Matrix.CreateRotationY(MathF.Atan2(toward.X, toward.Y)) * Matrix.CreateTranslation(p.X, Crossings.Surface(s, road), p.Y);
        }

        // A convex polygon cut to the rectangle from (left, bottom) to (right, top)
        private static List<Vector2> Clip(IEnumerable<Vector2> polygon, float left, float right, float bottom, float top)
        {
            var points = new List<Vector2>(polygon);
            foreach (var (inside, cross) in new (Func<Vector2, float>, Func<Vector2, Vector2, Vector2>)[]
            {
                (p => p.X - left, (a, b) => Vector2.Lerp(a, b, (left - a.X) / (b.X - a.X))),
                (p => right - p.X, (a, b) => Vector2.Lerp(a, b, (right - a.X) / (b.X - a.X))),
                (p => p.Y - bottom, (a, b) => Vector2.Lerp(a, b, (bottom - a.Y) / (b.Y - a.Y))),
                (p => top - p.Y, (a, b) => Vector2.Lerp(a, b, (top - a.Y) / (b.Y - a.Y))),
            })
            {
                var kept = new List<Vector2>();
                for (var k = 0; k < points.Count; k++)
                {
                    var (a, b) = (points[k], points[(k + 1) % points.Count]);
                    var (ina, inb) = (inside(a) >= 0f, inside(b) >= 0f);
                    if (ina)
                        kept.Add(a);
                    if (ina != inb)
                        kept.Add(cross(a, b));
                }
                points = kept;
                if (points.Count == 0)
                    break;
            }
            return points;
        }
    }
}
