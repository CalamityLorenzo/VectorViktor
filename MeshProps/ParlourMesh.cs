using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A room with no front wall, to be seen through a window: a floorboarded floor, three papered walls, a
    // ceiling, and a picture hung on the back wall. Its furniture goes in separately. The open front is at z = 0,
    // the room running back from it to z = Depth, centred on x, its floor on y = 0.
    public static class ParlourMesh
    {
        public const int Floor = 0, SideWall = 1, BackWall = 2, Ceiling = 3, Picture = 4, PictureFrame = 5;
        public const int PaletteSize = 6;

        public const float Width = 4.4f, Depth = 3.4f, Height = 2.3f;
        private const float BoardWidth = 0.4f;

        public static Color[] Palette(Color floor, Color walls, Color ceiling, Color picture, Color frame)
        {
            var palette = new Color[PaletteSize];
            palette[Floor] = floor;
            palette[SideWall] = new Color((int)(walls.R * 0.8f), (int)(walls.G * 0.8f), (int)(walls.B * 0.8f));
            palette[BackWall] = walls;
            palette[Ceiling] = ceiling;
            palette[Picture] = picture;
            palette[PictureFrame] = frame;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float hw = Width / 2f;
            var mesh = new MeshBuilder();

            var a = new Vector3(-hw, 0f, 0f);         // floor: front left, back left, back right, front right
            var b = new Vector3(-hw, 0f, Depth);
            var c = new Vector3(hw, 0f, Depth);
            var d = new Vector3(hw, 0f, 0f);
            var up = Vector3.Up * Height;

            mesh.AddQuad(Floor, a, b, c, d);
            mesh.AddQuad(SideWall, a, b, b + up, a + up);
            mesh.AddQuad(SideWall, d, c, c + up, d + up);
            mesh.AddQuad(BackWall, b, c, c + up, b + up);
            mesh.AddQuad(Ceiling, a + up, b + up, c + up, d + up);

            // The corners, and the floorboards running back from the front
            mesh.AddLine(a, b); mesh.AddLine(b, c); mesh.AddLine(c, d);
            mesh.AddLine(a + up, b + up); mesh.AddLine(b + up, c + up); mesh.AddLine(c + up, d + up);
            mesh.AddLine(b, b + up); mesh.AddLine(c, c + up);
            for (var x = -hw + BoardWidth; x < hw - 0.01f; x += BoardWidth)
                mesh.AddLine(new Vector3(x, 0f, 0f), new Vector3(x, 0f, Depth));

            // A landscape in a frame, above where the settee stands, a little off the wall
            const float pictureWidth = 0.9f, pictureHeight = 0.6f, pictureBottom = 1.15f, frame = 0.06f;
            var z = Depth - 0.01f;
            Vector3 On(float x, float y) => new Vector3(x, y, z);
            var (l, r, bottom, top) = (-pictureWidth / 2f, pictureWidth / 2f, pictureBottom, pictureBottom + pictureHeight);
            var outer = new[] { On(l - frame, bottom - frame), On(r + frame, bottom - frame), On(r + frame, top + frame), On(l - frame, top + frame) };
            mesh.AddQuad(PictureFrame, outer[0], outer[1], outer[2], outer[3]);
            mesh.AddLineLoop(outer);
            z -= 0.005f;   // the picture just in front of its frame
            mesh.AddQuad(Picture, On(l, bottom), On(r, bottom), On(r, top), On(l, top));
            mesh.AddLineLoop(On(l, bottom), On(r, bottom), On(r, top), On(l, top));

            return mesh.Build(device);
        }
    }
}
