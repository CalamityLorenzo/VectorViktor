using MeshCore.Library;
using MeshProps;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Core.Physics;
using World.Maps;
using static Maps.Coast.CoastTerrain;

namespace Maps.Coast
{
    // The railway, a monorail: one concrete beam, a train to sit astride it, from the buffer stop at the station, east
    // along z = 0 across the plateau and the high land, over the gorge on a truss bridge, round a curve to the north,
    // and north along the cliff top, the sea off to the east, off the edge of the world. Level all the way, at the
    // plateau's height, so through the rolling land it runs in shallow cuttings and on low banks: its formation, the
    // ground levelled for it, is FormationHalf either side of the line, and the beam stands on that (see RailwayMesh).
    // The beam's solid: too high to step up onto, but you can climb up onto it and walk along it.
    //
    // The bridge's gates are shut, and its trusses are too tall to climb or jump, so the river is still no way across:
    // only a train, some day, or the gates opened.
    public sealed class Railway : IDistrict
    {
        public const float Formation = LandHeight;
        public const float BeamTop = Formation + RailwayMesh.BeamHeight;
        public const float FormationHalf = 4f;

        public const float BufferX = -462f;              // the line's west end, at the station
        public const float CurveRadius = 100f;
        public const float CoastLineX = 288f;            // the stretch north along the cliff top
        public const float CurveStartX = CoastLineX - CurveRadius;
        public static readonly float NorthEndZ = -Size * CellSize / 2f - 10f;   // past the edge of the world
        public const float PieceLength = 10f;
        private const int CurvePieces = 16;
        private const float LedgeStep = 2f;               // the beam's top, to stand on, is a chain of straights no longer than this

        // The bridge: its deck from BridgeWest to BridgeEast, reaching 2 m onto the land past the gorge's edges
        // wherever the formation crosses it; BridgeWidth between its trusses, BridgeHeight up to their tops; a gate
        // GateHeight tall just inside each end.
        public static readonly float BridgeWest, BridgeEast;
        public const float BridgeWidth = 6f, BridgeHeight = 5.5f, DeckDepth = 0.8f, GateHeight = 2.4f, GateInset = 0.3f;

        // The formation's levelled ground stops this far short of the bridge's ends, so that the ground it eases back
        // into the land over (see TerrainGenerator.Pad) never reaches the gorge, softening its walls into a way down:
        // only the abutments, levelled with next to no easing, go on to meet the bridge.
        private const float AbutmentLength = 14f;

        static Railway()
        {
            float west = float.MaxValue, east = float.MinValue;
            for (var z = -FormationHalf; z <= FormationHalf; z += 0.25f)
            {
                west = MathF.Min(west, RiverAt(z) - GorgeHalfWidth);
                east = MathF.Max(east, RiverAt(z) + GorgeHalfWidth);
            }
            BridgeWest = MathF.Floor(west) - 2f;
            BridgeEast = MathF.Ceiling(east) + 2f;
        }

        public static float BridgeMiddle => (BridgeWest + BridgeEast) / 2f;

        // A piece of beam (see RailwayMesh.Beam): where it starts, which way it's heading there - turned about the
        // vertical as Matrix.CreateRotationY turns +Z, so east is a quarter turn - how long it is, and how far it
        // turns towards its +X (north, heading east) over that.
        public readonly record struct Piece(Vector2 Start, float Heading, float Length, float Bend)
        {
            public Vector2 End => At(Length);
            public Vector2 Middle => At(Length / 2f);
            public float EndHeading => Heading + Bend;

            // Where it is `s` along it, in the world
            public Vector2 At(float s)
            {
                var (at, _, _) = RailwayMesh.Along(Length, Bend, s);
                var (sin, cos) = MathF.SinCos(Heading);
                return Start + new Vector2(at.X * cos + at.Y * sin, -at.X * sin + at.Y * cos);
            }
        }

