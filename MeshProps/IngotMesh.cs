using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    public static class IngotMesh
    {
        // Palette slots. Solid ranges below are grouped by colour so each is one contiguous draw.
        public const int Top = 0, Side = 1, Other = 2;

        public static Color[] Palette(Color top, Color side, Color other) => new[] { top, side, other };

        // Imagine we imported this from a 3D model, but we can also just define it manually. The ingot is a frustum (truncated pyramid) shape.
        // 0-3: bottom (larger) rectangle. 4-7: top (smaller) rectangle, inset on both
        // axes so the sides slope inward.
        private static readonly Vector3[] Corners =
        {
            new Vector3(-0.6f, -0.3f, -0.3f),
            new Vector3(0.6f, -0.3f, -0.3f),
            new Vector3(0.6f, -0.3f, 0.3f),

            new Vector3(-0.6f, -0.3f, 0.3f),
            new Vector3(-0.4f, 0.3f, -0.2f),
            new Vector3(0.4f, 0.3f, -0.2f),
            new Vector3(0.4f, 0.3f, 0.2f),
            
            new Vector3(-0.4f, 0.3f, 0.2f),
        };

        public static MeshData Build(GraphicsDevice device)
        {
            var (b0, b1, b2, b3) = (Corners[0], Corners[1], Corners[2], Corners[3]);
            var (t0, t1, t2, t3) = (Corners[4], Corners[5], Corners[6], Corners[7]);

            var mesh = new MeshBuilder();
            mesh.AddQuad(Top, t0, t1, t2, t3);
            mesh.AddQuad(Other, b1, b0, b3, b2);   // bottom
            mesh.AddQuad(Side, b0, b1, t1, t0);    // back
            mesh.AddQuad(Side, b2, b3, t3, t2);    // front
            mesh.AddQuad(Side, b3, b0, t0, t3);    // left
            mesh.AddQuad(Side, b1, b2, t2, t1);    // right

            mesh.AddLineLoop(t0, t1, t2, t3);      // top loop
            mesh.AddLineLoop(b0, b1, b2, b3);      // bottom loop
            for (var i = 0; i < 4; i++)
                mesh.AddLine(Corners[i], Corners[4 + i]);   // vertical edges
            return mesh.Build(device);
        }
    }
}
