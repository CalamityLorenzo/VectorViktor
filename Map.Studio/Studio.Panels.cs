using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using System;
using System.IO;
using System.Linq;
using World.Buildings;
using World.Maps;
using World.Maps.Files;
using Num = System.Numerics;

namespace MapStudio
{
    // The studio's panels (Dear ImGui): the map (its files, districts, view, layers and grid), what can be added, what's
    // selected and its values, and a status line. A panel changes an entry as you drag a value, so you see it at once;
    // the change is recorded as one edit when you let go (see SelectedPanel).
    public sealed partial class Studio
    {
        private const float PanelWidth = 340f;
        private bool _showTerrain = true, _showWater = true, _showBuildings = true, _showFixtures = true, _showThings = true;
        private bool _asThing = true;            // catalogue items placed loose (things) or fixed (props)
        private string _newDistrict = "";
        private bool _districtsAdded;            // a district file added to the map since it was saved

        private bool Unsaved => _history.Unsaved || _districtsAdded;

        protected override void DrawOverlay(GameTime gameTime)
        {
            _renderer.View.ShowTerrain = _showTerrain;
            _renderer.View.ShowWater = _showWater;
            _renderer.View.ShowBuildings = _showBuildings;
            _renderer.View.ShowFixtures = _showFixtures;
            _renderer.View.ShowThings = _showThings;

            _imgui.BeginFrame(_frameSeconds);
            MapPanel();
            AddPanel();
            SelectedPanel();
            StatusLine();
            if (_showMarkers)
                Labels();
            _imgui.EndFrame();
        }

