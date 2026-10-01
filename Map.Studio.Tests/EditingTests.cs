using MapStudio;
using Maps.Home;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using World.Core;
using World.Maps;
using World.Maps.Files;
using Xunit;

namespace Map.Studio.Tests
{
    // Edits, undone and redone; entries moved, turned and boxed as the studio does
    public class EditingTests
    {
        private static readonly Lazy<MapLibrary> Library = new Lazy<MapLibrary>(() =>
        {
            var library = new MapLibrary();
            HomeMap.AddTo(library);
            return library;
        });

        private static DistrictFile ThreeCrates() => new DistrictFile
        {
            Things = { new ThingEntry("crate.wood", new Vector2(0f, 0f)), new ThingEntry("crate.wood", new Vector2(1f, 0f)), new ThingEntry("crate.wood", new Vector2(2f, 0f)) },
        };

        private static EntryRef Thing(int index) => new EntryRef("d", EntryKind.Thing, index);

        [Fact]
        public void EditsUndoAndRedoInOrder()
        {
            var file = ThreeCrates();
            var history = new EditHistory();
            var moved = file.Things[1] with { At = new Vector2(5f, 5f) };
            history.Do(new ChangeEntry(file, Thing(1), file.Things[1], moved, "move"));
            history.Do(new RemoveEntry(file, Thing(0), file.Things[0], "remove"));
            Assert.Equal(new Vector2(5f, 5f), file.Things[0].At);   // the moved one, now first
            Assert.Equal(2, file.Things.Count);

            history.Undo();   // the removal: back where it was, so the indexes are as they were
            Assert.Equal(3, file.Things.Count);
            Assert.Equal(Vector2.Zero, file.Things[0].At);
            Assert.Equal(new Vector2(5f, 5f), file.Things[1].At);
            history.Undo();   // the move
            Assert.Equal(new Vector2(1f, 0f), file.Things[1].At);
            Assert.False(history.CanUndo);

            history.Redo();
            Assert.Equal(new Vector2(5f, 5f), file.Things[1].At);
            Assert.True(history.CanRedo);
            history.Do(new AddEntry(file, Thing(3), new ThingEntry("crate.steel", Vector2.One), "add"));
            Assert.False(history.CanRedo);   // a new edit: the one undone can't come back
            Assert.Equal(4, file.Things.Count);
        }

        [Fact]
        public void UnsavedCountsEditsEitherSideOfTheSave()
        {
            var file = ThreeCrates();
            var history = new EditHistory();
            Assert.False(history.Unsaved);
            history.Do(new RemoveEntry(file, Thing(2), file.Things[2], "remove"));
            Assert.True(history.Unsaved);
            history.MarkSaved();
            Assert.False(history.Unsaved);
            history.Undo();
            Assert.True(history.Unsaved);
            history.Redo();
            Assert.False(history.Unsaved);   // back as saved
            history.Undo();
            history.Do(new RemoveEntry(file, Thing(0), file.Things[0], "remove another"));
            history.Undo();
            Assert.True(history.Unsaved);    // the saved state was undone and replaced: nothing gets back to it
        }

        [Fact]
        public void TheSelectionFollowsTheEdit()
        {
            var file = ThreeCrates();
            var history = new EditHistory();
            EntryRef? selected = Thing(2);
            history.Changed += s => selected = s;
            history.Do(new RemoveEntry(file, Thing(2), file.Things[2], "remove"));
            Assert.Null(selected);
            history.Undo();
            Assert.Equal(Thing(2), selected);   // back, and selected again
        }

        [Fact]
        public void APortalMovesWithWhereItTakesYou()
        {
            var portal = new PortalEntry(new Vector2(0f, 0f), new Vector2(2f, 0f), 0.5f, new Vector3(10f, 1f, 10f), 90f);
            var moved = (PortalEntry)Entries.MovedTo(portal, new Vector2(4f, 3f));
            Assert.Equal(new Vector2(3f, 3f), moved.A);
            Assert.Equal(new Vector2(5f, 3f), moved.B);
            Assert.Equal(new Vector3(13f, 1f, 13f), moved.To);
            Assert.Equal(new Vector2(4f, 3f), Entries.Where(moved));
        }

        [Fact]
        public void AThingTurnsAQuarterAtATime()
        {
            var thing = new ThingEntry("crate.pallet", Vector2.Zero, 10f);
            Assert.Equal(90f, ((ThingEntry)Entries.TurnedBy(thing, 15f)).Turn);
            Assert.Equal(270f, ((ThingEntry)Entries.TurnedBy(thing, -15f)).Turn);
            var prop = new PropEntry("plant.oak", Vector2.Zero, 350f);
            Assert.Equal(5f, ((PropEntry)Entries.TurnedBy(prop, 15f)).Turn);   // round past north
        }

        [Fact]
        public void APropsBoxIsWhereTheGameBuildsIt()
        {
            var terrain = Terrain.FromFunction(64, 64, 1f, (x, z) => 0.1f * x);
            var prop = new PropEntry("furniture.sideboard", new Vector2(3f, -2f), 90f, 0.5f);
            var (place, size) = Entries.Box(prop, terrain, Library.Value);
            Assert.Equal(FileDistrict.PropPlace(prop, terrain), place);
            Assert.Equal(Library.Value.Item("furniture.sideboard").Size, size);
            Assert.Equal(terrain.HeightAt(3f, -2f) + 0.5f, place.Translation.Y, 0.001f);
        }

        [Fact]
        public void AStartDroppedFromAboveIsMarkedOnTheFloorItLandsOn()
        {
            // The town's attic start drops you from 100 m up, onto the attic floor
            var built = WorldBuilder.Build(HomeMap.Map);
            var attic = built.Starts["attic"];
            var floor = built.Buildings.SelectMany(b => b.Rooms).Single(r => r.Id == "houseattic").WorldOffset.Y;
            Assert.Equal(floor, Entries.StartFloor(attic.At, attic.Above, built.Terrain, built.Ground), 0.01f);
            Assert.Equal(built.Terrain.HeightAt(attic.At.X, attic.At.Y) + attic.Above, Entries.StartFloor(attic.At, attic.Above, built.Terrain, null));
        }
    }
}
