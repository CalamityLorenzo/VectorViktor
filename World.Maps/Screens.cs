using MeshProps;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using World.Buildings;

namespace World.Maps
{
    // Static: a small picture of random greys, made afresh every couple of ticks. Drawn point-sampled, so its pixels stay
    // hard. The same picture on every screen showing static; nobody's going to compare.
    public sealed class StaticPicture : IDisposable
    {
        public const int Width = 48, Height = 36;
        private const float Every = 2f / 60f;   // seconds between pictures

        private readonly Color[] _pixels = new Color[Width * Height];
        private uint _seed = 2463534242u;
        private float _madeAt = float.NegativeInfinity;

        public Texture2D Texture { get; }

        public StaticPicture(GraphicsDevice device) => Texture = new Texture2D(device, Width, Height, false, SurfaceFormat.Color);

        // A new picture, if it's time for one, `clock` seconds in.
        public void Update(float clock)
        {
            if (clock - _madeAt < Every && clock >= _madeAt)
                return;
            _madeAt = clock;
            for (var i = 0; i < _pixels.Length; i++)
            {
                _seed ^= _seed << 13;   // xorshift: quick, and random enough to look at
                _seed ^= _seed >> 17;
                _seed ^= _seed << 5;
                var grey = (byte)(40 + _seed % 200);
                _pixels[i] = new Color(grey, grey, grey);
            }
            Texture.SetData(_pixels);
        }

        public void Dispose() => Texture.Dispose();
    }

    // A picture on a television's screen (see ScreenSpec): a grid over its bulging glass (see TelevisionMesh.ScreenPoint),
    // in the world, a hair in front of it, textured with whatever the screen's channel is showing.
    public sealed class ScreenView
    {
        private const float Proud = 0.003f;

        private readonly VertexPositionTexture[] _vertices;
        private readonly short[] _indices;

        public string Channel { get; }
        public BoundingBox Bounds { get; }

        // `placement` puts the television where it stands in the world.
        public ScreenView(Matrix placement, string channel)
        {
            Channel = channel;
            const int cells = TelevisionMesh.ScreenCells;
            _vertices = new VertexPositionTexture[(cells + 1) * (cells + 1)];
            for (var j = 0; j <= cells; j++)
                for (var i = 0; i <= cells; i++)
                {
                    var (u, v) = (i / (float)cells, j / (float)cells);
                    var at = Vector3.Transform(TelevisionMesh.ScreenPoint(2f * u - 1f, 2f * v - 1f, Proud), placement);
                    _vertices[j * (cells + 1) + i] = new VertexPositionTexture(at, new Vector2(u, 1f - v));   // the picture's top at the screen's
                }
            _indices = new short[cells * cells * 6];
            var k = 0;
            for (var j = 0; j < cells; j++)
                for (var i = 0; i < cells; i++)
                {
                    var a = (short)(j * (cells + 1) + i);
                    var b = (short)(a + 1);
                    var c = (short)(a + cells + 2);
                    var d = (short)(a + cells + 1);
                    (_indices[k++], _indices[k++], _indices[k++]) = (a, b, c);
                    (_indices[k++], _indices[k++], _indices[k++]) = (a, c, d);
                }
            Bounds = BoundingBox.CreateFromPoints(Array.ConvertAll(_vertices, x => x.Position));
        }

        // A screen standing in a room (see RoomSpec.Screens), placed in the world.
        public ScreenView(RoomSpec room, ScreenSpec screen)
            : this(Matrix.CreateRotationY(MathHelper.ToRadians(screen.YawDegrees)) * Matrix.CreateTranslation(room.WorldOffset + screen.Position), screen.Channel)
        {
        }

        // With `effect` already set up for textures, the camera and the fog: this one's picture on it.
        public void Draw(GraphicsDevice device, BasicEffect effect, Texture2D picture)
        {
            effect.Texture = picture;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, _vertices, 0, _vertices.Length, _indices, 0, _indices.Length / 3);
            }
        }
    }
}
