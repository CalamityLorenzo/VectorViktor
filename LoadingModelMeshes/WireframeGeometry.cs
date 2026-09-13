using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace LoadingModelMeshes
{
    // Turns a loaded MonoGame Model into the data the wireframe renderer needs: a hard-edge
    // line list per mesh, and the model's local bounding sphere (used for camera framing and
    // for spinning a model around its own center rather than the scene origin).
    internal static class WireframeGeometry
    {
        public static Dictionary<ModelMesh, VertexPosition[]> BuildHardEdgeVertices(Model model)
        {
            var result = new Dictionary<ModelMesh, VertexPosition[]>();
            foreach (ModelMesh mesh in model.Meshes)
                result[mesh] = BuildHardEdgeVertices(mesh);
            return result;
        }

        // A mesh edge is "hard" (a real feature of the shape, not a triangulation seam) when
        // it borders only one triangle (a mesh boundary) or when its two adjacent triangles'
        // face normals diverge - i.e. the surface actually creases there. The diagonal MonoGame
        // adds to triangulate a flat quad face has two adjacent triangles with the same normal,
        // so it's correctly excluded.
        private static VertexPosition[] BuildHardEdgeVertices(ModelMesh mesh)
        {
            const float hardAngleDotThreshold = 0.999f;
            var lineVertices = new List<VertexPosition>();

            foreach (ModelMeshPart part in mesh.MeshParts)
            {
                if (part.PrimitiveCount == 0)
                    continue;

                Vector3[] positions = ExtractPositions(part.VertexBuffer);
                int[] indices = ExtractIndices(part.IndexBuffer);
                var edgeNormals = new Dictionary<(int a, int b), List<Vector3>>();

                for (int triangle = 0; triangle < part.PrimitiveCount; triangle++)
                {
                    int baseIndex = part.StartIndex + triangle * 3;
                    int i0 = indices[baseIndex] + part.VertexOffset;
                    int i1 = indices[baseIndex + 1] + part.VertexOffset;
                    int i2 = indices[baseIndex + 2] + part.VertexOffset;

                    Vector3 normal = Vector3.Cross(positions[i1] - positions[i0], positions[i2] - positions[i0]);
                    if (normal.LengthSquared() < 1e-12f)
                        continue; // degenerate triangle
                    normal.Normalize();

                    AddEdgeNormal(edgeNormals, i0, i1, normal);
                    AddEdgeNormal(edgeNormals, i1, i2, normal);
                    AddEdgeNormal(edgeNormals, i2, i0, normal);
                }

                foreach (KeyValuePair<(int a, int b), List<Vector3>> edge in edgeNormals)
                {
                    List<Vector3> normals = edge.Value;
                    bool isHardEdge = normals.Count == 1;
                    for (int i = 0; i < normals.Count && !isHardEdge; i++)
                        for (int j = i + 1; j < normals.Count && !isHardEdge; j++)
                            if (Vector3.Dot(normals[i], normals[j]) < hardAngleDotThreshold)
                                isHardEdge = true;

                    if (isHardEdge)
                    {
                        lineVertices.Add(new VertexPosition(positions[edge.Key.a]));
                        lineVertices.Add(new VertexPosition(positions[edge.Key.b]));
                    }
                }
            }

            return lineVertices.ToArray();
        }

        private static void AddEdgeNormal(Dictionary<(int a, int b), List<Vector3>> edges, int a, int b, Vector3 normal)
        {
            (int a, int b) key = a < b ? (a, b) : (b, a);
            if (!edges.TryGetValue(key, out List<Vector3> normals))
            {
                normals = new List<Vector3>();
                edges[key] = normals;
            }

            normals.Add(normal);
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
