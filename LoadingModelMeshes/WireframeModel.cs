using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LoadingModelMeshes
{
    // One placed instance of a Model in the wireframe scene: the model asset, its precomputed
    // hard-edge line cache, and its own position/spin. Several instances can share the same
    // Model (and its edge cache) - the edge extraction only needs to run once per asset.
    public class WireframeModel
    {
        public Model Model { get; }
        public IReadOnlyDictionary<ModelMesh, VertexPosition[]> EdgeVerticesByMesh { get; }
        public BoundingSphere LocalBounds { get; }

        public Vector3 Position { get; set; }
        public float RotationY { get; set; }

        public WireframeModel(
            Model model,
            IReadOnlyDictionary<ModelMesh, VertexPosition[]> edgeVerticesByMesh,
            BoundingSphere localBounds,
            Vector3 position = default)
        {
            Model = model;
            EdgeVerticesByMesh = edgeVerticesByMesh;
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
