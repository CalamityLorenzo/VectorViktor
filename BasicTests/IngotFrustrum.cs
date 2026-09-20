using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace BasicTests
{
    internal class IngotFrustrum
    {
        // IMagine we imported this from a 3D model, but we can also just define it manually. The ingot is a frustum (truncated pyramid) shape.
        // 0-3: bottom (larger) rectangle. 4-7: top (smaller) rectangle, inset on both
        // axes so the sides slope inward.
        private static readonly Vector3[] GoldBarCorners =
        {
            new Vector3(-0.6f, -0.3f, -0.3f),
            new Vector3(0.6f, -0.3f, -0.3f),
            new Vector3(0.6f, -0.3f, 0.3f),
            new Vector3(-0.6f, -0.3f, 0.3f),
            new Vector3(-0.4f, 0.3f, -0.2f),
            new Vector3(0.4f, 0.3f, -0.2f),
            new Vector3(0.4f, 0.3f, 0.2f),
            new Vector3(-0.4f, 0.3f, 0.2f),
        };
        private Color _topColor;
        private Color _sideColor;
        private Color _otherColor;
        private VertexBuffer _solidBuffer;
        private VertexBuffer _edgeBuffer;
        private BlendState _depthOnlyBlend;
        private RasterizerState _occluderRasterizer;

        // The state is the source of truth; the world matrix is rebuilt from it, never accumulated.
        public Vector3 Position { get; set; } = new Vector3(0.7f, 0f, 0f);
        public float Pitch { get; set; } = MathHelper.ToRadians(20f);   // fixed jaunty tilt
        public float Yaw { get; set; } = MathHelper.ToRadians(35f);
        public float YawSpeed { get; set; } = MathHelper.ToRadians(45f); // radians per second

        public Matrix World => Matrix.CreateRotationX(Pitch)
            * Matrix.CreateRotationY(Yaw)
            * Matrix.CreateTranslation(Position);

        public IngotFrustrum(Color TopColor, Color SideColor)
        {
            _topColor = TopColor;
            _sideColor = SideColor;
            _otherColor = Color.Lerp(TopColor, SideColor, 0.5f); // for the diagonal split lines
        }

        /// <summary>
        /// This should only be called once.
        /// </summary>
        /// <param name="device"></param>
        public void Configure(GraphicsDevice device)
        {
            var _edgeVertices = BuildEdges(_topColor, _sideColor, _otherColor);
            var _solidVertices = BuildSolid(_topColor, _sideColor, _otherColor);
            _solidBuffer = new VertexBuffer(device, typeof(VertexPositionColor), _solidVertices.Length, BufferUsage.WriteOnly);
            _solidBuffer.SetData(_solidVertices);
            _edgeBuffer = new VertexBuffer(device, typeof(VertexPositionColor), _edgeVertices.Length, BufferUsage.WriteOnly);
            _edgeBuffer.SetData(_edgeVertices);
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
                DrawBuffer(graphicsDevice, basicEffect, _solidBuffer, PrimitiveType.TriangleList, 12);
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
            DrawBuffer(graphicsDevice, basicEffect, _solidBuffer, PrimitiveType.TriangleList, 12);
           
            graphicsDevice.BlendState = previousBlend;
            graphicsDevice.RasterizerState = previousRasterizer;
            // Pass 2: the lines, depth-tested against those invisible faces, so any edge
            // behind the bar is discarded.
            // 12 == 24 vertices / 2 vertices per line
            DrawBuffer(graphicsDevice, basicEffect, _edgeBuffer, PrimitiveType.LineList, 12);

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

        // Edges-only alternative to FillMode.WireFrame: just the ingot's 12 real
        // edges as a LineList, so the internal quad-split diagonals never appear.
        private static VertexPositionColor[] BuildEdges(Color TopColor, Color SideColor, Color OtherColor)
        {
            var vertices = new VertexPositionColor[24];
            var index = 0;
            for (var i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                AddLine(vertices, GoldBarCorners[4 + i], GoldBarCorners[4 + next], TopColor, ref index);  // top loop
                AddLine(vertices, GoldBarCorners[i], GoldBarCorners[next], SideColor, ref index);         // bottom loop
                AddLine(vertices, GoldBarCorners[i], GoldBarCorners[4 + i], SideColor, ref index);        // vertical edge
            }

            return vertices;
        }


        private static VertexPositionColor[] BuildSolid(Color TopColor, Color SideColor, Color OtherColor)
        {
            var b0 = GoldBarCorners[0];
            var b1 = GoldBarCorners[1];
            var b2 = GoldBarCorners[2];
            var b3 = GoldBarCorners[3];
            var t0 = GoldBarCorners[4];
            var t1 = GoldBarCorners[5];
            var t2 = GoldBarCorners[6];
            var t3 = GoldBarCorners[7];

            var vertices = new VertexPositionColor[36];
            var index = 0;
            AddQuad(vertices, t0, t1, t2, t3, TopColor, ref index);   // top
            AddQuad(vertices, b1, b0, b3, b2, OtherColor, ref index);  // bottom
            AddQuad(vertices, b0, b1, t1, t0, SideColor, ref index);  // back
            AddQuad(vertices, b2, b3, t3, t2, SideColor, ref index);  // front
            AddQuad(vertices, b3, b0, t0, t3, SideColor, ref index);  // left
            AddQuad(vertices, b1, b2, t2, t1, SideColor, ref index);  // right
            return vertices;
        }

        private static void AddLine(VertexPositionColor[] vertices, Vector3 a, Vector3 b, Color color, ref int index)
        {
            vertices[index++] = new VertexPositionColor(a, color);
            vertices[index++] = new VertexPositionColor(b, color);
        }


        // Splits quad a-b-c-d (corners given in order round the perimeter) into two
        // triangles, all vertices tinted the same flat colour.
        private static void AddQuad(VertexPositionColor[] vertices, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, ref int index)
        {
            vertices[index++] = new VertexPositionColor(a, color);
            vertices[index++] = new VertexPositionColor(b, color);
            vertices[index++] = new VertexPositionColor(c, color);

            vertices[index++] = new VertexPositionColor(a, color);
            vertices[index++] = new VertexPositionColor(c, color);
            vertices[index++] = new VertexPositionColor(d, color);
        }
    }
}
