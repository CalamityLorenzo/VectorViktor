using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Tools.DearImGui;
using World.Buildings;
using World.Core;
using World.Maps;
using World.Maps.Files;
using MapOf = World.Maps.Map;

namespace MapStudio
{
    // The map studio: a map file open, with no player in it, to look round and edit. It draws the map as the game does
    // (see WorldRenderer), from a camera of its own (see StudioCamera), with markers for what the game doesn't show
    // (pads, starts, portals), and panels (Dear ImGui) to add, change and remove what's in the map's district files.
    // Every edit can be undone (see Edits). After each, the map's built again from what's in memory, so what you see is
    // what the game will build; Save writes the files.
    //
    // Keys (when no panel has the keyboard): W, A, S, D fly, R and F rise and sink, Shift faster; the mouse wheel zooms;
    // the right button held looks about, the middle button (or Alt and the left) orbits, Shift and the middle pans;
    // 1 overhead, 2 front, 3 side, 4 three-quarter, 0 free (perspective); F frames what's selected; G the grid.
    // Editing: click to select, drag it or its arrows to move it (Ctrl: off the grid); Q and E turn it (Shift: a quarter
    // turn); Delete removes it, Ctrl+D copies it; Ctrl+Z undoes, Ctrl+Y redoes, Ctrl+S saves; Escape stops placing or
    // selecting. P plays here: the playtest harness on this map, the droid dropped under the mouse. C colours, L the
    // low-resolution look the game has.
    public sealed partial class Studio : RetroGame
    {
        private const int WindowWidth = 1600, WindowHeight = 900;
        private const int LowResWidth = 640, LowResHeight = 360;
        private const float SeeingDistance = 320f;   // how far out the terrain's built and drawn
        private const float FarPlane = 1000f;
        private const int SettleTicks = 90;          // after each build, things settle onto the ground for this long

        private readonly MapLibrary _library;
        private readonly string _path;
        private MapDocument _document = null!;
        private MapOf _map = null!;
        private BuiltWorld _built = null!;
        private WorldRenderer _renderer = null!;
        private MeshCache _worldMeshes = null!;
        private DebugLines _lines = null!, _handles = null!;
        private ImGuiRenderer _imgui = null!;
        private MapWatcher? _watcher;
        private readonly StudioCamera _camera = new StudioCamera();
        private readonly EditHistory _history = new EditHistory();

        private Dictionary<Building, BoundingBox> _codeBuildings = new Dictionary<Building, BoundingBox>();
        private string _status = "";
        private double _buildMilliseconds;
        private string _timing = "";   // how long each part of the last build took
        private float _frameSeconds;
        private MouseState _mouse;
        private bool _fog, _showMarkers = true, _showGrid;

        public Studio(MapLibrary library, string path) : base(WindowWidth, WindowHeight, LowResWidth, LowResHeight, colorsKey: Keys.C)
        {
            _library = library;
            _path = path;
            LowResOn = false;   // a tool: sharp, at the window's resolution; L for the game's look
            Window.AllowUserResizing = true;
        }

        protected override bool KeyboardCaptured => _imgui?.WantsKeyboard == true;
        protected override bool EscapeExits => false;   // it stops placing or selecting instead
        protected override bool ShotOfWholeWindow => true;

        protected override void LoadWorld()
        {
            _lines = new DebugLines(GraphicsDevice);
            _handles = new DebugLines(GraphicsDevice, onTop: true);
            _imgui = new ImGuiRenderer(GraphicsDevice, Window);
            _history.Changed += selects =>
            {
                // What the edit points at, or nothing: after a removal, the index that was selected is another entry's now
                if (selects is { } entry)
                    Select(entry);
                else
                    Deselect();
                Rebuild();
            };
            Open();
            var start = _built.Starts[_map.DefaultStart];
            _camera.Target = new Vector3(start.At.X, _built.Terrain.HeightAt(start.At.X, start.At.Y), start.At.Y);
            _camera.Yaw = start.Yaw;

            // A screenshot's keys (see RetroGame): 1 to 4 a snap view, g the grid, m no markers, t no terrain lines, n no terrain grid, s the first entry selected,
            // a and z below
            if (Shot is { } shot)
            {
                foreach (var (key, kind) in new[] { ('1', ViewKind.Overhead), ('2', ViewKind.Front), ('3', ViewKind.Side), ('4', ViewKind.ThreeQuarter) })
                    if (shot.Keys.Contains(key))
                        _camera.SnapTo(kind, _camera.Target);
                _showGrid = shot.Keys.Contains('g');
                _showMarkers = !shot.Keys.Contains('m');
                _showTerrainLines = !shot.Keys.Contains('t');
                _showTerrainGrid = !shot.Keys.Contains('n');
                if (shot.Keys.Contains('s') && _editing != null && FirstEntry(_editing) is { } first)
                    Select(first);
                // a: a wooden crate added 3 m east of where it's looking, as a click would; z: that undone
                if (shot.Keys.Contains('a'))
                {
                    _placing = new Placing("crate.wood", at => new ThingEntry("crate.wood", at));
                    Place(new Vector2(Snap(_camera.Target.X + 3f), Snap(_camera.Target.Z)));
                    _placing = null;
                }
                if (shot.Keys.Contains('z'))
                    _history.Undo();
            }
        }

