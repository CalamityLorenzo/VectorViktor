using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // The inside of an aircraft hangar, with no front wall, to be seen through a window: a concrete floor ruled
    // into squares, a half-round roof of corrugated panels on ribs, and a back wall with a pair of big sliding
    // doors in it. The open front is at z = 0, the hangar running back from it to z = Depth, centred on x, its
    // floor on y = 0.
    public static class HangarMesh
    {
        public const int Floor = 0, RoofA = 1, RoofB = 2, BackWall = 3, Doors = 4;
        public const int PaletteSize = 5;

        public const float Width = 24f, Depth = 32f, Height = Width / 2f;
        private const int Panels = 16;           // round the roof's half circle
        private const float Square = 4f;         // the floor's squares, and between the roof's ribs
        private const float DoorWidth = 12f, DoorHeight = 7f;

        public static Color[] Palette(Color floor, Color roof, Color backWall, Color doors)
        {
            var palette = new Color[PaletteSize];
            palette[Floor] = floor;
            palette[RoofA] = roof;
            palette[RoofB] = Color.Lerp(roof, Color.Black, 0.2f);
            palette[BackWall] = backWall;
            palette[Doors] = doors;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device) => Build(device, withFloor: true);

        // Without its floor: to stand in a room whose own floor is walked on (see RoomSpec), not to fight with it.
        public static MeshData Build(GraphicsDevice device, bool withFloor)
        {
            var mesh = new MeshBuilder();

            // The floor, ruled into squares
            if (withFloor)
                AddFloor(mesh);
            AddRoof(mesh);
            return mesh.Build(device);
        }

        private static void AddFloor(MeshBuilder mesh)
        {
            const float radius = Width / 2f;
            mesh.AddQuad(Floor, new Vector3(-radius, 0f, 0f), new Vector3(-radius, 0f, Depth), new Vector3(radius, 0f, Depth), new Vector3(radius, 0f, 0f));
            for (var x = -radius; x <= radius + 0.01f; x += Square)
                mesh.AddLine(new Vector3(x, 0f, 0f), new Vector3(x, 0f, Depth));
            for (var z = Square; z <= Depth + 0.01f; z += Square)
                mesh.AddLine(new Vector3(-radius, 0f, z), new Vector3(radius, 0f, z));
        }

        // The roof and the back wall, with its doors
        private static void AddRoof(MeshBuilder mesh)
        {
            const float radius = Width / 2f;

            // The roof's half circle, from the floor on one side over to the other
            var arch = new Vector3[Panels + 1];
            for (var k = 0; k <= Panels; k++)
            {
                var angle = MathHelper.Pi * k / Panels;
                arch[k] = new Vector3(-radius * MathF.Cos(angle), radius * MathF.Sin(angle), 0f);
            }
            var back = Vector3.UnitZ * Depth;

            // Its panels, running front to back, alternately shaded; a line along each seam between them
            for (var k = 0; k < Panels; k++)
                mesh.AddQuad(k % 2 == 0 ? RoofA : RoofB, arch[k], arch[k] + back, arch[k + 1] + back, arch[k + 1]);
            foreach (var p in arch)
                mesh.AddLine(p, p + back);

            // A rib round it every few metres
            for (var z = Square; z <= Depth + 0.01f; z += Square)
                for (var k = 0; k < Panels; k++)
                    mesh.AddLine(arch[k] + Vector3.UnitZ * z, arch[k + 1] + Vector3.UnitZ * z);

            // The back wall, and its doors a little in front of it, parted down the middle
            var wall = Array.ConvertAll(arch, p => p + back);
            for (var k = 0; k < Panels; k++)
                mesh.AddTri(BackWall, back, wall[k], wall[k + 1]);
            var inFront = back - Vector3.UnitZ * 0.05f;
            Vector3 Door(float x, float y) => inFront + new Vector3(x, y, 0f);
            mesh.AddQuad(Doors, Door(-DoorWidth / 2f, 0f), Door(DoorWidth / 2f, 0f), Door(DoorWidth / 2f, DoorHeight), Door(-DoorWidth / 2f, DoorHeight));
            mesh.AddLineLoop(Door(-DoorWidth / 2f, 0f), Door(DoorWidth / 2f, 0f), Door(DoorWidth / 2f, DoorHeight), Door(-DoorWidth / 2f, DoorHeight));
            mesh.AddLine(Door(0f, 0f), Door(0f, DoorHeight));
            for (var x = -DoorWidth / 2f + 1.5f; x < DoorWidth / 2f; x += 1.5f)   // the doors' panels
                if (MathF.Abs(x) > 0.01f)
                    mesh.AddLine(Door(x, 0.3f), Door(x, DoorHeight - 0.3f));
        }
    }
}
