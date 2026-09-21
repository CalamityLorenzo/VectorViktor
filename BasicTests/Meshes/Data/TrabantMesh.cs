using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BasicTests.Meshes
{
    // Named for the East German Trabant it resembles. Ported from VectorViktor's BuildCarGeometry: a low chassis box on four box wheels, with a
    // shorter, narrower cabin set toward the rear. The car faces +Z (heading 0); the per-heading
    // copies and grid movement of the original are handled by MeshInstance's transform instead.
    // Each part (body/cabin/wheels) has three shades (see MeshBuilder): 3 consecutive palette slots per part.
    // All four wheels share one colour.
    static class TrabantMesh
    {
        public const int BodyBase = 0, CabinBase = 3, WheelBase = 6;   // + MeshBuilder.Side / Dim / Top

        // Dimensions from the original (its values are its 0.8 / 0.42 / ... scaled by 1.15).
        private const float CarLength = 0.92f;
        private const float CarWidth = 0.483f;
        private const float CarBodyHeight = 0.115f;
        private const float CarCabinHeight = 0.161f;
        private const float CarCabinLength = CarLength * 0.55f;
        private const float CarCabinWidth = CarWidth * 0.72f;
        private const float CarCabinSetback = CarLength * 0.06f;   // glasshouse sits toward the rear
        private const float WheelLength = 0.253f;
        private const float WheelTrack = 0.115f;    // wheel thickness across the car's width
        private const float WheelHeight = 0.184f;   // also the body's ground clearance
        private const float WheelOutset = 0.092f;   // how far the wheels poke out past the body sides

        // The original's colours were a dark green body, light blue cabin and dark grey wheels.
        public static Color[] Palette(Color body, Color cabin, Color wheel)
        {
            var palette = new Color[9];
            MeshBuilder.SetBoxShades(palette, BodyBase, body);
            MeshBuilder.SetBoxShades(palette, CabinBase, cabin);
            MeshBuilder.SetBoxShades(palette, WheelBase, wheel);
            return palette;
        }

        public static MeshData Build(GraphicsDevice device)
        {
            // The original is built with its wheels' bottoms at y = 0; shift it so the car is
            // centred vertically and tumbles about its middle like the other meshes.
            var totalHeight = WheelHeight + CarBodyHeight + CarCabinHeight;
            var ground = new Vector3(0f, -totalHeight * 0.5f, 0f);

            var mesh = new MeshBuilder();

            // Wheels: boxes at the four corners, giving the body its ground clearance.
            // Their undersides are sealed as they can be seen from below.
            var outerEdge = CarWidth * 0.5f + WheelOutset;
            var wheelX = outerEdge - WheelTrack * 0.5f;
            var axleZ = CarLength * 0.32f;
            foreach (var z in new[] { axleZ, -axleZ })
                foreach (var side in new[] { 1f, -1f })
                    mesh.AddBox(WheelBase, ground + new Vector3(side * wheelX, 0f, z), WheelLength, WheelTrack, WheelHeight, sealBottom: true);

            // Body: a low chassis box riding on top of the wheels (sealed underneath)
            var bodyBottom = ground + Vector3.Up * WheelHeight;
            mesh.AddBox(BodyBase, bodyBottom, CarLength, CarWidth, CarBodyHeight, sealBottom: true);

            // Cabin: a shorter, narrower glasshouse set back toward the rear (hatchback roofline)
            var cabinBottom = bodyBottom + Vector3.Up * CarBodyHeight - Vector3.UnitZ * CarCabinSetback;
            mesh.AddBox(CabinBase, cabinBottom, CarCabinLength, CarCabinWidth, CarCabinHeight);

            return mesh.Build(device);
        }
    }
}
