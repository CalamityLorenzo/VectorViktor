using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Maps;

namespace Maps.Home
{
    // A cottage like the ones by the lane: HouseMesh scaled up to 9 m along its front, which faces `Facing` (the lane's
    // face east, onto it). It has no insides, just its four walls to walk into, and a window onto somewhere else (see
    // Window): what's beyond it, `beyond`, as if behind it, or with nothing beyond it, a pane always shut. Its door, in
    // its front, is a Portal if the district makes it one.
    //
    // It's a district of its own, its walls, its mesh and its window, so a map file can put one up anywhere as a kind
    // of building (see HomeMap.AddTo), and the lane gathers its two into its own.
    public sealed class LaneCottage : IDistrict
    {
        public const float Scale = 4.5f;
        public const float DoorHalf = HouseMesh.DoorWidth * Scale / 2f;

        // Half its footprint as if it faced east: its front along Z
        public static readonly Vector2 Half = new Vector2(HouseMesh.Depth, HouseMesh.Width) * (Scale / 2f);

        // How far along its front, from its middle, its door and its window are: facing east, south is along (the
        // mesh's -X end of its front is the north end, turned)
        public const float DoorAlong = HouseMesh.DoorOffset * Scale, WindowAlong = HouseMesh.WindowOffset * Scale;

        public string Id { get; }
        public Vector2 At { get; }
        public Color[] Palette { get; }
        public float Facing { get; }   // the way its front faces, clockwise from north, as a start's yaw
        private readonly Func<IReadOnlyList<ScenePart>> _beyond;

        public LaneCottage(string id, Vector2 at, Color[] palette, Func<IReadOnlyList<ScenePart>> beyond, float facing = MathHelper.PiOver2)
        {
            Id = id;
            At = at;
            Palette = palette;
            Facing = facing;
            _beyond = beyond;
        }

        // From as if it faced east to as it does, about its middle: the turn that takes east to `Facing`
        private Matrix Turn => Matrix.CreateRotationY(MathHelper.PiOver2 - Facing);

        // A point as if it faced east, `x` out from its middle towards the front and `z` south along it, where it is
        private Vector2 Place(float x, float z)
        {
            var p = Vector3.Transform(new Vector3(x, 0f, z), Turn);
            return At + new Vector2(p.X, p.Z);
        }

        // The point `along` its front from the middle (see DoorAlong), `out` in front of it
        public Vector2 OnFront(float along, float @out = 0f) => Place(Half.X + @out, along);

        // Its front door's two sides, on the ground: for a Portal
        public (Vector2 A, Vector2 B) Doorway => (OnFront(DoorAlong - DoorHalf), OnFront(DoorAlong + DoorHalf));

        public float Ground(Terrain terrain) => terrain.HeightAt(At.X, At.Y);

        // Half its footprint as it stands, across the world's X and Z
        public Vector2 Footprint
        {
            get
            {
                var (x, z) = (Place(Half.X, Half.Y) - At, Place(Half.X, -Half.Y) - At);
                return new Vector2(MathF.Max(MathF.Abs(x.X), MathF.Abs(z.X)), MathF.Max(MathF.Abs(x.Y), MathF.Abs(z.Y)));
            }
        }

        // Its plot, a metre round it, level with `levelWith`
        public TerrainGenerator.Pad Pad(Vector2 levelWith) =>
            new TerrainGenerator.Pad(At, Footprint + new Vector2(1f), Apron: 1f, Blend: 4f, LevelWith: levelWith);

        // Its four walls, up to the eaves
        public IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            var floor = Ground(terrain);
            var corners = new[] { Place(-Half.X, -Half.Y), Place(Half.X, -Half.Y), Place(Half.X, Half.Y), Place(-Half.X, Half.Y) };
            for (var k = 0; k < 4; k++)
                yield return new WallSegment(corners[k], corners[(k + 1) % 4], floor - 0.2f, floor + HouseMesh.WallHeight * Scale);
        }

        // Its front window, and what's beyond it in its own space: the window's +Z, back from it, into the cottage, its
        // +X along the front (south, facing east), and its origin on the ground under the window. Its pane's the colour
        // the window's is, drawn shut.
        public Window FrontWindow(Terrain terrain) =>
            new Window(Matrix.CreateRotationY(-MathHelper.PiOver2) * Matrix.CreateTranslation(Half.X, 0f, WindowAlong) * Standing(terrain),
                HouseMesh.WindowWidth * Scale, HouseMesh.WindowSill * Scale, HouseMesh.WindowHeight * Scale,
                Palette[HouseMesh.WindowBase + MeshBuilder.Dim], _beyond?.Invoke() ?? Array.Empty<ScenePart>());

        // Its window, if it looks onto anything: with nothing beyond it, it's only ever its pane, shut
        public IEnumerable<Window> Windows(Terrain terrain)
        {
            if (_beyond != null)
                yield return FrontWindow(terrain);
        }

        // Its front turned to face the way it does (the mesh's front faces -Z: turned a quarter, east), stood on its
        // floor (the mesh is centred on its height). Near its window, one with a hole for the window, to see through.
        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var at = Matrix.CreateScale(Scale) * Matrix.CreateRotationY(-MathHelper.PiOver2) *
                Matrix.CreateTranslation(0f, HouseMesh.Height / 2f * Scale, 0f) * Standing(terrain);
            var shut = new MeshSource(Id, HouseMesh.Build, Palette);
            if (_beyond == null)
            {
                yield return new Fixture(shut, at);
                yield break;
            }
            var window = FrontWindow(terrain);
            yield return new Fixture(shut, at, you => !window.Open(you));
            yield return new Fixture(new MeshSource(Id + "-open", d => HouseMesh.Build(d, openWindow: true), Palette), at, window.Open);
        }

        // From as if it faced east, its middle on the origin, to where it stands
        private Matrix Standing(Terrain terrain) => Turn * Matrix.CreateTranslation(At.X, Ground(terrain), At.Y);
    }
}