        // The map file, and its districts' files, read (again); what was being edited and selected forgotten.
        private void Open()
        {
            _document = MapDocument.Open(_path);
            _districtsAdded = false;
            _editing = _document.Districts.Keys.FirstOrDefault();
            _history.Clear();
            Deselect();
            Rebuild();
            _watcher?.Dispose();
            _watcher = new MapWatcher(_document.Files);
            Window.Title = Title();
        }

        private string Title() => $"Map Studio - {_document.Name}{(Unsaved ? " *" : "")}";

        // The map built again from what's in memory now, and drawn afresh. If it doesn't build (a start's name used twice,
        // say), the last world that did stays, and the status line says what's wrong: undo, or put it right.
        //
        // Most edits don't change the ground, and making the terrain and drawing it are most of the work (a second or
        // two), so while the pads are the same, the terrain is the same one, and while its water is too, so are its
        // meshes. When the ground does change, the terrain's drawn again a few chunks a frame, nearest first.
        private void Rebuild()
        {
            var clock = Stopwatch.StartNew();
            BuiltWorld built;
            try
            {
                _map = _document.ToMap(_library);
                built = WorldBuilder.Build(_map.Districts(), SameGround(_map.MakeTerrain));
            }
            catch (Exception e) when (e is InvalidDataException or InvalidOperationException)
            {
                _status = "It doesn't build: " + e.Message;
                return;
            }
            var builtAt = clock.Elapsed.TotalMilliseconds;
            for (var tick = 0; tick < SettleTicks; tick++)
                built.Physics.Step(1f / 60f);

            var first = _renderer == null;
            var sameTerrain = !first && built.Terrain == _built.Terrain && built.Terrain.Pools.SequenceEqual(_drawnPools);
            var terrain = sameTerrain ? _renderer!.View.ReleaseTerrain() : null;
            _built = built;
            _drawnPools = built.Terrain.Pools.ToArray();
            _renderer?.Dispose();
            _worldMeshes?.Dispose();
            _worldMeshes = new MeshCache();   // its own, thrown away with it: see Droid.Playground's MakeRenderer
            var settledAt = clock.Elapsed.TotalMilliseconds;
            _renderer = new WorldRenderer(_built, GraphicsDevice, _worldMeshes, SeeingDistance, terrain) { Fog = _fog };
            var rendererAt = clock.Elapsed.TotalMilliseconds;
            if (first)
                _renderer.View.Update(GraphicsDevice, _camera.Target, _camera.Target, all: true);

            var fromFiles = _document.Districts.Values.SelectMany(d => d.Buildings).Select(b => b.Name ?? b.Id).ToHashSet();
            _codeBuildings = _built.Buildings.Where(b => !fromFiles.Contains(b.Name)).ToDictionary(b => b, Bounds);
            _buildMilliseconds = clock.Elapsed.TotalMilliseconds;
            _timing = $"the world {builtAt:F0} ms, settling {settledAt - builtAt:F0}, meshes {rendererAt - settledAt:F0}, " +
                      (sameTerrain ? "the terrain as it was" : first ? $"terrain {_buildMilliseconds - rendererAt:F0}" : "terrain to come");
            if (_status.StartsWith("It doesn't build"))
                _status = "";
            if (_selected is { } selected)
                _committed = Entry(selected);
            Window.Title = Title();
        }

        private TerrainGenerator.Pad[] _terrainPads = Array.Empty<TerrainGenerator.Pad>();
        private Terrain? _terrain;
        private int _terrainsOwnPools;
        private Pool[] _drawnPools = Array.Empty<Pool>();

        // The map's way of making its terrain, but giving back the terrain it made last time if the pads are the same,
        // emptied of the water the districts added to it (WorldBuilder fills it again)
        private Func<IReadOnlyList<TerrainGenerator.Pad>, Terrain> SameGround(Func<IReadOnlyList<TerrainGenerator.Pad>, Terrain> make) => pads =>
        {
            if (_terrain != null && pads.SequenceEqual(_terrainPads))
            {
                _terrain.Drain(_terrainsOwnPools);
                return _terrain;
            }
            _terrain = make(pads);
            _terrainPads = pads.ToArray();
            _terrainsOwnPools = _terrain.Pools.Count;
            return _terrain;
        };

