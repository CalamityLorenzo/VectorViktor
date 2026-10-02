using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Globalization;
using World.Buildings;

namespace World.Rendering
{
    // A Doorstep's ramp as drawn: its sloping top, edged all round, and its two sides, down into the ground under its
    // foot. (The threshold it starts from is the building's: see BuildingMesh.) In world coordinates.
    public static class DoorstepMesh
    {
        public const int Top = 0, Side = 1;
        public const int PaletteSize = 2;

        public static Color[] Palette(Doorstep step) => new[] { step.Color, Color.Lerp(step.Color, Color.Black, 0.2f) };

        // Keyed by where it is and its shape, so a ramp moved (in the map studio) is a new mesh, not the old one's
        public static MeshSource Source(Doorstep step) => new MeshSource(Key(step), d => Build(d, step), Palette(step));

        private static string Key(Doorstep s) => string.Create(CultureInfo.InvariantCulture,
            $"doorstep:{s.Outer.X:0.###},{s.Outer.Y:0.###},{s.Out.X:0.###},{s.Out.Y:0.###},{s.HalfWidth:0.###},{s.Floor:0.###},{s.Foot:0.###},{s.Length:0.###}");

        public static MeshData Build(GraphicsDevice device, Doorstep step)
        {
            var mesh = new MeshBuilder();
            var across = new Vector2(-step.Out.Y, step.Out.X) * step.HalfWidth;
            Vector3 At(Vector2 p, float y) => new Vector3(p.X, y, p.Y);
            var bottom = step.Foot - Doorstep.Depth;

            var highLeft = At(step.Outer - across, step.Floor);
            var highRight = At(step.Outer + across, step.Floor);
            var lowLeft = At(step.End - across, step.Foot);
            var lowRight = At(step.End + across, step.Foot);
            mesh.AddQuad(Top, highLeft, highRight, lowRight, lowLeft);
            mesh.AddLineLoop(highLeft, highRight, lowRight, lowLeft);

            mesh.AddQuad(Side, highLeft, lowLeft, At(step.End - across, bottom), At(step.Outer - across, bottom));
            mesh.AddQuad(Side, highRight, lowRight, At(step.End + across, bottom), At(step.Outer + across, bottom));
            return mesh.Build(device);
        }
    }
}
