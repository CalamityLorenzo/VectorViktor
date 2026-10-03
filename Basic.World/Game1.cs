using Maps.Home;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using World.Buildings;
using World.Core.Animation;
using World.Core.Characters;
using World.Core.Movement;
using World.Core.Physics;
using World.Core.Vehicles;
using World.Maps;
using World.Maps.Files;

namespace Basic.World
{
    // Out of doors: walk over rolling hills, climb the causeway onto the plateau, jump off its cliffs, or
    // go down into the basin. Boxes and crates lie about: push them (the heavier, the slower - some won't
    // budge), knock them into each other, step or jump up onto them, shove them off the plateau. Tall
    // ones topple when you push them; push into a stack and it comes down. South-east, a cottage, a
    // two-storey house and a barn (see Town) stand on levelled ground: walk in through their doorways,
    // up the house's stair to the bedroom, up the barn's ladder to its loft. Their doors are shut: E opens
    // or shuts the one in front of you (see BuildingGround.Interact). There's a lake in the basin and a
    // pond west of the cottage: wade in and it slows you, deeper and you swim; you get wet as high as the
    // water comes up you (the title says how wet, and you darken from the feet up), and dry off out of it.
    // Light things float. East of the town, a street (see Street): houses either side of a road,
    // front gardens sloping down to it, picket fences round the back gardens, a swimming pool in one of
    // them, and billboards for the Commodore 64 and Atari either side of the road; down a lane from it (see Lane), an old
    // cottage whose front door, walked into, takes you to a long corridor, and back, and next door another, whose window shows a hangar. See the
    // world through your own eyes, or from your camera drone as it flies after you. Overhead, a bird roams the
    // country round where you start (see Bird): watch it from the camera chasing it, from the start with the command line's "bird".
    // Drawn to a small render target and scaled up with hard pixels (see RetroGame).
    // Up / W and Down / S walk (hold Shift to run), Left / Right turn, A / D sidestep, Space jumps, E opens or shuts a door.
    // Where there's a car (on the pass), E by it gets in, and E again, stopped, gets out; driving, Up / W is the
    // throttle, Down / S brakes and then reverses, Left / Right or A / D steer, and Space is the handbrake.
    // R / F tilt your own view up and down, Home levels it again. V switches between your own view and the drone's, B to the bird's chase camera and back. H shows or hides the compass, which
    // way you're facing (see Compass), across the top. G the grid on the ground (off: just its
    // shading, and the outlines round its cliffs). C toggles colours / wireframe, L the low-resolution
    // look, F11 full screen, Escape exits.
    //
    // A map from a file (see World.Maps.Files) is built again whenever one of its files is saved, by the map studio or
    // anything else, with you where you were.
    //
    // For development, a BASIC_WORLD_SHOT (see RetroGame) also saves where every body is beside the picture.
    // Its keys, all optional: v starts in the drone view, b following the bird, w holds walk forward (or the throttle,
    // driving), r runs, e presses E once, halfway to the shot, n the grid on the ground off.
    public class Game1 : RetroGame
    {
        private const int WindowWidth = 1440;
        private const int WindowHeight = 810;
        private const int LowResWidth = 640;      // a wide strip, 2.5 : 1, letterboxed: 2 x (1280 x 512) in the window,
        private const int LowResHeight = 256;     // 3 x (1920 x 768) full screen at 1920 x 1080

        private const float StepTime = 1f / 60f;      // the world always moves on in steps of this
        private const float MaxFrame = 0.25f;
        private const float TiltSpeed = 1f;   // radians a second, R / F     // after a stall, catch up no more than this, rather than fall through the world

        private Map _map;
        private Func<Map> _reopen;
        private readonly Func<string, Map> _open;
        private MapWatcher _watcher;
        private readonly string _start;
        private bool _followBird;
        private bool _terrainGrid = true;   // G: the squares on the ground, or just its shading and its cliffs' outlines
        private bool _showCompass = true;   // H: which way you're facing, across the top
        private Compass _compass;

        private BuiltWorld _built;
        private PhysicsWorld _world;
        private Player _player;
        private Bird _bird;
        private WorldRenderer _renderer;
        private MeshCache _worldMeshes;
        private int _titleWetness = -1, _titleSpeed = -1;
        private BuildingGround _ground;
        private float _pending;          // time not yet stepped through
        private bool _jumpPressed;       // since the last tick
        private bool _shotPressedE;
        private readonly (Vector3 feet, float radius, float height)[] _walkers = new (Vector3, float, float)[1];   // who the doors must not swing into

