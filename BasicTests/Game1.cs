using BasicTests.Meshes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace BasicTests
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private BasicEffect _basicEffect;
        private KeyboardState _previousKeyboard;
        private RasterizerState _rasterizerState;
        private VertexPositionColor[] _triangleVertices;
        private MeshCache _meshCache = new MeshCache(RawData.Basic_Ingot_Frustrum);
        private readonly List<IngotFrustrum> _ingots = new List<IngotFrustrum>();
        private bool _edgesOnly;
        private bool _noEdgeColor;

        private const int IngotCount = 40;
        private static readonly Vector3 CameraPosition = new Vector3(0f, 0f, 3f);

        // (top, side, other) schemes for the coloured ingots.
        private static readonly (Color Top, Color Side, Color Other)[] ColorSchemes =
        {
            (Color.Gold, Color.DarkGoldenrod, Color.Silver),
            (Color.OrangeRed, Color.DarkRed, Color.SaddleBrown),
            (Color.LimeGreen, Color.DarkGreen, Color.Olive),
            (Color.DeepSkyBlue, Color.RoyalBlue, Color.Navy),
            (Color.Orchid, Color.DarkMagenta, Color.Indigo),
            (Color.Teal, Color.DarkSlateGray, Color.Aquamarine),
        };

        // "Without colour": greys, so the faces are still distinguishable (there's no lighting).
        private static readonly (Color Top, Color Side, Color Other) GreyScheme = (Color.LightGray, Color.Gray, Color.DimGray);

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            // TODO: Add your initialization logic here
            base.Initialize();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);

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
            _triangleVertices = BuildIsoscelesTriangle();
            // One shared mesh; every ingot only carries its own transform and colours.
            var ingotFrustrumMeshData = _meshCache.BuildIngot(GraphicsDevice);
            BuildIngots(ingotFrustrumMeshData);
        }

        private void BuildIngots(MeshData mesh)
        {
            var rng = new Random(1234);   // fixed seed: same scene every run
            var halfFovTan = MathF.Tan(MathHelper.PiOver4 / 2f);
            var aspect = GraphicsDevice.Viewport.AspectRatio;

            float Between(float min, float max) => min + (max - min) * rng.NextSingle();
            float RandomSign() => rng.Next(2) == 0 ? -1f : 1f;

            for (var i = 0; i < IngotCount; i++)
            {
                var colored = rng.NextDouble() < 0.6;
                var scheme = colored ? ColorSchemes[rng.Next(ColorSchemes.Length)] : GreyScheme;

                // Distance in front of the camera. Spread x/y across the visible area at that
                // depth (90% of it) so near and far ingots are all on screen.
                var distance = Between(3f, 20f);
                var halfHeight = halfFovTan * distance * 0.9f;
                var halfWidth = halfHeight * aspect;

                _ingots.Add(new IngotFrustrum(mesh, scheme.Top, scheme.Side, scheme.Other)
                {
                    Position = new Vector3(
                        Between(-halfWidth, halfWidth),
                        Between(-halfHeight, halfHeight),
                        CameraPosition.Z - distance),
                    Scale = Between(0.3f, 1.5f),
                    Yaw = Between(0f, MathHelper.TwoPi),
                    Pitch = Between(0f, MathHelper.TwoPi),
                    YawSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 120f)),
                    PitchSpeed = RandomSign() * MathHelper.ToRadians(Between(15f, 100f)),
                });
            }
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();

            if (keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space))
                _edgesOnly = !_edgesOnly;
            if (keyboard.IsKeyDown(Keys.K) && _previousKeyboard.IsKeyUp(Keys.K))
                _noEdgeColor = !_noEdgeColor;

            _previousKeyboard = keyboard;
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            foreach (var ingot in _ingots)
            {
                ingot.EdgesOnly = _edgesOnly;
                ingot.NoEdgeColor = _noEdgeColor;
                ingot.Yaw += ingot.YawSpeed * dt;
                ingot.Pitch += ingot.PitchSpeed * dt;
                ingot.Update(gameTime);
            }

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.RasterizerState = _rasterizerState;

            DrawIsoscelesTriangle();
            foreach (var ingot in _ingots)
                ingot.Draw(gameTime, GraphicsDevice, _basicEffect);

            base.Draw(gameTime);
        }

        // 3 vertices, one triangle: two equal-length sides (apex to each base corner),
        // base width (0.8) differs from the equal sides (~1.08), so it's isosceles not equilateral.
        private static VertexPositionColor[] BuildIsoscelesTriangle()
        {
            return new[]
            {
                new VertexPositionColor(new Vector3(-0.9f, 0.5f, 0f), Color.Red),
                new VertexPositionColor(new Vector3(-1.3f, -0.5f, 0f), Color.Green),
                new VertexPositionColor(new Vector3(-0.5f, -0.5f, 0f), Color.Blue),
            };
        }

        private void DrawIsoscelesTriangle()
        {
            _basicEffect.World = Matrix.Identity;

            foreach (var pass in _basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, _triangleVertices, 0, 1);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _meshCache.Dispose();
                _basicEffect?.Dispose();
                _rasterizerState?.Dispose();
                _spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
