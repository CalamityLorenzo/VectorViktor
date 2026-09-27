using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace Droid.Playground
{
    // Coloured lines in the world, gathered through a frame and drawn at its end over the world, hidden behind what's in
    // front of them: for showing what's usually unseen (walls to walk into, bodies' boxes, a rig's joints).
    public sealed class DebugLines : IDisposable
    {
        private const int Batch = 30000;   // lines drawn in one go

        private readonly List<VertexPositionColor> _points = new List<VertexPositionColor>();
        private readonly BasicEffect _effect;

        public DebugLines(GraphicsDevice device) =>
            _effect = new BasicEffect(device) { VertexColorEnabled = true, World = Matrix.Identity };

        public void Line(Vector3 a, Vector3 b, Color color)
        {
            _points.Add(new VertexPositionColor(a, color));
            _points.Add(new VertexPositionColor(b, color));
        }

        // The edges of a box `size` big, standing on the origin of `place` (its foot on y = 0, centred), placed by it.
        public void Box(Matrix place, Vector3 size, Color color)
        {
            var h = new Vector3(size.X / 2f, 0f, size.Z / 2f);
            Vector3 C(float x, float y, float z) => Vector3.Transform(new Vector3(x * h.X, y * size.Y, z * h.Z), place);
            var bottom = new[] { C(-1, 0, -1), C(1, 0, -1), C(1, 0, 1), C(-1, 0, 1) };
            var top = new[] { C(-1, 1, -1), C(1, 1, -1), C(1, 1, 1), C(-1, 1, 1) };
            for (var k = 0; k < 4; k++)
            {
                Line(bottom[k], bottom[(k + 1) % 4], color);
                Line(top[k], top[(k + 1) % 4], color);
                Line(bottom[k], top[k], color);
            }
        }

        // A level circle round `centre`.
        public void Circle(Vector3 centre, float radius, Color color, int sides = 16)
        {
            for (var k = 0; k < sides; k++)
            {
                var (a, b) = (k * MathHelper.TwoPi / sides, (k + 1) * MathHelper.TwoPi / sides);
                Line(centre + new Vector3(MathF.Cos(a), 0f, MathF.Sin(a)) * radius, centre + new Vector3(MathF.Cos(b), 0f, MathF.Sin(b)) * radius, color);
            }
        }

        // A joint's axes, `size` long: x red, y green, z blue.
        public void Axes(Matrix joint, float size)
        {
            var o = joint.Translation;
            Line(o, o + Vector3.Normalize(joint.Right) * size, Color.Red);
            Line(o, o + Vector3.Normalize(joint.Up) * size, Color.Lime);
            Line(o, o + Vector3.Normalize(joint.Backward) * size, Color.DeepSkyBlue);   // MonoGame's Backward is +Z
        }

        // Everything gathered, with this camera, then forgotten.
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            if (_points.Count == 0)
                return;
            _effect.View = view;
            _effect.Projection = projection;
            device.DepthStencilState = DepthStencilState.Default;
            device.BlendState = BlendState.Opaque;
            var points = _points.ToArray();
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                for (var start = 0; start < points.Length; start += Batch * 2)
                    device.DrawUserPrimitives(PrimitiveType.LineList, points, start, Math.Min(Batch, (points.Length - start) / 2));
            }
            _points.Clear();
        }

        public void Dispose() => _effect.Dispose();
    }
}
