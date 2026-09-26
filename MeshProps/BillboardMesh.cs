using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MeshProps
{
    // A roadside billboard: a board Width x Height with its bottom edge Clearance up, on two posts behind it,
    // facing +Z. Its white face carries a BillboardDesign's art (see BillboardDesigns: the Commodore 64, the Atari).
    // The origin is on the ground between the posts, which go PostSunk into it, each through a concrete footing
    // standing a little proud of the ground. Everything's gathered into one draw range per colour.
    public static class BillboardMesh
    {
        public const int Post = 0, Board = 3, Face = 6, Blue = 7, Red = 8, Footing = 9;
        public const int PaletteSize = 12;

        public const float Width = 7f, Height = 3.5f, Clearance = 2.4f;
        private const float BoardDepth = 0.2f, Border = 0.15f, Lift = 0.01f;
        public const float PostSize = 0.3f, PostSunk = 0.5f;
        private const float FootingSize = 0.8f, FootingProud = 0.2f;

        public static Color[] Palette(Color post, Color board)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, Post, post);
            MeshBuilder.SetBoxShades(palette, Board, board);
            palette[Face] = new Color(245, 245, 240);
            palette[Blue] = new Color(30, 55, 150);
            palette[Red] = new Color(215, 35, 40);
            MeshBuilder.SetBoxShades(palette, Footing, new Color(165, 160, 150));
            return palette;
        }

        // Where the posts stand, in its own X and Z: behind the board, a little in from its ends.
        public static readonly float[] PostsAt = { -Width * 0.3f, Width * 0.3f };
        public const float PostZ = -BoardDepth / 2f - PostSize / 2f;

        public static MeshSource Source(BillboardDesign design, Color post, Color board) =>
            new MeshSource("billboard:" + design.Key, d => Build(d, design), Palette(post, board));

        public static MeshData Build(GraphicsDevice device, BillboardDesign design)
        {
            var mesh = new MeshBuilder();

            // An upright box on its bottom centre: `depth` along Z, `width` along X, in a part's three shades
            void Box(int slot, Vector3 at, float width, float depth, float height) =>
                mesh.AddBox(slot, at, depth, width, height, sealBottom: true);

            // Posts behind the board, from well down in the ground up to the board's bottom edge, each in its
            // footing; the board; its white face. The posts stop there: any further and their edges, drawn
            // behind the board, would show through it at a distance, where faces are pushed back in depth.
            foreach (var x in PostsAt)
            {
                var foot = new Vector3(x, 0f, PostZ);
                Box(Post, foot - Vector3.Up * PostSunk, PostSize, PostSize, PostSunk + Clearance);
                Box(Footing, foot - Vector3.Up * PostSunk, FootingSize, FootingSize, PostSunk + FootingProud);
            }
            Box(Board, new Vector3(0f, Clearance, 0f), Width, BoardDepth, Height);

            var front = BoardDepth / 2f;
            var hw = Width / 2f - Border;
            var face = new BillboardFace(mesh, front + Lift);
            face.Shape(Face, new Vector2(-hw, Border), new Vector2(hw, Border), new Vector2(hw, Height - Border), new Vector2(-hw, Height - Border));

            // The art, a touch further out so it wins over the face
            design.Paint(new BillboardFace(mesh, front + 2f * Lift));
            return mesh.Build(device);
        }
    }
}
