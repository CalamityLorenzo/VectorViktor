using MeshCore.Library;
using MeshProps;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Core;
using World.Maps;

namespace Maps.Pass
{
    // The road, from the trailhead all the way to the town's basin, along the level strip the terrain's cut for it
    // (see PassRoute): tarmac down the middle, and a verge either side out to the strip's edge, where the mountains
    // rise. It's laid in pieces of about PieceLength, each a fixture, so what's out of sight is left undrawn.
    public sealed class Road : IDistrict
    {
        public const float PieceLength = 40f;
        public const float Verge = PassRoute.HalfWidth - RoadBuilder.LaneWidth;   // the tarmac's a lane either side of the middle

        public static Color[] Palette() =>
            RoadMesh.Palette(tarmac: new Color(70, 70, 75), pavement: new Color(170, 165, 155), paint: Color.White, verge: new Color(80, 150, 65));

        // The whole road, piece by piece from the trailhead: which of the middle line's points each runs through
        public static IReadOnlyList<(int from, int to)> Pieces { get; } = Divide();

        private static List<(int from, int to)> Divide()
        {
            var pieces = new List<(int, int)>();
            var from = 0;
            for (var i = 1; i < PassRoute.Points.Count; i++)
                if (PassRoute.Along[i] - PassRoute.Along[from] >= PieceLength || i == PassRoute.Points.Count - 1)
                {
                    pieces.Add((from, i));
                    from = i;
                }
            return pieces;
        }

        private static Vector3 Point(int i) => new Vector3(PassRoute.Points[i].X, PassRoute.Heights[i], PassRoute.Points[i].Y);

        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var palette = Palette();
            for (var k = 0; k < Pieces.Count; k++)
                yield return new Fixture(PieceSource(k, palette), Matrix.Identity);
        }

        // A piece's mesh, known by which piece it is: made here, so its build holds nothing else
        private static MeshSource PieceSource(int k, Color[] palette)
        {
            var (from, to) = Pieces[k];
            var path = new Vector3[to - from + 1];
            for (var i = from; i <= to; i++)
                path[i - from] = Point(i);
            Vector3? before = from > 0 ? Point(from - 1) : null;
            Vector3? after = to < PassRoute.Points.Count - 1 ? Point(to + 1) : null;
            var along = PassRoute.Along[from];
            return new MeshSource($"pass-road:{k}", d => RoadMesh.Winding(d, path, Verge, along, before, after), palette);
        }

        // Under the road and its verges, and the cells over their edges
        public bool Bare(float x, float z) => PassRoute.Within(new Vector2(x, z), PassRoute.HalfWidth + 1f);
    }
}
