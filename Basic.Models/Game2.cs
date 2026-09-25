using MeshCore.Library;
using MeshLoader;
using MeshRawData;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace Basic.Models
{
    // A showcase of every mesh: all 15 laid out in a 5 x 3 grid, each one turning slowly about the vertical.
    // Everything is at one scale, so how big each thing is next to the others is its true relative size.
    // Full screen at 1920 x 1080. Space toggles colours / wireframe, F11 toggles full screen, Escape exits.
    public class Game2 : Game
    {
        private const int ScreenWidth = 1920;
        private const int ScreenHeight = 1080;

        private const int Columns = 5;
        private const float CellSize = 2.6f;      // distance between neighbouring meshes, in world units
        private const float Scale = 1.0f;
        private const float SpinSpeed = 45f;      // degrees per second

        private static readonly Color BackgroundColor = Color.CornflowerBlue;
        private static readonly Vector3 CameraPosition = new Vector3(0f, 4f, 10f);   // above the grid, looking down at it

        // CentreY is the height of the mesh's middle above its own origin, so it can be centred in its cell:
        // the meshes that stand on the floor (oak, sofa...) have their origin at the bottom, the ones built to
        // tumble (ingot, house...) have it in the middle. Measured from the mesh data; update if a mesh changes.
        private record struct Showcase(string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette, float CentreY);

        private static readonly Showcase[] Showcases =
        {
            new("ingot",       IngotMesh.Build,       IngotMesh.Palette(Color.Gold, Color.DarkGoldenrod, Color.Silver), 0f),
            new("pyramid",     PyramidMesh.Build,     PyramidMesh.Palette(Color.SaddleBrown, Color.OrangeRed), 0.1f),
            new("house",       HouseMesh.Build,       HouseMesh.Palette(new Color(210, 140, 80), new Color(150, 80, 200), Color.White, Color.Blue, new Color(80, 40, 20)), 0f),
            new("trabant",     TrabantMesh.Build,     TrabantMesh.Palette(new Color(34, 85, 34), new Color(120, 200, 230), new Color(40, 40, 40)), 0f),
            new("car",         CarMesh.Build,         CarMesh.Palette(new Color(200, 30, 30), new Color(70, 110, 160), new Color(30, 30, 30)), 0f),

            new("barn",        BarnMesh.Build,        BarnMesh.Palette(new Color(170, 40, 35), new Color(110, 110, 120), new Color(235, 225, 200)), 0f),
            new("coffeetable", CoffeeTableMesh.Build, CoffeeTableMesh.Palette(new Color(205, 155, 95), new Color(120, 80, 50)), 0.14f),
            new("sofa",        SofaMesh.Build,        SofaMesh.Palette(new Color(60, 125, 125), new Color(100, 170, 160), new Color(150, 100, 60)), 0.34f),
            new("settee",      SetteeMesh.Build,      SetteeMesh.Palette(new Color(195, 145, 45), new Color(225, 180, 85), new Color(150, 100, 60)), 0.40f),
            new("sideboard",   SideboardMesh.Build,   SideboardMesh.Palette(new Color(130, 80, 45), new Color(170, 115, 65), new Color(90, 55, 30), new Color(205, 175, 90)), 0.32f),

            new("television",  TelevisionMesh.Build,  TelevisionMesh.Palette(new Color(110, 70, 40), new Color(120, 140, 130), new Color(235, 225, 200), new Color(60, 45, 35), new Color(90, 55, 30), new Color(190, 190, 195)), 0.37f),
            new("fern",        FernMesh.Build,        FernMesh.Palette(new Color(190, 95, 60), new Color(50, 150, 60)), 0.16f),
            new("tree",        TreeMesh.Build,        TreeMesh.Palette(new Color(110, 75, 45), new Color(60, 140, 60)), 0.56f),
            new("oak",         OakMesh.Build,         OakMesh.Palette(new Color(100, 70, 45), new Color(70, 145, 55)), 1.17f),
            new("spikybush",   SpikyBushMesh.Build,   SpikyBushMesh.Palette(new Color(95, 65, 40), new Color(90, 130, 50)), 0.86f),
        };

        private readonly GraphicsDeviceManager _graphics;
        private readonly MeshCache _meshCache = new MeshCache();
        private readonly List<SpinningMeshInstance> _instances = new List<SpinningMeshInstance>();
        private BasicEffect _basicEffect;
        private RasterizerState _rasterizerState;
        private KeyboardState _previousKeyboard;
        private bool _colorsOn = true;   // off = faces drawn in the background colour (wireframe look)

        public Game2()
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
                View = Matrix.CreateLookAt(CameraPosition, Vector3.Zero, Vector3.Up),
                Projection = Matrix.CreatePerspectiveFieldOfView(
                    MathHelper.PiOver4,
                    GraphicsDevice.Viewport.AspectRatio,
                    0.1f,
                    100f)
            };
            _rasterizerState = new RasterizerState { CullMode = CullMode.None };

            var rows = (Showcases.Length + Columns - 1) / Columns;
            for (var i = 0; i < Showcases.Length; i++)
            {
                var showcase = Showcases[i];
                var column = i % Columns;
                var row = i / Columns;

                // Row 0 is the top row; the grid is centred on the origin.
                var x = (column - (Columns - 1) * 0.5f) * CellSize;
                var y = ((rows - 1) * 0.5f - row) * CellSize;

                var mesh = _meshCache.GetOrAdd(GraphicsDevice, showcase.Key, showcase.Build);
                _instances.Add(new SpinningMeshInstance(mesh, showcase.Palette)
                {
                    Position = new Vector3(x, y - showcase.CentreY * Scale, 0f),
                    Scale = Scale,
                    Pitch = 0f,
                    Yaw = MathHelper.ToRadians(i * 25f),   // staggered, so they don't all face the same way at once
                    YawSpeed = MathHelper.ToRadians(SpinSpeed),
                    PitchSpeed = 0f,
                });
            }
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();

            if (keyboard.IsKeyDown(Keys.F11) && _previousKeyboard.IsKeyUp(Keys.F11))
                _graphics.ToggleFullScreen();

            if (keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space))
                _colorsOn = !_colorsOn;

            _previousKeyboard = keyboard;

            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            foreach (var instance in _instances)
            {
                instance.ColorsOn = _colorsOn;
                instance.Spin(dt);
            }

            base.Update(gameTime);
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
