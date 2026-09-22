using MeshCore.Library;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshRawData
{
    // A 1960s retro-futurist space plane: a slender rocket-bodied fuselage with a needle nose, a
    // bubble cockpit canopy, swept delta wings with dihedral tips, a single swept dorsal fin and
    // twin tail nozzles, standing on a tricycle undercarriage. About 6.9 m nose to tail, 5 m across
    // the wingtips, some 3.2 m to the top of the fin. Faces +Z, centred on X, standing on y = 0.
    public static class SpacePlaneMesh
    {
        public const int Hull = 0, Accent = 1, TailBand = 2, Canopy = 3, Nozzle = 4, Gear = 5;
        public const int PaletteSize = 6;

        public static Color[] Palette(Color hull, Color accent, Color glass, Color dark)
        {
            var palette = new Color[PaletteSize];
            palette[Hull] = hull;
            palette[Accent] = accent;
            palette[TailBand] = dark;
            palette[Canopy] = glass;
            palette[Nozzle] = dark;
            palette[Gear] = dark;
            return palette;
        }

        // Fuselage cross-section stations, nose to tail: z and radius. An octagonal tube is chained
        // between them (see BuildFuselage). The nose tip collapses to a point.
        private static readonly float[] StationZ = { 3.4f, 3.05f, 2.5f, 1.5f, 0.3f, -0.9f, -2.0f, -2.8f, -3.0f };
        private static readonly float[] StationRadius = { 0f, 0.10f, 0.30f, 0.55f, 0.62f, 0.60f, 0.42f, 0.28f, 0.24f };
        private const int Sides = 8;

        private const float Beltline = 1.1f;   // height of the fuselage axis above the floor

        private static Vector3 Axis(float z) => new Vector3(0f, Beltline, z);

        private static float RadiusAt(float z)
        {
            for (var i = 0; i < StationZ.Length - 1; i++)
                if (z <= StationZ[i] && z >= StationZ[i + 1])
                    return MathHelper.Lerp(StationRadius[i], StationRadius[i + 1], (StationZ[i] - z) / (StationZ[i] - StationZ[i + 1]));
            return StationRadius[^1];
        }

        public static MeshData Build(GraphicsDevice device)
        {
            var mesh = new MeshBuilder();

            BuildFuselage(mesh);
            BuildCanopy(mesh);
            BuildWings(mesh);
            BuildFin(mesh);
            BuildNozzles(mesh);
            BuildGear(mesh);

            return mesh.Build(device);
        }

        // The hull, chained as tapered octagonal tubes between stations. Nose cone (red) up front,
        // aluminium hull amidships, a darker tail band aft; a bright accent ring marks the cockpit collar.
        private static void BuildFuselage(MeshBuilder mesh)
        {
            for (var i = 0; i < StationZ.Length - 1; i++)
            {
                var midZ = (StationZ[i] + StationZ[i + 1]) / 2f;
                var slot = midZ > 2.4f ? Accent : midZ < -1.9f ? TailBand : Hull;
                mesh.AddTube(Axis(StationZ[i]), Axis(StationZ[i + 1]), StationRadius[i], StationRadius[i + 1], Sides, slot);
            }

            AddRing(mesh, 2.5f);    // nose cone / hull boundary
            AddRing(mesh, -2.0f);   // hull / tail band boundary
            AddRing(mesh, -3.0f);   // tail end

            const float collarZ = 1.5f, collarHalf = 0.04f;
            var collarR = RadiusAt(collarZ) + 0.01f;
            mesh.AddTube(Axis(collarZ + collarHalf), Axis(collarZ - collarHalf), collarR, collarR, Sides, Accent, ringEdges: true);
        }

        private static void AddRing(MeshBuilder mesh, float z)
        {
            var r = RadiusAt(z);
            var ring = new Vector3[Sides];
            for (var k = 0; k < Sides; k++)
            {
                var angle = (k + 0.5f) * MathHelper.TwoPi / Sides;
                ring[k] = Axis(z) + new Vector3(MathF.Cos(angle) * r, MathF.Sin(angle) * r, 0f);
            }
            mesh.AddLineLoop(ring);
        }

        // A bulging cockpit bubble sitting on top of the hull just behind the nose cone, cyan-tinted
        // glass with a cross of framing struts, built the way TelevisionMesh bulges its screen.
        private static void BuildCanopy(MeshBuilder mesh)
        {
            const float centreZ = 0.75f, halfLength = 0.55f, halfWidth = 0.30f, bulge = 0.34f;
            var baseY = Beltline + RadiusAt(centreZ) - 0.05f;
            const int cells = 4;

            Vector3 Point(int i, int j)
            {
                var u = -1f + 2f * i / cells;
                var v = -1f + 2f * j / cells;
                return new Vector3(u * halfWidth, baseY + bulge * (1f - u * u) * (1f - v * v), centreZ + v * halfLength);
            }

            // A point inside the dome, used only to tell AddOutlineTri which way each triangle faces.
            var inside = new Vector3(0f, baseY - 0.15f, centreZ);

            mesh.AddSolidRange(cells * cells * 2, Canopy);
            for (var i = 0; i < cells; i++)
                for (var j = 0; j < cells; j++)
                {
                    var a = Point(i, j); var b = Point(i + 1, j); var c = Point(i + 1, j + 1); var d = Point(i, j + 1);
                    mesh.AddTri(a, b, c);
                    mesh.AddTri(a, c, d);
                    // The dome's silhouette turns with the camera, so it can't be fixed edges (see AddOutlineTri).
                    mesh.AddOutlineTri(a, b, c, inside);
                    mesh.AddOutlineTri(a, c, d, inside);
                }

            // The seam where the bubble sits on the hull, flush all the way round
            var outline = new System.Collections.Generic.List<Vector3>();
            for (var i = 0; i < cells; i++) outline.Add(Point(i, 0));
            for (var j = 0; j < cells; j++) outline.Add(Point(cells, j));
            for (var i = cells; i > 0; i--) outline.Add(Point(i, cells));
            for (var j = cells; j > 0; j--) outline.Add(Point(0, j));
            mesh.AddLineLoop(outline.ToArray());

            // Framing struts along the crest, fore-aft and cross-wise, following the curve of the dome
            for (var j = 0; j < cells; j++) mesh.AddLine(Point(cells / 2, j), Point(cells / 2, j + 1));
            for (var i = 0; i < cells; i++) mesh.AddLine(Point(i, cells / 2), Point(i + 1, cells / 2));
        }

        // Swept delta wings, one each side, with dihedral (tips angled up) and an accent-coloured tip cap.
        private static void BuildWings(MeshBuilder mesh)
        {
            const float rootLEZ = 0.6f, rootTEZ = -1.0f, rootX = 0.50f;
            const float tipLEZ = -0.2f, tipTEZ = -0.85f, tipX = 2.5f, tipLift = 0.15f;
            const float tipCapSpan = 0.78f;   // fraction of the span painted as the wingtip accent

            foreach (var side in new[] { -1f, 1f })
            {
                var rootLE = new Vector3(side * rootX, Beltline, rootLEZ);
                var rootTE = new Vector3(side * rootX, Beltline, rootTEZ);
                var tipLE = new Vector3(side * tipX, Beltline + tipLift, tipLEZ);
                var tipTE = new Vector3(side * tipX, Beltline + tipLift, tipTEZ);
                var capLE = Vector3.Lerp(rootLE, tipLE, tipCapSpan);
                var capTE = Vector3.Lerp(rootTE, tipTE, tipCapSpan);

                mesh.AddSolidRange(2, Hull);
                mesh.AddQuad(rootLE, capLE, capTE, rootTE);
                mesh.AddSolidRange(2, Accent);
                mesh.AddQuad(capLE, tipLE, tipTE, capTE);

                mesh.AddLineLoop(rootLE, tipLE, tipTE, rootTE);
                mesh.AddLine(capLE, capTE);
            }
        }

        // A single swept dorsal fin astride the tail, with an accent-coloured tip cap like the wings.
        private static void BuildFin(MeshBuilder mesh)
        {
            const float rootLEZ = -0.5f, rootTEZ = -2.2f, tipLEZ = -1.5f, tipTEZ = -2.2f, height = 1.55f;
            const float tipCapSpan = 0.65f;

            var rootLE = new Vector3(0f, Beltline + RadiusAt(rootLEZ) - 0.05f, rootLEZ);
            var rootTE = new Vector3(0f, Beltline + RadiusAt(rootTEZ) - 0.05f, rootTEZ);
            var tipY = rootLE.Y + height;
            var tipLE = new Vector3(0f, tipY, tipLEZ);
            var tipTE = new Vector3(0f, tipY, tipTEZ);
            var capLE = Vector3.Lerp(rootLE, tipLE, tipCapSpan);
            var capTE = Vector3.Lerp(rootTE, tipTE, tipCapSpan);

            mesh.AddSolidRange(2, Hull);
            mesh.AddQuad(rootLE, capLE, capTE, rootTE);
            mesh.AddSolidRange(2, Accent);
            mesh.AddQuad(capLE, tipLE, tipTE, capTE);

            mesh.AddLineLoop(rootLE, tipLE, tipTE, rootTE);
            mesh.AddLine(capLE, capTE);
        }

        // Twin engine nozzles flaring out from the tail ring.
        private static void BuildNozzles(MeshBuilder mesh)
        {
            const float attachZ = -3.0f, exitZ = -3.5f, offsetX = 0.15f;
            var attachR = RadiusAt(attachZ);
            foreach (var side in new[] { -1f, 1f })
            {
                var attach = new Vector3(side * offsetX, Beltline, attachZ);
                var exit = new Vector3(side * offsetX, Beltline, exitZ);
                mesh.AddTube(attach, exit, attachR * 0.45f, attachR * 0.6f, 8, Nozzle, ringEdges: true);
            }
        }

        // A tricycle undercarriage: one nose leg and two main legs under the wing roots, each a thin
        // strut on a small skid pad, so the plane stands with its belly clear of the floor.
        private static void BuildGear(MeshBuilder mesh)
        {
            void Leg(float x, float z, float topY)
            {
                var bottom = new Vector3(x, 0.05f, z);
                var top = new Vector3(x, topY, z);
                mesh.AddTube(top, bottom, 0.05f, 0.035f, 6, Gear, ringEdges: true);
                mesh.AddFrustum(new Vector3(x, 0f, z), 0.10f, 0.08f, 0.05f, 8, Gear, bottomSlot: Gear, topSlot: Gear);
            }

            const float noseGearZ = 2.0f, mainGearZ = 0.3f, mainGearX = 0.34f;
            Leg(0f, noseGearZ, Beltline - RadiusAt(noseGearZ) + 0.02f);
            Leg(-mainGearX, mainGearZ, Beltline - RadiusAt(mainGearZ) + 0.05f);
            Leg(mainGearX, mainGearZ, Beltline - RadiusAt(mainGearZ) + 0.05f);
        }
    }
}