        // The whole line, piece by piece, from the buffer stop.
        public static IReadOnlyList<Piece> Pieces { get; } = LayPieces();

        private static List<Piece> LayPieces()
        {
            var pieces = new List<Piece>();
            var at = new Vector2(BufferX, 0f);
            var heading = MathHelper.PiOver2;

            void Straight(float length)
            {
                var count = (int)MathF.Ceiling(length / PieceLength);
                for (var k = 0; k < count; k++)
                {
                    var piece = new Piece(at, heading, length / count, 0f);
                    pieces.Add(piece);
                    at = piece.End;
                }
            }

            Straight(CurveStartX - BufferX);
            for (var k = 0; k < CurvePieces; k++)
            {
                var piece = new Piece(at, heading, CurveRadius * MathHelper.PiOver2 / CurvePieces, MathHelper.PiOver2 / CurvePieces);
                pieces.Add(piece);
                at = piece.End;
                heading = piece.EndHeading;
            }
            Straight(at.Y - NorthEndZ);
            return pieces;
        }

        private static readonly Vector2 CurveCentre = new Vector2(CurveStartX, -CurveRadius);

        // Points every `step` round the curve, from its start heading east to its end heading north.
        private static IEnumerable<Vector2> RoundTheCurve(float step)
        {
            var count = (int)MathF.Ceiling(CurveRadius * MathHelper.PiOver2 / step);
            for (var k = 0; k <= count; k++)
            {
                var angle = MathHelper.PiOver2 * k / count;
                yield return CurveCentre + CurveRadius * new Vector2(MathF.Sin(angle), MathF.Cos(angle));
            }
        }

        // Levelled at the plateau's height all the way: the formation, easing back into the land either side over
        // 8 m, and the abutments by the bridge. The curve's a chain of squares, pads being square to the world.
        public IEnumerable<TerrainGenerator.Pad> Pads
        {
            get
            {
                var level = PlateauCentre;   // the plateau's level everywhere on it
                TerrainGenerator.Pad Along(float x0, float x1, float half, float apron, float blend) =>
                    new TerrainGenerator.Pad(new Vector2((x0 + x1) / 2f, 0f), new Vector2((x1 - x0) / 2f, half), apron, blend, 0f, level);

                var westEnd = BridgeWest - AbutmentLength;
                var eastStart = BridgeEast + AbutmentLength;
                yield return Along(BufferX - 12f, westEnd, FormationHalf - 1f, 1f, 8f);
                yield return Along(westEnd - 1f, BridgeWest, FormationHalf, 0f, 0.5f);
                yield return Along(BridgeEast, eastStart + 1f, FormationHalf, 0f, 0.5f);
                yield return Along(eastStart, CurveStartX, FormationHalf - 1f, 1f, 8f);
                foreach (var p in RoundTheCurve(2f))
                    yield return new TerrainGenerator.Pad(p, new Vector2(FormationHalf - 1f), 1f, 8f, 0f, level);
                yield return new TerrainGenerator.Pad(new Vector2(CoastLineX, (NorthEndZ - CurveRadius) / 2f),
                                                      new Vector2(FormationHalf - 1f, (-CurveRadius - NorthEndZ) / 2f), 1f, 8f, 0f, level);
            }
        }

        public static Color[] Palette() => RailwayMesh.Palette(
            beam: new Color(185, 180, 170), platform: new Color(175, 170, 160), edge: new Color(235, 220, 110),
            steel: new Color(70, 95, 120), gate: new Color(150, 45, 35), buffer: new Color(200, 45, 40));

        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var palette = Palette();
            foreach (var piece in Pieces)
                yield return new Fixture(BeamSource(piece.Length, piece.Bend, palette),
                    Matrix.CreateRotationY(piece.Heading) * Matrix.CreateTranslation(piece.Start.X, Formation, piece.Start.Y));

