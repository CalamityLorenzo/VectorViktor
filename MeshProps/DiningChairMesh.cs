using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A 1950s dining chair to go with the dining table: a teak seat frame with an upholstered pad, two uprights at the
    // back leaning back a little with a curved-looking rail of three pieces across them, on four splayed tapered legs.
    // 0.46 wide, 0.47 deep, 0.45 to the seat, 0.84 to the top of its back. Its seat faces +Z (you sit looking that way),
    // centred on X and Z, standing on y = 0.
    public static class DiningChairMesh
    {
        // The frame and the pad have three shades each (see MeshBuilder); the legs one.
        public const int FrameBase = 0, SeatBase = 3, Leg = 6;
        public const int PaletteSize = 7;

        public const float SeatHeight = 0.45f;

        public static Color[] Palette(Color frame, Color seat, Color leg)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, FrameBase, frame);
            MeshBuilder.SetBoxShades(palette, SeatBase, seat);
            palette[Leg] = leg;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float width = 0.44f, depth = 0.42f, frameHeight = 0.04f, padHeight = 0.04f;
            const float frameBottom = SeatHeight - frameHeight - padHeight;
            const float back = -depth / 2f;
            var mesh = new MeshBuilder();

            // The seat frame, sealed underneath, and the pad on it, a little in from its edges
            mesh.AddBox(FrameBase, new Vector3(0f, frameBottom, 0f), depth, width, frameHeight, sealBottom: true);
            mesh.AddBox(SeatBase, new Vector3(0f, frameBottom + frameHeight, 0.01f), depth - 0.04f, width - 0.03f, padHeight);

            // The back: two uprights rising from the frame's back corners, leaning back, and a rail across their tops,
            // its ends swept forward a little
            const float lean = 0.06f, top = 0.84f, railHeight = 0.09f;
            var feet = new[] { new Vector3(-width / 2f + 0.025f, frameBottom, back + 0.02f), new Vector3(width / 2f - 0.025f, frameBottom, back + 0.02f) };
            foreach (var foot in feet)
                mesh.AddTube(foot, new Vector3(foot.X, top - railHeight / 2f, foot.Z - lean), 0.016f, 0.014f, 4, FrameBase + MeshBuilder.Side);
            var railZ = back + 0.02f - lean;
            mesh.AddBox(FrameBase, new Vector3(0f, top - railHeight, railZ), 0.025f, 0.26f, railHeight, sealBottom: true);
            foreach (var side in new[] { -1f, 1f })
                mesh.AddBox(FrameBase, new Vector3(side * 0.17f, top - railHeight, railZ + 0.012f), 0.025f, 0.08f, railHeight, sealBottom: true);

            // Splayed legs from under the frame's corners
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                    mesh.AddTube(
                        new Vector3(sx * (width / 2f - 0.04f), frameBottom, sz * (depth / 2f - 0.04f)),
                        new Vector3(sx * (width / 2f - 0.01f), 0f, sz * (depth / 2f - 0.01f)),
                        0.022f, 0.014f, 4, Leg);

            return mesh.Build(device);
        }
    }
}
