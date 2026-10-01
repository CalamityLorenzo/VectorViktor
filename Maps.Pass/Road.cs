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
    // rise. It's laid in pieces of about PieceLength, each a fixture, so what's out of sight is left undrawn. Where
    // the crossings shape it (see Crossings) - down through the ford, up the jump's ramp - it's laid a metre at a time,
    // to follow them, and over the jump's pit there's none: it stops square at the lip and starts again at the wall.
    public sealed class Road : IDistrict
    {
        public const float PieceLength = 40f;
        public const float Verge = PassRoute.HalfWidth - RoadBuilder.LaneWidth;   // the tarmac's a lane either side of the middle
        private const float Shaped = 1f;   // how far apart its points are where the crossings shape it

        public static Color[] Palette() =>
            RoadMesh.Palette(tarmac: new Color(70, 70, 75), pavement: new Color(170, 165, 155), paint: Color.White, verge: new Color(80, 150, 65));

        // The middle line it's laid along, point by point from the trailhead, and how far along each is: PassRoute's
        // points, with more where the crossings shape it, and the lip and the wall's top exactly
        public static IReadOnlyList<(Vector3 at, float s)> Path { get; } = MakePath();

        // The whole road, piece by piece from the trailhead: which of the Path's points each runs through
        public static IReadOnlyList<(int from, int to)> Pieces { get; } = Divide();

        private static List<(Vector3, float)> MakePath()
        {
            var ss = new SortedSet<float> { Crossings.JumpAt, Crossings.JumpFar };
            foreach (var s in PassRoute.Along)
                ss.Add(s);
            for (var s = 0f; s < PassRoute.Length; s += Shaped)
                if (Crossings.Shaped(s))
                    ss.Add(s);
            var path = new List<(Vector3, float)>();
            foreach (var s in ss)
                if (!Crossings.InThePit(s) && (path.Count == 0 || s - path[^1].Item2 > 0.05f))
                {
                    var (at, road, _) = PassRoute.At(s);
                    path.Add((new Vector3(at.X, Crossings.Surface(s, road), at.Y), s));
                }
            return path;
        }

        private static List<(int from, int to)> Divide()
        {
            var pieces = new List<(int, int)>();
            var from = 0;
            for (var i = 1; i < Path.Count; i++)
            {
                if (Path[i].s == Crossings.JumpFar)   // the pit before it: a piece ends at the lip, the next starts here
                {
                    if (i - 1 > from)
                        pieces.Add((from, i - 1));
                    from = i;
                    continue;
                }
                if (Path[i].s - Path[from].s >= PieceLength || i == Path.Count - 1)
                {
                    pieces.Add((from, i));
                    from = i;
                }
            }
            return pieces;
        }

        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var palette = Palette();
            for (var k = 0; k < Pieces.Count; k++)
                yield return new Fixture(PieceSource(k, palette), Matrix.Identity);
        }

        // A piece's mesh, known by which piece it is: made here, so its build holds nothing else. It joins the pieces
        // either side smoothly, but not across the pit.
        private static MeshSource PieceSource(int k, Color[] palette)
        {
            var (from, to) = Pieces[k];
            var path = new Vector3[to - from + 1];
            for (var i = from; i <= to; i++)
                path[i - from] = Path[i].at;
            Vector3? before = from > 0 && Path[from].s != Crossings.JumpFar ? Path[from - 1].at : null;
            Vector3? after = to < Path.Count - 1 && Path[to].s != Crossings.JumpAt ? Path[to + 1].at : null;
            var along = Path[from].s;
            return new MeshSource($"pass-road:{k}", d => RoadMesh.Winding(d, path, Verge, along, before, after), palette);
        }

        // Under the road and its verges, and the cells over their edges - but not in the pit, which is bare ground
        public bool Bare(float x, float z)
        {
            var p = new Vector2(x, z);
            return PassRoute.Within(p, PassRoute.HalfWidth + 1f) && !InThePit(p);
        }

        // Under the road and its verges, edge to edge - but not round past either end of it, where it stops square,
        // nor in the pit, where there's no road
        public bool Covered(float x, float z)
        {
            var p = new Vector2(x, z);
            if (!PassRoute.Within(p, PassRoute.HalfWidth))
                return false;
            var s = PassRoute.Nearest(p).s;
            return s > 0f && s < PassRoute.Length && !Crossings.InThePit(s);
        }

        private static bool InThePit(Vector2 p)
        {
            var s = PassRoute.Nearest(p).s;
            return s > Crossings.JumpAt - 1f && s < Crossings.JumpFar + 1f;
        }
    }
}
