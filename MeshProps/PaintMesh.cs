using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // What colour's carried in and kept in, for bringing it back to a world it's drained from (see GameDesign.md 3.3): a drop
    // of it, a barrel to pour it into, and the paint in the barrel.
    // Each is built standing on y = 0, round the y axis.
    public static class PaintMesh
    {
        // Slots, the drop's: its colour, and the light catching it
        public const int Paint = 0, Shine = 1, DropPaletteSize = 2;

        // The barrel's: its staves, its hoops, a stripe round it of the colour it takes, and its inside
        public const int Staves = 0, Hoops = 1, Stripe = 2, Inside = 3, BarrelPaletteSize = 4;

        public const float DropHeight = 0.32f, DropRadius = 0.11f;
        public const float BarrelHeight = 0.9f, BarrelRadius = 0.34f, PaintRadius = 0.3f, PaintBottom = 0.05f, PaintDepth = 0.8f;

        public static Color[] DropPalette(Color paint) => new[] { paint, Color.Lerp(paint, Color.White, 0.5f) };

        public static Color[] BarrelPalette(Color stripe) =>
            new[] { new Color(85, 85, 92), new Color(140, 140, 150), stripe, new Color(30, 30, 34) };

        public static MeshSource Drop(Color paint) => new MeshSource("paint-drop", BuildDrop, DropPalette(paint));
        public static MeshSource Barrel(Color stripe) => new MeshSource("paint-barrel", BuildBarrel, BarrelPalette(stripe));
        public static MeshSource PaintIn(Color paint) => new MeshSource("paint-column", BuildPaint, DropPalette(paint));

        // A drop: round below, drawn up to a point, eight-sided; its upper side catching the light
        public static MeshData BuildDrop(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            (float y, float r)[] rings = { (0.25f, 0.75f), (0.45f, 1f), (0.65f, 0.7f) };
            const int sides = 8;
            Vector3 At(int ring, int k)
            {
                var angle = (k + 0.5f) * MathHelper.TwoPi / sides;
                var (y, r) = rings[ring];
                return new Vector3(DropRadius * r * MathF.Cos(angle), DropHeight * y, DropRadius * r * MathF.Sin(angle));
            }
            var bottom = Vector3.Zero;
            var top = new Vector3(0f, DropHeight, 0f);
            for (var k = 0; k < sides; k++)
            {
                var next = (k + 1) % sides;
                mesh.AddTri(Paint, bottom, At(0, next), At(0, k));
                for (var ring = 0; ring + 1 < rings.Length; ring++)
                    mesh.AddQuad(ring == 0 ? Paint : Shine, At(ring, k), At(ring, next), At(ring + 1, next), At(ring + 1, k));
                mesh.AddTri(Shine, At(rings.Length - 1, k), At(rings.Length - 1, next), top);
                mesh.AddLine(At(1, k), At(1, next));   // its widest ring
            }
            mesh.AddLine(bottom, At(1, 0));
            mesh.AddLine(At(1, sides / 2), top);
            return mesh.Build(device);
        }

        // A steel barrel, bulging a little, with two hoops, a stripe round its middle, and an open top
        public static MeshData BuildBarrel(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const int sides = 12;
            const float half = BarrelHeight / 2f, bulge = BarrelRadius, end = BarrelRadius * 0.9f, stripe = 0.08f;
            mesh.AddFrustum(Vector3.Zero, end, bulge, half - stripe, sides, Staves, bottomSlot: Inside);
            mesh.AddFrustum(new Vector3(0f, half - stripe, 0f), bulge, bulge, 2f * stripe, sides, Stripe);
            mesh.AddFrustum(new Vector3(0f, half + stripe, 0f), bulge, end, half - stripe, sides, Staves);
            foreach (var y in new[] { 0.1f, BarrelHeight - 0.14f })
            {
                var r = MathHelper.Lerp(end, bulge, y < half ? y / half : (BarrelHeight - y) / half) + 0.012f;
                mesh.AddFrustum(new Vector3(0f, y, 0f), r, r, 0.04f, sides, Hoops);
            }
            // The inside: its wall, down to a floor just above the bottom
            const float wall = 0.02f;
            mesh.AddFrustum(new Vector3(0f, PaintBottom - 0.01f, 0f), end - wall, end - wall, BarrelHeight - PaintBottom + 0.01f, sides, Inside,
                            bottomSlot: Inside);
            // and the rim between it and the staves
            for (var k = 0; k < sides; k++)
            {
                Vector3 Rim(int i, float r) => new Vector3(r * MathF.Cos(i * MathHelper.TwoPi / sides), BarrelHeight, r * MathF.Sin(i * MathHelper.TwoPi / sides));
                mesh.AddQuad(Hoops, Rim(k, end), Rim(k + 1, end), Rim(k + 1, end - wall), Rim(k, end - wall));
            }
            return mesh.Build(device);
        }

        // The paint in a barrel: a column a metre tall, just inside its walls, to be squashed in y to how deep it is (at most
        // PaintDepth, full)
        public static MeshData BuildPaint(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddFrustum(Vector3.Zero, PaintRadius, PaintRadius, 1f, 12, Paint, topSlot: Shine);
            return mesh.Build(device);
        }
    }
}