        private void MapPanel()
        {
            ImGui.SetNextWindowPos(new Num.Vector2(10, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Num.Vector2(PanelWidth, 0), ImGuiCond.FirstUseEver);
            ImGui.Begin("Map");
            ImGui.Text($"{_document.Name}: {Path.GetFileName(_document.Path)}");
            ImGui.TextDisabled(_document.File.About ?? "");
            if (ImGui.Button("Save (Ctrl+S)"))
                Save();
            ImGui.SameLine();
            ImGui.Text(Unsaved ? "edits not saved" : "saved");
            if (_status.StartsWith("The map's files changed on disk") && ImGui.Button("Reopen (losing the edits here)"))
                Open();

            ImGui.BeginDisabled(!_history.CanUndo);
            if (ImGui.Button("Undo (Ctrl+Z)"))
                _history.Undo();
            ImGui.EndDisabled();
            ImGui.SameLine();
            ImGui.BeginDisabled(!_history.CanRedo);
            if (ImGui.Button("Redo (Ctrl+Y)"))
                _history.Redo();
            ImGui.EndDisabled();
            if (_history.Done.Count > 0)
                ImGui.TextDisabled("last: " + _history.Done[^1].Name);

            if (ImGui.CollapsingHeader("Districts", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextDisabled("in the order they're built; new things go in the one picked");
                foreach (var district in _document.File.Districts)
                    if (district.Code != null)
                        ImGui.TextDisabled("  code: " + district.Code);
                    else if (ImGui.RadioButton("file: " + district.File, _editing == district.File))
                        _editing = district.File;
                ImGui.SetNextItemWidth(150f);
                ImGui.InputText("##district", ref _newDistrict, 40);
                ImGui.SameLine();
                if (ImGui.Button("add a district file"))
                    AddDistrictFile();
            }

            if (ImGui.CollapsingHeader("View", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var (name, kind) in new[] { ("overhead 1", ViewKind.Overhead), ("front 2", ViewKind.Front), ("side 3", ViewKind.Side),
                                                     ("3/4 4", ViewKind.ThreeQuarter), ("free 0", ViewKind.Free) })
                {
                    if (ImGui.Button(name))
                        _camera.SnapTo(kind, _camera.Target);
                    if (kind != ViewKind.Free)
                        ImGui.SameLine();
                }
                ImGui.TextDisabled($"{_camera.Kind}{(_camera.Flat ? ", flat" : "")}, looking at {_camera.Target.X:F1}, {_camera.Target.Y:F1}, {_camera.Target.Z:F1}");
                ImGui.Checkbox("terrain", ref _showTerrain);
                ImGui.SameLine();
                ImGui.Checkbox("water", ref _showWater);
                ImGui.SameLine();
                ImGui.Checkbox("buildings", ref _showBuildings);
                ImGui.Checkbox("fixtures", ref _showFixtures);
                ImGui.SameLine();
                ImGui.Checkbox("things", ref _showThings);
                ImGui.SameLine();
                ImGui.Checkbox("markers", ref _showMarkers);
                ImGui.Checkbox("grid (G)", ref _showGrid);
                ImGui.SameLine();
                ImGui.Checkbox("fog, as in the game", ref _fog);
                var snaps = Snaps.Select(s => s == 0f ? "off" : $"{s} m").ToArray();
                ImGui.SetNextItemWidth(120f);
                ImGui.Combo("snap to the grid", ref _snap, snaps, snaps.Length);
            }

            if (ImGui.CollapsingHeader("Play", ImGuiTreeNodeFlags.DefaultOpen))
            {
                if (ImGui.Button("Play here"))
                    Play(_camera.Target);
                ImGui.SameLine();
                ImGui.TextDisabled("or P, under the mouse");
                if (PlayHere.Find() == null)
                    ImGui.TextWrapped("Build Droid.Playground first.");
            }
            ImGui.TextDisabled($"built in {_buildMilliseconds:F0} ms; {1f / MathF.Max(_frameSeconds, 1e-4f):F0} fps, {_renderer.Batch.DrawCalls} draw calls");
            ImGui.End();
        }

        private void AddDistrictFile()
        {
            var name = _newDistrict.Trim();
            if (name.Length == 0 || name.Any(c => !char.IsLetterOrDigit(c) && c != '-'))
            {
                _status = "A district file's name is letters, digits and dashes: 'garden', 'north-woods'.";
                return;
            }
            var path = $"{MapFolder.DistrictsFolder}/{name}{MapFolder.DistrictExtension}";
            if (_document.Districts.ContainsKey(path) || File.Exists(_document.PathOf(path)))
            {
                _status = $"There's already a district file {path}.";
                return;
            }
            _document.AddDistrict(path, new DistrictFile { About = $"{name}: made in the map studio" });
            _editing = path;
            _districtsAdded = true;
            _newDistrict = "";
            Rebuild();
            _status = $"Added {path}: it's written when the map's saved.";
        }

        private void AddPanel()
        {
            ImGui.SetNextWindowPos(new Num.Vector2(WindowWidth - PanelWidth - 10, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Num.Vector2(PanelWidth, 360), ImGuiCond.FirstUseEver);
            ImGui.Begin("Add");
            if (_editing == null)
            {
                ImGui.TextWrapped("This map has no district files to add to: add one in the Map panel.");
                ImGui.End();
                return;
            }
            ImGui.TextDisabled("into " + _editing);
            if (_placing != null)
                ImGui.TextColored(new Num.Vector4(1f, 0.7f, 0.3f, 1f), $"Placing {_placing.What}: click the ground. Escape stops.");
            var district = MapFolder.NameOf(_editing);

            ImGui.SeparatorText("The catalogue");
            if (ImGui.RadioButton("loose, to push about (a thing)", _asThing))
                _asThing = true;
            if (ImGui.RadioButton("fixed, built in (a prop)", !_asThing))
                _asThing = false;
            foreach (var item in _library.Items)
                Pick(item.Name, $"{item.About}\n{item.Size.X:0.##} x {item.Size.Z:0.##} m, {item.Size.Y:0.##} m tall, {item.Mass:0.#} kg",
                    () => _asThing ? new ThingEntry(item.Name, Vector2.Zero) : new PropEntry(item.Name, Vector2.Zero));

            ImGui.SeparatorText("Buildings");
            foreach (var kind in _library.BuildingKinds)
                Pick(kind.Name, kind.About, () => new BuildingEntry(kind.Name, UniqueBuilding(district), Vector2.Zero));

            ImGui.SeparatorText("The ground, and where to start");
            Pick("pad", "ground levelled, for something to stand on", () => new PadEntry(Vector2.Zero, new Vector2(4f, 4f)));
            Pick("pool", "water, filling whatever's below its level: dig a hollow for it with a pad", () => new PoolEntry(Vector2.Zero, 0f, 4f));
            Pick("start", "a named place to start", () => new StartEntry(UniqueStart("start"), Vector2.Zero, MathF.Round(MathHelper.ToDegrees(_camera.Yaw))));
            ImGui.End();
        }

        // A line in the Add panel: picked, it's what the next click places
        private void Pick(string name, string about, Func<object> make)
        {
            if (ImGui.Selectable(name, _placing?.What == name))
                _placing = _placing?.What == name ? null : new Placing(name, at =>
                {
                    var entry = Entries.MovedTo(make(), at);
                    // A pool's surface a little below the ground where it's put, so there's something to see
                    return entry is PoolEntry pool ? pool with { Level = MathF.Round(_built.Terrain.HeightAt(at.X, at.Y) - 0.3f, 2) } : entry;
                });
            if (about.Length > 0 && ImGui.IsItemHovered())
                ImGui.SetTooltip(about);
        }

        private void SelectedPanel()
        {
            ImGui.SetNextWindowPos(new Num.Vector2(WindowWidth - PanelWidth - 10, 380), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Num.Vector2(PanelWidth, 0), ImGuiCond.FirstUseEver);
            ImGui.Begin("Selected");
            if (_selected is { } selected)
            {
                var list = Entries.List(FileOf(selected), selected.Kind);
                var entry = list[selected.Index]!;
                ImGui.Text(Entries.Describe(entry));
                ImGui.TextDisabled($"in {selected.File}, {selected.Kind.ToString().ToLowerInvariant()} {selected.Index + 1} of {list.Count}");
                ImGui.PushItemWidth(PanelWidth * 0.6f);   // room for the labels beside
                var edited = Inspect(entry);
                ImGui.PopItemWidth();
                if (entry is ThingEntry { Mass: 0f } loose && _library.HasItem(loose.Item))
                    ImGui.TextDisabled($"mass 0: the catalogue's, {_library.Item(loose.Item).Mass:0.#} kg");
                if (!Equals(edited, entry))
                    list[selected.Index] = edited;   // shown at once; recorded below, once nothing's being dragged or typed in
                if (_drag == null && !ImGui.IsAnyItemActive() && _committed != null && !Equals(list[selected.Index], _committed))
                    _history.Record(new ChangeEntry(FileOf(selected), selected, _committed, list[selected.Index]!, "change " + Entries.Describe(entry)));

                ImGui.Separator();
                if (Entries.Facing(entry) != null)
                {
                    if (ImGui.Button("turn left (Q)")) Turn(-15f);
                    ImGui.SameLine();
                    if (ImGui.Button("turn right (E)")) Turn(15f);
                    ImGui.SameLine();
                }
                if (ImGui.Button("copy (Ctrl+D)")) Duplicate();
                if (ImGui.Button("remove (Delete)")) Delete();
                ImGui.SameLine();
                if (ImGui.Button("frame (F)")) Frame();
            }
            else switch (_picked)
            {
                case Thing thing:
                    ImGui.Text($"thing: {thing.Body.Name}, {thing.Body.Mass:0.#} kg");
                    ImGui.Text($"at {thing.Body.Position.X:F2}, {thing.Body.Position.Y:F2}, {thing.Body.Position.Z:F2}");
                    ImGui.TextWrapped($"Built in code, by {thing.From?.GetType().Name}: change it there.");
                    break;
                case Building building:
                    var b = _codeBuildings[building];
                    ImGui.Text($"building: {building.Name}, {building.Rooms.Count} room{(building.Rooms.Count == 1 ? "" : "s")}");
                    ImGui.Text($"from {b.Min.X:F1}, {b.Min.Z:F1} to {b.Max.X:F1}, {b.Max.Z:F1}");
                    ImGui.TextWrapped("Built in code: change it there.");
                    break;
                case Vector3 point:
                    ImGui.Text($"the ground at {point.X:F2}, {point.Z:F2}, {point.Y:F2} m up");
                    break;
                default:
                    ImGui.TextDisabled("Click something to select it.");
                    break;
            }
            ImGui.End();
        }

        private void StatusLine()
        {
            var height = 28f;
            ImGui.SetNextWindowPos(new Num.Vector2(0, WindowHeight - height));
            ImGui.SetNextWindowSize(new Num.Vector2(WindowWidth, height));
            ImGui.Begin("##status", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing);
            ImGui.Text(_status.Length > 0 ? _status
                : "WASD fly, R/F up and down, wheel zoom, right drag look, middle drag orbit; 1-4 views, 0 free; click to select, drag to move; P plays here");
            ImGui.End();
        }

        // Each start's name, beside its marker
        private void Labels()
        {
            var draw = ImGui.GetBackgroundDrawList();
            foreach (var (name, start) in _built.Starts)
            {
                var top = new Vector3(start.At.X, _built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above + Entries.StartHeight, start.At.Y);
                if (Vector3.Distance(top, _camera.Target) < SeeingDistance && ToScreen(top) is { } at && PictureArea.Contains(at.X, at.Y))
                    draw.AddText(new Num.Vector2(at.X + 4, at.Y - 14), 0xff80ff80, name);
            }
        }

        // ---- The selected entry's values

        // The entry with whatever's been changed in the panel this frame
        private object Inspect(object entry)
        {
            switch (entry)
            {
                case PropEntry p:
                    return p with { Item = ItemCombo(p.Item), At = Point("at", p.At), Turn = Number("turn", p.Turn, 1f), Above = Number("above", p.Above) };
                case ThingEntry t:
                    return t with
                    {
                        Item = ItemCombo(t.Item), At = Point("at", t.At), Turn = Number("turn", t.Turn, 1f),
                        Above = Number("above", t.Above), Name = Text("name", t.Name), Mass = Number("mass, kg", t.Mass, 0.5f),
                    };
                case PadEntry p:
                    var levelWith = Check("level with elsewhere", p.LevelWith != null) ? p.LevelWith ?? p.Centre : (Vector2?)null;
                    var apron = Number("apron", p.Apron ?? 2f);
                    var blend = Number("blend", p.Blend ?? 6f);
                    return p with
                    {
                        Centre = Point("centre", p.Centre), Half = Point("half size", p.Half),
                        Apron = apron != (p.Apron ?? 2f) ? apron : p.Apron, Blend = blend != (p.Blend ?? 6f) ? blend : p.Blend,
                        Raise = Number("raise", p.Raise), LevelWith = levelWith is { } w ? Point("level with", w) : null,
                    };
                case PoolEntry p:
                    var rectangle = Check("rectangle", p.Half != null);
                    return p with
                    {
                        Centre = Point("centre", p.Centre), Level = Number("level", p.Level),
                        Radius = rectangle ? 0f : Number("radius", p.Radius == 0f ? 4f : p.Radius),
                        Half = rectangle ? Point("half size", p.Half ?? new Vector2(p.Radius)) : null,
                    };
                case StartEntry s:
                    return s with
                    {
                        Name = Text("name", s.Name) ?? s.Name, At = Point("at", s.At), Yaw = Number("facing", s.Yaw, 1f),
                        Above = Number("above", s.Above), InCar = Check("in a car", s.InCar),
                    };
                case BuildingEntry b:
                    var kinds = _library.BuildingKinds.Select(k => k.Name).ToArray();
                    var kind = Array.IndexOf(kinds, b.Kind);
                    ImGui.Combo("kind", ref kind, kinds, kinds.Length);
                    var sides = Enum.GetNames<Side>().Select(n => n.ToLowerInvariant()).ToArray();
                    var door = (int)b.Door;
                    ImGui.Combo("door in its", ref door, sides, sides.Length);
                    return b with
                    {
                        Kind = kind >= 0 ? kinds[kind] : b.Kind, Id = Text("id", b.Id) ?? b.Id, Name = Text("name", b.Name), At = Point("at", b.At),
                        Door = (Side)door, Walls = Colour("walls", b.Walls), Roof = Colour("roof", b.Roof),
                        Flat = Check("flat roof", b.Flat), Attic = Check("attic", b.Attic),
                    };
                case PortalEntry p:
                    return p with
                    {
                        A = Point("door from", p.A), B = Point("door to", p.B), Floor = Number("floor", p.Floor),
                        To = Point3("takes you to", p.To), Yaw = Number("facing", p.Yaw, 1f),
                    };
                default:
                    return entry;
            }
        }

        // Each widget gives back its value, changed only if it was changed (so a value read from a file with more
        // decimals than the panel shows isn't changed by being shown)
        private static float Number(string label, float value, float speed = 0.05f)
        {
            var v = value;
            return ImGui.DragFloat(label, ref v, speed) ? MathF.Round(v, 3) : value;
        }

        private static Vector2 Point(string label, Vector2 value)
        {
            var v = new Num.Vector2(value.X, value.Y);
            return ImGui.DragFloat2(label, ref v, 0.05f) ? new Vector2(MathF.Round(v.X, 3), MathF.Round(v.Y, 3)) : value;
        }

        private static Vector3 Point3(string label, Vector3 value)
        {
            var v = new Num.Vector3(value.X, value.Y, value.Z);
            return ImGui.DragFloat3(label, ref v, 0.05f) ? new Vector3(MathF.Round(v.X, 3), MathF.Round(v.Y, 3), MathF.Round(v.Z, 3)) : value;
        }

        private static bool Check(string label, bool value)
        {
            var v = value;
            ImGui.Checkbox(label, ref v);
            return v;
        }

        // Text, or null for none
        private static string? Text(string label, string? value)
        {
            var v = value ?? "";
            return ImGui.InputText(label, ref v, 64) ? (v.Length > 0 ? v : null) : value;
        }

        private static Color? Colour(string label, Color? value)
        {
            var c = value ?? new Color(200, 200, 200);
            var v = new Num.Vector3(c.R / 255f, c.G / 255f, c.B / 255f);
            return ImGui.ColorEdit3(label, ref v) ? new Color(v.X, v.Y, v.Z) : value;
        }

        private string ItemCombo(string item)
        {
            var names = _library.Items.Select(i => i.Name).ToArray();
            var chosen = Array.IndexOf(names, item);
            return ImGui.Combo("item", ref chosen, names, names.Length) && chosen >= 0 ? names[chosen] : item;
        }
    }
}
