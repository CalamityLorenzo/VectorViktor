using MeshProps;
using MeshProps.Helpers;
using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core;

namespace Basic.World
{
    // Pieces of road laid end to end (see RoadMesh): what they cost the ground, what they draw, and where they are.
    // A district lays its own - the street's, the lane's - and a network answers for those alone.
    public sealed class RoadNetwork
    {
        // How high a road is somewhere: level with the hills at With, and Raise above them.
        public readonly record struct Level(Vector2 With, float Raise = 0f);

        // A piece of road (see RoadMesh), laid on its own pad of levelled ground.
        public abstract record Piece
        {
            // How far its pad blends back into the hills
            public float Blend { get; init; } = 6f;
        }

        // A straight from A to B, sloping evenly between the levels at its two ends.
        public sealed record Straight(Vector2 A, Vector2 B, Level AtA, Level AtB) : Piece;

        // A T-junction, the through road along Z turned by Turn, its side road off to the turned +X (see RoadMesh.TJunction).
        public sealed record Junction(Vector2 Centre, float Turn, Level At) : Piece;

        // A quarter-turn bend of radius BendRadius, turned by Turn (see RoadMesh.Corner): in heading the turned +Z, out heading the turned +X.
        public sealed record Bend(Vector2 Centre, float Turn, Level At) : Piece;

        // Centre line to the back of the pavement
        public static readonly float Half = RoadBuilder.LaneWidth + RoadBuilder.PavementWidth;

        private const float JunctionSize = 14f, JunctionCorner = 3f, BendRadius = 10f;
        private const float Lift = 0.03f;

        private readonly Piece[] _pieces;

        // Round each piece, in the map, for ruling most of them out at once: the square its turned footprint fits in
        private readonly (float minX, float maxX, float minZ, float maxZ)[] _bounds;

        public RoadNetwork(params Piece[] pieces)
        {
            _pieces = pieces;
            _bounds = Array.ConvertAll(pieces, piece =>
            {
                var (centre, reach) = piece switch
                {
                    Straight s => ((s.A + s.B) / 2f, (s.B - s.A).Length() / 2f + Half),
                    Junction j => (j.Centre, JunctionSize),
                    Bend b => (b.Centre, BendRadius * 1.5f),
                    _ => (Vector2.Zero, 0f),
                };
                return (centre.X - reach, centre.X + reach, centre.Y - reach, centre.Y + reach);
            });
        }

        // Each piece's ground, level or sloping with it: for pieces that aren't on some longer pad of their own
        // district's (the street's, say). Junctions have none.
        public IEnumerable<TerrainGenerator.Pad> Pads()
        {
            foreach (var piece in _pieces)
                switch (piece)
                {
                    case Straight s:
                        var half = s.A.X == s.B.X
                            ? new Vector2(Half, MathF.Abs(s.B.Y - s.A.Y) / 2f)
                            : new Vector2(MathF.Abs(s.B.X - s.A.X) / 2f, Half);
                        yield return new TerrainGenerator.Pad((s.A + s.B) / 2f, half, Apron: 1f, Blend: s.Blend, Raise: s.AtA.Raise, LevelWith: s.AtA.With,
                            Slope: new TerrainGenerator.PadSlope(s.A, s.B, s.AtB.With, s.AtB.Raise));
                        break;
                    case Bend b:
                        yield return new TerrainGenerator.Pad(b.Centre, new Vector2(BendRadius), Apron: 0.5f, Blend: b.Blend, Raise: b.At.Raise, LevelWith: b.At.With);
                        break;
                }
        }

        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var palette = RoadMesh.Palette(new Color(70, 70, 75), new Color(170, 165, 155), Color.White, new Color(60, 140, 50));
            // A little above its pad, besides the tarmac's own 2 cm: seen far off and edge on, the faces' depth bias
            // (see MeshInstance) would otherwise let the ground show through along the joins
            float Height(Vector2 p) => terrain.HeightAt(p.X, p.Y) + Lift;
            foreach (var piece in _pieces)
                switch (piece)
                {
                    case Straight s:
                    {
                        // Sheared up or down to lie on its pad - every point lifted by the rise over its distance along, not
                        // turned, so its kerbs stay upright and its ends stand exactly over the next pieces' ends
                        var along = s.B - s.A;
                        var length = along.Length();
                        var shear = Matrix.Identity;
                        shear.M32 = (Height(s.B) - Height(s.A)) / length;   // y += rise * z, z along it from A to B
                        var middle = (s.A + s.B) / 2f;
                        yield return new Fixture(new MeshSource($"road-straight:{length:F3}", d => RoadMesh.Straight(d, length), palette),
                            shear * Matrix.CreateRotationY(MathF.Atan2(along.X, along.Y)) *
                            Matrix.CreateTranslation(middle.X, (Height(s.A) + Height(s.B)) / 2f, middle.Y));
                        break;
                    }
                    case Junction j:
                        yield return new Fixture(new MeshSource($"road-tjunction:{JunctionSize}:{JunctionCorner}",
                                d => RoadMesh.TJunction(d, JunctionSize, JunctionCorner), palette),
                            Matrix.CreateRotationY(j.Turn) * Matrix.CreateTranslation(j.Centre.X, Height(j.Centre), j.Centre.Y));
                        break;
                    case Bend b:
                        yield return new Fixture(new MeshSource($"road-corner:{BendRadius}", d => RoadMesh.Corner(d, BendRadius), palette),
                            Matrix.CreateRotationY(b.Turn) * Matrix.CreateTranslation(b.Centre.X, Height(b.Centre), b.Centre.Y));
                        break;
                }
        }

        // Under a road: no terrain grid there (it'd show through the tarmac, 2 cm above it)
        public bool Paved(float x, float z)
        {
            for (var k = 0; k < _pieces.Length; k++)
            {
                var (minX, maxX, minZ, maxZ) = _bounds[k];
                if (x >= minX && x <= maxX && z >= minZ && z <= maxZ && Covers(_pieces[k], x, z))
                    return true;
            }
            return false;
        }

        // A point in a piece's own plan: turned back by `turn`, about its centre.
        private static Vector2 Local(Vector2 p, Vector2 centre, float turn)
        {
            var d = p - centre;
            var (sin, cos) = MathF.SinCos(turn);
            return new Vector2(d.X * cos - d.Y * sin, d.X * sin + d.Y * cos);
        }

        // Whether a piece's tarmac and pavements are over (x, z)
        private static bool Covers(Piece piece, float x, float z)
        {
            var p = new Vector2(x, z);
            switch (piece)
            {
                case Straight s:
                    var along = s.B - s.A;
                    var local = Local(p, (s.A + s.B) / 2f, MathF.Atan2(along.X, along.Y));
                    return MathF.Abs(local.X) <= Half && MathF.Abs(local.Y) <= along.Length() / 2f;
                case Junction j:
                    var inJunction = Local(p, j.Centre, j.Turn);
                    return inJunction.X >= -Half && inJunction.X <= JunctionSize / 2f && MathF.Abs(inJunction.Y) <= JunctionSize / 2f;
                case Bend b:
                    var inBend = Local(p, b.Centre, b.Turn);
                    var fromMiddle = Vector2.Distance(inBend, new Vector2(BendRadius, -BendRadius));   // the centre line curves round this
                    return MathF.Abs(inBend.X) <= BendRadius && MathF.Abs(inBend.Y) <= BendRadius && MathF.Abs(fromMiddle - BendRadius) <= Half;
                default:
                    return false;
            }
        }
    }
}
