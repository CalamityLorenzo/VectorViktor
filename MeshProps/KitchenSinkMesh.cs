using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A kitchen sink unit: a base cupboard with two doors and knobs, a worktop overhanging its front, a deep steel sink
    // sunk into the worktop's left half with a plug hole, a ribbed draining board on the right, and a swan-neck tap
    // behind the sink. 1.04 wide, 0.62 deep, 0.90 to the worktop. The doors face +Z, centred on X and Z, standing on
    // y = 0; its back goes against the wall.
    public static class KitchenSinkMesh
    {
        // The unit and the worktop have three shades each (see MeshBuilder); then the steel, the inside of the sink
        // (darker), and the knobs and plug hole.
        public const int UnitBase = 0, WorktopBase = 3, Steel = 6, SinkInside = 7, Knob = 8;
        public const int PaletteSize = 9;

        public const float Height = 0.90f;

        public static Color[] Palette(Color unit, Color worktop, Color steel, Color knob)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, UnitBase, unit);
            MeshBuilder.SetBoxShades(palette, WorktopBase, worktop);
            palette[Steel] = steel;
            palette[SinkInside] = Color.Lerp(steel, Color.Black, 0.3f);
            palette[Knob] = knob;
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float width = 1.0f, depth = 0.58f, worktopThickness = 0.03f, overhang = 0.02f;
            const float carcassTop = Height - worktopThickness, front = depth / 2f;
            const float sinkLeft = -0.44f, sinkRight = -0.02f, sinkBack = -0.20f, sinkFront = 0.19f, sinkDepth = 0.18f;
            var mesh = new MeshBuilder();

            // The carcass, open at the top, where the worktop covers it and the sink goes down into it; its two doors proud
            // of it, a knob each near the middle
            var (cx, cz, up) = (width / 2f, depth / 2f, Vector3.Up * carcassTop);
            var (a, b, c, e) = (new Vector3(-cx, 0f, -cz), new Vector3(-cx, 0f, cz), new Vector3(cx, 0f, cz), new Vector3(cx, 0f, -cz));
            mesh.AddQuad(UnitBase + MeshBuilder.Side, a, b, b + up, a + up);
            mesh.AddQuad(UnitBase + MeshBuilder.Side, c, e, e + up, c + up);
            mesh.AddQuad(UnitBase + MeshBuilder.Dim, b, c, c + up, b + up);
            mesh.AddQuad(UnitBase + MeshBuilder.Dim, e, a, a + up, e + up);
            mesh.AddQuad(UnitBase + MeshBuilder.Dim, a, b, c, e);
            mesh.AddLineLoop(a, b, c, e);
            mesh.AddLine(a, a + up); mesh.AddLine(b, b + up); mesh.AddLine(c, c + up); mesh.AddLine(e, e + up);
            foreach (var side in new[] { -1f, 1f })
            {
                mesh.AddBox(UnitBase, new Vector3(side * 0.25f, 0.08f, front + 0.008f), 0.016f, 0.48f, carcassTop - 0.12f);
                mesh.AddTube(new Vector3(side * 0.04f, carcassTop - 0.15f, front + 0.016f), new Vector3(side * 0.04f, carcassTop - 0.15f, front + 0.035f),
                    0.012f, 0.012f, 6, Knob, ringEdges: true);
            }

            // The worktop, with a hole in it for the sink: its top as four pieces round the hole, its edges as a box
            var (w, d) = (width / 2f + overhang, front + overhang);
            Vector3 T(float x, float z) => new Vector3(x, Height, z);
            var back = -depth / 2f;
            mesh.AddQuad(WorktopBase + MeshBuilder.Top, T(-w, back), T(w, back), T(w, sinkBack), T(-w, sinkBack));
            mesh.AddQuad(WorktopBase + MeshBuilder.Top, T(-w, sinkFront), T(w, sinkFront), T(w, d), T(-w, d));
            mesh.AddQuad(WorktopBase + MeshBuilder.Top, T(-w, sinkBack), T(sinkLeft, sinkBack), T(sinkLeft, sinkFront), T(-w, sinkFront));
            mesh.AddQuad(Steel, T(sinkRight, sinkBack), T(w - 0.02f, sinkBack), T(w - 0.02f, sinkFront), T(sinkRight, sinkFront));   // the drainer
            mesh.AddQuad(WorktopBase + MeshBuilder.Top, T(w - 0.02f, sinkBack), T(w, sinkBack), T(w, sinkFront), T(w - 0.02f, sinkFront));
            Vector3 B(float x, float z) => new Vector3(x, carcassTop, z);
            mesh.AddQuad(WorktopBase + MeshBuilder.Side, B(-w, back), B(-w, d), T(-w, d), T(-w, back));
            mesh.AddQuad(WorktopBase + MeshBuilder.Side, B(w, d), B(w, back), T(w, back), T(w, d));
            mesh.AddQuad(WorktopBase + MeshBuilder.Dim, B(-w, d), B(w, d), T(w, d), T(-w, d));
            mesh.AddQuad(WorktopBase + MeshBuilder.Dim, B(-w, front), B(w, front), B(w, d), B(-w, d));   // the overhang's underside
            mesh.AddLineLoop(T(-w, back), T(w, back), T(w, d), T(-w, d));
            mesh.AddLine(B(-w, d), B(w, d));
            mesh.AddLine(B(-w, d), T(-w, d));
            mesh.AddLine(B(w, d), T(w, d));

            // The sink: four sides and a bottom, down into the unit, the plug hole in the bottom
            Vector3 S(float x, float z, float down) => new Vector3(x, Height - down, z);
            var rim = new[] { S(sinkLeft, sinkBack, 0f), S(sinkRight, sinkBack, 0f), S(sinkRight, sinkFront, 0f), S(sinkLeft, sinkFront, 0f) };
            var floor = new[] { S(sinkLeft, sinkBack, sinkDepth), S(sinkRight, sinkBack, sinkDepth), S(sinkRight, sinkFront, sinkDepth), S(sinkLeft, sinkFront, sinkDepth) };
            for (var k = 0; k < 4; k++)
                mesh.AddQuad(SinkInside, rim[k], rim[(k + 1) % 4], floor[(k + 1) % 4], floor[k]);
            mesh.AddPolygon(Steel, floor);
            mesh.AddLineLoop(rim);
            mesh.AddLineLoop(floor);
            var plug = new Vector3[8];
            for (var k = 0; k < plug.Length; k++)
            {
                var angle = k * MathHelper.TwoPi / plug.Length;
                plug[k] = S((sinkLeft + sinkRight) / 2f + 0.025f * MathF.Cos(angle), (sinkBack + sinkFront) / 2f + 0.025f * MathF.Sin(angle), sinkDepth - 0.001f);
            }
            mesh.AddPolygon(Knob, plug);
            mesh.AddLineLoop(plug);

            // The drainer's ribs, as lines running down to the sink
            for (var k = 1; k <= 6; k++)
            {
                var z = MathHelper.Lerp(sinkBack, sinkFront, k / 7f);
                mesh.AddLine(T(sinkRight + 0.02f, z), T(w - 0.04f, z));
            }

            // The tap: a pillar behind the sink, a neck curving up and over, and its spout pointing down into the sink
            var tapX = (sinkLeft + sinkRight) / 2f;
            var foot = new Vector3(tapX, Height, -0.25f);
            mesh.AddFrustum(foot, 0.02f, 0.016f, 0.12f, 8, Steel);
            var neck = new[] { foot + new Vector3(0f, 0.12f, 0f), foot + new Vector3(0f, 0.22f, 0.04f), foot + new Vector3(0f, 0.24f, 0.12f), foot + new Vector3(0f, 0.20f, 0.18f) };
            for (var i = 0; i < neck.Length - 1; i++)
                mesh.AddTube(neck[i], neck[i + 1], 0.012f, 0.012f, 6, Steel);
            foreach (var side in new[] { -1f, 1f })   // the two handles either side of the pillar
                mesh.AddTube(foot + new Vector3(0f, 0.06f, 0f), foot + new Vector3(side * 0.07f, 0.08f, 0f), 0.008f, 0.008f, 4, Steel, ringEdges: true);

            return mesh.Build(device);
        }
    }
}
