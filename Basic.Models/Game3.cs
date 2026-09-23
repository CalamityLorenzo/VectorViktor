using MeshCore.Library;
using MeshLoader;
using MeshRawData;
using MeshRawData.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace Basic.Models
{
    // Three staircases (a straight run, and a quarter turn each way), each standing on the ground and
    // turning slowly about the vertical, seen through an orbit camera. Same step count and rise for all
    // three, so their sizes compare truly.
    // Left-drag or the arrow keys orbit, the mouse wheel or W / S zoom. Space toggles colours / wireframe,
    // F11 toggles full screen, Escape exits. Full screen at 1920 x 1080.
    public class Game3 : Game
    {
        private const int ScreenWidth = 1920;
        private const int ScreenHeight = 1080;

        private const float Spacing = 6.0f;         // distance between neighbouring staircases
        private const float SpinSpeed = 30f;        // degrees per second

        // The orbit camera looks at this point, about the middle of the tallest staircase (10 steps at
        // a 0.18 rise climb 1.8 high).
        private static readonly Vector3 CameraTarget = new Vector3(0f, 0.9f, 0f);
        private const float MinDistance = 1.5f, MaxDistance = 40f;
        private const float MinPitch = -10f, MaxPitch = 85f;   // degrees; stops it flipping over the top
        private const float MouseOrbitSpeed = 0.005f;          // radians per pixel dragged
        private const float KeyOrbitSpeed = 1.5f;              // radians per second
        private const float WheelZoom = 0.9f;                  // distance multiplier per wheel notch
        private const float KeyZoomSpeed = 2.0f;               // zoom factor per second

        private static readonly Color BackgroundColor = Color.CornflowerBlue;

        private record struct Showcase(string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette);

        private static readonly Color[] StaircasePalette = StaircaseMesh.Palette(new Color(160, 120, 75), new Color(150, 150, 155));

        private static readonly Showcase[] Showcases =
        {
            new("stair-straight",   d => StaircaseMesh.BuildStraight(d, steps: 10),                     StaircasePalette),
            new("stair-turn-right", d => StaircaseMesh.BuildQuarterTurn(d, 5, 5, StairTurn.Right),       StaircasePalette),
            new("stair-turn-left",  d => StaircaseMesh.BuildQuarterTurn(d, 5, 5, StairTurn.Left),        StaircasePalette),
        };

        private readonly GraphicsDeviceManager _graphics;
        private readonly MeshCache _meshCache = new MeshCache();
        private readonly List<MeshInstance> _instances = new List<MeshInstance>();
        private BasicEffect _basicEffect;
        private RasterizerState _rasterizerState;
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;
        private bool _colorsOn = true;   // off = faces drawn in the background colour (wireframe look)

        // Where the camera is, as spherical coordinates round CameraTarget.
        private float _cameraYaw = 0f;
        private float _cameraPitch = MathHelper.ToRadians(20f);
        private float _cameraDistance = 9f;

        public Game3()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = ScreenWidth,
                PreferredBackBufferHeight = ScreenHeight,
                IsFullScreen = true,
                // Exclusive fullscreen leaves the process running (window gone, exe alive) after exit; use borderless.
                HardwareModeSwitch = false,
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            _basicEffect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                World = Matrix.Identity,
                Projection = Matrix.CreatePerspectiveFieldOfView(
                    MathHelper.PiOver4,
                    GraphicsDevice.Viewport.AspectRatio,
                    0.1f,
                    100f)
            };
            UpdateCamera();
            _rasterizerState = new RasterizerState { CullMode = CullMode.None };

            for (var i = 0; i < Showcases.Length; i++)
            {
                var showcase = Showcases[i];
                var mesh = _meshCache.GetOrAdd(GraphicsDevice, showcase.Key, showcase.Build);
                _instances.Add(new MeshInstance(mesh, showcase.Palette)
                {
                    // In a row along X. Their origin is the foot of the first step, so y = 0 is the ground.
                    Position = new Vector3((i - (Showcases.Length - 1) * 0.5f) * Spacing, 0f, 0f),
                    Pitch = 0f,
                    Yaw = MathHelper.ToRadians(i * 40f),
                    YawSpeed = MathHelper.ToRadians(SpinSpeed),
                    PitchSpeed = 0f,
                });
            }
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();
            var mouse = Mouse.GetState();
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();

            if (keyboard.IsKeyDown(Keys.F11) && _previousKeyboard.IsKeyUp(Keys.F11))
                _graphics.ToggleFullScreen();

            if (keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space))
                _colorsOn = !_colorsOn;

            if (IsActive)
                UpdateOrbitCamera(keyboard, mouse, dt);

            _previousKeyboard = keyboard;
            _previousMouse = mouse;

            foreach (var instance in _instances)
            {
                instance.ColorsOn = _colorsOn;
                instance.Yaw += instance.YawSpeed * dt;
                instance.Update(gameTime);
            }

            base.Update(gameTime);
        }

        private void UpdateOrbitCamera(KeyboardState keyboard, MouseState mouse, float dt)
        {
            // Orbit: dragging with the left button (only if it was already down last frame, so a click doesn't jump), or the arrows.
            if (mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Pressed)
            {
                _cameraYaw -= (mouse.X - _previousMouse.X) * MouseOrbitSpeed;
                _cameraPitch += (mouse.Y - _previousMouse.Y) * MouseOrbitSpeed;
            }
            if (keyboard.IsKeyDown(Keys.Left)) _cameraYaw -= KeyOrbitSpeed * dt;
            if (keyboard.IsKeyDown(Keys.Right)) _cameraYaw += KeyOrbitSpeed * dt;
            if (keyboard.IsKeyDown(Keys.Up)) _cameraPitch += KeyOrbitSpeed * dt;
            if (keyboard.IsKeyDown(Keys.Down)) _cameraPitch -= KeyOrbitSpeed * dt;
            _cameraPitch = MathHelper.Clamp(_cameraPitch, MathHelper.ToRadians(MinPitch), MathHelper.ToRadians(MaxPitch));
            _cameraYaw = MathHelper.WrapAngle(_cameraYaw);

            // Zoom: each wheel notch (120 units) scales the distance, so it feels the same near and far.
            var notches = (mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue) / 120f;
            _cameraDistance *= MathF.Pow(WheelZoom, notches);
            if (keyboard.IsKeyDown(Keys.W)) _cameraDistance /= MathF.Pow(KeyZoomSpeed, dt);
            if (keyboard.IsKeyDown(Keys.S)) _cameraDistance *= MathF.Pow(KeyZoomSpeed, dt);
            _cameraDistance = MathHelper.Clamp(_cameraDistance, MinDistance, MaxDistance);

            UpdateCamera();
        }

        private void UpdateCamera()
        {
            var offset = new Vector3(
                MathF.Sin(_cameraYaw) * MathF.Cos(_cameraPitch),
                MathF.Sin(_cameraPitch),
                MathF.Cos(_cameraYaw) * MathF.Cos(_cameraPitch));
            _basicEffect.View = Matrix.CreateLookAt(CameraTarget + offset * _cameraDistance, CameraTarget, Vector3.Up);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(BackgroundColor);
            GraphicsDevice.RasterizerState = _rasterizerState;

            foreach (var instance in _instances)
                instance.Draw(gameTime, GraphicsDevice, _basicEffect, BackgroundColor);

            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _meshCache.Dispose();
                _basicEffect?.Dispose();
                _rasterizerState?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
