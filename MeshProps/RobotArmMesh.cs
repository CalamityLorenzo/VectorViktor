using MeshCore.Library;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A giant robot's right arm, torn off at the shoulder, lying on its front: in the same boxy Transformers style as
    // RobotHeadMesh, and to the same scale. From the shoulder, out along +Z: the shoulder block, the upper arm, the
    // elbow's round joint, the chunky forearm with a panel and vents on its back, the wrist, and the hand, palm down,
    // its four fingers in three blocks each and its thumb out to the left. Cables trail from the torn shoulder, back
    // along -Z. The shoulder's end is at z = 0, centred on X, everything lying on y = 0; about 4.1 long.
    public static class RobotArmMesh
    {
        // The armour and the joints and hand have three shades each (see MeshBuilder); the cables one
        public const int ArmourBase = 0, MetalBase = 3, Cable = 6;
        public const int PaletteSize = 7;

        // Its parts' tops, to stand on: each (x, z) from one corner to the other, and how high it is
        public static readonly (Vector2 min, Vector2 max, float top)[] Tops =
        {
            (new Vector2(-0.45f, 0f), new Vector2(0.45f, 0.8f), 0.8f),          // the shoulder
            (new Vector2(-0.23f, 0.8f), new Vector2(0.23f, 1.6f), 0.42f),       // the upper arm
            (new Vector2(-0.26f, 1.5f), new Vector2(0.26f, 2.0f), 0.48f),       // the elbow, flat enough on top
            (new Vector2(-0.3f, 1.9f), new Vector2(0.3f, 3.0f), 0.55f),         // the forearm
            (new Vector2(-0.19f, 3.0f), new Vector2(0.19f, 3.15f), 0.32f),      // the wrist
            (new Vector2(-0.27f, 3.15f), new Vector2(0.27f, 3.52f), 0.22f),     // the palm
            (new Vector2(-0.28f, 3.52f), new Vector2(0.28f, 3.62f), 0.28f),     // the knuckles
            (new Vector2(-0.27f, 3.61f), new Vector2(0.27f, 4.08f), 0.13f),     // the fingers, as one
            (new Vector2(-0.49f, 3.2f), new Vector2(-0.27f, 3.5f), 0.14f),      // the thumb
        };

        public static Color[] Palette(Color armour, Color metal, Color cable)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, ArmourBase, armour);
            MeshBuilder.SetBoxShades(palette, MetalBase, metal);
            palette[Cable] = cable;
            return palette;
        }

        public static MeshSource Source(Color[] palette) => new MeshSource("robot-arm", Build, palette);

        // A box from z0 to z1, `width` across and `height` high, on the ground
        private static void Block(MeshBuilder mesh, int baseSlot, float x, float z0, float z1, float width, float height, float y = 0f) =>
            mesh.AddBox(baseSlot, new Vector3(x, y, (z0 + z1) / 2f), z1 - z0, width, height, sealBottom: true);

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            const float lift = 0.004f;   // how far a line drawn on a face stands off it

            // ---- The shoulder: a big block, with a panel on its top, and a rim round its torn end
            Block(mesh, ArmourBase, 0f, 0f, 0.8f, 0.9f, 0.8f);
            const float top = 0.8f + lift;
            mesh.AddLineLoop(new Vector3(-0.3f, top, 0.15f), new Vector3(0.3f, top, 0.15f), new Vector3(0.3f, top, 0.65f), new Vector3(-0.3f, top, 0.65f));
            mesh.AddLineLoop(new Vector3(-0.35f, 0.1f, -lift), new Vector3(0.35f, 0.1f, -lift), new Vector3(0.35f, 0.7f, -lift), new Vector3(-0.35f, 0.7f, -lift));

            // Cables trailing from it, sagging down to the ground, their ends capped
            foreach (var (x, y, reach) in new[] { (-0.18f, 0.45f, 0.55f), (0.05f, 0.55f, 0.7f), (0.2f, 0.3f, 0.4f) })
                AddCable(mesh, new Vector3(x, y, 0.05f), new Vector3(x * 1.5f, 0.05f, -reach), 0.05f);

            // ---- The upper arm, and the elbow's round joint
            Block(mesh, ArmourBase, 0f, 0.8f, 1.6f, 0.46f, 0.42f);
            SlabBuilder.AddRoundBar(mesh, MetalBase, -0.26f, 0.26f, new Vector2(1.75f, 0.24f), 0.26f, 8);

            // ---- The forearm, chunkier, with a panel and three vents on its back
            Block(mesh, ArmourBase, 0f, 1.9f, 3.0f, 0.6f, 0.55f);
            const float back = 0.55f + lift;
            mesh.AddLineLoop(new Vector3(-0.2f, back, 2.0f), new Vector3(0.2f, back, 2.0f), new Vector3(0.2f, back, 2.45f), new Vector3(-0.2f, back, 2.45f));
            foreach (var z in new[] { 2.6f, 2.7f, 2.8f })
                mesh.AddLine(new Vector3(-0.18f, back, z), new Vector3(0.18f, back, z));

            // ---- The wrist, and the hand: its palm, the ridge of its knuckles, four fingers of three blocks
            Block(mesh, MetalBase, 0f, 3.0f, 3.15f, 0.38f, 0.32f);
            Block(mesh, MetalBase, 0f, 3.15f, 3.62f, 0.54f, 0.22f);
            Block(mesh, MetalBase, 0f, 3.52f, 3.62f, 0.56f, 0.06f, y: 0.22f);
            const float fingerWidth = 0.125f, gap = 0.015f, overlap = 0.01f;   // each block tucked into the one before
            float[] lengths = { 0.18f, 0.14f, 0.12f }, heights = { 0.15f, 0.13f, 0.11f };
            for (var f = 0; f < 4; f++)
            {
                var x = -0.21f + f * (fingerWidth + gap);
                var z = 3.62f - overlap;
                var shorter = f == 3 ? 0.03f : f == 1 ? -0.02f : 0f;   // the little finger shortest, the middle longest
                for (var k = 0; k < 3; k++)
                {
                    var length = lengths[k] - shorter;
                    Block(mesh, MetalBase, x, z, z + length, fingerWidth, heights[k]);
                    z += length - overlap;
                }
            }

            // The thumb, out to the left from the palm's inner side, then turning forward
            Block(mesh, MetalBase, -0.335f, 3.2f, 3.34f, 0.13f, 0.14f);
            Block(mesh, MetalBase, -0.44f, 3.3f, 3.5f, 0.1f, 0.12f);

            return mesh.Build(device);
        }

        // A six-sided cable from `start` to `end`, closed at its end
        private static void AddCable(MeshBuilder mesh, Vector3 start, Vector3 end, float radius)
        {
            mesh.AddTube(start, end, radius, radius, 6, Cable);
            // Its end, as AddTube rings it
            var axis = Vector3.Normalize(end - start);
            var u = Vector3.Normalize(Vector3.Cross(axis, MathF.Abs(axis.Y) < 0.9f ? Vector3.Up : Vector3.UnitX));
            var v = Vector3.Cross(axis, u);
            var ring = new Vector3[6];
            for (var k = 0; k < 6; k++)
            {
                var angle = (k + 0.5f) * MathHelper.TwoPi / 6;
                ring[k] = end + radius * (MathF.Cos(angle) * u + MathF.Sin(angle) * v);
            }
            mesh.AddPolygon(Cable, ring);
            mesh.AddLineLoop(ring);
        }
    }
}
