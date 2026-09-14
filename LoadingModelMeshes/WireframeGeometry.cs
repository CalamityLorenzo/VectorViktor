using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LoadingModelMeshes
{
    // Turns a loaded MonoGame Model into the data the wireframe renderer needs: edge data per
    // mesh (either a static hard-edge line list, or dynamic silhouette-edge candidates), and
    // the model's local bounding sphere (used for camera framing and for spinning a model
    // around its own center rather than the scene origin).
    public static class WireframeGeometry
    {
        // How divergent two adjacent triangles' face normals must be for the edge between them
        // to be a real feature of the shape rather than a triangulation seam. A flat quad face's
        // diagonal has two adjacent triangles with the same normal (dot ~= 1), so it's excluded.
        private const float HardAngleDotThreshold = 0.999f;

        // One candidate edge: its endpoints, and the face normal + centroid of each triangle
        // that borders it (one entry if it's a mesh boundary, normally two otherwise). Used to
        // decide, per frame and from a given (local-space) viewpoint, whether the edge currently
        // lies on the model's silhouette - i.e. one adjacent triangle faces the viewer and the
        // other faces away.
        public readonly struct SilhouetteEdgeCandidate
        {
            public readonly Vector3 A;
            public readonly Vector3 B;
            public readonly bool IsBoundary;
            public readonly Vector3[] Normals;
            public readonly Vector3[] Centroids;

            public SilhouetteEdgeCandidate(Vector3 a, Vector3 b, bool isBoundary, Vector3[] normals, Vector3[] centroids)
            {
                A = a;
                B = b;
                IsBoundary = isBoundary;
                Normals = normals;
                Centroids = centroids;
            }
        }

        // One edge accumulated while walking a mesh part's triangles: its two endpoint positions,
        // and the face normal + centroid of every triangle found bordering it.
        private sealed class EdgeAccumulator
        {
            public Vector3 A;
            public Vector3 B;
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector3> Centroids = new List<Vector3>();
        }

        public static Dictionary<ModelMesh, VertexPosition[]> BuildHardEdgeVertices(Model model)
        {
            var result = new Dictionary<ModelMesh, VertexPosition[]>();
            foreach (ModelMesh mesh in model.Meshes)
                result[mesh] = BuildHardEdgeVertices(mesh);
            return result;
        }

        // A mesh edge is "hard" (a real feature of the shape, not a triangulation seam) when
        // it borders only one triangle (a mesh boundary) or when its two adjacent triangles'
        // face normals diverge past HardAngleDotThreshold.
        private static VertexPosition[] BuildHardEdgeVertices(ModelMesh mesh)
        {
            var lineVertices = new List<VertexPosition>();

            foreach (var edge in EnumerateCandidateEdges(mesh))
            {
                lineVertices.Add(new VertexPosition(edge.A));
                lineVertices.Add(new VertexPosition(edge.B));
            }

            return lineVertices.ToArray();
        }

        public static Dictionary<ModelMesh, SilhouetteEdgeCandidate[]> BuildSilhouetteCandidates(Model model)
        {
            var result = new Dictionary<ModelMesh, SilhouetteEdgeCandidate[]>();
            foreach (ModelMesh mesh in model.Meshes)
                result[mesh] = BuildSilhouetteCandidates(mesh);
            return result;
        }

        private static SilhouetteEdgeCandidate[] BuildSilhouetteCandidates(ModelMesh mesh)
        {
            var candidates = new List<SilhouetteEdgeCandidate>();

            foreach (var edge in EnumerateCandidateEdges(mesh))
                candidates.Add(new SilhouetteEdgeCandidate(edge.A, edge.B, edge.IsBoundary, edge.Normals, edge.Centroids));

            return candidates.ToArray();
        }

        // Picks out the same "real feature of the shape" edges BuildHardEdgeVertices always
        // draws - a mesh boundary, or a crease past HardAngleDotThreshold - but returns each
        // one's face normals/centroids instead of committing it to a line, so the caller can
        // either draw it unconditionally (hard edges) or test it against a viewpoint each frame
        // (silhouette edges).
        private static IEnumerable<(Vector3 A, Vector3 B, bool IsBoundary, Vector3[] Normals, Vector3[] Centroids)> EnumerateCandidateEdges(ModelMesh mesh)
        {
            foreach (ModelMeshPart part in mesh.MeshParts)
            {
                if (part.PrimitiveCount == 0)
                    continue;

                Vector3[] positions = ExtractPositions(part.VertexBuffer);
                int[] indices = ExtractIndices(part.IndexBuffer);

                // Keyed by (quantized) position rather than vertex index: content pipelines
                // routinely duplicate a vertex at a UV seam or a smoothing-group split, so two
                // triangles that share an edge in space often DON'T share a vertex index for it.
                // Matching by index would then see every such edge as bordering only one
                // triangle - a false "mesh boundary" - and draw it unconditionally regardless of
                // mode. Matching by position finds the true adjacency instead.
                var edges = new Dictionary<(Vector3 a, Vector3 b), EdgeAccumulator>();

                for (int triangle = 0; triangle < part.PrimitiveCount; triangle++)
                {
                    int baseIndex = part.StartIndex + triangle * 3;
                    int i0 = indices[baseIndex] + part.VertexOffset;
                    int i1 = indices[baseIndex + 1] + part.VertexOffset;
                    int i2 = indices[baseIndex + 2] + part.VertexOffset;
                    Vector3 p0 = positions[i0];
                    Vector3 p1 = positions[i1];
                    Vector3 p2 = positions[i2];

                    Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
                    if (normal.LengthSquared() < 1e-12f)
                        continue; // degenerate triangle
                    normal.Normalize();
                    Vector3 centroid = (p0 + p1 + p2) / 3f;

                    AddEdgeFace(edges, p0, p1, normal, centroid);
                    AddEdgeFace(edges, p1, p2, normal, centroid);
                    AddEdgeFace(edges, p2, p0, normal, centroid);
                }

                foreach (EdgeAccumulator edge in edges.Values)
                {
                    List<Vector3> normals = edge.Normals;
                    bool isBoundary = normals.Count == 1;
                    bool isHardEdge = isBoundary;
                    for (int i = 0; i < normals.Count && !isHardEdge; i++)
                        for (int j = i + 1; j < normals.Count && !isHardEdge; j++)
                            if (Vector3.Dot(normals[i], normals[j]) < HardAngleDotThreshold)
                                isHardEdge = true;

                    if (isHardEdge)
                        yield return (edge.A, edge.B, isBoundary, normals.ToArray(), edge.Centroids.ToArray());
                }
            }
        }

        // Given a mesh's silhouette-edge candidates and the current viewpoint (in that mesh's
        // local space), returns the edges that presently lie on the silhouette: a boundary edge
        // always qualifies, and an interior edge qualifies when its adjacent triangles aren't
        // all facing the viewpoint the same way (one faces it, the other faces away). Recompute
        // this every frame - which edges qualify changes as the camera or model turns.
        public static VertexPosition[] ComputeSilhouetteEdgeVertices(SilhouetteEdgeCandidate[] candidates, Vector3 viewPositionLocal)
        {
            var lineVertices = new List<VertexPosition>();

            foreach (SilhouetteEdgeCandidate candidate in candidates)
            {
                bool isSilhouette = candidate.IsBoundary;
                if (!isSilhouette)
                {
                    int firstSign = Math.Sign(Vector3.Dot(candidate.Normals[0], viewPositionLocal - candidate.Centroids[0]));
                    for (int i = 1; i < candidate.Normals.Length; i++)
                    {
                        int sign = Math.Sign(Vector3.Dot(candidate.Normals[i], viewPositionLocal - candidate.Centroids[i]));
                        if (sign != firstSign)
                        {
                            isSilhouette = true;
                            break;
                        }
                    }
                }

                if (isSilhouette)
                {
                    lineVertices.Add(new VertexPosition(candidate.A));
                    lineVertices.Add(new VertexPosition(candidate.B));
                }
            }

            return lineVertices.ToArray();
        }

        private static void AddEdgeFace(Dictionary<(Vector3 a, Vector3 b), EdgeAccumulator> edges, Vector3 a, Vector3 b, Vector3 normal, Vector3 centroid)
        {
            Vector3 qa = Quantize(a);
            Vector3 qb = Quantize(b);
            bool aFirst = ComparePositions(qa, qb) <= 0;
            (Vector3 a, Vector3 b) key = aFirst ? (qa, qb) : (qb, qa);

            if (!edges.TryGetValue(key, out EdgeAccumulator accumulator))
            {
                // Keep the first occurrence's un-quantized positions as the line's endpoints.
                accumulator = new EdgeAccumulator { A = aFirst ? a : b, B = aFirst ? b : a };
                edges[key] = accumulator;
            }

            accumulator.Normals.Add(normal);
            accumulator.Centroids.Add(centroid);
        }

        // Snaps a position onto a coarse grid so vertices that are supposed to coincide (but may
        // differ in the last bit or two after export/import round-tripping) hash and compare
        // equal. Tight enough not to merge genuinely distinct nearby vertices on any reasonably
        // scaled model.
        private static Vector3 Quantize(Vector3 v)
        {
            const float stepsPerUnit = 10000f; // ~4 decimal places
            return new Vector3(
                MathF.Round(v.X * stepsPerUnit) / stepsPerUnit,
                MathF.Round(v.Y * stepsPerUnit) / stepsPerUnit,
                MathF.Round(v.Z * stepsPerUnit) / stepsPerUnit);
        }

        // Arbitrary but consistent total order over positions, just so an edge between two
        // vertices hashes the same way regardless of which triangle visits it first (A,B) vs
        // (B,A).
        private static int ComparePositions(Vector3 a, Vector3 b)
        {
            int byX = a.X.CompareTo(b.X);
            if (byX != 0)
                return byX;
            int byY = a.Y.CompareTo(b.Y);
            return byY != 0 ? byY : a.Z.CompareTo(b.Z);
        }

        private static Vector3[] ExtractPositions(VertexBuffer vertexBuffer)
        {
            VertexElement[] elements = vertexBuffer.VertexDeclaration.GetVertexElements();
            VertexElement positionElement = default;
            foreach (VertexElement element in elements)
            {
                if (element.VertexElementUsage == VertexElementUsage.Position && element.UsageIndex == 0)
                {
                    positionElement = element;
                    break;
                }
            }

            int stride = vertexBuffer.VertexDeclaration.VertexStride;
            int count = vertexBuffer.VertexCount;
            byte[] raw = new byte[stride * count];
            vertexBuffer.GetData(raw);

            var positions = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int offset = i * stride + positionElement.Offset;
                positions[i] = new Vector3(
                    BitConverter.ToSingle(raw, offset),
                    BitConverter.ToSingle(raw, offset + 4),
                    BitConverter.ToSingle(raw, offset + 8));
            }

            return positions;
        }

        private static int[] ExtractIndices(IndexBuffer indexBuffer)
        {
            int count = indexBuffer.IndexCount;
            var indices = new int[count];

            if (indexBuffer.IndexElementSize == IndexElementSize.SixteenBits)
            {
                var raw = new ushort[count];
                indexBuffer.GetData(raw);
                for (int i = 0; i < count; i++)
                    indices[i] = raw[i];
            }
            else
            {
                indexBuffer.GetData(indices);
            }

            return indices;
        }

        public static BoundingSphere ComputeLocalBounds(Model model)
        {
            Matrix[] boneTransforms = new Matrix[model.Bones.Count];
            model.CopyAbsoluteBoneTransformsTo(boneTransforms);

            BoundingSphere sphere = new BoundingSphere(Vector3.Zero, 0);
            foreach (ModelMesh mesh in model.Meshes)
            {
                BoundingSphere transformed = mesh.BoundingSphere.Transform(boneTransforms[mesh.ParentBone.Index]);
                sphere = BoundingSphere.CreateMerged(sphere, transformed);
            }

            return sphere;
        }
    }
}
