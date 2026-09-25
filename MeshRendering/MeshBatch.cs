using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRendering
{
    // A frame's meshes, drawn together. Each is culled as it's added - left out unless its bounds are in
    // the view frustum - and then every face goes down first, then every edge, so the rasterizer state
    // changes twice a frame rather than twice a mesh. (Edges are depth-tested against faces, so it doesn't
    // matter whose faces are drawn before whose edges.)
    //
    // With fog, set the projection's far plane where the fog ends: past it everything is the fog's colour,
    // which is what the background is cleared to, so it looks the same - and the frustum then culls it.
    public sealed class MeshBatch
    {
        private readonly List<(MeshInstance instance, Matrix world)> _items = new();
        private readonly BoundingFrustum _frustum = new BoundingFrustum(Matrix.Identity);

        // How many were drawn and how many left out, since Begin.
        public int Drawn => _items.Count;
        public int Culled { get; private set; }

        // How many draw calls the last Draw made: one per colour in each mesh, one for its edges, and one for
        // a rounded canopy's outline.
        public int DrawCalls { get; private set; }

        public void Begin(Matrix view, Matrix projection)
        {
            _items.Clear();
            Culled = 0;
            _frustum.Matrix = view * projection;
        }

        // Whether a box in the world can be in view at all: for leaving out whole groups at once.
        public bool InView(BoundingBox bounds) => _frustum.Intersects(bounds);

        // Adds it to be drawn, if it's in view. Returns whether it is.
        public bool Add(MeshInstance instance)
        {
            var world = instance.World;
            if (!_frustum.Intersects(instance.BoundsIn(world)))
            {
                Culled++;
                return false;
            }
            _items.Add((instance, world));
            return true;
        }

        public void Draw(GraphicsDevice device, BasicEffect effect, Color background, bool colorsOn)
        {
            var world = effect.World;
            var diffuse = effect.DiffuseColor;
            var vertexColor = effect.VertexColorEnabled;
            var rasterizer = device.RasterizerState;
            // The buffers are position-only; colour comes from DiffuseColor per draw range.
            effect.VertexColorEnabled = false;
            try
            {
                device.RasterizerState = MeshInstance.FaceRasterizer;
                Color? faces = colorsOn ? null : background;
                DrawCalls = 0;
                foreach (var (instance, placed) in _items)
                {
                    instance.DrawSolids(device, effect, placed, faces);
                    DrawCalls += instance.Mesh.SolidRanges.Length + (instance.Mesh.Edges != null ? 1 : 0) + (instance.Mesh.Outline != null ? 1 : 0);
                }

                device.RasterizerState = rasterizer;
                foreach (var (instance, placed) in _items)
                    instance.DrawEdges(device, effect, placed);
            }
            finally
            {
                device.RasterizerState = rasterizer;
                effect.World = world;
                effect.DiffuseColor = diffuse;
                effect.VertexColorEnabled = vertexColor;
            }
        }
    }
}
