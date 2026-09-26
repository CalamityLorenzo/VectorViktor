using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;

namespace Basic.World
{
    // A cottage by the lane: HouseMesh scaled up to 9 m along its front, which faces east, onto the lane, a small front
    // garden back from the pavement. It has no insides, just its four walls to walk into, and a window onto somewhere
    // else (see Window): what's beyond it, `Beyond`, as if behind it. Its door, in the east wall, is a Portal if the
    // district makes it one.
    public sealed class LaneCottage
    {
        public const float Scale = 4.5f;
        public const float DoorHalf = HouseMesh.DoorWidth * Scale / 2f;

        // Half its footprint, turned: its front along Z
        public static readonly Vector2 Half = new Vector2(HouseMesh.Depth, HouseMesh.Width) * (Scale / 2f);

        public string Id { get; }
        public Vector2 At { get; }
        public Color[] Palette { get; }
        private readonly Func<IReadOnlyList<ScenePart>> _beyond;

        public LaneCottage(string id, Vector2 at, Color[] palette, Func<IReadOnlyList<ScenePart>> beyond)
        {
            Id = id;
            At = at;
            Palette = palette;
            _beyond = beyond;
        }

        public float Front => At.X + Half.X;
        public float WindowZ => At.Y + HouseMesh.WindowOffset * Scale;   // south of its door: the mesh's +X end of the front, turned
        public float DoorZ => At.Y + HouseMesh.DoorOffset * Scale;       // the mesh's -X end of the front is north, turned
        public float Ground(Terrain terrain) => terrain.HeightAt(At.X, At.Y);

        // Its plot, a metre round it, level with `levelWith`
        public TerrainGenerator.Pad Pad(Vector2 levelWith) =>
            new TerrainGenerator.Pad(At, Half + new Vector2(1f), Apron: 1f, Blend: 4f, LevelWith: levelWith);

        // Its four walls, up to the eaves
        public IEnumerable<WallSegment> Walls(Terrain terrain)
        {
            var floor = Ground(terrain);
            var corners = new[]
            {
                At - Half, At + new Vector2(Half.X, -Half.Y),
                At + Half, At + new Vector2(-Half.X, Half.Y),
            };
            for (var k = 0; k < 4; k++)
                yield return new WallSegment(corners[k], corners[(k + 1) % 4], floor - 0.2f, floor + HouseMesh.WallHeight * Scale);
        }

        // Its front window, and what's beyond it in its own space: the window's +Z, back from it, turned west, into the
        // cottage, its +X south, along the front, and its origin on the ground under the window. Its pane's the colour
        // the window's is, drawn shut.
        public Window FrontWindow(Terrain terrain) =>
            new Window(Matrix.CreateRotationY(-MathHelper.PiOver2) * Matrix.CreateTranslation(Front, Ground(terrain), WindowZ),
                HouseMesh.WindowWidth * Scale, HouseMesh.WindowSill * Scale, HouseMesh.WindowHeight * Scale,
                Palette[HouseMesh.WindowBase + MeshBuilder.Dim], _beyond());

        // Its front turned to face east (the mesh's -Z), stood on its floor (the mesh is centred on its height). Near
        // its window, one with a hole for the window, to see through.
        public IEnumerable<Fixture> Fixtures(Terrain terrain)
        {
            var at = Matrix.CreateScale(Scale) * Matrix.CreateRotationY(-MathHelper.PiOver2) *
                Matrix.CreateTranslation(At.X, Ground(terrain) + HouseMesh.Height / 2f * Scale, At.Y);
            var window = FrontWindow(terrain);
            yield return new Fixture(new MeshSource(Id, HouseMesh.Build, Palette), at, you => !window.Open(you));
            yield return new Fixture(new MeshSource(Id + "-open", d => HouseMesh.Build(d, openWindow: true), Palette), at, window.Open);
        }
    }
}