        // Round a building built in code: its rooms' outer walls, from the bottom of its ground floor to a roof's height
        // over its top floor
        private static BoundingBox Bounds(Building building)
        {
            var points = building.Rooms.SelectMany(room => building.OuterOutline(room).SelectMany(p => new[]
            {
                new Vector3(p.X, room.WorldOffset.Y, p.Y), new Vector3(p.X, room.WorldOffset.Y + room.Height + 2f, p.Y),
            }));
            return BoundingBox.CreateFromPoints(points);
        }

        // ---- Each frame

        protected override void UpdateWorld(GameTime gameTime, KeyboardState keyboard)
        {
            _frameSeconds = (float)gameTime.ElapsedGameTime.TotalSeconds;
            var mouse = Mouse.GetState(Window);
            var keys = IsActive && !_imgui.WantsKeyboard;
            var pointer = IsActive && !_imgui.WantsMouse && PictureArea.Contains(mouse.X, mouse.Y);

            if (_watcher?.Changed() == true)
                ChangedOnDisk();
            if (keys)
                KeyboardInput(keyboard);
            if (pointer || _drag != null)
                MouseInput(mouse, keyboard);
            _camera.Step(_frameSeconds);
            _mouse = mouse;
            Window.Title = Title();
        }

        // The files changed, but not by this: by a text editor, say, or git. Read again, unless there are edits here
        // that would be lost; and not at all if they're just what this saved.
        private void ChangedOnDisk()
        {
            MapDocument onDisk;
            try
            {
                onDisk = MapDocument.Open(_path);
            }
            catch (Exception e) when (e is InvalidDataException or IOException)
            {
                _status = "The map's files changed, and don't read now: " + e.Message;
                return;
            }
            if (Same(onDisk, _document))
                return;
            if (Unsaved)
                _status = "The map's files changed on disk, and there are edits here not saved: Save keeps these, Reopen takes theirs.";
            else
            {
                Open();
                _status = "The map's files changed on disk: read again.";
            }
        }

        private static bool Same(MapDocument a, MapDocument b) =>
            MapJson.Write(a.File) == MapJson.Write(b.File) && a.Districts.Count == b.Districts.Count &&
            a.Districts.All(d => b.Districts.TryGetValue(d.Key, out var other) && MapJson.Write(d.Value) == MapJson.Write(other));

        private void KeyboardInput(KeyboardState keyboard)
        {
            var control = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
            var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);
            if (control)
            {
                if (Pressed(keyboard, Keys.Z)) { if (shift) _history.Redo(); else _history.Undo(); }
                if (Pressed(keyboard, Keys.Y)) _history.Redo();
                if (Pressed(keyboard, Keys.S)) Save();
                if (Pressed(keyboard, Keys.D)) Duplicate();
                return;
            }

            foreach (var (key, kind) in new[] { (Keys.D1, ViewKind.Overhead), (Keys.D2, ViewKind.Front), (Keys.D3, ViewKind.Side),
                                                (Keys.D4, ViewKind.ThreeQuarter), (Keys.D0, ViewKind.Free) })
                if (Pressed(keyboard, key))
                    _camera.SnapTo(kind, _camera.Target);
            if (Pressed(keyboard, Keys.F)) Frame();
            if (Pressed(keyboard, Keys.G)) _showGrid = !_showGrid;
            if (Pressed(keyboard, Keys.Delete)) Delete();
            if (Pressed(keyboard, Keys.Q)) Turn(shift ? -90f : -15f);
            if (Pressed(keyboard, Keys.E)) Turn(shift ? 90f : 15f);
            if (Pressed(keyboard, Keys.P)) Play(GroundUnderMouse() ?? _camera.Target);
            if (Pressed(keyboard, Keys.Escape))
            {
                if (_placing != null) _placing = null;
                else Deselect();
            }

            // Flying: faster the further back it is, so it crosses what's in view in about the same time near or far
            var speed = MathF.Max(4f, _camera.Distance) * (shift ? 2.5f : 0.8f) * _frameSeconds;
            var across = Axis(keyboard, Keys.D, Keys.A);
            var ahead = Axis(keyboard, Keys.W, Keys.S);
            var up = Axis(keyboard, Keys.R, Keys.F);
            if (across != 0f || ahead != 0f || up != 0f)
                _camera.Fly(across * speed, ahead * speed, up * speed);
        }

