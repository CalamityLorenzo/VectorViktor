using BasicTests.Meshes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace BasicTests
{
    internal class IngotFrustrum
    {

        private static readonly BlendState _depthOnlyBlend;
        private static readonly RasterizerState _occluderRasterizer;
        private MeshData _meshData;
        private readonly Color _topColor;
        private readonly Color _sideColor;
        private readonly Color _otherColor;

        public bool EdgesOnly { get; set; } = false;
        public bool NoEdgeColor { get; set; } = false;

        // The state is the source of truth; the world matrix is rebuilt from it, never accumulated.
        public Vector3 Position { get; set; } = new Vector3(0.7f, 0f, 0f);
        public float Pitch { get; set; } = MathHelper.ToRadians(20f);   // fixed jaunty tilt
        public float Yaw { get; set; } = MathHelper.ToRadians(35f);
        public float YawSpeed { get; set; } = MathHelper.ToRadians(45f); // radians per second
        public float PitchSpeed { get; set; } = MathHelper.ToRadians(37f); // radians per second

        public float Scale { get; set; } = 1f;

        public Matrix World => Matrix.CreateScale(Scale)
            * Matrix.CreateRotationX(Pitch)
            * Matrix.CreateRotationY(Yaw)
            * Matrix.CreateTranslation(Position);

        public IngotFrustrum(MeshData meshData, Color TopColor, Color SideColor, Color OtherColor)
        {
            _meshData = meshData ?? throw new ArgumentNullException(nameof(meshData));
            _topColor = TopColor;
            _sideColor = SideColor;
            _otherColor = OtherColor;
        }

        // A static constructor is used to initialize static members of the class. It is called automatically before the first instance is created or any static members are referenced.
        static IngotFrustrum()
        {
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

        public void Draw(GameTime gameTime, GraphicsDevice graphicsDevice, BasicEffect basicEffect)
        {
            var world = basicEffect.World;
            var diffuse = basicEffect.DiffuseColor;
            var vertexColor = basicEffect.VertexColorEnabled;
            basicEffect.World = World;
            // The buffers are position-only; colour comes from DiffuseColor per draw range.
            basicEffect.VertexColorEnabled = false;
            try
            {
                /// Solid only needs 1 pass.
                if (!EdgesOnly)
                {
                    DrawSolid(graphicsDevice, basicEffect);
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
                DrawBuffer(graphicsDevice, basicEffect, _meshData.Solids, PrimitiveType.TriangleList, _meshData.Solids.VertexCount / 3);

                graphicsDevice.BlendState = previousBlend;
                graphicsDevice.RasterizerState = previousRasterizer;
                // Pass 2: the lines, depth-tested against those invisible faces, so any edge
                // behind the bar is discarded.
                DrawEdges(graphicsDevice, basicEffect);
            }
            finally
            {
                basicEffect.World = world;
                basicEffect.DiffuseColor = diffuse;
                basicEffect.VertexColorEnabled = vertexColor;
            }
        }

        private void DrawSolid(GraphicsDevice gd, BasicEffect fx)
        {
            gd.SetVertexBuffer(_meshData.Solids);
            DrawRange(gd, fx, _topColor, PrimitiveType.TriangleList, MeshData.SolidTopStart, MeshData.SolidTopPrimitives);
            DrawRange(gd, fx, _otherColor, PrimitiveType.TriangleList, MeshData.SolidBottomStart, MeshData.SolidBottomPrimitives);
            DrawRange(gd, fx, _sideColor, PrimitiveType.TriangleList, MeshData.SolidSidesStart, MeshData.SolidSidesPrimitives);
        }

        private void DrawEdges(GraphicsDevice gd, BasicEffect fx)
        {
            gd.SetVertexBuffer(_meshData.Edges);
            DrawRange(gd, fx, NoEdgeColor? Color.White: _topColor, PrimitiveType.LineList, MeshData.EdgeTopStart, MeshData.EdgeTopPrimitives);
            DrawRange(gd, fx, NoEdgeColor ? Color.White : _sideColor, PrimitiveType.LineList, MeshData.EdgeSidesStart, MeshData.EdgeSidesPrimitives);
        }

        private static void DrawRange(GraphicsDevice gd, BasicEffect fx, Color c,
                                      PrimitiveType type, int startVertex, int primitiveCount)
        {
            fx.DiffuseColor = c.ToVector3();
            foreach (var pass in fx.CurrentTechnique.Passes)
            {
                pass.Apply();                       // must re-apply so the new colour uploads
                gd.DrawPrimitives(type, startVertex, primitiveCount);
            }
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
