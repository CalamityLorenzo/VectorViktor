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
        private Vector3 _eye;

        // How many were drawn and how many left out, since Begin.
        public int Drawn => _items.Count;
        public int Culled { get; private set; }

        // The smallest a mesh can look and still be drawn: how wide its bounds must be, as a fraction of how far off they
        // are (a pixel's width, at the distance of one metre). Anything narrower would cover less than a pixel and cost a
        // draw call or more for a speck: the parts of a droid fifty metres off. 0, the default, draws everything.
        public float SmallestSeen { get; set; }

        // How many draw calls the last Draw made: one for each mesh's faces (all its colours at once: see PaletteEffect),
        // one for its edges, and one for a rounded canopy's outline.
        public int DrawCalls { get; private set; }

        public void Begin(Matrix view, Matrix projection)
        {
            _items.Clear();
            Culled = 0;
            _frustum.Matrix = view * projection;
            _eye = Matrix.Invert(view).Translation;
        }

        // Whether a box in the world can be in view at all: for leaving out whole groups at once.
        public bool InView(BoundingBox bounds) => _frustum.Intersects(bounds);

        // Adds it to be drawn, if it's in view, near enough to be seen (see MeshInstance.SeenWithin) and big enough to be (see
        // SmallestSeen). Returns whether it is.
        public bool Add(MeshInstance instance)
        {
            var world = instance.World;
            var bounds = instance.BoundsIn(world);
            if (!_frustum.Intersects(bounds) || TooFarOrSmall(instance, bounds))
            {
                Culled++;
                return false;
            }
            _items.Add((instance, world));
            return true;
        }

        private bool TooFarOrSmall(MeshInstance instance, BoundingBox bounds)
        {
            var far = instance.SeenWithin;
            if (far == float.PositiveInfinity && SmallestSeen <= 0f)
                return false;
            var distanceSquared = Vector3.DistanceSquared(_eye, (bounds.Min + bounds.Max) * 0.5f);
            if (distanceSquared > far * far)
                return true;
            var wide = (bounds.Max - bounds.Min).Length();
            return wide * wide < SmallestSeen * SmallestSeen * distanceSquared;
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
                    DrawCalls += (instance.Mesh.Solids != null ? 1 : 0) +
                        (instance.EdgesOn ? (instance.Mesh.Edges != null ? 1 : 0) + (instance.Mesh.Outline != null ? 1 : 0) : 0);
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
