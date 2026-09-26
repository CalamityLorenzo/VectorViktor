using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
    // Light things float. East of the town, a street (see Neighbourhood): houses either side of a road,
    // front gardens sloping down to it, picket fences round the back gardens, a swimming pool in one of
    // them, and billboards for the Commodore 64 and Atari either side of the road; down a lane from it, an old cottage whose front door, walked
    // into, takes you to a long corridor, and back, and next door another, whose window shows a hangar. See the
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

        private const float FogStart = 20f, FogEnd = 95f;   // metres

        // The world's parts, in the order they're put together (see WorldBuilder): each district's pads are
        // levelled over the ones before it. Where you can start (the optional command-line argument) is theirs.
        private static IDistrict[] Districts() => new IDistrict[] { new Countryside(), new Town(), new Neighbourhood() };
        private const string DefaultStart = "hills";

        private readonly string _start;
        private BasicEffect _basicEffect;

        private BuiltWorld _built;
        private PhysicsWorld _world;
        private Player _player;
        private WorldView _worldView;
        private readonly MeshBatch _batch = new MeshBatch();
        private MeshInstance _playerView, _droneView;
        private Color[] _playerColors;   // dry: as drawn, they're darker where wet
        private int _titleWetness = -1;
        private BuildingGround _ground;
        private float _pending;          // time not yet stepped through
        private bool _jumpPressed;       // since the last tick
        private bool _shotPressedE;

        public Game1(string start = null) : base(WindowWidth, WindowHeight, LowResWidth, LowResHeight, colorsKey: Keys.C)
        {
            _start = start ?? DefaultStart;
        }

        protected override void LoadWorld()
        {
            // Distance fades everything into the sky: otherwise, a long way off, the terrain's grid lines are
            // closer together than the pixels are and the far hills turn solid white
            _basicEffect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true, World = Matrix.Identity,
                FogEnabled = true, FogColor = BackgroundColor.ToVector3(), FogStart = FogStart, FogEnd = FogEnd,
            };

            _built = WorldBuilder.Build(Districts());
            _ground = _built.Ground;
            _world = _built.Physics;
            if (!_built.Starts.TryGetValue(_start, out var start))
                start = _built.Starts[DefaultStart];
            var dropFrom = start.Above > 0f ? _built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above : 0f;
            _player = new Player(new Vector3(start.At.X, dropFrom, start.At.Y), start.Yaw, _world);

            // The terrain's built a chunk at a time round the camera, out to where the fog has hidden it all
            _worldView = new WorldView(_built, GraphicsDevice, MeshCache, FogEnd + 15f);
            _worldView.Update(GraphicsDevice, _player.Eye, _player.Body.Position, all: true);
            _playerColors = PlayerMesh.Palette(new Color(50, 60, 120), new Color(200, 60, 40), new Color(230, 180, 140));
            _playerView = MeshCache.CreateInstance(GraphicsDevice, new MeshSource("player", PlayerMesh.Build, _playerColors));
            _droneView = MeshCache.CreateInstance(GraphicsDevice, new MeshSource("drone", DroneMesh.Build,
                DroneMesh.Palette(new Color(90, 90, 100), new Color(60, 60, 65), new Color(40, 40, 45), new Color(120, 220, 230))));
            _droneView.Scale = 1.5f;   // so it reads at low resolution, even a few metres off
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

        // Wet clothes are darker: the legs first, as you wade in, then the body and the head (see PlayerMesh's
        // heights), as high as you've been soaked.
        private static readonly (int part, float bottom, float top)[] Soakable =
            { (PlayerMesh.Legs, 0f, 0.85f), (PlayerMesh.Body, 0.85f, 1.5f), (PlayerMesh.Head, 1.5f, 1.8f) };

        private void DampenPlayer()
        {
            var soaked = _player.Wetness * Player.Height;
            foreach (var (part, bottom, top) in Soakable)
            {
                var wet = MathHelper.Clamp((soaked - bottom) / (top - bottom), 0f, 1f);
                for (var shade = 0; shade < 3; shade++)
                {
                    var dry = _playerColors[part + shade];
                    _playerView.SetColor(part + shade, Color.Lerp(dry, Color.Lerp(dry, Color.Black, 0.45f), wet));
                }
            }
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
                _ground.StepDoors(StepTime, _world.Bodies, new[] { (_player.Body.Position, CharacterController.Radius, Player.Height) });
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
            DampenPlayer();

            var body = _player.Body;
            Vector3 eye, lookAt;
            if (_player.View == ViewMode.FirstPerson)
            {
                eye = _player.Eye;
                lookAt = eye + body.Heading;
            }
            else
            {
                eye = _player.Drone.Position;
                lookAt = _player.Eye;
            }
            // The far plane is where the fog ends: past it everything's the background's colour anyway, and the
            // batch leaves out whatever's beyond it
            _basicEffect.View = Matrix.CreateLookAt(eye, lookAt, Vector3.Up);
            _basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(70f), GraphicsDevice.Viewport.AspectRatio, 0.1f, FogEnd);

            // The meshes face +Z; a yaw of 0 here faces -Z (north), hence Pi - yaw
            _playerView.Position = body.Position;
            _playerView.Yaw = MathHelper.Pi - body.Yaw;
            _droneView.Position = _player.Drone.Position;
            _droneView.Yaw = MathHelper.Pi - _player.Drone.Yaw;

            // Neither camera sees the thing it's in: from inside your own head (or the drone), you'd only
            // see the inside of it. Turn round in your own view, though, and the drone's there, following.
            _worldView.Update(GraphicsDevice, eye, body.Position);
            _batch.Begin(_basicEffect.View, _basicEffect.Projection);
            _worldView.Collect(_batch, eye, body.Position, Clock);
            _batch.Add(_player.View == ViewMode.Drone ? _playerView : _droneView);
            _worldView.DrawWindows(GraphicsDevice, _basicEffect, eye, body.Position, Clock, BackgroundColor, ColorsOn);
            _batch.Draw(GraphicsDevice, _basicEffect, BackgroundColor, ColorsOn);
        }

        // Where everything ended up, beside the screenshot
        protected override void WriteShotReport(string path)
        {
            var report = new System.Text.StringBuilder().AppendLine($"player {_player.Body.Position}")
                .AppendLine($"terrain chunks: {_built.Terrain.ChunksMade} of {_built.Terrain.ChunksX * _built.Terrain.ChunksZ} worked out, {_worldView.Terrain.Built} built in {_worldView.Terrain.BuildTime.TotalMilliseconds:F0} ms, {_worldView.Terrain.Drawn} drawn")
                .AppendLine($"meshes: {_batch.Drawn} drawn, {_batch.Culled} culled, {_batch.DrawCalls} draw calls");
            foreach (var thing in _built.Things.Select(t => t.Body))
                report.AppendLine($"{thing.Name} {thing.Position} size {thing.Size} resting {thing.Resting} on {(thing.Floating ? "water" : thing.Support?.Name ?? "ground")}");
            System.IO.File.WriteAllText(path, report.ToString());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _worldView?.Dispose();
                _basicEffect?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