        // `followBird`: seen from the camera chasing the bird, to begin with. `map`: the home map if none. `reopen` opens the
        // map again from its files, when they've been saved (see MapWatcher). `open` opens another map by its name, for a
        // door onto it (see Portal.ToMap): without it, those doors stay shut.
        public Game1(string start = null, bool followBird = false, Map map = null, Func<Map> reopen = null, Func<string, Map> open = null)
            : base(WindowWidth, WindowHeight, LowResWidth, LowResHeight, colorsKey: Keys.C)
        {
            _map = map ?? HomeMap.Map;
            _reopen = reopen;
            _open = open;
            _start = start ?? _map.DefaultStart;
            _followBird = followBird;
        }

        protected override void LoadWorld()
        {
            Build();
            _compass = new Compass(GraphicsDevice);
            if (!_built.Starts.TryGetValue(_start, out var start))
                start = _built.Starts[_map.DefaultStart];
            var dropFrom = start.Above > 0f ? _built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above : 0f;
            _player = new Player(new Vector3(start.At.X, dropFrom, start.At.Y), start.Yaw, _world);
            if (start.InCar)
                _player.GetIn(CarAt(start));
            _bird = new Bird(start.At, _ground.SkylineAt, start.Yaw);
            MakeRenderer();
            if (_reopen != null && _map.Files.Count > 0)
                _watcher = new MapWatcher(_map.Files);
            if (Shot?.Keys.Contains('v') == true)
                _player.ToggleView();
            _followBird |= Shot?.Keys.Contains('b') == true;
            _terrainGrid = Shot?.Keys.Contains('n') != true;
            UpdateTitle();
        }

        private void Build()
        {
            _built = WorldBuilder.Build(_map);
            _ground = _built.Ground;
            _world = _built.Physics;
        }

        private void MakeRenderer()
        {
            // A mesh cache of its own, thrown away with it: some meshes are built from the world they're in (a fence follows
            // the ground), so a world built again needs them built again too
            _worldMeshes = new MeshCache();
            _renderer = new WorldRenderer(_built, GraphicsDevice, _worldMeshes);
            _renderer.BuildTerrain(_player);
            // The drone's camera, for the televisions tuned to it (see ScreenSpec): you, from wherever it's following
            _renderer.Feed(WorldRenderer.DroneChannel, () => _player.Driving != null ? null
                    : new CameraView(_player.Drone.Position, Vector3.Normalize(_player.Eye - _player.Drone.Position), Vector3.Up),
                batch => _renderer.AddPlayer(batch, _player));
        }

        // The map's files have been saved: the map built again from them, and you put back where you were, on foot (a
        // car you were in is gone with the old world). If they don't make a map now - a mistake, part way through an
        // edit - the world stays as it was, and the title says what's wrong.
        private void Reload()
        {
            try
            {
                _map = _reopen();
                Build();
                _watcher.Dispose();
                _watcher = new MapWatcher(_map.Files);   // it may have gained a district file, or lost one
            }
            catch (Exception e) when (e is System.IO.InvalidDataException or System.IO.IOException or InvalidOperationException)
            {
                Window.Title = "Basic.World - the map didn't load: " + e.Message;
                return;
            }
            var (feet, yaw) = (_player.Body.Position, _player.Body.Yaw);
            _player = new Player(feet + Vector3.Up * 0.1f, yaw, _world);
            _bird = new Bird(new Vector2(feet.X, feet.Z), _ground.SkylineAt, yaw);
            _renderer.Dispose();
            _worldMeshes.Dispose();
            MakeRenderer();
            UpdateTitle();
        }

        // The car parked at a start, or one brought there if there's none
        private Car CarAt(Start start)
        {
            var near = _built.Cars.FirstOrDefault(c => Vector2.Distance(new Vector2(c.Position.X, c.Position.Z), start.At) < 10f);
            if (near != null)
                return near;
            var car = new Car(new Vector3(start.At.X, _built.Terrain.HeightAt(start.At.X, start.At.Y), start.At.Y), start.Yaw, _world);
            _built.Cars.Add(car);
            return car;
        }

