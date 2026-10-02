using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Runtime.CompilerServices;

namespace MeshRendering
{
    // Draws a mesh's faces in one go, whatever colours they are (Shaders/PaletteEffect.fx): every corner carries the
    // slot of the palette its face is coloured from (see VertexPositionSlot), and the instance's palette goes to the
    // shader as an array of colours. BasicEffect can only colour a draw all one colour, so a mesh of eight colours was
    // eight draws, each re-sending the effect: that was most of what drawing cost (see ArchitectureReviewPlan.md 2.1).
    //
    // Everything else (edges, outlines, the screens) is still drawn with BasicEffect; this takes its view, projection
    // and fog from the BasicEffect it's drawn beside, so the faces fog just as the lines do.
    //
    // The shader is compiled by MonoGame's content builder into Shaders/PaletteEffect.xnb, which is kept in the
    // repository and built into this library. MeshRendering.csproj compiles it again whenever the .fx is newer, if the
    // content builder's there (any game here restores it).
    public sealed class PaletteEffect
    {
        public const int MaxColours = 64;   // as many as the shader has room for (MAX_COLOURS)

        private static readonly ConditionalWeakTable<GraphicsDevice, PaletteEffect> ForDevice = new ConditionalWeakTable<GraphicsDevice, PaletteEffect>();

        // The one for this device, loaded the first time it's asked for
        public static PaletteEffect For(GraphicsDevice device) => ForDevice.GetValue(device, d => new PaletteEffect(d));

        private readonly Effect _effect;
        private readonly EffectPass _pass;
        private readonly EffectParameter _worldViewProj, _worldView, _palette, _fogColor, _fogStart, _fogEnd, _fogOn;
        private Matrix _view, _projection;

        private PaletteEffect(GraphicsDevice device)
        {
            _effect = new Effect(device, CompiledShader());
            _pass = _effect.Techniques["Faces"].Passes[0];
            var p = _effect.Parameters;
            (_worldViewProj, _worldView, _palette) = (p["WorldViewProj"], p["WorldView"], p["Palette"]);
            (_fogColor, _fogStart, _fogEnd, _fogOn) = (p["FogColor"], p["FogStart"], p["FogEnd"], p["FogOn"]);
        }

        // The view, projection and fog to draw with: the BasicEffect's, which the rest of the scene is drawn with.
        public void Take(BasicEffect fx)
        {
            (_view, _projection) = (fx.View, fx.Projection);
            _fogOn.SetValue(fx.FogEnabled ? 1f : 0f);
            _fogColor.SetValue(fx.FogColor);
            _fogStart.SetValue(fx.FogStart);
            _fogEnd.SetValue(MathF.Max(fx.FogEnd, fx.FogStart + 1e-3f));
        }

        // The faces in `faces` (VertexPositionSlot), placed by `world`, each in its slot's colour from `palette`.
        public void Draw(GraphicsDevice device, VertexBuffer faces, Matrix world, Vector4[] palette)
        {
            var worldView = world * _view;
            _worldView.SetValue(worldView);
            _worldViewProj.SetValue(worldView * _projection);
            _palette.SetValue(palette);
            device.SetVertexBuffer(faces);
            _pass.Apply();
            device.DrawPrimitives(PrimitiveType.TriangleList, 0, faces.VertexCount / 3);
        }

        // The compiled shader: an .xnb holding the effect, as MonoGame's EffectReader reads it
        internal static byte[] CompiledShader()
        {
            using var stream = typeof(PaletteEffect).Assembly.GetManifestResourceStream("MeshRendering.Shaders.PaletteEffect.xnb")
                ?? throw new InvalidOperationException("The compiled palette shader isn't built into MeshRendering.");
            using var reader = new BinaryReader(stream);
            if (new string(reader.ReadChars(3)) != "XNB")
                throw new InvalidDataException("The compiled palette shader isn't an .xnb.");
            reader.ReadByte();                                  // platform
            reader.ReadByte();                                  // version
            if ((reader.ReadByte() & 0xC0) != 0)                // flags
                throw new InvalidDataException("The compiled palette shader is compressed: build it with /compress:False.");
            reader.ReadInt32();                                 // its size
            var readers = reader.Read7BitEncodedInt();
            for (var k = 0; k < readers; k++)
            {
                reader.ReadString();                            // a type reader's name
                reader.ReadInt32();                             // and version
            }
            reader.Read7BitEncodedInt();                        // shared resources
            reader.Read7BitEncodedInt();                        // which type reader the effect needs
            return reader.ReadBytes(reader.ReadInt32());        // the effect itself
        }
    }
}
