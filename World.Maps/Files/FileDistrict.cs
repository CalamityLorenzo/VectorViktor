using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using World.Buildings;
using World.Core;
using World.Core.Physics;

namespace World.Maps.Files
{
    // A district from a file (see DistrictFile): each of its entries made into what an IDistrict hands over, its names
    // looked up in `library`. Every name's looked up when it's made, so a file naming something there isn't fails at
    // once, saying what there is, rather than when the world's half built.
    public sealed class FileDistrict : IDistrict
    {
        private readonly DistrictFile _file;
        private readonly BuildingKind[] _kinds;
        private readonly CatalogueItem[] _props, _things;

        // `name` is what it's called (its file's name), for naming the things in it and in any mistake found.
        public FileDistrict(string name, DistrictFile file, MapLibrary library)
        {
            Name = name;
            _file = file;
            try
            {
                _kinds = file.Buildings.Select(b => library.BuildingKind(b.Kind)).ToArray();
                for (var i = 0; i < _kinds.Length; i++)
                    if (_kinds[i].Mistake(file.Buildings[i]) is { } mistake)
                        throw new InvalidDataException($"Building '{file.Buildings[i].Id}': {mistake}");
                _props = file.Props.Select(p => library.Item(p.Item)).ToArray();
                _things = file.Things.Select(t => library.Item(t.Item)).ToArray();
                var starts = new Dictionary<string, Start>();
                foreach (var start in file.Starts)
                    if (string.IsNullOrEmpty(start.Name) || !starts.TryAdd(start.Name, ToStart(start)))
                        throw new InvalidDataException($"Its starts need names, each different: '{start.Name}' isn't.");
                Starts = starts;
            }
            catch (InvalidDataException e)
            {
                throw new InvalidDataException($"District '{name}': {e.Message}", e);
            }
        }

        public string Name { get; }

        public IReadOnlyDictionary<string, Start> Starts { get; }

        public static float Radians(float degrees) => MathHelper.ToRadians(degrees);

        // A start's facing (see Start): the file's degrees, clockwise from north, as a yaw.
        private static Start ToStart(StartEntry start) => new Start(start.At, Radians(start.Yaw), start.Above, start.InCar);

        // The turn about the vertical that faces a mesh (which faces +Z, south) `degrees` clockwise from north.
        public static float MeshTurn(float degrees) => MathHelper.Pi - Radians(degrees);

        // Where a thing's box is and which way its mesh faces: a body's box can only face the four ways, so the turn's to
        // the nearest quarter, and a quarter turn either way swaps the box's width and depth.
        public static (Vector3 size, float turn) ThingBox(Vector3 size, float degrees)
        {
            var quarters = ((int)MathF.Round(degrees / 90f) % 4 + 4) % 4;
            return (quarters % 2 == 1 ? new Vector3(size.Z, size.Y, size.X) : size, MeshTurn(quarters * 90f));
        }

        // Where a prop stands: turned, on the ground (or Above it).
        public static Matrix PropStand(PropEntry prop, Terrain terrain) =>
            Matrix.CreateRotationY(MeshTurn(prop.Turn)) *
            Matrix.CreateTranslation(prop.At.X, terrain.HeightAt(prop.At.X, prop.At.Y) + prop.Above, prop.At.Y);

        // Its mesh there, at its scale (about its foot, so it still stands on the ground)
        public static Matrix PropPlace(PropEntry prop, Terrain terrain) => Matrix.CreateScale(prop.Scale ?? 1f) * PropStand(prop, terrain);

        // Its box, at its scale: the catalogue's size of it, times its own
        public static Vector3 PropSize(PropEntry prop, CatalogueItem item) => item.Size * (prop.Scale ?? 1f);

        // A building's pad, under its footprint
        public static TerrainGenerator.Pad BuildingPad(BuildingEntry building, BuildingKind kind) => new TerrainGenerator.Pad(building.At, kind.HalfOf(building));

        public IEnumerable<TerrainGenerator.Pad> Pads =>
            _file.Pads.Select(p => new TerrainGenerator.Pad(p.Centre, p.Half, p.Apron ?? 2f, p.Blend ?? 6f, p.Raise, p.LevelWith))
                .Concat(_file.Buildings.Select((b, i) => BuildingPad(b, _kinds[i])));

        public IEnumerable<Pool> Pools(Terrain terrain) =>
            _file.Pools.Select(p => new Pool(p.Centre, p.Radius, p.Level, p.Half ?? Vector2.Zero, p.Shore));

        private Terrain _putUpOn;
        private IDistrict[] _putUp;

        // What each building puts up (see BuildingKind), on this terrain: once, though each part of it's asked for
        // separately, so its rooms are only ever built the once
        private IDistrict[] PutUp(Terrain terrain)
        {
            if (_putUpOn != terrain)
            {
                _putUp = _file.Buildings.Select((b, i) => _kinds[i].Put(b, new Vector3(b.At.X, terrain.HeightAt(b.At.X, b.At.Y) + BuildingKind.Step, b.At.Y))).ToArray();
                _putUpOn = terrain;
            }
            return _putUp;
        }

        public IEnumerable<Building> Buildings(Terrain terrain) => PutUp(terrain).SelectMany(b => b.Buildings(terrain));

        public IEnumerable<Fixture> Fixtures(Terrain terrain) =>
            _file.Props.Select((p, i) => new Fixture(_props[i].Mesh, PropPlace(p, terrain)))
                .Concat(PutUp(terrain).SelectMany(b => b.Fixtures(terrain)));

        public IEnumerable<Window> Windows(Terrain terrain) => PutUp(terrain).SelectMany(b => b.Windows(terrain));

        public IEnumerable<WallSegment> Walls(Terrain terrain) => PropWalls(terrain).Concat(PutUp(terrain).SelectMany(b => b.Walls(terrain)));

        // The four sides of each solid prop's box, to walk into
        private IEnumerable<WallSegment> PropWalls(Terrain terrain)
        {
            for (var i = 0; i < _file.Props.Count; i++)
            {
                if (!_props[i].Solid)
                    continue;
                var size = PropSize(_file.Props[i], _props[i]);
                var place = PropStand(_file.Props[i], terrain);
                var corners = new[] { new Vector3(-1, 0, -1), new Vector3(1, 0, -1), new Vector3(1, 0, 1), new Vector3(-1, 0, 1) }
                    .Select(c => Vector3.Transform(c * new Vector3(size.X / 2f, 0f, size.Z / 2f), place)).ToArray();
                var bottom = place.Translation.Y;
                for (var k = 0; k < 4; k++)
                {
                    var (a, b) = (corners[k], corners[(k + 1) % 4]);
                    yield return new WallSegment(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z), bottom, bottom + size.Y);
                }
            }
        }

        public IEnumerable<Thing> Things(PhysicsWorld world, Terrain terrain)
        {
            var named = new Dictionary<string, int>();
            for (var i = 0; i < _file.Things.Count; i++)
            {
                var thing = _file.Things[i];
                var item = _things[i];
                var (size, turn) = ThingBox(item.Size, thing.Turn);
                var count = named[thing.Item] = named.GetValueOrDefault(thing.Item) + 1;
                yield return Scenery.Prop(world, terrain, thing.Name ?? $"{Name}: {thing.Item} {count}", item.Mesh, size,
                    thing.Mass > 0f ? thing.Mass : item.Mass, thing.At.X, thing.At.Y, turn, thing.Above);
            }
        }

        public IEnumerable<Portal> Portals(Terrain terrain) =>
            _file.Portals.Select(p => new Portal(p.A, p.B, p.Floor, p.To, Radians(p.Yaw)));
    }
}
