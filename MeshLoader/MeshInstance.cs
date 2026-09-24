using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshLoader
{
    // Borrows a MeshData from the MeshCache (must not outlive it); carries only transform + palette.
    public class MeshInstance
    {

        private static readonly RasterizerState _faceRasterizer;
        private readonly MeshData _meshData;
        private readonly Color[] _palette;
        private OutlineView? _outlineView;   // the outline for the current view; it changes as the mesh or camera moves

        // Off: faces take the background colour (wireframe). Edges are always drawn, always white.
        public bool ColorsOn { get; set; } = true;

        // The state is the source of truth; the world matrix is rebuilt from it, never accumulated.
        public Vector3 Position { get; set; } = new Vector3(0.7f, 0f, 0f);
        public float Pitch { get; set; } = MathHelper.ToRadians(20f);   // fixed jaunty tilt
        public float Yaw { get; set; } = MathHelper.ToRadians(35f);
        public float YawSpeed { get; set; } = MathHelper.ToRadians(45f); // radians per second
        public float PitchSpeed { get; set; } = MathHelper.ToRadians(37f); // radians per second

        public float Scale { get; set; } = 1f;

        // Set, it's the whole world matrix, in place of the one built from Scale, Pitch, Yaw and Position:
        // for something turned in ways those can't say (a box tipped over onto its side).
        public Matrix? Transform { get; set; }

        public Matrix World => Transform ?? Matrix.CreateScale(Scale)
            * Matrix.CreateRotationX(Pitch)
            * Matrix.CreateRotationY(Yaw)
            * Matrix.CreateTranslation(Position);

        public MeshInstance(MeshData meshData, Color[] palette)
        {
            _meshData = meshData ?? throw new ArgumentNullException(nameof(meshData));
            ArgumentNullException.ThrowIfNull(palette);
            if (palette.Length < meshData.PaletteSize)
                throw new ArgumentException($"Palette has {palette.Length} colours but the mesh needs {meshData.PaletteSize}.", nameof(palette));
            _palette = palette;
        }

        // A static constructor is used to initialize static members of the class. It is called automatically before the first instance is created or any static members are referenced.
        static MeshInstance()
        {
            _faceRasterizer = new RasterizerState
            {
                CullMode = CullMode.None,
                DepthBias = 0.0001f, // Nudges the faces slightly away so the edges on their boundaries don't z-fight.
                SlopeScaleDepthBias = 1f,
            };
        }

        public void Update(GameTime gameTime)
        {

        }

        // Faces are always drawn (in their palette colours, or the background colour when colours are
        // off, which gives the wireframe look and hides the lines behind them). Edges are then always
        // drawn on top in white, depth-tested against those faces.
        public void Draw(GameTime gameTime, GraphicsDevice graphicsDevice, BasicEffect basicEffect, Color backgroundColor)
        {
            var world = basicEffect.World;
            var diffuse = basicEffect.DiffuseColor;
            var vertexColor = basicEffect.VertexColorEnabled;
            var previousRasterizer = graphicsDevice.RasterizerState;
            basicEffect.World = World;
            // The buffers are position-only; colour comes from DiffuseColor per draw range.
            basicEffect.VertexColorEnabled = false;
            try
            {
                graphicsDevice.RasterizerState = _faceRasterizer;
                DrawSolid(graphicsDevice, basicEffect, backgroundColor);

                graphicsDevice.RasterizerState = previousRasterizer;
                DrawEdges(graphicsDevice, basicEffect);
                DrawOutline(graphicsDevice, basicEffect);
            }
            finally
            {
                graphicsDevice.RasterizerState = previousRasterizer;
                basicEffect.World = world;
                basicEffect.DiffuseColor = diffuse;
                basicEffect.VertexColorEnabled = vertexColor;
            }
        }

        private void DrawSolid(GraphicsDevice gd, BasicEffect fx, Color backgroundColor)
        {
            gd.SetVertexBuffer(_meshData.Solids);
            foreach (var range in _meshData.SolidRanges)
                DrawRange(gd, fx, ColorsOn ? _palette[range.ColorSlot] : backgroundColor, PrimitiveType.TriangleList, range.Start, range.Primitives);
        }

        private void DrawEdges(GraphicsDevice gd, BasicEffect fx)
        {
            gd.SetVertexBuffer(_meshData.Edges);
            DrawRange(gd, fx, Color.White, PrimitiveType.LineList, 0, _meshData.Edges.VertexCount / 2);
        }

        // For a rounded canopy: its outline as seen from the camera, in white like the edges. It depends on
        // where the eye is, so it is worked out each draw (in the mesh's own space) and not kept in a buffer.
        private void DrawOutline(GraphicsDevice gd, BasicEffect fx)
        {
            var outline = _meshData.Outline;
            if (outline == null)
                return;

            // Only recalculated if the eye has moved relative to the mesh since last time.
            var eye = Vector3.Transform(Matrix.Invert(fx.View).Translation, Matrix.Invert(fx.World));
            _outlineView ??= outline.CreateView();
            _outlineView.Update(eye);
            if (_outlineView.VertexCount == 0)
                return;

            fx.DiffuseColor = Color.White.ToVector3();
            foreach (var pass in fx.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.LineList, _outlineView.Vertices, 0, _outlineView.VertexCount / 2);
            }
        }

        private static void DrawRange(GraphicsDevice gd, BasicEffect fx, Color c,
                                      PrimitiveType type, int startVertex, int primitiveCount)
        {
            fx.DiffuseColor = c.ToVector3();
            foreach (var pass in fx.CurrentTechnique.Passes)
            {
                pass.Apply();                       // must re-apply so the new colour uploads
                gd.DrawPrimitives(type, startVertex, primitiveCount);
            }
        }
    }
}
