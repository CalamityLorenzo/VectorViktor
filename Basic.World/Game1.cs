using MeshLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Linq;
using World.Buildings;
using World.Core;
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
    // them, and a billboard for the Commodore 64. See the
    // world through your own eyes, or from your camera drone as it flies after you. Drawn to a small render target and scaled up with hard pixels, like Basic.Levels.
    // Up / W and Down / S walk (hold Shift to run), Left / Right turn, A / D sidestep, Space jumps, E opens or shuts a door.
    // V switches between your own view and the drone's. C toggles colours / wireframe (not Tab, which
    // Alt+Tab would press on the way out), L the low-resolution look, F11 full screen, Escape exits.
    //
    // For development, BASIC_WORLD_SHOT="file.png;seconds;keys" saves one low-resolution frame to the
    // file after that many seconds (default 3), and where every body is to file.txt, then exits, with no need of the screen. keys, all optional:
    // v starts in the drone view, w holds walk forward, r runs, e presses E once, halfway to the shot.
    public class Game1 : Game
    {
        private const int WindowWidth = 1440;
        private const int WindowHeight = 810;
        private const int LowResWidth = 640;      // 16:9, so 3 x fits the window and 4 x fills 1920 x 1080
        private const int LowResHeight = 256;

        private const float StepTime = 1f / 60f;      // the world always moves on in steps of this
        private const float MaxFrame = 0.25f;     // after a stall, catch up no more than this, rather than fall through the world

        private static readonly Color BackgroundColor = new Color(27, 13, 120);
        private const float FogStart = 20f, FogEnd = 95f;   // metres

        // The world's parts, in the order they're put together (see WorldBuilder): each district's pads are
        // levelled over the ones before it. Where you can start (the optional command-line argument) is theirs.
        private static IDistrict[] Districts() => new IDistrict[] { new Countryside(), new Town(), new Neighbourhood() };
        private const string DefaultStart = "hills";

        private readonly GraphicsDeviceManager _graphics;
        private readonly MeshCache _meshCache = new MeshCache();
        private readonly string _start;
        private BasicEffect _basicEffect;
        private RasterizerState _rasterizerState;
        private RenderTarget2D _lowRes;
        private SpriteBatch _spriteBatch;
        private KeyboardState _previousKeyboard;
        private bool _colorsOn = true;   // off = faces drawn in the background colour (wireframe look)
        private bool _lowResOn = true;

        private BuiltWorld _built;
        private PhysicsWorld _world;
        private Player _player;
        private WorldView _worldView;
        private readonly MeshBatch _batch = new MeshBatch();
        private MeshInstance _playerView, _droneView;
        private Color[] _playerColors, _playerPalette;   // dry, and as drawn: darker where wet
        private int _titleWetness = -1;
        private BuildingGround _ground;
        private float _pending;          // time not yet stepped through
        private bool _jumpPressed;       // since the last tick

        private readonly (string file, float after, string keys)? _shot = ReadShot();
        private float _clock;
        private bool _shotPressedE;

        private static (string, float, string)? ReadShot()
        {
            var setting = Environment.GetEnvironmentVariable("BASIC_WORLD_SHOT");
            if (string.IsNullOrEmpty(setting))
                return null;
            var parts = setting.Split(';');
            var after = parts.Length > 1 && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : 3f;
            return (parts[0], after, parts.Length > 2 ? parts[2] : "");
        }

        public Game1(string start = null)
        {
            _start = start ?? DefaultStart;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = WindowWidth,
                PreferredBackBufferHeight = WindowHeight,
                // Exclusive fullscreen leaves the process running (window gone, exe alive) after exit; use borderless.
                HardwareModeSwitch = false,
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            // Distance fades everything into the sky: otherwise, a long way off, the terrain's grid lines are
            // closer together than the pixels are and the far hills turn solid white
            _basicEffect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true, World = Matrix.Identity,
                FogEnabled = true, FogColor = BackgroundColor.ToVector3(), FogStart = FogStart, FogEnd = FogEnd,
            };
            _rasterizerState = new RasterizerState { CullMode = CullMode.None };
            _lowRes = new RenderTarget2D(GraphicsDevice, LowResWidth, LowResHeight, false, SurfaceFormat.Color, DepthFormat.Depth24);
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            _built = WorldBuilder.Build(Districts());
            _ground = _built.Ground;
            _world = _built.Physics;
            if (!_built.Starts.TryGetValue(_start, out var start))
                start = _built.Starts[DefaultStart];
            var dropFrom = start.Above > 0f ? _built.Terrain.HeightAt(start.At.X, start.At.Y) + start.Above : 0f;
            _player = new Player(new Vector3(start.At.X, dropFrom, start.At.Y), start.Yaw, _world);

            // The terrain's built a chunk at a time round the camera, out to where the fog has hidden it all
            _worldView = new WorldView(_built, GraphicsDevice, _meshCache, FogEnd + 15f);
            _worldView.Update(GraphicsDevice, _player.Eye, all: true);
            _playerColors = PlayerMesh.Palette(new Color(50, 60, 120), new Color(200, 60, 40), new Color(230, 180, 140));
            _playerPalette = (Color[])_playerColors.Clone();
            _playerView = new MeshInstance(_meshCache.GetOrAdd(GraphicsDevice, "player", PlayerMesh.Build), _playerPalette);
            _droneView = new MeshInstance(_meshCache.GetOrAdd(GraphicsDevice, "drone", DroneMesh.Build),
                DroneMesh.Palette(new Color(90, 90, 100), new Color(60, 60, 65), new Color(40, 40, 45), new Color(120, 220, 230)))
            {
                Scale = 1.5f,   // so it reads at low resolution, even a few metres off
            };
            if (_shot?.keys.Contains('v') == true)
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
        private void DampenPlayer()
        {
            var soaked = _player.Wetness * Player.Height;
            foreach (var (part, bottom, top) in new[] { (PlayerMesh.Legs, 0f, 0.85f), (PlayerMesh.Body, 0.85f, 1.5f), (PlayerMesh.Head, 1.5f, 1.8f) })
            {
                var wet = MathHelper.Clamp((soaked - bottom) / (top - bottom), 0f, 1f);
                for (var shade = 0; shade < 3; shade++)
                    _playerPalette[part + shade] = Color.Lerp(_playerColors[part + shade], Color.Lerp(_playerColors[part + shade], Color.Black, 0.45f), wet);
            }
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();

            if (Pressed(keyboard, Keys.F11))
                _graphics.ToggleFullScreen();
            if (Pressed(keyboard, Keys.C))
                _colorsOn = !_colorsOn;
            if (Pressed(keyboard, Keys.L))
                _lowResOn = !_lowResOn;
            if (Pressed(keyboard, Keys.V))
            {
                _player.ToggleView();
                UpdateTitle();
            }
            _jumpPressed |= Pressed(keyboard, Keys.Space);
            if (Pressed(keyboard, Keys.E) || (_shot is { } pressing && pressing.keys.Contains('e') && !_shotPressedE && _clock >= pressing.after / 2f))
            {
                _shotPressedE = _shot != null;
                _ground.Interact(_player.Body.Position, _player.Body.Heading);
            }

            var input = IsActive ? ReadInput(keyboard) : MoveInput.None;
            if (_shot is { } shot && shot.keys.Contains('w'))
                input = new MoveInput(new Vector2(0f, 1f), Run: shot.keys.Contains('r'));
            _pending += MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrame);
            while (_pending >= StepTime)
            {
                _ground.StepDoors(StepTime, _world.Bodies, new[] { (_player.Body.Position, CharacterController.Radius, Player.Height) });
                _player.Step(input with { Jump = _jumpPressed }, StepTime, _world);
                _world.Step(StepTime);
                _jumpPressed = false;   // a jump happens on one tick, not every tick this frame
                _pending -= StepTime;
            }

            _previousKeyboard = keyboard;
            base.Update(gameTime);
        }

        private bool Pressed(KeyboardState keyboard, Keys key) => keyboard.IsKeyDown(key) && _previousKeyboard.IsKeyUp(key);

        private MoveInput ReadInput(KeyboardState keyboard)
        {
            var forward = Axis(keyboard, Keys.Up, Keys.Down) + Axis(keyboard, Keys.W, Keys.S);
            return new MoveInput(
                new Vector2(Axis(keyboard, Keys.D, Keys.A), MathHelper.Clamp(forward, -1f, 1f)),
                Axis(keyboard, Keys.Right, Keys.Left),
                keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
        }

        private static float Axis(KeyboardState keyboard, Keys positive, Keys negative) =>
            (keyboard.IsKeyDown(positive) ? 1f : 0f) - (keyboard.IsKeyDown(negative) ? 1f : 0f);

        protected override void Draw(GameTime gameTime)
        {
            if ((int)MathF.Round(_player.Wetness * 100f) != _titleWetness)
                UpdateTitle();
            DampenPlayer();

            var target = _lowResOn ? _lowRes : null;
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(BackgroundColor);

            // SpriteBatch leaves these changed, so set them each frame
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = _rasterizerState;

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
            _worldView.Update(GraphicsDevice, eye);
            _batch.Begin(_basicEffect.View, _basicEffect.Projection);
            _worldView.Collect(_batch, eye);
            _batch.Add(_player.View == ViewMode.Drone ? _playerView : _droneView);
            _batch.Draw(GraphicsDevice, _basicEffect, BackgroundColor, _colorsOn);

            _clock += (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_shot is { } saving && _clock >= saving.after)
            {
                GraphicsDevice.SetRenderTarget(null);
                using (var file = System.IO.File.Create(saving.file))
                    _lowRes.SaveAsPng(file, LowResWidth, LowResHeight);
                // and where everything ended up, beside it
                var report = new System.Text.StringBuilder().AppendLine($"player {_player.Body.Position}")
                    .AppendLine($"terrain chunks: {_built.Terrain.ChunksMade} of {_built.Terrain.ChunksX * _built.Terrain.ChunksZ} worked out, {_worldView.Terrain.Built} built in {_worldView.Terrain.BuildTime.TotalMilliseconds:F0} ms, {_worldView.Terrain.Drawn} drawn")
                    .AppendLine($"meshes: {_batch.Drawn} drawn, {_batch.Culled} culled, {_batch.DrawCalls} draw calls");
                foreach (var thing in _built.Things.Select(t => t.Body))
                    report.AppendLine($"{thing.Name} {thing.Position} size {thing.Size} resting {thing.Resting} on {(thing.Floating ? "water" : thing.Support?.Name ?? "ground")}");
                System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(saving.file, ".txt"), report.ToString());
                Exit();
                return;
            }

            if (_lowResOn)
            {
                GraphicsDevice.SetRenderTarget(null);
                GraphicsDevice.Clear(BackgroundColor);

                // Largest whole-number scale that fits, centred, so every low-res pixel is the same size
                var back = GraphicsDevice.PresentationParameters;
                var scale = Math.Max(1, Math.Min(back.BackBufferWidth / LowResWidth, back.BackBufferHeight / LowResHeight));
                var width = LowResWidth * scale;
                var height = LowResHeight * scale;
                var destination = new Rectangle((back.BackBufferWidth - width) / 2, (back.BackBufferHeight - height) / 2, width, height);

                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _spriteBatch.Draw(_lowRes, destination, Color.White);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _meshCache.Dispose();
                _worldView?.Dispose();
                _basicEffect?.Dispose();
                _rasterizerState?.Dispose();
                _lowRes?.Dispose();
                _spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
