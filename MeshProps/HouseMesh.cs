using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // Ported from VectorViktor's BuildHouseGeometry: walls, hipped roof, door, window (with a plus
    // cross) and chimney. The house is 2 wide (X) by 1 deep (Z); the front faces -Z.
    // Each box part (wall/door/window/chimney) has three shades (see MeshBuilder): 3 consecutive palette slots per part.
    public static class HouseMesh
    {
        public const int WallBase = 0, DoorBase = 3, WindowBase = 6, ChimneyBase = 9;   // + MeshBuilder.Side / Dim / Top
        public const int Roof = 12;

        // Its size, in the mesh's units: Width along X, Depth along Z, and Height from its floor to the roof's
        // peak, the middle of which is at y = 0
        public const float Width = 2.0f, Depth = 1.0f, WallHeight = 0.6f, RoofPeakHeight = 0.3f;
        public const float Height = WallHeight + RoofPeakHeight;

        // The front door: its middle's offset along the front (from the middle, along X), its width and its height
        public const float DoorOffset = Width * -0.22f, DoorWidth = 0.24f, DoorHeight = WallHeight * 0.85f;

        // The front window, on the other side of the front: its middle's offset along it, its size, and how far
        // above the floor its bottom is
        public const float WindowOffset = Width * 0.22f, WindowWidth = 0.52f, WindowHeight = 0.33f, WindowSill = WallHeight * 0.3f;

        // Builds the 13-slot palette from one base colour per part.
        public static Color[] Palette(Color wall, Color roof, Color door, Color window, Color chimney)
        {
            var palette = new Color[13];
            MeshBuilder.SetBoxShades(palette, WallBase, wall);
            MeshBuilder.SetBoxShades(palette, DoorBase, door);
            MeshBuilder.SetBoxShades(palette, WindowBase, window);
            MeshBuilder.SetBoxShades(palette, ChimneyBase, chimney);
            palette[Roof] = roof;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device) => Build(device, openWindow: false);

        // With openWindow, the window is a hole in the front wall, its frame and cross drawn round it but no pane:
        // to see through (see WindowPortals, which draws what's seen through it, and its pane).
        public static MeshData Build(GraphicsDevice device, bool openWindow)
        {
            const float houseWidth = Width;
            const float houseDepth = Depth;
            const float wallHeight = WallHeight;
            const float roofPeakHeight = RoofPeakHeight;

            // The original is built with its base at y = 0; shift it so the house is centred
            // vertically, otherwise it would tumble about its floor when spun like the other meshes.
            var origin = new Vector3(0f, -(wallHeight + roofPeakHeight) * 0.5f, 0f);

            var mesh = new MeshBuilder();

            // Main walls, with a flat floor so the hull is sealed
            if (openWindow)
                AddWallsWithWindowHole(mesh, origin);
            else
                mesh.AddBox(WallBase, origin, houseDepth, houseWidth, wallHeight, sealBottom: true);

            // Roof: four triangular faces
            var roofBase = origin + Vector3.Up * wallHeight;
            var roofPeak = roofBase + Vector3.Up * roofPeakHeight;
            var halfW = houseWidth * 0.5f;
            var halfD = houseDepth * 0.5f;
            var frontLeft = roofBase - Vector3.UnitZ * halfD - Vector3.UnitX * halfW;
            var frontRight = roofBase - Vector3.UnitZ * halfD + Vector3.UnitX * halfW;
            var backLeft = roofBase + Vector3.UnitZ * halfD - Vector3.UnitX * halfW;
            var backRight = roofBase + Vector3.UnitZ * halfD + Vector3.UnitX * halfW;

            mesh.AddTri(Roof, frontLeft, frontRight, roofPeak);   // front slope
            mesh.AddTri(Roof, backRight, backLeft, roofPeak);     // back slope
            mesh.AddTri(Roof, frontLeft, backLeft, roofPeak);     // left slope
            mesh.AddTri(Roof, backRight, frontRight, roofPeak);   // right slope

            mesh.AddLine(frontLeft, roofPeak);
            mesh.AddLine(frontRight, roofPeak);
            mesh.AddLine(backLeft, roofPeak);
            mesh.AddLine(backRight, roofPeak);
            mesh.AddLine(frontLeft, backLeft);
            mesh.AddLine(frontRight, backRight);

            // Front door, on one side of the facade
            const float doorWidth = DoorWidth;
            var doorHeight = DoorHeight;
            var doorBottom = origin
                + Vector3.UnitX * DoorOffset
                - Vector3.UnitZ * (houseDepth * 0.5f + 0.01f);
            mesh.AddBox(DoorBase, doorBottom, 0.01f, doorWidth, doorHeight);

            // Larger front window on the opposite side, with a plus cross
            const float windowWidth = WindowWidth;
            const float windowHeight = WindowHeight;
            var windowBottom = origin
                + Vector3.UnitX * WindowOffset
                - Vector3.UnitZ * (houseDepth * 0.5f + 0.02f)
                + Vector3.Up * WindowSill;
            if (!openWindow)
                mesh.AddBox(WindowBase, windowBottom, 0.01f, windowWidth, windowHeight);

            // The cross sits 0.01 in front of the pane so it doesn't z-fight with it; with no pane, in the hole.
            var crossOrigin = openWindow ? windowBottom + Vector3.UnitZ * 0.02f : windowBottom - Vector3.UnitZ * 0.01f;
            var windowMid = crossOrigin + Vector3.Up * (windowHeight * 0.5f);
            mesh.AddLine(windowMid - Vector3.UnitX * (windowWidth * 0.5f), windowMid + Vector3.UnitX * (windowWidth * 0.5f));
            mesh.AddLine(crossOrigin, crossOrigin + Vector3.Up * windowHeight);

            // Chimney, on the right side of the roof
            const float chimneyWidth = 0.15f;
            const float chimneyDepth = 0.1f;
            const float chimneyHeight = 0.3f;
            var chimneyBottom = roofBase + Vector3.UnitX * (halfW - chimneyWidth * 0.5f) - Vector3.UnitZ * (halfD * 0.5f);
            mesh.AddBox(ChimneyBase, chimneyBottom, chimneyDepth, chimneyWidth, chimneyHeight);

            return mesh.Build(device);
        }

        // The walls as AddBox makes them (same shades, same edges), but for the front: four strips round the
        // window's hole, and its edge round the hole as the frame.
        private static void AddWallsWithWindowHole(MeshBuilder mesh, Vector3 origin)
        {
            var hw = Width * 0.5f;
            var hd = Depth * 0.5f;
            float bottom = origin.Y, top = origin.Y + WallHeight;
            Vector3 At(float x, float y, float z) => new Vector3(x, y, z);

            var a = At(-hw, bottom, -hd); var b = At(-hw, bottom, hd); var c = At(hw, bottom, hd); var d = At(hw, bottom, -hd);
            var e = At(-hw, top, -hd); var f = At(-hw, top, hd); var g = At(hw, top, hd); var h = At(hw, top, -hd);

            mesh.AddQuad(WallBase + MeshBuilder.Side, a, b, f, e);   // ends
            mesh.AddQuad(WallBase + MeshBuilder.Side, c, d, h, g);
            mesh.AddQuad(WallBase + MeshBuilder.Dim, b, c, g, f);    // back
            mesh.AddQuad(WallBase + MeshBuilder.Dim, a, b, c, d);    // floor
            mesh.AddQuad(WallBase + MeshBuilder.Top, e, f, g, h);    // top

            // The front, round the hole: full height either side of it, and below and above it
            float left = WindowOffset - WindowWidth * 0.5f, right = WindowOffset + WindowWidth * 0.5f;
            float sill = bottom + WindowSill, lintel = sill + WindowHeight;
            void Front(float x0, float y0, float x1, float y1) =>
                mesh.AddQuad(WallBase + MeshBuilder.Dim, At(x1, y0, -hd), At(x0, y0, -hd), At(x0, y1, -hd), At(x1, y1, -hd));
            Front(-hw, bottom, left, top);
            Front(right, bottom, hw, top);
            Front(left, bottom, right, sill);
            Front(left, lintel, right, top);

            mesh.AddLine(a, b); mesh.AddLine(b, c); mesh.AddLine(c, d); mesh.AddLine(d, a);
            mesh.AddLine(e, f); mesh.AddLine(f, g); mesh.AddLine(g, h); mesh.AddLine(h, e);
            mesh.AddLine(a, e); mesh.AddLine(b, f); mesh.AddLine(c, g); mesh.AddLine(d, h);
            mesh.AddLineLoop(At(left, sill, -hd), At(right, sill, -hd), At(right, lintel, -hd), At(left, lintel, -hd));
        }
    }
}
