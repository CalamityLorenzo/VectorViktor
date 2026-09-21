using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace BasicTests.Meshes
{
    // A bushy tree: a short tapered trunk carrying a canopy of six overlapping low-poly blobs (each a
    // slightly squashed icosahedron), turned by different amounts so the outline looks organic.
    // Faces are shaded by which way they point: up = light, down = dark, the rest in between.
    // Built with the trunk's base on y = 0 (not centred), so it sits on the ground rather than tumbles.
    static class TreeMesh
    {
        // Slots: trunk, then the three leaf shades (see MeshBuilder) starting at LeafBase.
        public const int Trunk = 0, LeafBase = 1;
        public const int PaletteSize = 4;

        public static Color[] Palette(Color trunk, Color leaf)
        {
            var palette = new Color[PaletteSize];
            palette[Trunk] = trunk;
            MeshBuilder.SetBoxShades(palette, LeafBase, leaf);
            return palette;
        }

        // Icosahedron: 12 vertices and 20 triangular faces.
        private static readonly Vector3[] IcoVertices = MakeIcosahedron();
        private static readonly int[][] IcoFaces =
        {
            new[] { 0, 11, 5 }, new[] { 0, 5, 1 },  new[] { 0, 1, 7 },   new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
            new[] { 1, 5, 9 },  new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
            new[] { 3, 9, 4 },  new[] { 3, 4, 2 },  new[] { 3, 2, 6 },   new[] { 3, 6, 8 },  new[] { 3, 8, 9 },
            new[] { 4, 9, 5 },  new[] { 2, 4, 11 }, new[] { 6, 2, 10 },  new[] { 8, 6, 7 },  new[] { 9, 8, 1 },
        };

        private static Vector3[] MakeIcosahedron()
        {
            var t = (1f + MathF.Sqrt(5f)) / 2f;
            var raw = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            };
            for (var i = 0; i < raw.Length; i++)
                raw[i] = Vector3.Normalize(raw[i]);
            return raw;
        }

        // Canopy blobs: centre, radius and how far each is turned about the vertical.
        private static readonly (Vector3 centre, float radius, float yaw)[] Blobs =
        {
            (new Vector3(0.00f, 0.72f, 0.00f), 0.34f, 0.0f),
            (new Vector3(0.24f, 0.58f, 0.05f), 0.22f, 0.4f),
            (new Vector3(-0.22f, 0.60f, -0.08f), 0.22f, 0.9f),
            (new Vector3(0.04f, 0.58f, 0.24f), 0.20f, 1.3f),
            (new Vector3(-0.06f, 0.60f, -0.24f), 0.20f, 0.2f),
            (new Vector3(0.02f, 0.98f, 0.00f), 0.20f, 0.6f),
        };

        private const float Squash = 0.85f;   // blobs are slightly flattened

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();

            // Trunk: only the sides (its ends are hidden by the ground and the canopy). The vertical
            // edges are its silhouette, so they are needed to see it in wireframe.
            mesh.AddFrustum(Vector3.Zero, 0.07f, 0.05f, 0.45f, 6, Trunk, verticalEdges: true);

            // Canopy faces, split by which way they point, so each leaf shade is one contiguous range
            var faceSets = new List<(Vector3 a, Vector3 b, Vector3 c)>[3];   // indexed by MeshBuilder.Side / Dim / Top
            for (var s = 0; s < 3; s++)
                faceSets[s] = new List<(Vector3, Vector3, Vector3)>();
            var edges = new List<(Vector3, Vector3)>();

            foreach (var (centre, radius, yaw) in Blobs)
            {
                var turn = Matrix.CreateRotationY(yaw);
                var local = new Vector3[IcoVertices.Length];   // squashed, around the origin
                var world = new Vector3[IcoVertices.Length];
                for (var i = 0; i < IcoVertices.Length; i++)
                {
                    var v = IcoVertices[i] * radius;
                    v.Y *= Squash;
                    local[i] = v;
                    world[i] = centre + Vector3.Transform(v, turn);
                }

                foreach (var face in IcoFaces)
                {
                    var facing = Vector3.Normalize((local[face[0]] + local[face[1]] + local[face[2]]) / 3f).Y;
                    var shade = facing > 0.4f ? MeshBuilder.Top : facing < -0.25f ? MeshBuilder.Dim : MeshBuilder.Side;
                    faceSets[shade].Add((world[face[0]], world[face[1]], world[face[2]]));
                }

                // Each edge once (shared by two faces)
                var seen = new HashSet<(int, int)>();
                foreach (var face in IcoFaces)
                    for (var k = 0; k < 3; k++)
                    {
                        var a = face[k];
                        var b = face[(k + 1) % 3];
                        if (seen.Add((Math.Min(a, b), Math.Max(a, b))))
                            edges.Add((world[a], world[b]));
                    }
            }

            for (var shade = 0; shade < 3; shade++)
            {
                if (faceSets[shade].Count == 0)
                    continue;
                mesh.AddSolidRange(faceSets[shade].Count, LeafBase + shade);
                foreach (var (a, b, c) in faceSets[shade])
                    mesh.AddTri(a, b, c);
            }
            foreach (var (a, b) in edges)
                mesh.AddLine(a, b);

            return mesh.Build(device);
        }
    }
}
