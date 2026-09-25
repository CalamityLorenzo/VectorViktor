using MeshCore.Library;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // A sleek wedge sports coupé in the flat-shaded polygon style of Hard Drivin' / Race Drivin':
    // a low pointed nose, a body that widens towards the rear haunches, a raked windscreen into a
    // short roof and a long fastback, side strakes behind the door, and round (octagonal) wheels
    // with hubs. Several steps up from the boxy TrabantMesh. The car faces +Z.
    public static class CarMesh
    {
        // Slots: body has 3 shades (MeshBuilder.Side / Dim / Top), glass 2, then trim, tyre, tyre cap, hub.
        public const int BodyBase = 0;
        public const int Glass = 3, GlassDim = 4;   // windscreen + rear window / side windows
        public const int Trim = 5;                  // side strakes
        public const int Tyre = 6, TyreCap = 7, Hub = 8;
        public const int PaletteSize = 9;

        public static Color[] Palette(Color body, Color glass, Color wheel)
        {
            var palette = new Color[PaletteSize];
            MeshBuilder.SetBoxShades(palette, BodyBase, body);
            palette[Glass] = glass;
            palette[GlassDim] = new Color((int)(glass.R * 0.7f), (int)(glass.G * 0.7f), (int)(glass.B * 0.7f));
            palette[Trim] = new Color(25, 25, 30);
            palette[Tyre] = wheel;
            palette[TyreCap] = Color.Lerp(wheel, Color.White, 0.15f);
            palette[Hub] = new Color(175, 165, 145);
            return palette;
        }

        // Lower body cross-sections, nose to tail: z, half-width, underside y, top-surface y.
        private static readonly float[] StationZ = { 0.50f, 0.44f, 0.30f, 0.12f, -0.10f, -0.32f, -0.46f, -0.50f };
        private static readonly float[] StationHalfWidth = { 0.15f, 0.20f, 0.24f, 0.255f, 0.265f, 0.27f, 0.26f, 0.25f };
        private static readonly float[] StationBottom = { 0.06f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.06f, 0.07f };
        private static readonly float[] StationTop = { 0.10f, 0.125f, 0.15f, 0.17f, 0.175f, 0.185f, 0.19f, 0.17f };

        // Linear interpolation of one of the station arrays at z (stations run from +z to -z).
        private static float At(float z, float[] values)
        {
            for (var i = 0; i < StationZ.Length - 1; i++)
            {
                if (z <= StationZ[i] && z >= StationZ[i + 1])
                    return MathHelper.Lerp(values[i], values[i + 1], (StationZ[i] - z) / (StationZ[i] - StationZ[i + 1]));
            }
            throw new ArgumentOutOfRangeException(nameof(z));
        }

        public static MeshData Build(GraphicsDevice device)
        {
            const float roofY = 0.29f;   // highest point, for centring

            // Centred vertically so the car tumbles about its middle like the other meshes.
            var shift = new Vector3(0f, -roofY * 0.5f, 0f);
            Vector3 P(float x, float y, float z) => new Vector3(x, y, z) + shift;

            var n = StationZ.Length;
            Vector3 TL(int i) => P(-StationHalfWidth[i], StationTop[i], StationZ[i]);
            Vector3 TR(int i) => P(StationHalfWidth[i], StationTop[i], StationZ[i]);
            Vector3 BL(int i) => P(-StationHalfWidth[i], StationBottom[i], StationZ[i]);
            Vector3 BR(int i) => P(StationHalfWidth[i], StationBottom[i], StationZ[i]);

            // ---- Glasshouse key points. Base points sit on the body's top surface; the roof is narrower (tumblehome).
            const float baseHalf = 0.21f, roofHalf = 0.165f;
            const float windscreenBaseZ = 0.16f, roofFrontZ = 0.0f, roofRearZ = -0.14f, rearBaseZ = -0.40f;
            var windscreenBaseY = At(windscreenBaseZ, StationTop);
            var rearBaseY = At(rearBaseZ, StationTop);
            var cabinBaseFrontL = P(-baseHalf, windscreenBaseY, windscreenBaseZ);
            var cabinBaseFrontR = P(baseHalf, windscreenBaseY, windscreenBaseZ);
            var cabinBaseRearL = P(-baseHalf, rearBaseY, rearBaseZ);
            var cabinBaseRearR = P(baseHalf, rearBaseY, rearBaseZ);
            var roofFrontL = P(-roofHalf, roofY - 0.005f, roofFrontZ);
            var roofFrontR = P(roofHalf, roofY - 0.005f, roofFrontZ);
            var roofRearL = P(-roofHalf, roofY, roofRearZ);
            var roofRearR = P(roofHalf, roofY, roofRearZ);

            var mesh = new MeshBuilder();

            // ---- Body sides (28 tris)
            for (var i = 0; i < n - 1; i++)
            {
                mesh.AddQuad(BodyBase + MeshBuilder.Side, BL(i), BL(i + 1), TL(i + 1), TL(i));   // left
                mesh.AddQuad(BodyBase + MeshBuilder.Side, BR(i), BR(i + 1), TR(i + 1), TR(i));   // right
            }

            // ---- Underside and the nose / tail end panels (18 tris)
            for (var i = 0; i < n - 1; i++)
                mesh.AddQuad(BodyBase + MeshBuilder.Dim, BL(i), BR(i), BR(i + 1), BL(i + 1));
            mesh.AddQuad(BodyBase + MeshBuilder.Dim, BL(0), BR(0), TR(0), TL(0));
            mesh.AddQuad(BodyBase + MeshBuilder.Dim, BL(n - 1), BR(n - 1), TR(n - 1), TL(n - 1));

            // ---- Top surface: hood, shoulders, rear deck, and the roof (16 tris)
            for (var i = 0; i < n - 1; i++)
                mesh.AddQuad(BodyBase + MeshBuilder.Top, TL(i), TR(i), TR(i + 1), TL(i + 1));
            mesh.AddQuad(BodyBase + MeshBuilder.Top, roofFrontL, roofFrontR, roofRearR, roofRearL);

            // ---- Glass: windscreen and rear window (4 tris), then the side windows (4 tris)
            mesh.AddQuad(Glass, cabinBaseFrontL, cabinBaseFrontR, roofFrontR, roofFrontL);   // windscreen
            mesh.AddQuad(Glass, roofRearL, roofRearR, cabinBaseRearR, cabinBaseRearL);       // rear window
            mesh.AddQuad(GlassDim, cabinBaseFrontL, cabinBaseRearL, roofRearL, roofFrontL);     // left window
            mesh.AddQuad(GlassDim, cabinBaseFrontR, cabinBaseRearR, roofRearR, roofFrontR);     // right window

            // ---- Side strakes: three slanted dark slashes behind the door on each side (12 tris)
            const float strakeBottomY = 0.085f, strakeTopY = 0.145f, strakeWidth = 0.03f, strakeLean = 0.03f;
            foreach (var side in new[] { -1f, 1f })
                foreach (var z0 in new[] { -0.02f, -0.10f, -0.18f })
                {
                    // Sits 0.002 proud of the flank so it doesn't z-fight with it.
                    Vector3 Q(float z, float y) => P(side * (At(z, StationHalfWidth) + 0.002f), y, z);
                    var strake = new[]
                    {
                        Q(z0, strakeBottomY),
                        Q(z0 - strakeWidth, strakeBottomY),
                        Q(z0 - strakeWidth - strakeLean, strakeTopY),
                        Q(z0 - strakeLean, strakeTopY),
                    };
                    mesh.AddQuad(Trim, strake[0], strake[1], strake[2], strake[3]);
                    mesh.AddLineLoop(strake);
                }

            // ---- Wheels: octagonal (flat top and bottom), at the four corners.
            const float apothem = 0.085f, halfTrack = 0.035f, hubInset = 0.55f;
            var radius = apothem / MathF.Cos(MathF.PI / 8f);
            var wheelCentres = new[]
            {
                (x: -0.26f, z: 0.31f), (x: 0.26f, z: 0.31f), (x: -0.26f, z: -0.32f), (x: 0.26f, z: -0.32f),
            };

            // Ring of 8 points round a wheel at the given x, scaled by `scale` (1 = the tyre's outline).
            Vector3[] Ring(float x, float z, float scale)
            {
                var ring = new Vector3[8];
                for (var k = 0; k < 8; k++)
                {
                    var angle = (k + 0.5f) * MathHelper.TwoPi / 8f;
                    ring[k] = P(x, apothem + radius * scale * MathF.Sin(angle), z + radius * scale * MathF.Cos(angle));
                }
                return ring;
            }

            // Each wheel: its tread, both flat faces, and a hub on the outward face only
            foreach (var (x, z) in wheelCentres)
            {
                var a = Ring(x - halfTrack, z, 1f);
                var b = Ring(x + halfTrack, z, 1f);
                for (var k = 0; k < 8; k++)
                    mesh.AddQuad(Tyre, a[k], a[(k + 1) % 8], b[(k + 1) % 8], b[k]);
                mesh.AddPolygon(TyreCap, a);
                mesh.AddPolygon(TyreCap, b);

                var hub = Ring(x + MathF.Sign(x) * (halfTrack + 0.002f), z, hubInset);
                mesh.AddPolygon(Hub, hub);
                mesh.AddLineLoop(hub);
            }

            // ---- Edges: the hull outline (see below), the glasshouse outline; the strakes, hubs and wheels are edged where they are built or below
            // Hull: only its outline. The four long ridges run nose to tail, and just the nose and
            // tail panels are outlined, with no cross-section lines in between.
            mesh.AddLineLoop(BL(0), BR(0), TR(0), TL(0));
            mesh.AddLineLoop(BL(n - 1), BR(n - 1), TR(n - 1), TL(n - 1));
            for (var i = 0; i < n - 1; i++)
            {
                mesh.AddLine(BL(i), BL(i + 1));
                mesh.AddLine(BR(i), BR(i + 1));
                mesh.AddLine(TL(i), TL(i + 1));
                mesh.AddLine(TR(i), TR(i + 1));
            }

            mesh.AddLineLoop(cabinBaseFrontL, roofFrontL, roofRearL, cabinBaseRearL);
            mesh.AddLineLoop(cabinBaseFrontR, roofFrontR, roofRearR, cabinBaseRearR);
            mesh.AddLine(cabinBaseFrontL, cabinBaseFrontR);
            mesh.AddLine(roofFrontL, roofFrontR);
            mesh.AddLine(roofRearL, roofRearR);
            mesh.AddLine(cabinBaseRearL, cabinBaseRearR);

            // Wheels: the octagon on both faces only, with no lines across the width.
            foreach (var (x, z) in wheelCentres)
            {
                var outer = Ring(x + MathF.Sign(x) * halfTrack, z, 1f);
                var reverse = Ring(x - MathF.Sign(x) * halfTrack, z, 1f);
                mesh.AddLineLoop(outer);
                mesh.AddLineLoop(reverse);
            }

            return mesh.Build(device);
        }
    }
}
