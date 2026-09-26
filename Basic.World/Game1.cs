using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using World.Buildings;
using World.Core.Characters;
using World.Core.Movement;
using World.Core.Physics;

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
    // world through your own eyes, or from your camera drone as it flies after you. Drawn to a small render target and scaled up with hard pixels (see RetroGame).
    // Up / W and Down / S walk (hold Shift to run), Left / Right turn, A / D sidestep, Space jumps, E opens or shuts a door.
    // V switches between your own view and the drone's. C toggles colours / wireframe, L the low-resolution
    // look, F11 full screen, Escape exits.
    //
    // For development, a BASIC_WORLD_SHOT (see RetroGame) also saves where every body is beside the picture.
    // Its keys, all optional: v starts in the drone view, w holds walk forward, r runs, e presses E once,
    // halfway to the shot.
    public class Game1 : RetroGame
    {
        private const int WindowWidth = 1440;
        private const int WindowHeight = 810;
        private const int LowResWidth = 640;      // a wide strip, 2.5 : 1, letterboxed: 2 x (1280 x 512) in the window,
        private const int LowResHeight = 256;     // 3 x (1920 x 768) full screen at 1920 x 1080

        private const float StepTime = 1f / 60f;      // the world always moves on in steps of this
        private const float MaxFrame = 0.25f;     // after a stall, catch up no more than this, rather than fall through the world

        private readonly string _start;

        private BuiltWorld _built;
        private PhysicsWorld _world;
        private Player _player;
        private WorldRenderer _renderer;
        private int _titleWetness = -1;
        private BuildingGround _ground;
        private float _pending;          // time not yet stepped through
        private bool _jumpPressed;       // since the last tick
        private bool _shotPressedE;
        private readonly (Vector3 feet, float radius, float height)[] _walkers = new (Vector3, float, float)[1];   // who the doors must not swing into

        public Game1(string start = null) : base(WindowWidth, WindowHeight, LowResWidth, LowResHeight, colorsKey: Keys.C)
        {
            _start = start ?? WorldBuilder.DefaultStart;
        }

        protected override void LoadWorld()
        {
            _built = WorldBuilder.Build(WorldBuilder.Standard());
            _ground = _built.Ground;
            _world = _built.Physics;
            if (!_built.Starts.TryGetValue(_start, out var start))
                start = _built.Starts[WorldBuilder.DefaultStart];
            var dropFrom = start.Above > 0f ? _built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above : 0f;
            _player = new Player(new Vector3(start.At.X, dropFrom, start.At.Y), start.Yaw, _world);

            _renderer = new WorldRenderer(_built, GraphicsDevice, MeshCache);
            _renderer.BuildTerrain(_player);
            if (Shot?.Keys.Contains('v') == true)
                _player.ToggleView();
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            _titleWetness = (int)MathF.Round(_player.Wetness * 100f);
            Window.Title = "Basic.World - " + (_player.View == ViewMode.FirstPerson ? "your view" : "drone view") +
                (_player.Body.Swimming ? " - swimming" : "") + (_titleWetness > 0 ? $" - wet {_titleWetness}%" : "");
        }

        protected override void UpdateWorld(GameTime gameTime, KeyboardState keyboard)
        {
            if (Pressed(keyboard, Keys.V))
            {
                _player.ToggleView();
                UpdateTitle();
            }
            _jumpPressed |= Pressed(keyboard, Keys.Space);
            if (Pressed(keyboard, Keys.E) || (Shot is { } pressing && pressing.Keys.Contains('e') && !_shotPressedE && Clock >= pressing.After / 2f))
            {
                _shotPressedE = Shot != null;
                _ground.Interact(_player.Body.Position, _player.Body.Heading);
            }

            var input = IsActive ? ReadInput(keyboard) : MoveInput.None;
            if (Shot is { } shot && shot.Keys.Contains('w'))
                input = new MoveInput(new Vector2(0f, 1f), Run: shot.Keys.Contains('r'));
            _pending += MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrame);
            while (_pending >= StepTime)
            {
                _walkers[0] = (_player.Body.Position, CharacterController.Radius, Player.Height);
                _ground.StepDoors(StepTime, _world.Bodies, _walkers);
                _player.Step(input with { Jump = _jumpPressed }, StepTime, _world);
                GoThroughPortals();
                _world.Step(StepTime);
                _jumpPressed = false;   // a jump happens on one tick, not every tick this frame
                _pending -= StepTime;
            }
        }

        // Walked into a door that leads elsewhere: through it
        private void GoThroughPortals()
        {
            foreach (var portal in _built.Portals)
                if (portal.WalkedInto(_player.Body.Position, CharacterController.Radius))
                {
                    _player.Teleport(portal.To, portal.Yaw, _world);
                    return;
                }
        }

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
            if ((int)MathF.Round(_player.Wetness * 100f) != _titleWetness)
                UpdateTitle();
            _renderer.Draw(_player, Clock, ColorsOn);
        }

        // Where everything ended up, beside the screenshot
        protected override void WriteShotReport(string path)
        {
            var report = new System.Text.StringBuilder().AppendLine($"player {_player.Body.Position}")
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
            }
            base.Dispose(disposing);
        }
    }
}
