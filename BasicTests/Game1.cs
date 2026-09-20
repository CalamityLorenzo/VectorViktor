using BasicTests.Meshes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace BasicTests
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private SpriteBatch _spriteBatch;
        private BasicEffect _basicEffect;
        private KeyboardState _previousKeyboard;
        private bool _edgesOnly;
        private RasterizerState _rasterizerState;
        private VertexPositionColor[] _triangleVertices;

        private IngotFrustrum _ingotFrustrum;

        private static readonly Color TopColor = Color.Gold;
        private static readonly Color SideColor = Color.DarkGoldenrod;
        private static readonly Color OtherColor = Color.Silver;

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
                View = Matrix.CreateLookAt(new Vector3(0f, 0f, 3f), Vector3.Zero, Vector3.Up),
                Projection = Matrix.CreatePerspectiveFieldOfView(
                    MathHelper.PiOver4,
                    GraphicsDevice.Viewport.AspectRatio,
                    0.1f,
                    100f)
            };

            _rasterizerState = new RasterizerState { CullMode = CullMode.None };
            _triangleVertices = BuildIsoscelesTriangle();
            _ingotFrustrum = new IngotFrustrum(new IngotFrustrumMeshBuilder(RawData.Basic_Ingot_Frustrum), TopColor, SideColor, OtherColor);
            _ingotFrustrum.Configure(GraphicsDevice);

        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();

            if (keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space))
                _edgesOnly = !_edgesOnly;



                _previousKeyboard = keyboard;
            _ingotFrustrum.Yaw += _ingotFrustrum.YawSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            _ingotFrustrum.Pitch += _ingotFrustrum.PitchSpeed * (float)gameTime.ElapsedGameTime.TotalSeconds;
            _ingotFrustrum.Update(gameTime);

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            GraphicsDevice.RasterizerState = _rasterizerState;

            DrawIsoscelesTriangle();
            _ingotFrustrum.Draw(gameTime, GraphicsDevice, _basicEffect, _edgesOnly);

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
    }
}