        private void MouseInput(MouseState mouse, KeyboardState keyboard)
        {
            var moved = new Vector2(mouse.X - _mouse.X, mouse.Y - _mouse.Y);
            var alt = keyboard.IsKeyDown(Keys.LeftAlt) || keyboard.IsKeyDown(Keys.RightAlt);
            var shift = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            var wheel = mouse.ScrollWheelValue - _mouse.ScrollWheelValue;
            if (wheel != 0)
                _camera.Zoom(MathF.Pow(0.88f, wheel / 120f));
            if (mouse.RightButton == ButtonState.Pressed && _mouse.RightButton == ButtonState.Pressed)
                _camera.Look(moved.X * 0.004f, -moved.Y * 0.004f);
            var middle = mouse.MiddleButton == ButtonState.Pressed && _mouse.MiddleButton == ButtonState.Pressed;
            var altLeft = alt && mouse.LeftButton == ButtonState.Pressed && _mouse.LeftButton == ButtonState.Pressed;
            if (middle && shift)
            {
                // Panning: the world follows the mouse, a pixel's worth of world at the target for each pixel moved
                var perPixel = _camera.Height / PictureArea.Height;
                _camera.Fly(-moved.X * perPixel, moved.Y * perPixel, 0f);
            }
            else if (middle || altLeft)
                _camera.Orbit(moved.X * 0.006f, -moved.Y * 0.006f);
            if (alt)
                return;

            var pressed = mouse.LeftButton == ButtonState.Pressed && _mouse.LeftButton == ButtonState.Released;
            var released = mouse.LeftButton == ButtonState.Released && _mouse.LeftButton == ButtonState.Pressed;
            var control = keyboard.IsKeyDown(Keys.LeftControl) || keyboard.IsKeyDown(Keys.RightControl);
            if (pressed)
                Press(new Vector2(mouse.X, mouse.Y));
            else if (_drag != null && mouse.LeftButton == ButtonState.Pressed)
                DragTo(new Vector2(mouse.X, mouse.Y), snap: !control);
            else if (released)
                Release();
        }

        // ---- The camera and the picture

        private Viewport Picture => new Viewport(0, 0, PictureArea.Width, PictureArea.Height);
        private float Aspect => PictureArea.Width / (float)Math.Max(1, PictureArea.Height);

        private Ray RayAt(Vector2 window) =>
            Picking.RayAt(window - new Vector2(PictureArea.X, PictureArea.Y), Picture, _camera.View, _camera.Projection(Aspect, FarPlane));

        // Where a point in the world is in the window, if it's in front of the camera
        private Vector2? ToScreen(Vector3 world)
        {
            var p = Picture.Project(world, _camera.Projection(Aspect, FarPlane), _camera.View, Matrix.Identity);
            return p.Z is < 0f or > 1f ? null : new Vector2(p.X + PictureArea.X, p.Y + PictureArea.Y);
        }

        private Vector3? GroundUnderMouse()
        {
            var mouse = Mouse.GetState(Window);
            return PictureArea.Contains(mouse.X, mouse.Y) ? Ground(RayAt(new Vector2(mouse.X, mouse.Y))) : null;
        }

        private Vector3? Ground(Ray ray) =>
            Picking.Ground(ray, _built.Terrain, StudioCamera.FlatBack + FarPlane, _camera.Flat ? StudioCamera.FlatBack - _camera.Distance : 0f);

        protected override void DrawWorld(GameTime gameTime)
        {
            var aspect = GraphicsDevice.Viewport.AspectRatio;
            _renderer.Fog = _fog;
            _renderer.FarPlane = _fog ? WorldRenderer.FogEnd : FarPlane;
            _renderer.ProjectionOverride = _camera.Projection(aspect, _renderer.FarPlane);
            _renderer.TerrainCentre = _camera.Target;
            _renderer.DrawFrom(_camera.Eye, _camera.Target, _camera.Up, _camera.Target, Clock, ColorsOn, AddPreview);

            AddMarkers();
            _lines.Draw(GraphicsDevice, _renderer.ViewMatrix, _renderer.Projection);
            _handles.Draw(GraphicsDevice, _renderer.ViewMatrix, _renderer.Projection);
        }

        protected override void WriteShotReport(string path) =>
            File.WriteAllText(path, $"map {_document.Name}\nview {_camera.Kind} flat {_camera.Flat}\ntarget {_camera.Target}\n" +
                $"distance {_camera.Distance}\nbuilt in {_buildMilliseconds:F0} ms: {_timing}\nselected {_selected}\nstatus {_status}\n");

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _watcher?.Dispose();
                _imgui?.Dispose();
                _lines?.Dispose();
                _handles?.Dispose();
                _renderer?.Dispose();
                _worldMeshes?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
