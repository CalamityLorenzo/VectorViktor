using Maps.Coast;
using Maps.Home;
using Maps.Pass;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using World.Maps;
using World.Maps.Files;
using Xunit;

namespace World.Core.Tests
{
    // Map and district files (see World.Maps.Files): that they read back as they were written, that a map from a file is
    // the map it says it is, and that a mistake in one is caught at once and says what's wrong.
    public class MapFileTests
    {
        private static readonly Lazy<MapLibrary> Library = new Lazy<MapLibrary>(() =>
        {
            var library = new MapLibrary();
            HomeMap.AddTo(library);
            CoastMap.AddTo(library);
            PassMap.AddTo(library);
            return library;
        });

        private static string Temporary(string text, string name = "test.district.json")
        {
            var folder = Path.Combine(Path.GetTempPath(), "VectorViktorMapTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, name);
            File.WriteAllText(path, text);
            return path;
        }

        // Every file in the repository's Maps folder, as it'd be saved having been read: the same, to the character. So
        // the studio saving a map it hasn't changed leaves its files alone, and a change is the lines that changed.
        public static IEnumerable<object[]> MapFolderFiles() =>
            Directory.GetFiles(MapFolder.Find(), "*.json", SearchOption.AllDirectories).Select(p => new object[] { Path.GetRelativePath(MapFolder.Find(), p) });

        [Theory]
        [MemberData(nameof(MapFolderFiles))]
        public void AMapFolderFileSavesAsItWasRead(string file)
        {
            var path = Path.Combine(MapFolder.Find(), file);
            var text = File.ReadAllText(path).ReplaceLineEndings();
            var saved = path.EndsWith(MapFolder.MapExtension) ? MapJson.Write(MapFile.Load(path)) : MapJson.Write(DistrictFile.Load(path));
            Assert.Equal(text, saved.ReplaceLineEndings());
        }

        [Fact]
        public void EveryKindOfEntryRoundTrips()
        {
            var district = new DistrictFile
            {
                About = "every kind of entry, every value set",
                Pads = { new PadEntry(new Vector2(1.5f, -2f), new Vector2(3f, 4f), Apron: 0f, Blend: 0.5f, Raise: -1.25f, LevelWith: new Vector2(7f, 8f)) },
                Pools = { new PoolEntry(new Vector2(1f, 2f), 0f, 5f), new PoolEntry(new Vector2(3f, 4f), -1.5f, Half: new Vector2(2f, 1f), Shore: 0.4f) },
                Buildings = { new BuildingEntry("house.bungalow", "b1", new Vector2(-5f, 6f), "A \"quoted\", name: really", Side.South,
                                                new Color(1, 2, 3), new Color(250, 251, 252), Flat: true, Attic: true) },
                Props = { new PropEntry("plant.oak", Vector2.Zero, 33.5f, 0.25f, 1.5f) },
                Things = { new ThingEntry("crate.wood", new Vector2(0.1f, 0.2f), 90f, 1f, "named", 12.5f) },
                Starts = { new StartEntry("there", new Vector2(9f, 10f), -45f, 3f, InCar: true) },
                Portals = { new PortalEntry(new Vector2(1f, 1f), new Vector2(2f, 1f), 0.5f, new Vector3(4f, 5f, 6f), 180f) },
            };
            var text = MapJson.Write(district);
            var back = DistrictFile.Load(Temporary(text));
            Assert.Equal(text, MapJson.Write(back));
            Assert.Equal(district.Pads, back.Pads);
            Assert.Equal(district.Pools, back.Pools);
            Assert.Equal(district.Buildings, back.Buildings);
            Assert.Equal(district.Props, back.Props);
            Assert.Equal(district.Things, back.Things);
            Assert.Equal(district.Starts, back.Starts);
            Assert.Equal(district.Portals, back.Portals);
        }

        [Fact]
        public void WhatsAtItsDefaultIsLeftOut()
        {
            var district = new DistrictFile { Props = { new PropEntry("plant.oak", new Vector2(0f, 0f)) } };
            var text = MapJson.Write(district);
            Assert.Contains("{\"item\": \"plant.oak\", \"at\": [0, 0]}", text);   // where it is is always written, even at 0, 0
            Assert.DoesNotContain("turn", text);
            Assert.DoesNotContain("scale", text);   // 1, the catalogue's size
            Assert.DoesNotContain("pads", text);   // nor an empty list
        }

        // The home map's file: everything of the map built in code, as it was, and the yard from its district file (and
        // whatever else has been added to it since)
        [Fact]
        public void TheHomeMapFileHasTheHomeMapAndTheYard()
        {
            var fromFile = WorldBuilder.Build(Library.Value.Open("home"));
            var inCode = WorldBuilder.Build(HomeMap.Map);

            foreach (var (name, start) in inCode.Starts)
                Assert.Equal(start, fromFile.Starts[name]);
            Assert.True(fromFile.Starts.ContainsKey("yard"));
            Assert.Equal(inCode.Buildings.Select(b => b.Name), fromFile.Buildings.Select(b => b.Name).Take(inCode.Buildings.Count));
            Assert.True(fromFile.Things.Count >= inCode.Things.Count + 7);
            foreach (var start in new[] { "hills", "town", "street", "plateau", "pond" })
            {
                var at = inCode.Starts[start].At;
                Assert.Equal(inCode.Terrain.HeightAt(at.X, at.Y), fromFile.Terrain.HeightAt(at.X, at.Y));
            }
        }

        [Fact]
        public void TheCoastMapFileIsTheCoastMap()
        {
            var fromFile = WorldBuilder.Build(Library.Value.Open("coast"));
            var inCode = WorldBuilder.Build(CoastMap.Map);
            Assert.Equal(inCode.Starts, fromFile.Starts);
            Assert.Equal(inCode.Buildings.Select(b => b.Name), fromFile.Buildings.Select(b => b.Name));
            Assert.True(fromFile.Fixtures.Count >= inCode.Fixtures.Count);   // and whatever its district files add
            for (var x = -400f; x <= 400f; x += 37f)
                Assert.Equal(inCode.Terrain.HeightAt(x, x / 3f), fromFile.Terrain.HeightAt(x, x / 3f));
        }

        [Fact]
        public void TheYardsCratesStandInAStack()
        {
            var world = WorldBuilder.Build(Library.Value.Open("home"));
            for (var tick = 0; tick < 120; tick++)
                world.Physics.Step(1f / 60f);
            var stack = world.Things.Where(t => t.Body.Name.StartsWith("yard: crate.wood")).Select(t => t.Body).OrderBy(b => b.Position.Y).ToArray();
            Assert.Equal(3, stack.Length);
            for (var k = 1; k < 3; k++)
            {
                Assert.Equal(stack[k - 1].Position.Y + 0.8f, stack[k].Position.Y, 0.05f);   // each on the one below
                Assert.Equal(-15f, stack[k].Position.X, 0.05f);
            }
        }

        [Fact]
        public void AFileBuildingStandsOnLevelledGround()
        {
            // On the home map's hills, where the ground isn't flat to start with
            var district = new DistrictFile { Buildings = { new BuildingEntry("house.bungalow", "filebungalow", new Vector2(-15f, -21f), "File bungalow") } };
            var world = WorldBuilder.Build(HomeMap.Districts().Append(new FileDistrict("test", district, Library.Value)).ToArray());
            var terrain = world.Terrain;
            var middle = terrain.HeightAt(-15f, -21f);
            foreach (var (dx, dz) in new[] { (-4.7f, -3.2f), (4.7f, -3.2f), (4.7f, 3.2f), (-4.7f, 3.2f) })
                Assert.Equal(middle, terrain.HeightAt(-15f + dx, -21f + dz), 0.001f);
            var room = world.Buildings.Single(b => b.Name == "File bungalow").Rooms[0];
            Assert.Equal(middle + BuildingKind.Step, room.WorldOffset.Y, 0.001f);
        }

        [Theory]
        [InlineData(Side.North)]
        [InlineData(Side.East)]
        public void ALaneCottageFromAFileFacesTheWayItsDoorIs(Side door)
        {
            var at = new Vector2(10f, 20f);
            var district = new DistrictFile { Buildings = { new BuildingEntry("house.lane-cottage", "filecottage", at, Door: door) } };
            var world = WorldBuilder.Build(new IDistrict[] { new FileDistrict("test", district, Library.Value) });
            Assert.Empty(world.Buildings);   // no insides: its walls, its mesh (shut and open) and its window
            Assert.Equal(2, world.Fixtures.Count);
            var walls = world.Ground.Walls.ToArray();
            Assert.Equal(4, walls.Length);

            // 9 m along its front, 4.5 m deep; its window in its front wall
            var (along, deep) = (LaneCottage.Half.Y, LaneCottage.Half.X);
            var half = door == Side.North ? new Vector2(along, deep) : new Vector2(deep, along);
            Assert.Equal(at.X + half.X, walls.Max(w => MathF.Max(w.A.X, w.B.X)), 0.001f);
            Assert.Equal(at.Y + half.Y, walls.Max(w => MathF.Max(w.A.Y, w.B.Y)), 0.001f);
            var window = Assert.Single(world.Windows).Frame.Translation;
            if (door == Side.North)
                Assert.Equal(at.Y - deep, window.Z, 0.001f);
            else
                Assert.Equal(at.X + deep, window.X, 0.001f);

            // The ground levelled under it, turned with it
            var middle = world.Terrain.HeightAt(at.X, at.Y);
            Assert.Equal(middle, world.Terrain.HeightAt(at.X + half.X - 0.1f, at.Y + half.Y - 0.1f), 0.001f);
        }

        [Fact]
        public void ASolidPropHasWallsRoundItsBox()
        {
            var district = new DistrictFile { Props = { new PropEntry("furniture.sideboard", new Vector2(2f, 3f), 90f) } };
            var world = WorldBuilder.Build(new IDistrict[] { new FileDistrict("test", district, Library.Value) });
            var size = Library.Value.Item("furniture.sideboard").Size;
            var walls = world.Ground.Walls.ToArray();
            Assert.Equal(4, walls.Length);
            // Turned a quarter, its width runs north-south
            var xs = walls.SelectMany(w => new[] { w.A.X, w.B.X }).ToArray();
            var zs = walls.SelectMany(w => new[] { w.A.Y, w.B.Y }).ToArray();
            Assert.Equal(size.Z, xs.Max() - xs.Min(), 0.01f);
            Assert.Equal(size.X, zs.Max() - zs.Min(), 0.01f);
            Assert.All(walls, w => Assert.Equal(size.Y, w.Top - w.Bottom, 0.01f));
        }

        [Fact]
        public void AScaledPropsWallsAreScaledWithIt()
        {
            var district = new DistrictFile { Props = { new PropEntry("furniture.sideboard", new Vector2(2f, 3f), Scale: 1.5f) } };
            var world = WorldBuilder.Build(new IDistrict[] { new FileDistrict("test", district, Library.Value) });
            var size = Library.Value.Item("furniture.sideboard").Size * 1.5f;
            var walls = world.Ground.Walls.ToArray();
            var xs = walls.SelectMany(w => new[] { w.A.X, w.B.X }).ToArray();
            var zs = walls.SelectMany(w => new[] { w.A.Y, w.B.Y }).ToArray();
            Assert.Equal(size.X, xs.Max() - xs.Min(), 0.01f);
            Assert.Equal(size.Z, zs.Max() - zs.Min(), 0.01f);
            Assert.All(walls, w => Assert.Equal(size.Y, w.Top - w.Bottom, 0.01f));
            Assert.Equal(1.5f, world.Fixtures.Single().Transform.Right.Length(), 0.001f);   // and its mesh
        }

        [Fact]
        public void AThingTurnedAQuarterHasItsBoxTurnedToo()
        {
            var item = Library.Value.Item("crate.pallet");   // 1.2 wide, 1.0 deep
            var district = new DistrictFile
            {
                Things = { new ThingEntry("crate.pallet", new Vector2(0f, 0f)), new ThingEntry("crate.pallet", new Vector2(5f, 0f), 80f) },
            };
            var world = WorldBuilder.Build(new IDistrict[] { new FileDistrict("test", district, Library.Value) });
            Assert.Equal(item.Size, world.Things[0].Body.Size);
            Assert.Equal(new Vector3(item.Size.Z, item.Size.Y, item.Size.X), world.Things[1].Body.Size);   // 80 is nearest a quarter
        }

        [Fact]
        public void TheCataloguesBoxesAreMeasuredFromTheirMeshes()
        {
            Assert.Equal(new Vector3(0.8f, 0.8f, 0.8f), Library.Value.Item("crate.wood").Size);
            Assert.All(Library.Value.Items, item =>
            {
                Assert.True(item.Size.X > 0.02f && item.Size.Y > 0.02f && item.Size.Z > 0.02f, item.Name);   // a book is 0.035 thick
                Assert.True(item.Mass > 0f, item.Name);
            });
        }

        // ---- Mistakes

        private static InvalidDataException Fails(Action read) => Assert.Throws<InvalidDataException>(read);

        [Fact]
        public void ANameThatIsntThereSaysWhatThereIs()
        {
            var district = new DistrictFile { Things = { new ThingEntry("crate.wod", Vector2.Zero) } };
            var e = Fails(() => new FileDistrict("yard", district, Library.Value));
            Assert.Contains("District 'yard'", e.Message);
            Assert.Contains("no catalogue item called 'crate.wod'", e.Message);
            Assert.Contains("crate.wood", e.Message);

            var building = new DistrictFile { Buildings = { new BuildingEntry("house.castle", "c", Vector2.Zero) } };
            Assert.Contains("no kind of building called 'house.castle'", Fails(() => new FileDistrict("x", building, Library.Value)).Message);

            Assert.Contains("no district built in code called 'home.moon'", Fails(() => Library.Value.District("home.moon")).Message);
            Assert.Contains("no terrain called 'moon'", Fails(() => Library.Value.Terrain("moon")).Message);
        }

        [Fact]
        public void AMapNamingADistrictThatIsntThereFailsWhenItsOpened()
        {
            var path = Temporary("{\"version\": 1, \"terrain\": \"home\", \"defaultStart\": \"hills\", \"districts\": [{\"code\": \"home.towm\"}]}",
                                 "test.map.json");
            Assert.Contains("'home.towm'", Fails(() => Library.Value.Open(path)).Message);
        }

        [Fact]
        public void AMisspeltNameInAFileIsAnError()
        {
            var path = Temporary("{\"version\": 1, \"props\": [{\"item\": \"plant.oak\", \"at\": [0, 0], \"trun\": 90}]}");
            var e = Fails(() => DistrictFile.Load(path));
            Assert.Contains(path, e.Message);
            Assert.Contains("trun", e.Message);
        }

        [Fact]
        public void AnEntryWithoutWhereItIsIsAnError()
        {
            var path = Temporary("{\"version\": 1, \"props\": [{\"item\": \"plant.oak\"}]}");
            Assert.Contains("at", Fails(() => DistrictFile.Load(path)).Message);
        }

        [Fact]
        public void AFileFromANewerVersionOrNoneIsntRead()
        {
            Assert.Contains("version 2", Fails(() => DistrictFile.Load(Temporary("{\"version\": 2}"))).Message);
            Assert.Contains("version", Fails(() => DistrictFile.Load(Temporary("{\"props\": []}"))).Message);
        }

        [Fact]
        public void BadNumbersAndColoursSayHowTheyreWritten()
        {
            Assert.Contains("[x, z]", Fails(() => DistrictFile.Load(Temporary("{\"version\": 1, \"starts\": [{\"name\": \"a\", \"at\": [1, 2, 3]}]}"))).Message);
            Assert.Contains("#rrggbb", Fails(() => DistrictFile.Load(Temporary(
                "{\"version\": 1, \"buildings\": [{\"kind\": \"house.bungalow\", \"id\": \"b\", \"at\": [0, 0], \"walls\": \"red\"}]}"))).Message);
        }

        [Fact]
        public void ASettingABuildingsKindDoesntUseIsAnError()
        {
            string Mistake(BuildingEntry building) =>
                Fails(() => new FileDistrict("x", new DistrictFile { Buildings = { building } }, Library.Value)).Message;
            Assert.Contains("north or south", Mistake(new BuildingEntry("house.bungalow", "b", Vector2.Zero, Door: Side.East)));
            Assert.Contains("attic", Mistake(new BuildingEntry("house.bungalow", "b", Vector2.Zero, Attic: true)));
            Assert.Contains("no window", Mistake(new BuildingEntry("house.two-storey", "h", Vector2.Zero, View: "hangar")));
            Assert.Contains("parlour", Mistake(new BuildingEntry("house.lane-cottage", "c", Vector2.Zero, View: "garden")));   // what there is
            Assert.Contains("flat roof", Mistake(new BuildingEntry("house.lane-cottage", "c", Vector2.Zero, Flat: true)));
        }

        [Theory]
        [InlineData(null, 1, 2)]       // a parlour
        [InlineData("hangar", 1, 2)]
        [InlineData("none", 0, 1)]     // no window: just the cottage, shut
        public void ALaneCottagesWindowLooksOntoWhatItSays(string view, int windows, int fixtures)
        {
            var district = new DistrictFile { Buildings = { new BuildingEntry("house.lane-cottage", "c", Vector2.Zero, View: view) } };
            var world = WorldBuilder.Build(new IDistrict[] { new FileDistrict("test", district, Library.Value) });
            Assert.Equal(windows, world.Windows.Count);
            Assert.Equal(fixtures, world.Fixtures.Count);
            if (windows > 0)
                Assert.Equal(view == "hangar" ? Hangar.SeenFromCottage().Length : Parlour.Parts().Length, world.Windows[0].Beyond.Count);
        }

        [Fact]
        public void ALaneCottagesDoorwayIsInItsFront()
        {
            var cottage = new BuildingEntry("house.lane-cottage", "c", new Vector2(10f, 20f), Door: Side.West);
            var (a, b) = Library.Value.BuildingKind("house.lane-cottage").Doorway(cottage);
            Assert.Equal(10f - LaneCottage.Half.X, a.X, 0.001f);   // facing west: its front's the west wall
            Assert.Equal(10f - LaneCottage.Half.X, b.X, 0.001f);
            Assert.Equal(LaneCottage.DoorHalf * 2f, Vector2.Distance(a, b), 0.001f);
            Assert.Null(Library.Value.BuildingKind("house.bungalow").Doorway);   // its door opens: it has a room behind it
        }

        [Fact]
        public void TwoStartsWithOneNameAreAnError()
        {
            var district = new DistrictFile { Starts = { new StartEntry("a", Vector2.Zero), new StartEntry("a", Vector2.One) } };
            Assert.Contains("'a'", Fails(() => new FileDistrict("x", district, Library.Value)).Message);
        }

        // ---- Watching

        [Fact]
        public void AWatcherSeesAFileSavedOnceItsQuiet()
        {
            var path = Temporary("{}");
            using var watcher = new MapWatcher(new[] { path });
            Assert.False(watcher.Changed());
            File.WriteAllText(path, "{ }");
            var waited = 0;
            while (!watcher.Changed() && waited < 5000)
            {
                Thread.Sleep(50);
                waited += 50;
            }
            Assert.True(waited < 5000, "the change was never seen");
            Assert.True(waited >= 200, "it was seen before the file had been quiet for long enough");
            Assert.False(watcher.Changed());   // once for each change
        }
    }
}
