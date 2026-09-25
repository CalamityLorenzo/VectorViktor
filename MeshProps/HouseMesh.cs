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

        public static MeshData Build(GraphicsDevice device)
        {
            const float houseWidth = 2.0f;
            const float houseDepth = 1.0f;
            const float wallHeight = 0.6f;
            const float roofPeakHeight = 0.3f;

            // The original is built with its base at y = 0; shift it so the house is centred
            // vertically, otherwise it would tumble about its floor when spun like the other meshes.
            var origin = new Vector3(0f, -(wallHeight + roofPeakHeight) * 0.5f, 0f);

            var mesh = new MeshBuilder();

            // Main walls, with a flat floor so the hull is sealed
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
            const float doorWidth = 0.24f;
            var doorHeight = wallHeight * 0.85f;
            var doorBottom = origin
                + Vector3.UnitX * (houseWidth * -0.22f)
                - Vector3.UnitZ * (houseDepth * 0.5f + 0.01f);
            mesh.AddBox(DoorBase, doorBottom, 0.01f, doorWidth, doorHeight);

            // Larger front window on the opposite side, with a plus cross
            const float windowWidth = 0.52f;
            const float windowHeight = 0.33f;
            var windowBottom = origin
                + Vector3.UnitX * (houseWidth * 0.22f)
                - Vector3.UnitZ * (houseDepth * 0.5f + 0.02f)
                + Vector3.Up * (wallHeight * 0.45f);
            mesh.AddBox(WindowBase, windowBottom, 0.01f, windowWidth, windowHeight);

            // The cross sits 0.01 in front of the pane so it doesn't z-fight with it.
            var crossOrigin = windowBottom - Vector3.UnitZ * 0.01f;
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
    }
}
