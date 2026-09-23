using MeshLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using World.Core;
using World.Core.Characters;
using World.Core.Movement;

namespace Basic.World
{
    // Out of doors: walk over rolling hills, climb the causeway onto the plateau, jump off its cliffs, or
    // go down into the basin. See the world through your own eyes, or from your camera drone as it flies
    // after you. Drawn to a small render target and scaled up with hard pixels, like Basic.Levels.
    // Up / W and Down / S walk (hold Shift to run), Left / Right turn, A / D sidestep, Space jumps.
    // V switches between your own view and the drone's. Tab toggles colours / wireframe, L the
    // low-resolution look, F11 full screen, Escape exits.
    public class Game1 : Game
    {
        private const int WindowWidth = 1440;
        private const int WindowHeight = 810;
        private const int LowResWidth = 480;      // 16:9, so 3 x fits the window and 4 x fills 1920 x 1080
        private const int LowResHeight = 270;

        private const float StepTime = 1f / 60f;      // the world always moves on in steps of this
        private const float MaxFrame = 0.25f;     // after a stall, catch up no more than this, rather than fall through the world

        private static readonly Color BackgroundColor = new Color(27, 13, 120);
        private const float FogStart = 20f, FogEnd = 95f;   // metres

        // Where you can start (the optional command-line argument), and which way you face
        private static readonly Dictionary<string, (Vector2 at, float yaw)> Starts = new Dictionary<string, (Vector2, float)>
        {
            ["hills"] = (Vector2.Zero, MathHelper.PiOver4),                                // looking north-east to the plateau
            ["plateau"] = (TerrainGenerator.PlateauCentre, MathHelper.Pi),                 // on top, facing its sheer south side
            ["causeway"] = (new Vector2(TerrainGenerator.PlateauCentre.X - TerrainGenerator.PlateauRadius - TerrainGenerator.RampLength,
                                        TerrainGenerator.PlateauCentre.Y), MathHelper.PiOver2),   // at its foot, facing up it
            ["basin"] = (TerrainGenerator.BasinCentre, MathHelper.PiOver4),
        };

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

        private Terrain _terrain;
        private Player _player;
        private MeshInstance _terrainView, _playerView, _droneView;
        private float _pending;          // time not yet stepped through
        private bool _jumpPressed;       // since the last tick

        public Game1(string start = null)
        {
            _start = start != null && Starts.ContainsKey(start) ? start : "hills";
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

            _terrain = TerrainGenerator.Create();
            var (at, yaw) = Starts[_start];
            _player = new Player(new Vector3(at.X, 0f, at.Y), yaw, _terrain);

            var terrainMesh = _meshCache.GetOrAdd(GraphicsDevice, "terrain", d => TerrainMesh.Build(d, _terrain, TerrainGenerator.WaterLevel + 0.5f));
            _terrainView = Placed(new MeshInstance(terrainMesh, TerrainMesh.Palette()));
            _playerView = Placed(new MeshInstance(_meshCache.GetOrAdd(GraphicsDevice, "player", PlayerMesh.Build),
                PlayerMesh.Palette(new Color(50, 60, 120), new Color(200, 60, 40), new Color(230, 180, 140))));
            _droneView = Placed(new MeshInstance(_meshCache.GetOrAdd(GraphicsDevice, "drone", DroneMesh.Build),
                DroneMesh.Palette(new Color(90, 90, 100), new Color(60, 60, 65), new Color(40, 40, 45), new Color(120, 220, 230))));
            _droneView.Scale = 1.5f;   // so it reads at low resolution, even a few metres off
            UpdateTitle();
        }

        // Stands upright where it's put, with no spin of its own.
        private static MeshInstance Placed(MeshInstance instance)
        {
            instance.Position = Vector3.Zero;
            instance.Pitch = 0f;
            instance.Yaw = 0f;
            instance.YawSpeed = 0f;
            instance.PitchSpeed = 0f;
            return instance;
        }

        private void UpdateTitle() =>
            Window.Title = "Basic.World - " + (_player.View == ViewMode.FirstPerson ? "your view" : "drone view");

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();

            if (Pressed(keyboard, Keys.F11))
                _graphics.ToggleFullScreen();
            if (Pressed(keyboard, Keys.Tab))
                _colorsOn = !_colorsOn;
            if (Pressed(keyboard, Keys.L))
                _lowResOn = !_lowResOn;
            if (Pressed(keyboard, Keys.V))
            {
                _player.ToggleView();
                UpdateTitle();
            }
            _jumpPressed |= Pressed(keyboard, Keys.Space);

            var input = IsActive ? ReadInput(keyboard) : MoveInput.None;
            _pending += MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, MaxFrame);
            while (_pending >= StepTime)
            {
                _player.Step(input with { Jump = _jumpPressed }, StepTime, _terrain);
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
            _basicEffect.View = Matrix.CreateLookAt(eye, lookAt, Vector3.Up);
            _basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(70f), GraphicsDevice.Viewport.AspectRatio, 0.1f, 300f);

            // The meshes face +Z; a yaw of 0 here faces -Z (north), hence Pi - yaw
            _playerView.Position = body.Position;
            _playerView.Yaw = MathHelper.Pi - body.Yaw;
            _droneView.Position = _player.Drone.Position;
            _droneView.Yaw = MathHelper.Pi - _player.Drone.Yaw;

            // Neither camera sees the thing it's in: from inside your own head (or the drone), you'd only
            // see the inside of it. Turn round in your own view, though, and the drone's there, following.
            Draw(_terrainView, gameTime);
            if (_player.View == ViewMode.Drone)
                Draw(_playerView, gameTime);
            else
                Draw(_droneView, gameTime);

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

        private void Draw(MeshInstance instance, GameTime gameTime)
        {
            instance.ColorsOn = _colorsOn;
            instance.Draw(gameTime, GraphicsDevice, _basicEffect, BackgroundColor);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _meshCache.Dispose();
                _basicEffect?.Dispose();
                _rasterizerState?.Dispose();
                _lowRes?.Dispose();
                _spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
