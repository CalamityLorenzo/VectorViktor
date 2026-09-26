using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRendering
{
    // Windows onto somewhere else: each a quad in the world through which another scene is seen, as if it stood
    // behind it - however big that scene is, and whatever's really behind the window. They're drawn before the
    // world. Each scene goes down masked to its window by the stencil, and over it the window's glass: a faint tint,
    // however close you are, so it still looks like you're seeing it through a window, and the pane, dithered (a
    // 4 x 4 Bayer pattern on the screen's pixels, the way 16-bit games faded things in and out) as thickly as it's
    // opaque. Then Seal leaves just the windows in the depth buffer, so the world drawn after them covers them
    // wherever it's in front of them, and nothing behind them shows through.
    //
    // Needs a depth buffer with a stencil (see RetroGame).
    public sealed class WindowPortals : IDisposable
    {
        private static readonly BlendState NoColour = new BlendState { ColorWriteChannels = ColorWriteChannels.None };

        // The window's shape into the stencil, wherever it's on screen: whatever's in front of it is drawn later, over it
        private static readonly DepthStencilState Mark = new DepthStencilState
        {
            DepthBufferEnable = false, DepthBufferWriteEnable = false,
            StencilEnable = true, StencilFunction = CompareFunction.Always, StencilPass = StencilOperation.Replace, ReferenceStencil = 1,
        };

        // The scene, depth tested against itself, only inside the window
        private static readonly DepthStencilState Inside = new DepthStencilState
        {
            DepthBufferEnable = true, DepthBufferWriteEnable = true, DepthBufferFunction = CompareFunction.LessEqual,
            StencilEnable = true, StencilFunction = CompareFunction.Equal, StencilPass = StencilOperation.Keep, ReferenceStencil = 1,
        };

        // The pane, over all of the scene inside the window
        private static readonly DepthStencilState Pane = new DepthStencilState
        {
            DepthBufferEnable = false, DepthBufferWriteEnable = false,
            StencilEnable = true, StencilFunction = CompareFunction.Equal, StencilPass = StencilOperation.Keep, ReferenceStencil = 1,
        };

        // Each pixel of a 4 x 4 tile's threshold, 0 to 15, in the order they're filled in as the pane thickens
        private static readonly int[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };

        private readonly Texture2D _dither;
        private readonly AlphaTestEffect _paneEffect;
        private readonly VertexPosition[] _quad = new VertexPosition[6];
        // The tint over the scene, as much of it as the device's blend factor says (a bound state can't be changed)
        private static readonly BlendState TintBlend = new BlendState
        {
            ColorSourceBlend = Blend.BlendFactor, ColorDestinationBlend = Blend.InverseBlendFactor,
            AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One,
        };
        private readonly VertexPositionTexture[] _screen = new VertexPositionTexture[6];

        public WindowPortals(GraphicsDevice device)
        {
            // White, each pixel's threshold in its alpha: a pixel of the pane's drawn if its threshold's under the opacity
            _dither = new Texture2D(device, 4, 4);
            _dither.SetData(Array.ConvertAll(Bayer, t => new Color(255, 255, 255, (int)MathF.Round((t + 0.5f) / 16f * 255f))));
            _paneEffect = new AlphaTestEffect(device)
            {
                World = Matrix.Identity, View = Matrix.Identity, Projection = Matrix.Identity,   // drawn straight onto the screen
                Texture = _dither, VertexColorEnabled = false, FogEnabled = false,
                AlphaFunction = CompareFunction.Less,
            };
        }

        // One window and what's seen through it. `corners` go round the window, in the world. The scene's in its own
        // space, which `frame` places in the world: `scene` must have been begun with frame * the effect's view (see
        // MeshBatch.Begin), and is drawn with that view. The pane's `opacity` runs from 0 (clear) to 1 (solid); under it,
        // the glass tints the scene `tint`, `tintStrength` of the way (0 for none). With the colours off, there's no tint.
        // Draw them furthest first, so a nearer one's drawn over one it's in front of.
        public void Draw(GraphicsDevice device, BasicEffect effect, Vector3[] corners, Matrix frame, MeshBatch scene,
            Color pane, float opacity, Color tint, float tintStrength, Color background, bool colorsOn)
        {
            var view = effect.View;
            try
            {
                device.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1f, 0);
                device.BlendState = NoColour;
                device.DepthStencilState = Mark;
                DrawQuad(device, effect, corners);

                device.BlendState = BlendState.Opaque;
                device.DepthStencilState = Inside;
                effect.View = frame * view;
                scene.Draw(device, effect, background, colorsOn);
                effect.View = view;

                device.DepthStencilState = Pane;
                if (colorsOn && tintStrength > 0f)
                {
                    // The tint, blended over the whole of it: every threshold's under full opacity
                    var strength = (int)MathF.Round(MathHelper.Clamp(tintStrength, 0f, 1f) * 255f);
                    device.BlendState = TintBlend;
                    device.BlendFactor = new Color(strength, strength, strength, strength);
                    DrawPane(device, tint, 1f);
                    device.BlendState = BlendState.Opaque;
                    device.BlendFactor = Color.White;
                }
                if (opacity > 0f)
                    DrawPane(device, colorsOn ? pane : background, opacity);
            }
            finally
            {
                effect.View = view;
                device.BlendState = BlendState.Opaque;
                device.DepthStencilState = DepthStencilState.Default;
            }
        }

        // After the windows, before the world: the depth buffer cleared back to nothing but them.
        public void Seal(GraphicsDevice device, BasicEffect effect, IEnumerable<Vector3[]> windows)
        {
            device.Clear(ClearOptions.DepthBuffer | ClearOptions.Stencil, Color.Black, 1f, 0);
            device.BlendState = NoColour;
            device.DepthStencilState = DepthStencilState.Default;
            foreach (var corners in windows)
                DrawQuad(device, effect, corners);
            device.BlendState = BlendState.Opaque;
        }

        private void DrawQuad(GraphicsDevice device, BasicEffect effect, Vector3[] corners)
        {
            _quad[0] = new VertexPosition(corners[0]); _quad[1] = new VertexPosition(corners[1]); _quad[2] = new VertexPosition(corners[2]);
            _quad[3] = new VertexPosition(corners[0]); _quad[4] = new VertexPosition(corners[2]); _quad[5] = new VertexPosition(corners[3]);
            var world = effect.World;
            var vertexColor = effect.VertexColorEnabled;
            effect.World = Matrix.Identity;
            effect.VertexColorEnabled = false;
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, _quad, 0, 2);
            }
            effect.World = world;
            effect.VertexColorEnabled = vertexColor;
        }

        // The whole screen, where the stencil lets it, the dither tiled one texel to a pixel
        private void DrawPane(GraphicsDevice device, Color pane, float opacity)
        {
            var viewport = device.Viewport;
            float u = viewport.Width / 4f, v = viewport.Height / 4f;
            var topLeft = new VertexPositionTexture(new Vector3(-1f, 1f, 0f), Vector2.Zero);
            var topRight = new VertexPositionTexture(new Vector3(1f, 1f, 0f), new Vector2(u, 0f));
            var bottomRight = new VertexPositionTexture(new Vector3(1f, -1f, 0f), new Vector2(u, v));
            var bottomLeft = new VertexPositionTexture(new Vector3(-1f, -1f, 0f), new Vector2(0f, v));
            _screen[0] = topLeft; _screen[1] = topRight; _screen[2] = bottomRight;
            _screen[3] = topLeft; _screen[4] = bottomRight; _screen[5] = bottomLeft;

            _paneEffect.DiffuseColor = pane.ToVector3();
            _paneEffect.ReferenceAlpha = (int)MathF.Round(MathHelper.Clamp(opacity, 0f, 1f) * 255f);
            var sampler = device.SamplerStates[0];
            device.SamplerStates[0] = SamplerState.PointWrap;
            foreach (var pass in _paneEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, _screen, 0, 2);
            }
            device.SamplerStates[0] = sampler;
        }

        public void Dispose()
        {
            _dither.Dispose();
            _paneEffect.Dispose();
        }
    }
}
