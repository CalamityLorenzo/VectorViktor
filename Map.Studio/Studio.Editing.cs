using MeshRendering;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Buildings;
using World.Maps;
using World.Maps.Files;

namespace MapStudio
{
    // Selecting and editing: what's under the mouse, picked; what's selected, dragged by its arrows or itself; things
    // placed from the catalogue, removed, copied, turned; the map saved. Every change is an edit (see Edits) given to the
    // history, which builds the map again once it's made.
    public sealed partial class Studio
    {
        // Grid steps that positions snap to (none, a decimetre, ...)
        private static readonly float[] Snaps = { 0f, 0.1f, 0.25f, 0.5f, 1f, 2f, 5f };
        private int _snap = 3;
        private float SnapStep => Snaps[_snap];

        private string? _editing;     // the district file new things go in (its path from the map file)
        private EntryRef? _selected;  // an entry in a district file
        private object? _picked;      // or else something built in code (a Thing, a Building), or a point on the ground
        private object? _committed;   // the selected entry as it was last built: a change to it in a panel shows against this

        // Placing something new: what it is, and how to make one at a point on the ground
        private sealed record Placing(string What, Func<Vector2, object> Make);
        private Placing? _placing;

        // Dragging what's selected: by an arrow, or (Axis None) over the ground at its own height
        private sealed class Drag
        {
            public required EntryRef Entry { get; init; }
            public required object Before { get; init; }
            public GizmoAxis Axis { get; init; }
            public Vector2 Mouse { get; init; }    // where the mouse was when it was grabbed
            public Vector2 Offset { get; init; }   // from where it was grabbed to its middle, on the ground
            public float Height { get; init; }     // of the level it slides on
        }
        private Drag? _drag;

        // ---- Entries

        private DistrictFile FileOf(EntryRef entry) => _document.Districts[entry.File];
        private object Entry(EntryRef entry) => Entries.List(FileOf(entry), entry.Kind)[entry.Index]!;

        private bool Exists(EntryRef entry) =>
            _document.Districts.TryGetValue(entry.File, out var file) && entry.Index < Entries.List(file, entry.Kind).Count;

        private EntryRef? FirstEntry(string file) =>
            Enum.GetValues<EntryKind>().Where(k => Entries.List(_document.Districts[file], k).Count > 0)
                .Select(k => (EntryRef?)new EntryRef(file, k, 0)).FirstOrDefault();

        private void Select(EntryRef entry)
        {
            _selected = entry;
            _picked = null;
            _committed = Entry(entry);
        }

        private void Deselect()
        {
            _selected = null;
            _picked = null;
            _committed = null;
        }

        // Its foot: where its arrows start, and the level it slides over
        private Vector3 FootOf(object entry)
        {
            var (place, _) = Entries.Box(entry, _built.Terrain, _library);
            var where = Entries.Where(entry);
            return new Vector3(where.X, place.Translation.Y, where.Y);
        }

        private float Snap(float value) => MathF.Round(SnapStep > 0f ? MathF.Round(value / SnapStep) * SnapStep : value, 3);

        // ---- Picking

        private sealed record Hit(float Distance, EntryRef? Entry = null, object? Code = null);

        // The nearest thing the ray goes through: an entry in a district file, something built in code, or the ground.
        private Hit? PickAt(Ray ray)
        {
            Hit? best = null;
            void Consider(float? distance, EntryRef? entry = null, object? code = null)
            {
                if (distance is { } d && (best == null || d < best.Distance))
                    best = new Hit(d, entry, code);
            }

            foreach (var (path, district) in _document.Districts)
                foreach (var kind in Enum.GetValues<EntryKind>())
                {
                    var list = Entries.List(district, kind);
                    for (var i = 0; i < list.Count; i++)
                    {
                        var (place, size) = Entries.Box(list[i]!, _built.Terrain, _library);
                        Consider(Picking.Box(ray, place, size), new EntryRef(path, kind, i));
                    }
                }
            foreach (var thing in _built.Things.Where(t => t.From is not FileDistrict))
                Consider(Picking.Box(ray, thing.Body.Pose, thing.Body.OriginalSize), code: thing);
            foreach (var (building, bounds) in _codeBuildings)
                Consider(ray.Intersects(bounds), code: building);
            if (Ground(ray) is { } ground)
                Consider(Vector3.Distance(ray.Position, ground), code: ground);
            return best;
        }

        // ---- The mouse's left button

