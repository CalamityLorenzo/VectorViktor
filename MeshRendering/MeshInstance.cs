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
        private readonly Vector4[] _colours;   // the palette, as the shader takes it (see PaletteEffect)
        private Vector4[]? _faded;              // and faded to the background, with colours off
        private (Color background, float tint) _fadedFor;
        private OutlineView? _outlineView;   // the outline for the current view; it changes as the mesh or camera moves
        private DynamicVertexBuffer? _outlineBuffer;   // and as sent to the GPU, sent again only when it's changed
        private int _outlineSent = -1;                 // which of the view's versions that is

        public MeshData Mesh { get; }

        // Off: faces take the background colour (wireframe). Edges are always drawn, always white, unless EdgesOn's off.
        public bool ColorsOn { get; set; } = true;

        // Off: only its faces are drawn, none of its lines (the terrain without its grid, say).
        public bool EdgesOn { get; set; } = true;

        // How far off it can be seen at all: something so fine (a cable) that further away it's under a pixel, so the
        // batch leaves it out (see MeshBatch.Add) rather than spend a draw call on nothing. Anywhere, unless it's set.
        public float SeenWithin { get; set; } = float.PositiveInfinity;

        // With colours off, its faces this far from the background colour towards their own (0 to 1): the terrain,
        // without the grid that shows its shape when it's the background colour, faintly shaded instead.
        public float ColorsOffTint { get; set; }

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
            if (meshData.PaletteSize > PaletteEffect.MaxColours)
                throw new ArgumentException($"The mesh uses {meshData.PaletteSize} colours, more than the {PaletteEffect.MaxColours} it can be drawn with.", nameof(meshData));
            _palette = (Color[])palette.Clone();   // its own: recolouring one instance never recolours another
            _colours = new Vector4[Math.Min(_palette.Length, PaletteEffect.MaxColours)];
            for (var slot = 0; slot < _colours.Length; slot++)
                _colours[slot] = _palette[slot].ToVector4();
        }

        // The colour it draws a slot of its palette in.
        public Color GetColor(int slot) => _palette[slot];
        public void SetColor(int slot, Color color)
        {
            if (_palette[slot] == color)
                return;
            _palette[slot] = color;
            if (slot < _colours.Length)
                _colours[slot] = color.ToVector4();
            _faded = null;
        }

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

        // The faces, placed by `world`, in their palette colours or, given one, all in `faces` (or tinted: see ColorsOffTint): in
        // one draw, with the palette shader (see PaletteEffect), which takes its view and fog from `fx`. The caller sets the
        // rasterizer state (FaceRasterizer).
        public void DrawSolids(GraphicsDevice gd, BasicEffect fx, Matrix world, Color? faces)
        {
            ThrowIfDisposed();
            if (Mesh.Solids == null)
                return;
            var effect = PaletteEffect.For(gd);
            effect.Take(fx);
            effect.Draw(gd, Mesh.Solids, world, faces is { } background ? Faded(background) : _colours);
        }

        // Its palette faded to `background`, all but ColorsOffTint of the way: worked out again only when either changes
        private Vector4[] Faded(Color background)
        {
            if (_faded != null && _fadedFor == (background, ColorsOffTint))
                return _faded;
            _faded ??= new Vector4[_colours.Length];
            for (var slot = 0; slot < _faded.Length; slot++)
                _faded[slot] = (ColorsOffTint > 0f ? Color.Lerp(background, _palette[slot], ColorsOffTint) : background).ToVector4();
            _fadedFor = (background, ColorsOffTint);
            return _faded;
        }

        // The edges, placed by `world`, in white, and a rounded canopy's outline as seen from the camera.
        public void DrawEdges(GraphicsDevice gd, BasicEffect fx, Matrix world)
        {
            ThrowIfDisposed();
            if (!EdgesOn)
                return;
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
            var eye = Vector3.Transform(EyeOf(fx.View), Matrix.Invert(fx.World));
            _outlineView ??= outline.CreateView();
            _outlineView.Update(eye);
            var count = _outlineView.VertexCount;
            if (count == 0)
                return;

            // To the GPU only when it's changed (or the GPU's lost what it had)
            _outlineBuffer ??= new DynamicVertexBuffer(gd, typeof(VertexPosition), outline.MaxVertices, BufferUsage.WriteOnly);
            if (_outlineSent != _outlineView.Version || _outlineBuffer.IsContentLost)
            {
                _outlineBuffer.SetData(_outlineView.Vertices, 0, count, SetDataOptions.Discard);
                _outlineSent = _outlineView.Version;
            }
            fx.DiffuseColor = Color.White.ToVector3();
            gd.SetVertexBuffer(_outlineBuffer);
            foreach (var pass in fx.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawPrimitives(PrimitiveType.LineList, 0, count / 2);
            }
        }

        // Where the eye is, in the world, for a view: worked out once for each view, not for every outline drawn in it
        private static Matrix _eyeView;
        private static Vector3 _eye;
        private static Vector3 EyeOf(Matrix view)
        {
            if (view != _eyeView)
                (_eyeView, _eye) = (view, Matrix.Invert(view).Translation);
            return _eye;
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