        private void UpdateTitle()
        {
            _titleWetness = (int)MathF.Round(_player.Wetness * 100f);
            _titleSpeed = _player.Driving is { } car ? (int)MathF.Round(MathF.Abs(car.Speed) * 3.6f) : -1;
            var view = _followBird ? "bird view" : _player.Driving != null ? (_player.View == ViewMode.FirstPerson ? "driving" : "driving, chase view")
                : _player.View == ViewMode.FirstPerson ? "your view" : "drone view";
            Window.Title = "Basic.World - " + view + (_titleSpeed >= 0 ? $" - {_titleSpeed} km/h" : "") +
                (_player.Driving?.Flooded == true ? " - flooded" : "") +
                (_player.Body.Swimming ? " - swimming" : "") + (_titleWetness > 0 ? $" - wet {_titleWetness}%" : "");
        }

        protected override void UpdateWorld(GameTime gameTime, KeyboardState keyboard)
        {
            if (_watcher?.Changed() == true)
                Reload();
            if (Pressed(keyboard, Keys.V))
            {
                if (_followBird)
                    _followBird = false;   // back to whichever view of yours it was
                else
                    _player.ToggleView();
                UpdateTitle();
            }
            if (Pressed(keyboard, Keys.G))
                _terrainGrid = !_terrainGrid;
            if (Pressed(keyboard, Keys.H))
                _showCompass = !_showCompass;
            if (Pressed(keyboard, Keys.B))
            {
                _followBird = !_followBird;
                UpdateTitle();
            }
            _jumpPressed |= Pressed(keyboard, Keys.Space);
            if (IsActive)
            {
                // R / F tilt your own view up and down, Home levels it
                _player.LookUp += Axis(keyboard, Keys.R, Keys.F) * TiltSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (Pressed(keyboard, Keys.Home))
                    _player.LookUp = 0f;
            }
            if (Pressed(keyboard, Keys.E) || (Shot is { } pressing && pressing.Keys.Contains('e') && !_shotPressedE && Clock >= pressing.After / 2f))
            {
                _shotPressedE = Shot != null;
                // Out of the car you're in, into one you're by, or else a door
                var car = _built.Cars.FirstOrDefault(_player.CanReach);
                if (_player.Driving != null)
                    _player.GetOut(_world);
                else if (car != null)
                    _player.GetIn(car);
                else
                    _ground.Interact(_player.Body.Position, _player.Body.Heading);
                UpdateTitle();
            }

            var input = IsActive ? ReadInput(keyboard) : MoveInput.None;
            var drive = IsActive ? ReadDrive(keyboard) : DriveInput.None;
            if (Shot is { } shot && shot.Keys.Contains('w'))
            {
                input = new MoveInput(new Vector2(0f, 1f), Run: shot.Keys.Contains('r'));
                drive = new DriveInput(1f, 0f);
            }
            _pending += MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrame);
            while (_pending >= StepTime)
            {
                _walkers[0] = (_player.Body.Position, CharacterController.Radius, Player.Height);
                _ground.StepDoors(StepTime, _world.Bodies, _walkers);
                if (_player.Driving != null)
                    _player.Drive(drive, StepTime, _world);
                else
                {
                    _player.Step(input with { Jump = _jumpPressed }, StepTime, _world);
                    GoThroughPortals();
                }
                foreach (var car in _built.Cars)
                    if (car != _player.Driving)
                        car.Step(DriveInput.Parked, StepTime, _world);
                _world.Step(StepTime);
                _bird.Step(StepTime);
                _jumpPressed = false;   // a jump happens on one tick, not every tick this frame
                _pending -= StepTime;
            }
        }

        // Walked into a door that leads elsewhere: through it, onto another map if it leads to one
        private void GoThroughPortals()
        {
            foreach (var portal in _built.Portals)
            {
                if (!portal.WalkedInto(_player.Body.Position, CharacterController.Radius))
                    continue;
                if (!portal.LeadsOffMap)
                    _player.Teleport(portal.To, portal.Yaw, _world);
                else if (_open != null)
                    GoOnto(portal.ToMap, portal.ToStart);
                return;
            }
        }