        private void Press(Vector2 mouse)
        {
            var ray = RayAt(mouse);
            if (_placing != null)
            {
                if (Ground(ray) is { } ground)
                    Place(new Vector2(Snap(ground.X), Snap(ground.Z)));
                return;
            }
            if (_selected is { } selected)
            {
                var entry = Entry(selected);
                var foot = FootOf(entry);
                var axis = MoveGizmo.Over(mouse, foot, GizmoLength(foot), ToScreen);
                if (axis != GizmoAxis.None)
                {
                    _drag = new Drag { Entry = selected, Before = entry, Axis = axis, Mouse = mouse };
                    return;
                }
            }

            var hit = PickAt(ray);
            if (hit?.Entry is { } picked)
            {
                Select(picked);
                var entry = Entry(picked);
                var foot = FootOf(entry);
                var grabbed = Picking.Level(ray, foot.Y) ?? foot;
                _drag = new Drag
                {
                    Entry = picked, Before = entry, Mouse = mouse, Height = foot.Y,
                    Offset = Entries.Where(entry) - new Vector2(grabbed.X, grabbed.Z),
                };
            }
            else
            {
                Deselect();
                _picked = hit?.Code;
            }
        }

        private void DragTo(Vector2 mouse, bool snap)
        {
            var drag = _drag!;
            var from = Entries.Where(drag.Before);
            float Round(float v) => snap ? Snap(v) : MathF.Round(v, 3);
            Vector2 to;
            if (drag.Axis != GizmoAxis.None)
            {
                var foot = FootOf(drag.Before);
                var along = MoveGizmo.Dragged(mouse - drag.Mouse, foot, drag.Axis, GizmoLength(foot), ToScreen);
                to = drag.Axis == GizmoAxis.X ? new Vector2(Round(from.X + along), from.Y) : new Vector2(from.X, Round(from.Y + along));
            }
            else if (Picking.Level(RayAt(mouse), drag.Height) is { } point)
                to = new Vector2(Round(point.X + drag.Offset.X), Round(point.Z + drag.Offset.Y));
            else
                return;
            // Shown at once (see AddPreview, AddMarkers), built when it's let go
            Entries.List(FileOf(drag.Entry), drag.Entry.Kind)[drag.Entry.Index] = Entries.MovedTo(drag.Before, to);
        }

        private void Release()
        {
            if (_drag is not { } drag)
                return;
            _drag = null;
            var after = Entry(drag.Entry);
            if (!Equals(after, drag.Before))
                _history.Record(new ChangeEntry(FileOf(drag.Entry), drag.Entry, drag.Before, after, "move " + Entries.Describe(after)));
        }

        private float GizmoLength(Vector3 at) => MoveGizmo.Length(at, _camera.Eye, _camera, Math.Max(1, PictureArea.Height));

        // ---- Edits

        private void Place(Vector2 at)
        {
            if (_editing == null)
            {
                _status = "There's no district file to put it in: add one (the Map panel's Districts).";
                return;
            }
            var entry = _placing!.Make(at);
            var kind = Entries.KindOf(entry);
            var list = Entries.List(_document.Districts[_editing], kind);
            _history.Do(new AddEntry(_document.Districts[_editing], new EntryRef(_editing, kind, list.Count), entry, "add " + Entries.Describe(entry)));
        }

        private void Delete()
        {
            if (_selected is not { } selected)
                return;
            var entry = Entry(selected);
            _history.Do(new RemoveEntry(FileOf(selected), selected, entry, "remove " + Entries.Describe(entry)));
        }

        // A copy of what's selected, a step east of it (renamed, where a name must be different)
        private void Duplicate()
        {
            if (_selected is not { } selected)
                return;
            var entry = Entry(selected);
            var copy = Entries.MovedTo(entry, Entries.Where(entry) + new Vector2(Math.Max(1f, SnapStep * 2f), 0f));
            copy = copy switch
            {
                StartEntry s => s with { Name = UniqueStart(s.Name) },
                BuildingEntry b => b with { Id = UniqueBuilding(b.Id), Name = null },
                ThingEntry t when t.Name != null => t with { Name = t.Name + " copy" },
                _ => copy,
            };
            var list = Entries.List(FileOf(selected), selected.Kind);
            _history.Do(new AddEntry(FileOf(selected), selected with { Index = list.Count }, copy, "copy " + Entries.Describe(entry)));
        }

        private void Turn(float degrees)
        {
            if (_selected is not { } selected || Entries.Facing(Entry(selected)) == null)
                return;
            var before = Entry(selected);
            var after = Entries.TurnedBy(before, degrees);
            _history.Do(new ChangeEntry(FileOf(selected), selected, before, after, $"turn {Entries.Describe(before)}"));
        }

        private void Save()
        {
            var written = _document.Save();
            _history.MarkSaved();
            _districtsAdded = false;
            _watcher?.Dispose();
            _watcher = new MapWatcher(_document.Files);   // a district file added is one more to watch
            _status = written.Count == 0 ? "Nothing's changed since it was saved."
                : "Saved " + string.Join(", ", written.Select(p => System.IO.Path.GetRelativePath(_document.Folder, p)));
        }

