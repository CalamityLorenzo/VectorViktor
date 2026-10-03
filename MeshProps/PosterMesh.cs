using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace MeshProps
{
    // A poster stuck to a wall, a little askew (Tilt): a sheet Width x Height, its outline, and in it a unicorn, side on,
    // as an outline too - a horse's head, neck, back, tail, legs and a horn. In wireframe it's the two outlines; with
    // colours on, the sheet's colour behind them. Flat against the wall in the X-Y plane, its back on z = 0 and its face
    // towards +Z, its middle on the origin: hang it at the height of its middle.
    public static class PosterMesh
    {
        public const int Sheet = 0;
        public const int PaletteSize = 1;

        public const float Width = 0.6f, Height = 0.85f;
        public const float Tilt = 0.12f;   // radians, anticlockwise as you face it

        private const float Stand = 0.004f, LineStand = 0.007f;   // off the wall, so neither z-fights it

        public static Color[] Palette(Color sheet) => new[] { sheet };

        // The unicorn, facing left, in a unit square (x right, y up): one closed outline, from the horn's tip
        private static readonly Vector2[] Unicorn =
        {
            new(0.08f, 0.98f), new(0.19f, 0.81f),                                       // the horn, up from the forehead
            new(0.24f, 0.84f), new(0.27f, 0.92f), new(0.30f, 0.82f),                  // an ear
            new(0.36f, 0.74f), new(0.42f, 0.62f), new(0.47f, 0.52f),                  // the mane, down the neck
            new(0.62f, 0.50f), new(0.78f, 0.50f),                                       // the back
            new(0.84f, 0.53f), new(0.94f, 0.62f), new(0.98f, 0.50f), new(0.91f, 0.45f), new(0.86f, 0.42f),   // the tail
            new(0.85f, 0.30f), new(0.87f, 0.08f), new(0.82f, 0.06f), new(0.79f, 0.09f), new(0.78f, 0.28f),   // a hind leg
            new(0.72f, 0.31f), new(0.70f, 0.08f), new(0.66f, 0.06f), new(0.64f, 0.09f), new(0.65f, 0.31f),   // the other
            new(0.52f, 0.32f),                                                         // the belly
            new(0.49f, 0.28f), new(0.49f, 0.08f), new(0.45f, 0.06f), new(0.42f, 0.09f), new(0.42f, 0.30f),   // a foreleg
            new(0.38f, 0.40f), new(0.34f, 0.52f),                                       // the chest
            new(0.26f, 0.62f), new(0.18f, 0.66f),                                       // the throat and jaw
            new(0.10f, 0.66f), new(0.07f, 0.70f), new(0.10f, 0.75f), new(0.15f, 0.79f),   // the muzzle, up the face
        };

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();
            var turn = Matrix.CreateRotationZ(Tilt);
            Vector3 At(float x, float y, float z) => Vector3.Transform(new Vector3(x, y, z), turn);

            var (hw, hh) = (Width / 2f, Height / 2f);
            mesh.AddQuad(Sheet, At(-hw, -hh, Stand), At(hw, -hh, Stand), At(hw, hh, Stand), At(-hw, hh, Stand));
            mesh.AddLineLoop(At(-hw, -hh, LineStand), At(hw, -hh, LineStand), At(hw, hh, LineStand), At(-hw, hh, LineStand));

            // The unicorn, filling most of the sheet, a little above its middle (room for a title under it, one day)
            const float margin = 0.07f;
            var size = Width - 2f * margin;
            var (left, bottom) = (-size / 2f, -hh + margin + 0.12f);
            mesh.AddLineLoop(Array.ConvertAll(Unicorn, p => At(left + p.X * size, bottom + p.Y * size, LineStand)));
            mesh.AddLine(At(left + 0.17f * size, bottom + 0.73f * size, LineStand), At(left + 0.19f * size, bottom + 0.73f * size, LineStand));   // its eye
            return mesh.Build(device);
        }
    }
}
