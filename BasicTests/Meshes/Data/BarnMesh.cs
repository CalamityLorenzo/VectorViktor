using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BasicTests.Meshes
{
    // A simple barn in the flat-shaded polygon style of Hard Drivin' / Race Drivin': a long
    // rectangular body, a two-pitch (gambrel) roof, and big double doors with X bracing plus a
    // small loft hatch on the front gable. The front faces -Z; the long sides run along Z.
    static class BarnMesh
    {
        // Slots: walls have 3 shades (MeshBuilder.Side / Dim / Top), roof has 2, doors 3.
        public const int WallBase = 0;
        public const int RoofLow = 3, RoofHigh = 4;   // steep lower pitch / shallower upper pitch
        public const int DoorBase = 5;
        public const int PaletteSize = 8;

        public static Color[] Palette(Color wall, Color roof, Color door)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, WallBase, wall);
            palette[RoofLow] = roof;
            palette[RoofHigh] = Color.Lerp(roof, Color.White, 0.25f);
            MeshBuilder.SetBoxShades(palette, DoorBase, door);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float halfWidth = 0.6f;       // x
            const float halfLength = 0.9f;      // z
            const float wallHeight = 0.6f;
            const float kneeHalf = 0.42f;       // where the roof changes pitch
            const float kneeY = 0.85f;
            const float ridgeY = 1.0f;

            // Centred vertically so the barn tumbles about its middle like the other meshes.
            var shift = new Vector3(0f, -ridgeY * 0.5f, 0f);
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z) + shift;

            // Gable profile at a given z, going round: bottom-left, bottom-right, up the right wall,
            // right knee, ridge, left knee, down the left wall.
            Vector3[] Gable(float z) => new[]
            {
                P(-halfWidth, 0f, z), P(halfWidth, 0f, z),
                P(halfWidth, wallHeight, z), P(kneeHalf, kneeY, z), P(0f, ridgeY, z), P(-kneeHalf, kneeY, z),
                P(-halfWidth, wallHeight, z),
            };
            var front = Gable(-halfLength);
            var back = Gable(halfLength);

            var mesh = new MeshBuilder();

            // Long side walls
            mesh.AddSolidRange(4, WallBase + MeshBuilder.Side);
            mesh.AddQuad(front[0], back[0], back[6], front[6]);   // left
            mesh.AddQuad(back[1], front[1], front[2], back[2]);   // right

            // Gable ends and the floor (darker, like the nose/tail shade of the boxes)
            mesh.AddSolidRange(12, WallBase + MeshBuilder.Dim);
            mesh.AddPolygon(front);
            mesh.AddPolygon(back);
            mesh.AddQuad(front[0], front[1], back[1], back[0]);   // floor

            // Roof: steep lower pitch, then the shallower upper pitch, on each side
            mesh.AddSolidRange(4, RoofLow);
            mesh.AddQuad(front[6], back[6], back[5], front[5]);   // left, lower
            mesh.AddQuad(back[2], front[2], front[3], back[3]);   // right, lower
            mesh.AddSolidRange(4, RoofHigh);
            mesh.AddQuad(front[5], back[5], back[4], front[4]);   // left, upper
            mesh.AddQuad(back[3], front[3], front[4], back[4]);   // right, upper

            // Double doors: two thin leaves on the front gable, each with an X brace
            const float leafWidth = 0.3f, leafHeight = 0.45f, leafDepth = 0.02f;
            var doorZ = -halfLength - leafDepth * 0.5f;
            foreach (var x in new[] { -leafWidth * 0.5f, leafWidth * 0.5f })
            {
                mesh.AddBox(DoorBase, P(x, 0f, doorZ), leafDepth, leafWidth, leafHeight);

                // Brace lines sit just in front of the leaf's face so they don't z-fight with it.
                var braceZ = doorZ - leafDepth * 0.5f - 0.005f;
                mesh.AddLine(P(x - leafWidth * 0.5f, 0f, braceZ), P(x + leafWidth * 0.5f, leafHeight, braceZ));
                mesh.AddLine(P(x - leafWidth * 0.5f, leafHeight, braceZ), P(x + leafWidth * 0.5f, 0f, braceZ));
            }

            // Loft hatch above the doors
            const float hatchSize = 0.16f;
            mesh.AddBox(DoorBase, P(0f, 0.62f, -halfLength - leafDepth * 0.5f), leafDepth, hatchSize, hatchSize);

            // ---- Edges: both gable outlines, plus a line along the barn from each profile point
            mesh.AddLineLoop(front);
            mesh.AddLineLoop(back);
            for (var i = 0; i < front.Length; i++)
                mesh.AddLine(front[i], back[i]);

            return mesh.Build(device);
        }
    }
}