            // The bridge and its gates, turned from along Z to along X
            var span = BridgeEast - BridgeWest;
            var eastwards = Matrix.CreateRotationY(MathHelper.PiOver2);
            yield return new Fixture(new MeshSource($"railway-bridge:{span:F3}", d => RailwayMesh.Bridge(d, span, BridgeWidth, BridgeHeight, DeckDepth), palette),
                eastwards * Matrix.CreateTranslation(BridgeMiddle, Formation, 0f));
            var gate = new MeshSource("railway-gate", d => RailwayMesh.Gate(d, BridgeWidth - 0.4f, GateHeight), palette);
            foreach (var x in GateXs)
                yield return new Fixture(gate, eastwards * Matrix.CreateTranslation(x, Formation, 0f));

            yield return new Fixture(new MeshSource("railway-buffer-stop", RailwayMesh.BufferStop, palette),
                eastwards * Matrix.CreateTranslation(BufferX, Formation, 0f));
        }

        // A piece's mesh, known by what shapes it: made here, so its build holds nothing else of the piece's
        private static MeshSource BeamSource(float length, float bend, Color[] palette) =>
            new MeshSource($"railway-beam:{length:F3}:{bend:F5}", d => RailwayMesh.Beam(d, length, bend), palette);

        public static IEnumerable<float> GateXs => new[] { BridgeWest + GateInset, BridgeEast - GateInset };

        // The bridge's trusses and its shut gates enclose its deck, and the buffer stop stands over the beam's end
        public IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            var bottom = Formation - DeckDepth;
            foreach (var z in new[] { -BridgeWidth / 2f, BridgeWidth / 2f })
                yield return new WallSegment(new Vector2(BridgeWest, z), new Vector2(BridgeEast, z), bottom, Formation + BridgeHeight);
            foreach (var x in GateXs)
                yield return new WallSegment(new Vector2(x, -BridgeWidth / 2f), new Vector2(x, BridgeWidth / 2f), bottom, Formation + GateHeight);

            float back = BufferX - RailwayMesh.BufferStopDepth, half = RailwayMesh.BufferStopWidth / 2f;
            var corners = new[] { new Vector2(BufferX, -half), new Vector2(BufferX, half), new Vector2(back, half), new Vector2(back, -half) };
            for (var k = 0; k < corners.Length; k++)
                yield return new WallSegment(corners[k], corners[(k + 1) % corners.Length], Formation - 0.3f, Formation + RailwayMesh.BufferStopHeight);
        }

        // The beam, to stand on and walk along: every piece a straight, or a chain of short ones round a bend
        public IEnumerable<Ledge> Ledges(Terrain terrain)
        {
            foreach (var piece in Pieces)
            {
                var count = piece.Bend == 0f ? 1 : (int)MathF.Ceiling(piece.Length / LedgeStep);
                for (var k = 0; k < count; k++)
                    yield return new Ledge(piece.At(piece.Length * k / count), piece.At(piece.Length * (k + 1) / count), RailwayMesh.BeamWidth / 2f,
                                           Formation - 0.3f, BeamTop);
            }
        }

        // The bridge's deck, to stand on (there's no ground under it but the gorge): drawn with the bridge, so it's
        // only a body in the world, not a thing to draw.
        public IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain)
        {
            world.Add(Body.Fixed("bridge deck", new Vector3(BridgeEast - BridgeWest, DeckDepth, BridgeWidth), new Vector3(BridgeMiddle, Formation - DeckDepth, 0f)));
            yield break;
        }

        public IReadOnlyDictionary<string, Start> Starts { get; } = new Dictionary<string, Start>
        {
            ["bridge"] = new(new Vector2(BridgeWest - 8f, 0f), MathHelper.PiOver2, Above: 2f),       // on the beam, facing the bridge's gate
            ["coastline"] = new(new Vector2(CoastLineX, -250f), 0f, Above: 2f),                     // on the beam along the cliff top, facing north
        };
    }
}
