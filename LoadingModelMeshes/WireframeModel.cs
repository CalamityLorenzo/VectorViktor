using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LoadingModelMeshes
{
    // How a WireframeModel's visible edges are determined.
    public enum WireframeMode
    {
        // Static, precomputed once per model asset: edges that border only one triangle or
        // whose adjacent faces' normals diverge past a threshold - real creases in the shape.
        HardEdge,

        // Recomputed every frame from the current viewpoint: edges where one adjacent triangle
        // faces the viewer and the other faces away. Gives a doubly-curved shape (e.g. a torus,
        // which has no flat faces for HardEdge to collapse) a clean contour outline instead of
        // its full triangulation grid.
        Silhouette
    }

    // One placed instance of a Model in the wireframe scene: the model asset, its precomputed
    // edge cache (shape depends on Mode), and its own position/spin. Several instances can
    // share the same Model (and its edge cache) - the edge extraction only needs to run once
    // per asset per mode.
    public class WireframeModel
    {
        public Model Model { get; }
        public WireframeMode Mode { get; }

        // Populated when Mode == HardEdge; null otherwise.
        public IReadOnlyDictionary<ModelMesh, VertexPosition[]> HardEdgeVerticesByMesh { get; }

        // Populated when Mode == Silhouette; null otherwise. Re-evaluated every frame via
        // WireframeGeometry.ComputeSilhouetteEdgeVertices, since which candidates currently lie
        // on the silhouette depends on the viewpoint.
        public IReadOnlyDictionary<ModelMesh, WireframeGeometry.SilhouetteEdgeCandidate[]> SilhouetteCandidatesByMesh { get; }

        public BoundingSphere LocalBounds { get; }

        public Vector3 Position { get; set; }
        public float RotationY { get; set; }

        public WireframeModel(
            Model model,
            WireframeMode mode,
            IReadOnlyDictionary<ModelMesh, VertexPosition[]> hardEdgeVerticesByMesh,
            IReadOnlyDictionary<ModelMesh, WireframeGeometry.SilhouetteEdgeCandidate[]> silhouetteCandidatesByMesh,
            BoundingSphere localBounds,
            Vector3 position = default)
        {
            Model = model;
            Mode = mode;
            HardEdgeVerticesByMesh = hardEdgeVerticesByMesh;
            SilhouetteCandidatesByMesh = silhouetteCandidatesByMesh;
            LocalBounds = localBounds;
            Position = position;
        }

        // Spins the model around its own bounding-sphere center rather than the scene origin,
        // then places it at Position.
        public Matrix GetWorldMatrix()
        {
            return Matrix.CreateTranslation(-LocalBounds.Center)
                 * Matrix.CreateRotationY(RotationY)
                 * Matrix.CreateTranslation(LocalBounds.Center)
                 * Matrix.CreateTranslation(Position);
        }

        public BoundingSphere GetWorldBounds()
        {
            return new BoundingSphere(LocalBounds.Center + Position, LocalBounds.Radius);
        }
    }
}
