using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // An angle-poise desk lamp: a round weighted base, a lower arm of two rods leaning back with a coil spring either side,
    // an upper arm of two rods reaching forward, and a conical shade on the end pointing down and forward with its bulb
    // in it. About 0.40 high. It shines towards +Z; the base is centred on X and Z, standing on y = 0 (stand it on a
    // desk: see OfficeDeskMesh.Height).
    public static class DeskLampMesh
    {
        // The enamel (base and shade) has three shades (see MeshBuilder); the rods and joints are one, the bulb another.
        public const int EnamelBase = 0, Metal = 3, Bulb = 4;
        public const int PaletteSize = 5;

        public static Color[] Palette(Color enamel, Color metal, Color bulb)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, EnamelBase, enamel);
            palette[Metal] = metal;
            palette[Bulb] = bulb;
            return palette;
        }

        private static readonly Vector3 Pivot = new Vector3(0f, 0.035f, -0.04f);   // where the lower arm meets the base
        private static readonly Vector3 Elbow = new Vector3(0f, 0.30f, -0.12f);
        private static readonly Vector3 Head = new Vector3(0f, 0.36f, 0.13f);      // where the upper arm holds the shade
        private static readonly Vector3 Aim = Vector3.Normalize(new Vector3(0f, -0.8f, 0.6f));

        public static MeshData Build(GraphicsDevice device)
        {
            const float rod = 0.006f;
            var mesh = new MeshBuilder();

            // The base: a low, wide drum, and a block on it at the back to hold the arm
            mesh.AddFrustum(Vector3.Zero, 0.085f, 0.078f, 0.025f, 12, EnamelBase + MeshBuilder.Side, topSlot: EnamelBase + MeshBuilder.Top);
            mesh.AddBox(EnamelBase, new Vector3(0f, 0.025f, Pivot.Z), 0.03f, 0.045f, 0.02f);

            // Each arm two rods side by side, with a knuckle across them at each joint
            foreach (var side in new[] { -1f, 1f })
            {
                var lower = Vector3.UnitX * (side * 0.012f);
                var upper = Vector3.UnitX * (side * 0.009f);
                mesh.AddTube(Pivot + lower, Elbow + lower, rod, rod, 4, Metal);
                mesh.AddTube(Elbow + upper, Head + upper, rod, rod, 4, Metal);
            }
            foreach (var joint in new[] { Pivot, Elbow, Head })
                mesh.AddTube(joint - Vector3.UnitX * 0.02f, joint + Vector3.UnitX * 0.02f, 0.009f, 0.009f, 6, Metal, ringEdges: true);

            // The springs, drawn as coils of lines, from the base block up most of the lower arm, either side of it
            foreach (var side in new[] { -1f, 1f })
            {
                var offset = Vector3.UnitX * (side * 0.03f);
                Coil(mesh, Pivot + offset + Vector3.UnitZ * 0.012f, Vector3.Lerp(Pivot, Elbow, 0.7f) + offset, 0.005f, 14);
            }

            // The shade: a closed cap behind the head, then a cone flaring out to its mouth; the bulb inside it
            var back = Head - Aim * 0.025f;
            var mouth = Head + Aim * 0.12f;
            mesh.AddTube(back, Head, 0.0005f, 0.026f, 10, EnamelBase + MeshBuilder.Top);
            mesh.AddTube(Head, mouth, 0.026f, 0.07f, 10, EnamelBase + MeshBuilder.Side, ringEdges: true);
            mesh.AddTube(Head + Aim * 0.015f, Head + Aim * 0.07f, 0.016f, 0.022f, 6, Bulb, ringEdges: true);

            return mesh.Build(device);
        }

        // A coil spring from `start` to `end`, `turns` times round, as lines only
        private static void Coil(MeshBuilder mesh, Vector3 start, Vector3 end, float radius, int turns)
        {
            const int perTurn = 6;
            var axis = Vector3.Normalize(end - start);
            var u = Vector3.Normalize(Vector3.Cross(axis, Vector3.UnitX));
            var v = Vector3.Cross(axis, u);
            Vector3 At(int k)
            {
                var angle = k * MathHelper.TwoPi / perTurn;
                return Vector3.Lerp(start, end, k / (float)(turns * perTurn)) + radius * (MathF.Cos(angle) * u + MathF.Sin(angle) * v);
            }
            for (var k = 0; k < turns * perTurn; k++)
                mesh.AddLine(At(k), At(k + 1));
        }
    }
}
