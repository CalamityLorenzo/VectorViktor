using BasicTests.Meshes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BasicTests
{
    internal class IngotFrustrum
    {

        private BlendState _depthOnlyBlend;
        private RasterizerState _occluderRasterizer;
        private MeshData _meshData;
        private readonly IngotFrustrumMeshBuilder _builder;
        private readonly Color _topColor;
        private readonly Color _sideColor;
        private readonly Color _otherColor;

        // The state is the source of truth; the world matrix is rebuilt from it, never accumulated.
        public Vector3 Position { get; set; } = new Vector3(0.7f, 0f, 0f);
        public float Pitch { get; set; } = MathHelper.ToRadians(20f);   // fixed jaunty tilt
        public float Yaw { get; set; } = MathHelper.ToRadians(35f);
        public float YawSpeed { get; set; } = MathHelper.ToRadians(45f); // radians per second
        public float PitchSpeed { get; set; } = MathHelper.ToRadians(37f); // radians per second

        public Matrix World => Matrix.CreateRotationX(Pitch)
            * Matrix.CreateRotationY(Yaw)
            * Matrix.CreateTranslation(Position);

        public IngotFrustrum(IngotFrustrumMeshBuilder builder, Color TopColor, Color SideColor, Color OtherColor)
        {
            _builder = builder;
            _topColor = TopColor;
            _sideColor = SideColor;
            _otherColor = OtherColor;
        }

        /// <summary>
        /// This should only be called once.
        /// </summary>
        /// <param name="device"></param>
        public void Configure(GraphicsDevice device)
        {
            _meshData = _builder.Build(device, _topColor,_sideColor, _otherColor    );

            // used to occulude edges we should not be able to see.
            _depthOnlyBlend = new BlendState { ColorWriteChannels = ColorWriteChannels.None };
            _occluderRasterizer = new RasterizerState
            {
                CullMode = CullMode.None,
                DepthBias = 0.0001f, // Nudges the faces slightly away so they don't z-fight.
                SlopeScaleDepthBias = 1f,
            };
        }

        public void Update(GameTime gameTime)
        {
            
        }

        public void Draw(GameTime gameTime, GraphicsDevice graphicsDevice, BasicEffect basicEffect, bool edgesOnly)
        {
            var _world = basicEffect.World;
            basicEffect.World = World;

            /// Solid only needs 1 pass.
            if (!edgesOnly)
            {
                // 12 == 36 vertices / 3 vertices per triangle
                DrawBuffer(graphicsDevice, basicEffect, _meshData.SolidBuffer, PrimitiveType.TriangleList, 12);
                return;
            }

            // Edges-only mode: draw the solid faces into the depth buffer first, then the edges on top of them.

            // Hidden-line removal. Pass 1: draw the solid faces into the depth buffer only
            // (no colour written), nudged slightly away from the camera so the edges that
            // sit exactly on the face boundaries don't z-fight with them.
            var previousBlend = graphicsDevice.BlendState;
            var previousRasterizer = graphicsDevice.RasterizerState;

            graphicsDevice.BlendState = _depthOnlyBlend;
            graphicsDevice.RasterizerState = _occluderRasterizer;
            DrawBuffer(graphicsDevice, basicEffect, _meshData.SolidBuffer, PrimitiveType.TriangleList, 12);
           
            graphicsDevice.BlendState = previousBlend;
            graphicsDevice.RasterizerState = previousRasterizer;
            // Pass 2: the lines, depth-tested against those invisible faces, so any edge
            // behind the bar is discarded.
            // 12 == 24 vertices / 2 vertices per line
            DrawBuffer(graphicsDevice, basicEffect, _meshData.EdgeBuffer, PrimitiveType.LineList, 12);

            basicEffect.World = _world;
        }

        private static void DrawBuffer(GraphicsDevice graphicsDevice, BasicEffect basicEffect, VertexBuffer buffer, PrimitiveType type, int primitiveCount)
        {
            graphicsDevice.SetVertexBuffer(buffer);
            foreach (var pass in basicEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.DrawPrimitives(type, 0, primitiveCount);
            }
        }


    }
}
