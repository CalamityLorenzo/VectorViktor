using System;
using System.Collections.Generic;
using World.Maps.Files;

namespace MapStudio
{
    // An edit to a map, as an object (the command pattern): what it does, and how to undo it. Everything the editor
    // changes goes through one, by EditHistory.Do, so everything can be undone and redone in order.
    public interface IEdit
    {
        string Name { get; }          // for the Edit panel: "move crate.wood"
        EntryRef? Selects { get; }        // what to select once it's done (or redone), if anything
        EntryRef? SelectsUndone { get; }  // and once it's undone
        void Do();
        void Undo();
    }

    // One entry swapped for another: moved, turned, any of its values changed. Entries are records that can't change, so
    // keeping the one before and the one after is all it takes to go either way.
    public sealed record ChangeEntry(DistrictFile File, EntryRef At, object Before, object After, string Name) : IEdit
    {
        public EntryRef? Selects => At;
        public EntryRef? SelectsUndone => At;
        public void Do() => Entries.List(File, At.Kind)[At.Index] = After;
        public void Undo() => Entries.List(File, At.Kind)[At.Index] = Before;
    }

    // A new entry, put in its list at an index (the end, usually).
    public sealed record AddEntry(DistrictFile File, EntryRef At, object Entry, string Name) : IEdit
    {
        public EntryRef? Selects => At;
        public EntryRef? SelectsUndone => null;
        public void Do() => Entries.List(File, At.Kind).Insert(At.Index, Entry);
        public void Undo() => Entries.List(File, At.Kind).RemoveAt(At.Index);
    }

    // An entry taken out; undone, it goes back where it was, so the indexes of everything after it are as they were.
    public sealed record RemoveEntry(DistrictFile File, EntryRef At, object Entry, string Name) : IEdit
    {
        public EntryRef? Selects => null;
        public EntryRef? SelectsUndone => At;
        public void Do() => Entries.List(File, At.Kind).RemoveAt(At.Index);
        public void Undo() => Entries.List(File, At.Kind).Insert(At.Index, Entry);
    }

    // The edits made, to undo (newest last), and the edits undone, to redo. A new edit clears the redo list: after
    // undoing three moves and making a fourth, "redo" has nothing it could sensibly mean.
    public sealed class EditHistory
    {
        private readonly List<IEdit> _done = new List<IEdit>();
        private readonly List<IEdit> _undone = new List<IEdit>();
        private int _saved;   // how many edits were done when last saved (-1: never, since an undo went past it)

        // Something changed the map: what to select now, if anything
        public event Action<EntryRef?>? Changed;

        public IReadOnlyList<IEdit> Done => _done;
        public IReadOnlyList<IEdit> Undone => _undone;
        public bool CanUndo => _done.Count > 0;
        public bool CanRedo => _undone.Count > 0;

        // Whether there are edits not yet saved: done or undone since the last save
        public bool Unsaved => _done.Count != _saved;

        public void Do(IEdit edit)
        {
            edit.Do();
            if (_saved > _done.Count)
                _saved = -1;   // the saved state was among the undone edits, now gone for good
            _done.Add(edit);
            _undone.Clear();
            Changed?.Invoke(edit.Selects);
        }

        // An edit already made by hand (a value dragged in a panel), recorded so it can be undone.
        public void Record(IEdit edit)
        {
            if (_saved > _done.Count)
                _saved = -1;
            _done.Add(edit);
            _undone.Clear();
            Changed?.Invoke(edit.Selects);
        }

        public void Undo()
        {
            if (!CanUndo)
                return;
            var edit = _done[^1];
            _done.RemoveAt(_done.Count - 1);
            edit.Undo();
            _undone.Add(edit);
            Changed?.Invoke(edit.SelectsUndone);
        }

        public void Redo()
        {
            if (!CanRedo)
                return;
            var edit = _undone[^1];
            _undone.RemoveAt(_undone.Count - 1);
            edit.Do();
            _done.Add(edit);
            Changed?.Invoke(edit.Selects);
        }

        public void MarkSaved() => _saved = _done.Count;

        public void Clear()
        {
            _done.Clear();
            _undone.Clear();
            _saved = 0;
        }
    }
}
