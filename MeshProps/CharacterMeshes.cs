using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // The player character, built from boxes: legs, a body, arms and a head, 1.8 m tall, with a nose so
    // you can tell which way it's facing. Origin between its feet, facing +Z.
    public static class PlayerMesh
    {
        public const int Legs = 0, Body = 3, Head = 6;
        public const int PaletteSize = 9;

        public static Color[] Palette(Color trousers, Color shirt, Color skin)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Legs, trousers);
            MeshBuilder.SetBoxShades(palette, Body, shirt);
            MeshBuilder.SetBoxShades(palette, Head, skin);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(Legs, new Vector3(-0.11f, 0f, 0f), 0.2f, 0.16f, 0.85f);
            mesh.AddBox(Legs, new Vector3(0.11f, 0f, 0f), 0.2f, 0.16f, 0.85f);
            mesh.AddBox(Body, new Vector3(0f, 0.85f, 0f), 0.26f, 0.46f, 0.65f, sealBottom: true);
            mesh.AddBox(Body, new Vector3(-0.3f, 0.9f, 0f), 0.14f, 0.12f, 0.58f, sealBottom: true);   // arms
            mesh.AddBox(Body, new Vector3(0.3f, 0.9f, 0f), 0.14f, 0.12f, 0.58f, sealBottom: true);
            mesh.AddBox(Head, new Vector3(0f, 1.52f, 0f), 0.24f, 0.22f, 0.28f, sealBottom: true);
            mesh.AddBox(Head, new Vector3(0f, 1.62f, 0.13f), 0.06f, 0.05f, 0.06f, sealBottom: true);  // nose
            return mesh.Build(device);
        }
    }

    // The camera drone: a flat body with four arms out to its rotors, and the camera itself hanging
    // under its front. Origin at its centre, facing +Z.
    public static class DroneMesh
    {
        public const int Body = 0, Arm = 3, Rotor = 4, Lens = 5;
        public const int PaletteSize = 8;

        public static Color[] Palette(Color body, Color arm, Color rotor, Color lens)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Body, body);
            palette[Arm] = arm;
            palette[Rotor] = rotor;
            MeshBuilder.SetBoxShades(palette, Lens, lens);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(Body, new Vector3(0f, -0.05f, 0f), 0.3f, 0.22f, 0.1f, sealBottom: true);
            foreach (var (x, z) in new[] { (1f, 1f), (-1f, 1f), (1f, -1f), (-1f, -1f) })
            {
                var tip = new Vector3(x * 0.25f, 0f, z * 0.25f);
                mesh.AddTube(Vector3.Zero, tip, 0.02f, 0.02f, 4, Arm);
                mesh.AddFrustum(tip + Vector3.Up * 0.02f, 0.12f, 0.12f, 0.01f, 8, Rotor, topSlot: Rotor);
            }
            mesh.AddBox(Body, new Vector3(0f, -0.13f, 0.12f), 0.08f, 0.08f, 0.08f, sealBottom: true);   // camera housing
            mesh.AddBox(Lens, new Vector3(0f, -0.11f, 0.17f), 0.02f, 0.05f, 0.05f, sealBottom: true);
            return mesh.Build(device);
        }
    }
}
