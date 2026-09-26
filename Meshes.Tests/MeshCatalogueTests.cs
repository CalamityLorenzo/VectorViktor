using Basic.World;
using MeshCore.Library;
using MeshProps;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using World.Rendering;
using Xunit;

namespace Meshes.Tests
{
    // Every mesh the game or its demos draw, built on the CPU (see MeshData.Headless) and held to what a mesh must be
    // (see MeshChecks): whole triangles and lines, finite, inside its bounds, and a palette with a colour for every
    // slot its faces use. In a debug build, which these run in, that includes MeshBuilder's check that no polygon's
    // fan folds back.
    public class MeshCatalogueTests
    {
        // Every MeshProps builder that needs nothing but the device (the rest have their own tests, below)
        public static IEnumerable<object[]> DefaultBuilders() =>
            typeof(BillboardMesh).Assembly.GetTypes()
                .Where(t => t.IsAbstract && t.IsSealed && t.IsPublic)   // static classes
                .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
                .Where(m => m.ReturnType == typeof(MeshData) && m.GetParameters().Length > 0 &&
                            m.GetParameters()[0].ParameterType == typeof(GraphicsDevice) &&
                            m.GetParameters().Skip(1).All(p => p.IsOptional))
                .Select(m => new object[] { $"{m.DeclaringType.Name}.{m.Name}", m });

        [Theory]
        [MemberData(nameof(DefaultBuilders))]
        public void A_prop_builds_to_a_sound_mesh(string name, MethodInfo build)
        {
            var arguments = build.GetParameters().Select(p => p.ParameterType == typeof(GraphicsDevice) ? null : p.DefaultValue).ToArray();
            var mesh = (MeshData)build.Invoke(null, arguments);
            MeshChecks.IsSound(name, mesh, PaletteFor(build.DeclaringType));
        }

        // Its own Palette(...), given colours to fill it with, if it has one that takes nothing else
        private static Color[] PaletteFor(Type type)
        {
            var palette = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Palette" && m.ReturnType == typeof(Color[]) && m.GetParameters().All(p => p.ParameterType == typeof(Color)));
            return (Color[])palette?.Invoke(null, palette.GetParameters().Select(_ => (object)Color.Gray).ToArray());
        }

        [Fact]
        public void The_props_that_take_more_than_a_device_build_to_sound_meshes()
        {
            var meshes = new Dictionary<string, MeshData>
            {
                ["billboard: Commodore 64"] = BillboardMesh.Build(null, BillboardDesign.Commodore64),
                ["billboard: Atari"] = BillboardMesh.Build(null, BillboardDesign.Atari),
                ["hangar with floor"] = HangarMesh.Build(null, true),
                ["hangar without floor"] = HangarMesh.Build(null, false),
                ["house"] = HouseMesh.Build(null, false),
                ["house, window open"] = HouseMesh.Build(null, true),
                ["ladder"] = LadderMesh.Build(null, 3f, 0.8f),
                ["platform"] = PlatformMesh.Build(null, 4f, 10f, 0.2f),
                ["pool surround"] = PoolSurroundMesh.Build(null, 4f, 2.5f),
                ["fence"] = FenceMesh.Build(null, (x, z) => 0f, new[] { new Vector2(0f, 0f), new Vector2(5f, 0f), new Vector2(5f, 4f) }),
                ["straight stair"] = StaircaseMesh.BuildStraight(null, 8),
                ["quarter-turn stair"] = StaircaseMesh.BuildQuarterTurn(null, 4, 4, StairTurn.Left),
                ["seating"] = SeatingBuilder.Build(null, 3, 0.2f, 0.3f, 0.15f, 0.4f),
            };
            foreach (var kind in Enum.GetValues<CrateKind>())
                meshes[$"crate: {kind}"] = CrateMesh.Build(null, kind, new Vector3(1f, 0.8f, 1.2f));
            foreach (var (name, mesh) in meshes)
                MeshChecks.IsSound(name, mesh);
        }

        // ---- What the world is made of

        private static readonly Lazy<BuiltWorld> World = new Lazy<BuiltWorld>(() => WorldBuilder.Build(WorldBuilder.Standard()));

        [Fact]
        public void Everything_the_world_draws_from_a_source_builds_to_a_sound_mesh_its_palette_covers()
        {
            var world = World.Value;
            var sources = world.Fixtures.Select(f => f.Mesh)
                .Concat(world.Things.Select(t => t.Mesh))
                .Concat(world.Moving.Select(m => m.Mesh))
                .Concat(world.Windows.SelectMany(w => w.Beyond).Select(p => p.Mesh))
                .Concat(world.Buildings.Select(BuildingMesh.Source))
                .Concat(world.Buildings.SelectMany(b => b.Rooms).SelectMany(r => r.Props).Select(p => p.Mesh))
                .Concat(world.Buildings.SelectMany(b => world.Ground.DoorsOf(b)).Select(DoorMesh.Source))
                .ToList();
            Assert.True(sources.Count > 50, $"only {sources.Count} sources: is the world empty?");

            foreach (var source in sources)
                MeshChecks.IsSound(source.Key, source.Build(null), source.Palette);
        }

        [Fact]
        public void The_rooms_and_the_ponds_and_lakes_build_to_sound_meshes()
        {
            var world = World.Value;
            foreach (var room in world.Buildings.SelectMany(b => b.Rooms))
                MeshChecks.IsSound("room " + room.Id, RoomMesh.Build(null, room), RoomMesh.Palette(room));
            foreach (var pool in world.Terrain.Pools)
                MeshChecks.IsSound("pool at " + pool.Centre, WaterMesh.Build(null, pool), WaterMesh.Palette());
        }

        [Fact]
        public void The_terrain_round_the_start_builds_to_sound_chunks()
        {
            var terrain = World.Value.Terrain;
            var (ci, cj) = ((terrain.ChunksX / 2), (terrain.ChunksZ / 2));   // the middle of the world, where you start
            for (var dj = -1; dj <= 1; dj++)
                for (var di = -1; di <= 1; di++)
                {
                    var (i0, j0, cellsX, cellsZ) = terrain.ChunkCellsOf(ci + di, cj + dj);
                    MeshChecks.IsSound($"terrain chunk {ci + di},{cj + dj}", TerrainMesh.Build(null, terrain, 0.5f, i0, j0, cellsX, cellsZ, World.Value.Bare), TerrainMesh.Palette());
                }
        }
    }
}