        private void Play(Vector3 at)
        {
            if (Unsaved)
                Save();
            _status = PlayHere.Start(_document.Path, new Vector2(at.X, at.Z), _camera.Yaw);
        }

        // The camera moved to look at what's selected
        private void Frame()
        {
            (Vector3 at, float size)? target = _selected is { } selected
                ? (FootOf(Entry(selected)), Entries.Box(Entry(selected), _built.Terrain, _library).size.Length())
                : _picked switch
                {
                    Thing t => (t.Body.Position, t.Body.Size.Length()),
                    Building b => ((_codeBuildings[b].Min + _codeBuildings[b].Max) / 2f, (_codeBuildings[b].Max - _codeBuildings[b].Min).Length()),
                    Vector3 p => (p, 4f),
                    _ => null,
                };
            if (target is { } t2)
                _camera.MoveTo(new StudioCamera.Pose(t2.at, _camera.Yaw, _camera.Pitch, MathF.Max(6f, t2.size * 2.5f)), _camera.Flat);
        }

        // ---- Names that must differ

        private IEnumerable<string> StartNames() => _built.Starts.Keys.Concat(_document.Districts.Values.SelectMany(d => d.Starts).Select(s => s.Name));

        private string UniqueStart(string like)
        {
            var taken = StartNames().ToHashSet();
            var stem = like.TrimEnd("0123456789".ToCharArray());
            for (var n = 1; ; n++)
                if (!taken.Contains(stem + n))
                    return stem + n;
        }

        private string UniqueBuilding(string like)
        {
            var taken = _built.Buildings.Select(b => b.Name).Concat(_built.Buildings.SelectMany(b => b.Rooms).Select(r => r.Id))
                .Concat(_document.Districts.Values.SelectMany(d => d.Buildings).Select(b => b.Id)).ToHashSet();
            var stem = like.TrimEnd("0123456789".ToCharArray());
            for (var n = 1; ; n++)
                if (!taken.Contains(stem + n) && !taken.Any(t => t.StartsWith(stem + n)))   // room ids are the id and more
                    return stem + n;
        }

        // ---- What's drawn for editing

        private readonly Dictionary<string, MeshInstance> _previews = new Dictionary<string, MeshInstance>();

        // What's selected, where it's being dragged to (or changed to in a panel), before it's built there
        private void AddPreview(MeshBatch batch)
        {
            if (_selected is not { } selected || Equals(Entry(selected), _committed))
                return;
            var (item, place) = Entry(selected) switch
            {
                PropEntry p when _library.HasItem(p.Item) => (_library.Item(p.Item), FileDistrict.PropPlace(p, _built.Terrain)),
                ThingEntry t when _library.HasItem(t.Item) => (_library.Item(t.Item),
                    Matrix.CreateRotationY(FileDistrict.ThingBox(_library.Item(t.Item).Size, t.Turn).turn) *
                    Matrix.CreateTranslation(t.At.X, _built.Terrain.HeightAt(t.At.X, t.At.Y) + t.Above, t.At.Y)),
                _ => (null, Matrix.Identity),
            };
            if (item == null)
                return;
            if (!_previews.TryGetValue(item.Name, out var view))
                _previews[item.Name] = view = MeshCache.CreateInstance(GraphicsDevice, item.Mesh);   // the studio's own cache: it outlives each world
            view.Transform = place;
            batch.Add(view);
        }