        // Onto another map, at one of its starts (its default if it hasn't that one): it's built, and you start afresh on
        // it, on foot. Its files, if it has any, are watched instead.
        private void GoOnto(string name, string startName)
        {
            _map = _open(name);
            _reopen = () => _open(name);
            Build();
            _watcher?.Dispose();
            _watcher = _map.Files.Count > 0 ? new MapWatcher(_map.Files) : null;
            if (startName == null || !_built.Starts.TryGetValue(startName, out var start))
                start = _built.Starts[_map.DefaultStart];
            var dropFrom = start.Above > 0f ? _built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above : 0f;
            _player = new Player(new Vector3(start.At.X, dropFrom, start.At.Y), start.Yaw, _world);
            _bird = new Bird(start.At, _ground.SkylineAt, start.Yaw);
            _renderer.Dispose();
            _worldMeshes.Dispose();
            MakeRenderer();
            UpdateTitle();
        }

        private static DriveInput ReadDrive(KeyboardState keyboard) => new DriveInput(
            MathHelper.Clamp(Axis(keyboard, Keys.Up, Keys.Down) + Axis(keyboard, Keys.W, Keys.S), -1f, 1f),
            MathHelper.Clamp(Axis(keyboard, Keys.Right, Keys.Left) + Axis(keyboard, Keys.D, Keys.A), -1f, 1f),
            keyboard.IsKeyDown(Keys.Space));

        private static MoveInput ReadInput(KeyboardState keyboard)
        {
            var forward = Axis(keyboard, Keys.Up, Keys.Down) + Axis(keyboard, Keys.W, Keys.S);
            return new MoveInput(
                new Vector2(Axis(keyboard, Keys.D, Keys.A), MathHelper.Clamp(forward, -1f, 1f)),
                Axis(keyboard, Keys.Right, Keys.Left),
                keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
        }

        protected override void DrawWorld(GameTime gameTime)
        {
            if ((int)MathF.Round(_player.Wetness * 100f) != _titleWetness ||
                (_player.Driving is { } car && (int)MathF.Round(MathF.Abs(car.Speed) * 3.6f) != _titleSpeed))
                UpdateTitle();
            _renderer.View.Terrain.ShowGrid = _terrainGrid;
            _renderer.DrawFeeds(_player.Body.Position, Clock, ColorsOn);
            _renderer.Draw(_player, Clock, ColorsOn, _bird, _followBird);
            if (_showCompass)
            {
                // Which way you're facing, or the car is: in the picture's own pixels, so it's scaled up with it
                var viewport = GraphicsDevice.Viewport;
                var unit = Math.Max(1, viewport.Width / LowResWidth);
                _compass.Draw(_player.Driving?.Yaw ?? _player.Body.Yaw, viewport.Width / 2, 3 * unit, unit,
                    Color.White, BackgroundColor);   // white, as every edge is, on the background
            }
        }

        // Where everything ended up, beside the screenshot
        protected override void WriteShotReport(string path)
        {
            var report = new System.Text.StringBuilder().AppendLine($"player {_player.Body.Position}" +
                    (_player.Driving is { } car ? $" driving at {car.Speed * 3.6f:F0} km/h, grounded {car.Grounded}" : ""))
                .AppendLine($"bird {_bird.Position} yaw {_bird.Yaw:F2} pitch {_bird.Pitch:F2} flap {_bird.FlapPhase:F2}")
                .AppendLine($"terrain chunks: {_built.Terrain.ChunksMade} of {_built.Terrain.ChunksX * _built.Terrain.ChunksZ} worked out, {_renderer.View.Terrain.Built} built in {_renderer.View.Terrain.BuildTime.TotalMilliseconds:F0} ms, {_renderer.View.Terrain.Drawn} drawn")
                .AppendLine($"meshes: {_renderer.Batch.Drawn} drawn, {_renderer.Batch.Culled} culled, {_renderer.Batch.DrawCalls} draw calls");
            foreach (var thing in _built.Things.Select(t => t.Body))
                report.AppendLine($"{thing.Name} {thing.Position} size {thing.Size} resting {thing.Resting} on {(thing.Floating ? "water" : thing.Support?.Name ?? "ground")}");
            System.IO.File.WriteAllText(path, report.ToString());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _renderer?.Dispose();
                _worldMeshes?.Dispose();
                _watcher?.Dispose();
                _compass?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
