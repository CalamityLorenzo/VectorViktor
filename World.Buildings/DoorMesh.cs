using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace World.Buildings
{
    // A door leaf (see Door): a slab Door.Thickness thick, from its hinge at the origin along +X for its
    // width, standing on y = 0, with a handle on each face near its far edge. Placed with Transform
    // (see Transform), it turns about its hinge.
    public static class DoorMesh
    {
        public const int Leaf = 0, Handle = 3;
        public const int PaletteSize = 6;

        public static Color[] Palette(Color leaf)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Leaf, leaf);
            MeshBuilder.SetBoxShades(palette, Handle, new Color(210, 180, 60));
            return palette;
        }

        public static string Key(Door door) => $"door:{door.Width:F2}x{door.Height:F2}";

        public static MeshData Build(GraphicsDevice device, float width, float height)
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(Leaf, new Vector3(width / 2f, 0f, 0f), Door.Thickness, width, height);
            foreach (var side in new[] { -1f, 1f })
                mesh.AddBox(Handle, new Vector3(width - 0.12f, 0.95f, side * (Door.Thickness / 2f + 0.03f)), 0.06f, 0.12f, 0.04f);
            return mesh.Build(device);
        }

        // Where the leaf's mesh goes for the door as it is now: turned about the hinge to lie along the leaf.
        public static Matrix Transform(Door door)
        {
            var along = door.Direction(door.Angle);
            return Matrix.CreateRotationY(System.MathF.Atan2(-along.Y, along.X)) * Matrix.CreateTranslation(door.Hinge.X, door.Bottom, door.Hinge.Y);
        }
    }
}