        private void AddMarkers()
        {
            var terrain = _built.Terrain;
            if (_showGrid)
                AddGrid();
            if (_showMarkers)
            {
                foreach (var (path, district) in _document.Districts)
                {
                    var editing = path == _editing;
                    foreach (var pad in district.Pads)
                        Rectangle(pad.Centre, pad.Half, terrain.HeightAt(pad.Centre.X, pad.Centre.Y) + 0.05f, editing ? Color.Yellow : Color.Olive);
                    foreach (var pool in district.Pools)
                        if (pool.Half is { } half)
                            Rectangle(pool.Centre, half, pool.Level, Color.Aqua);
                        else
                            _lines.Circle(new Vector3(pool.Centre.X, pool.Level, pool.Centre.Y), pool.Radius, Color.Aqua, 32);
                }
                var fromFiles = _document.Districts.Values.SelectMany(d => d.Starts).Select(s => s.Name).ToHashSet();
                foreach (var (name, start) in _built.Starts)
                    StartMarker(start, fromFiles.Contains(name) ? Color.Lime : Color.ForestGreen);
                foreach (var portal in _built.Portals)
                {
                    var (a, b) = (new Vector3(portal.A.X, portal.Floor, portal.A.Y), new Vector3(portal.B.X, portal.Floor, portal.B.Y));
                    _lines.Line(a, b, Color.Magenta);
                    _lines.Line(a + Vector3.Up * 2.1f, b + Vector3.Up * 2.1f, Color.Magenta);
                    _lines.Line(a, a + Vector3.Up * 2.1f, Color.Magenta);
                    _lines.Line(b, b + Vector3.Up * 2.1f, Color.Magenta);
                }
            }

            if (_selected is { } selected)
            {
                var entry = Entry(selected);
                var (place, size) = Entries.Box(entry, terrain, _library);
                _lines.Box(place, size, Color.Yellow);
                var foot = FootOf(entry);
                var mouse = new Vector2(_mouse.X, _mouse.Y);
                var hot = _drag?.Axis ?? (_imgui.WantsMouse ? GizmoAxis.None : MoveGizmo.Over(mouse, foot, GizmoLength(foot), ToScreen));
                MoveGizmo.Draw(_handles, foot, GizmoLength(foot), hot);
            }
            switch (_picked)
            {
                case Thing thing:
                    _lines.Box(thing.Body.Pose, thing.Body.OriginalSize, Color.White);
                    break;
                case Building building:
                    var bounds = _codeBuildings[building];
                    _lines.Box(Matrix.CreateTranslation((bounds.Min.X + bounds.Max.X) / 2f, bounds.Min.Y, (bounds.Min.Z + bounds.Max.Z) / 2f),
                        bounds.Max - bounds.Min, Color.White);
                    break;
                case Vector3 point:
                    _handles.Circle(point, 0.3f, Color.White);
                    break;
            }
            if (_placing != null && GroundUnderMouse() is { } ground)
            {
                var at = new Vector2(Snap(ground.X), Snap(ground.Z));
                var (place, size) = Entries.Box(_placing.Make(at), terrain, _library);
                _lines.Box(place, size, Color.Orange);
            }
        }

        private void Rectangle(Vector2 centre, Vector2 half, float y, Color colour)
        {
            Vector3 C(float x, float z) => new Vector3(centre.X + x * half.X, y, centre.Y + z * half.Y);
            _lines.Line(C(-1, -1), C(1, -1), colour);
            _lines.Line(C(1, -1), C(1, 1), colour);
            _lines.Line(C(1, 1), C(-1, 1), colour);
            _lines.Line(C(-1, 1), C(-1, -1), colour);
        }

        // A post as tall as someone standing there, a ring round its foot, and an arrow the way they'd face
        private void StartMarker(Start start, Color colour)
        {
            var foot = new Vector3(start.At.X, _built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above, start.At.Y);
            var facing = new Vector3(MathF.Sin(start.Yaw), 0f, -MathF.Cos(start.Yaw));
            var side = Vector3.Cross(facing, Vector3.Up);
            _lines.Circle(foot + Vector3.Up * 0.05f, 0.4f, colour);
            _lines.Line(foot, foot + Vector3.Up * Entries.StartHeight, colour);
            var tip = foot + Vector3.Up * 0.05f + facing * 1.2f;
            _lines.Line(foot + Vector3.Up * 0.05f, tip, colour);
            _lines.Line(tip, tip - facing * 0.3f + side * 0.2f, colour);
            _lines.Line(tip, tip - facing * 0.3f - side * 0.2f, colour);
        }

        // The snapping grid round where the camera's looking, laid over the ground: as fine as the snap, made coarser as
        // the view takes in more, so there are never more than about eighty lines each way
        private void AddGrid()
        {
            var terrain = _built.Terrain;
            var reach = MathF.Min(_camera.Flat ? _camera.Height * Aspect * 0.6f : _camera.Distance * 1.5f, 250f);
            var step = SnapStep > 0f ? SnapStep : 1f;
            while (reach / step > 40f)
                step *= 2f;
            var centre = _camera.Target;
            var (x0, z0) = (MathF.Floor((centre.X - reach) / step) * step, MathF.Floor((centre.Z - reach) / step) * step);
            var count = (int)(2f * reach / step) + 1;
            var sample = MathF.Max(step, 1f);
            var colour = new Color(230, 170, 60);
            Vector3 On(float x, float z) => new Vector3(x, terrain.HeightAt(x, z) + 0.03f, z);
            for (var k = 0; k <= count; k++)
            {
                var (x, z) = (x0 + k * step, z0 + k * step);
                for (var s = 0f; s < 2f * reach; s += sample)
                {
                    _lines.Line(On(x, z0 + s), On(x, z0 + s + sample), colour);
                    _lines.Line(On(x0 + s, z), On(x0 + s + sample, z), colour);
                }
            }
        }
    }
}
