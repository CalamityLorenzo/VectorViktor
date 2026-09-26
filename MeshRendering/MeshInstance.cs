using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRendering
{
    // Borrows a MeshData from the MeshCache (must not outlive it); carries only transform + palette.
    // It stands where it's put: upright, unturned, at the origin, until told otherwise.
    public class MeshInstance
    {
        // Faces are drawn with this: no culling, and nudged slightly away so the edges on their boundaries don't z-fight.
        public static readonly RasterizerState FaceRasterizer = new RasterizerState
        {
            CullMode = CullMode.None,
            DepthBias = 0.0001f,
            SlopeScaleDepthBias = 1f,
        };

        private readonly Color[] _palette;
        private OutlineView? _outlineView;   // the outline for the current view; it changes as the mesh or camera moves

        public MeshData Mesh { get; }

        // Off: faces take the background colour (wireframe). Edges are always drawn, always white.
        public bool ColorsOn { get; set; } = true;

        // The state is the source of truth; the world matrix is rebuilt from it, never accumulated.
        public Vector3 Position { get; set; }
        public float Pitch { get; set; }
        public float Yaw { get; set; }
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
            Mesh = meshData ?? throw new ArgumentNullException(nameof(meshData));
            ArgumentNullException.ThrowIfNull(palette);
            if (palette.Length < meshData.PaletteSize)
                throw new ArgumentException($"Palette has {palette.Length} colours but the mesh needs {meshData.PaletteSize}.", nameof(palette));
            _palette = (Color[])palette.Clone();   // its own: recolouring one instance never recolours another
        }

        // The colour it draws a slot of its palette in.
        public Color GetColor(int slot) => _palette[slot];
        public void SetColor(int slot, Color color) => _palette[slot] = color;

        // The mesh's bounds, as placed in the world by `world` (still axis-aligned, so a turned mesh's is a
        // little bigger than it): the box's centre moved, its half-size spread over the axes it's turned onto.
        public BoundingBox BoundsIn(Matrix world)
        {
            var box = Mesh.Bounds;
            var centre = Vector3.Transform((box.Min + box.Max) * 0.5f, world);
            var half = (box.Max - box.Min) * 0.5f;
            var extent = new Vector3(
                MathF.Abs(world.M11) * half.X + MathF.Abs(world.M21) * half.Y + MathF.Abs(world.M31) * half.Z,
                MathF.Abs(world.M12) * half.X + MathF.Abs(world.M22) * half.Y + MathF.Abs(world.M32) * half.Z,
                MathF.Abs(world.M13) * half.X + MathF.Abs(world.M23) * half.Y + MathF.Abs(world.M33) * half.Z);
            return new BoundingBox(centre - extent, centre + extent);
        }

        // Faces are always drawn (in their palette colours, or the background colour when colours are
        // off, which gives the wireframe look and hides the lines behind them). Edges are then always
        // drawn on top in white, depth-tested against those faces. To draw many at once, faces first and
        // then edges, and only those in view, see MeshBatch.
        public void Draw(GameTime gameTime, GraphicsDevice graphicsDevice, BasicEffect basicEffect, Color backgroundColor)
        {
            var world = basicEffect.World;
            var diffuse = basicEffect.DiffuseColor;
            var vertexColor = basicEffect.VertexColorEnabled;
            var previousRasterizer = graphicsDevice.RasterizerState;
            var placed = World;
            // The buffers are position-only; colour comes from DiffuseColor per draw range.
            basicEffect.VertexColorEnabled = false;
            try
            {
                graphicsDevice.RasterizerState = FaceRasterizer;
                DrawSolids(graphicsDevice, basicEffect, placed, ColorsOn ? null : backgroundColor);

                graphicsDevice.RasterizerState = previousRasterizer;
                DrawEdges(graphicsDevice, basicEffect, placed);
            }
            finally
            {
                graphicsDevice.RasterizerState = previousRasterizer;
                basicEffect.World = world;
                basicEffect.DiffuseColor = diffuse;
                basicEffect.VertexColorEnabled = vertexColor;
            }
        }

        // The faces, placed by `world`, in their palette colours or, given one, all in `faces`. The caller sets
        // the rasterizer state (FaceRasterizer) and turns the effect's vertex colours off.
        public void DrawSolids(GraphicsDevice gd, BasicEffect fx, Matrix world, Color? faces)
        {
            ThrowIfDisposed();
            if (Mesh.Solids == null)
                return;
            fx.World = world;
            gd.SetVertexBuffer(Mesh.Solids);
            foreach (var range in Mesh.SolidRanges)
                DrawRange(gd, fx, faces ?? _palette[range.ColorSlot], PrimitiveType.TriangleList, range.Start, range.Primitives);
        }

        // The edges, placed by `world`, in white, and a rounded canopy's outline as seen from the camera.
        public void DrawEdges(GraphicsDevice gd, BasicEffect fx, Matrix world)
        {
            ThrowIfDisposed();
            fx.World = world;
            if (Mesh.Edges != null)
            {
                gd.SetVertexBuffer(Mesh.Edges);
                DrawRange(gd, fx, Color.White, PrimitiveType.LineList, 0, Mesh.Edges.VertexCount / 2);
            }
            DrawOutline(gd, fx);
        }

        // An instance outliving the mesh it borrowed (its cache, or a terrain chunk thrown away) would draw
        // freed buffers: say so plainly instead.
        private void ThrowIfDisposed()
        {
            if (Mesh.IsHeadless)
                throw new InvalidOperationException("This instance's mesh is headless (built with no graphics device, for a test): it can't be drawn.");
            if (Mesh.IsDisposed)
                throw new ObjectDisposedException(nameof(MeshData), "This instance's mesh has been disposed; the instance has outlived its owner.");
        }

        // For a rounded canopy: its outline as seen from the camera, in white like the edges. It depends on
        // where the eye is, so it is worked out each draw (in the mesh's own space) and not kept in a buffer.
        private void DrawOutline(GraphicsDevice gd, BasicEffect fx)
        {
            var outline = Mesh.Outline;
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
