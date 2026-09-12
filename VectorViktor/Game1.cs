using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace VectorViktor
{
    public class Game1 : Game
    {
        private GraphicsDeviceManager _graphics;
        private BasicEffect _effect;
        private SpriteBatch _spriteBatch;
        private RenderTarget2D _renderTarget;

        // Internal low-res buffer, point-upscaled for chunky 8/16-bit pixels
        private const int VirtualWidth = 400;
        private const int VirtualHeight = 180;

        // Grid layout
        private const int GridSquares = 14;      // squares per side
        private const float CellSize = 1.0f;
        private const float GridExtent = GridSquares * CellSize * 0.5f;

        private VertexPositionColor[] _gridLines;

        // World rotation
        private float _yaw;
        private float _pitch = 0.45f;
        private bool _autoSpin = true;
        private bool _colorsOn = true;
        private KeyboardState _prevKeys;

        // Camera zoom
        private const float DefaultCameraDistance = 16f;
        private const float MinCameraDistance = 6f;
        private const float MaxCameraDistance = 30f;
        private const float ZoomSpeed = 8f;
        private float _cameraDistance = DefaultCameraDistance;

        // Windowed size to restore when leaving fullscreen
        private const int WindowedWidth = 1280;
        private const int WindowedHeight = 720;

        // Animated bars
        private const int BarCount = 12;
        private const float BarMaxHeight = 3.0f;
        private const float BarInset = 0f;       // bars fill their square edge-to-edge
        private readonly Bar[] _bars = new Bar[BarCount];
        private readonly Random _rng = new Random();

        // Vector bird: slow orbit above the grid, wings ripple with a traveling wave
        private float _birdTime;
        private const float BirdOrbitRadius = 5.0f;
        private const float BirdHeight = 3.2f;
        private const float BirdOrbitSpeed = 0.35f;   // radians/sec around the grid
        private const int WingSegments = 5;
        private const float WingSpan = 1.4f;
        private const float WingSweep = 0.5f;         // tip curves backward
        private const float WingFlapAmp = 0.5f;
        private const float WingFlapFreq = 0.35f;     // Hz — slow flap
        private const float WingWaveCount = 1.1f;      // wave cycles across the span -> undulation
        private const float BirdScale = 1.3f;

        // Car: drives along the grid lines, turning at intersections
        private Car _car;
        private const float CarSpeed = 1.1f;      // grid cells per second
        private const float CarLength = 0.8f;
        private const float CarWidth = 0.42f;
        private const float WheelLength = 0.22f;
        private const float WheelTrack = 0.1f;    // wheel thickness across the car's width
        private const float WheelOutset = 0.08f;  // how far the wheels poke out past the body sides
        private static readonly int[] DirX4 = { 1, -1, 0, 0 };
        private static readonly int[] DirZ4 = { 0, 0, 1, -1 };
        private static readonly Color CarColor = Color.Yellow;
        private static readonly Color WheelColor = new Color(40, 40, 40);

        private static readonly Color BackgroundColor = Color.Black;

        private static readonly Color[] Palette =
        {
            Color.Cyan, Color.Magenta, Color.Yellow,
            Color.Lime, Color.Red, Color.Orange, Color.DeepSkyBlue
        };

        private struct Bar
        {
            public int CellX, CellZ;   // 0..GridSquares-1
            public Color Color;
            public float Time;         // seconds elapsed in this cycle
            public float Duration;     // full grow+shrink cycle length
        }

        private struct Car
        {
            public int GridX, GridZ;   // current intersection, 0..GridSquares
            public int DirX, DirZ;     // travel direction: one of (±1,0) or (0,±1)
            public float Progress;     // 0..1 progress toward the next intersection
        }

        public Game1()
        {
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = WindowedWidth,
                PreferredBackBufferHeight = WindowedHeight,
                PreferMultiSampling = false,
                HardwareModeSwitch = false   // borderless fullscreen, no display mode change
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Window.Title = "Vector Viktor";
        }

        protected override void Initialize()
        {
            BuildGridLines();
            for (int i = 0; i < BarCount; i++)
                SpawnBar(ref _bars[i], randomStartPhase: true);

            _car.GridX = GridSquares / 2;
            _car.GridZ = 0;
            _car.DirX = 0;
            _car.DirZ = 1;
            _car.Progress = 0f;

            base.Initialize();
        }

        protected override void LoadContent()
        {
            _effect = new BasicEffect(GraphicsDevice)
            {
                VertexColorEnabled = true,
                LightingEnabled = false
            };
            _spriteBatch = new SpriteBatch(GraphicsDevice);
            _renderTarget = new RenderTarget2D(GraphicsDevice, VirtualWidth, VirtualHeight,
                false, SurfaceFormat.Color, DepthFormat.Depth24);
        }

        private void BuildGridLines()
        {
            int lineCount = (GridSquares + 1) * 2;
            _gridLines = new VertexPositionColor[lineCount * 2];
            int v = 0;
            for (int i = 0; i <= GridSquares; i++)
            {
                float p = -GridExtent + i * CellSize;
                // Lines running along Z
                _gridLines[v++] = new VertexPositionColor(new Vector3(p, 0, -GridExtent), Color.White);
                _gridLines[v++] = new VertexPositionColor(new Vector3(p, 0, GridExtent), Color.White);
                // Lines running along X
                _gridLines[v++] = new VertexPositionColor(new Vector3(-GridExtent, 0, p), Color.White);
                _gridLines[v++] = new VertexPositionColor(new Vector3(GridExtent, 0, p), Color.White);
            }
        }

        private void SpawnBar(ref Bar bar, bool randomStartPhase = false)
        {
            bar.CellX = _rng.Next(GridSquares);
            bar.CellZ = _rng.Next(GridSquares);
            bar.Color = Palette[_rng.Next(Palette.Length)];
            bar.Duration = 2.0f + (float)_rng.NextDouble() * 3.0f;
            bar.Time = randomStartPhase ? (float)_rng.NextDouble() * bar.Duration : 0f;
        }

        protected override void Update(GameTime gameTime)
        {
            var keys = Keyboard.GetState();
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keys.IsKeyDown(Keys.Escape))
                Exit();

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Rotation controls: arrows rotate, Shift+arrows (Up/Down) zoom, Space toggles auto-spin, R resets
            const float rotSpeed = 1.6f;
            bool shiftHeld = keys.IsKeyDown(Keys.LeftShift) || keys.IsKeyDown(Keys.RightShift);
            if (keys.IsKeyDown(Keys.Left)) _yaw -= rotSpeed * dt;
            if (keys.IsKeyDown(Keys.Right)) _yaw += rotSpeed * dt;
            if (shiftHeld)
            {
                if (keys.IsKeyDown(Keys.Up)) _cameraDistance -= ZoomSpeed * dt;
                if (keys.IsKeyDown(Keys.Down)) _cameraDistance += ZoomSpeed * dt;
                _cameraDistance = MathHelper.Clamp(_cameraDistance, MinCameraDistance, MaxCameraDistance);
            }
            else
            {
                if (keys.IsKeyDown(Keys.Up)) _pitch -= rotSpeed * dt;
                if (keys.IsKeyDown(Keys.Down)) _pitch += rotSpeed * dt;
            }
            if (keys.IsKeyDown(Keys.Space) && _prevKeys.IsKeyUp(Keys.Space)) _autoSpin = !_autoSpin;
            if (keys.IsKeyDown(Keys.C) && _prevKeys.IsKeyUp(Keys.C)) _colorsOn = !_colorsOn;
            if (keys.IsKeyDown(Keys.R)) { _yaw = 0f; _pitch = 0.45f; _cameraDistance = DefaultCameraDistance; }

            // F11 or Alt+Enter toggles fullscreen
            bool altEnter = keys.IsKeyDown(Keys.Enter) && _prevKeys.IsKeyUp(Keys.Enter)
                && (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
            if ((keys.IsKeyDown(Keys.F11) && _prevKeys.IsKeyUp(Keys.F11)) || altEnter)
                ToggleFullscreen();

            _prevKeys = keys;

            if (_autoSpin)
                _yaw += 0.25f * dt;

            _pitch = MathHelper.Clamp(_pitch, -MathHelper.PiOver2, MathHelper.PiOver2);

            // Advance bar animations; respawn each bar elsewhere when its cycle ends
            for (int i = 0; i < BarCount; i++)
            {
                _bars[i].Time += dt;
                if (_bars[i].Time >= _bars[i].Duration)
                    SpawnBar(ref _bars[i]);
            }

            _birdTime += dt;
            UpdateCar(dt);

            base.Update(gameTime);
        }

        private void UpdateCar(float dt)
        {
            _car.Progress += CarSpeed * dt;
            while (_car.Progress >= 1f)
            {
                _car.Progress -= 1f;
                _car.GridX += _car.DirX;
                _car.GridZ += _car.DirZ;
                PickNextCarDirection();
            }
        }

        private void PickNextCarDirection()
        {
            Span<int> candX = stackalloc int[4];
            Span<int> candZ = stackalloc int[4];
            int count = 0;
            bool straightValid = false;
            for (int i = 0; i < 4; i++)
            {
                int dx = DirX4[i], dz = DirZ4[i];
                if (dx == -_car.DirX && dz == -_car.DirZ)
                    continue; // no U-turns
                int nx = _car.GridX + dx, nz = _car.GridZ + dz;
                if (nx < 0 || nx > GridSquares || nz < 0 || nz > GridSquares)
                    continue;
                candX[count] = dx;
                candZ[count] = dz;
                if (dx == _car.DirX && dz == _car.DirZ)
                    straightValid = true;
                count++;
            }

            if (count == 0)
            {
                // Dead end — shouldn't happen on a rectangular grid, but reverse as a fallback
                _car.DirX = -_car.DirX;
                _car.DirZ = -_car.DirZ;
                return;
            }

            if (straightValid && _rng.NextDouble() < 0.65)
                return; // keep going straight most of the time

            int pick = _rng.Next(count);
            _car.DirX = candX[pick];
            _car.DirZ = candZ[pick];
        }

        private void ToggleFullscreen()
        {
            if (_graphics.IsFullScreen)
            {
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = WindowedWidth;
                _graphics.PreferredBackBufferHeight = WindowedHeight;
            }
            else
            {
                var display = GraphicsDevice.Adapter.CurrentDisplayMode;
                _graphics.IsFullScreen = true;
                _graphics.PreferredBackBufferWidth = display.Width;
                _graphics.PreferredBackBufferHeight = display.Height;
            }
            _graphics.ApplyChanges();
        }

        protected override void Draw(GameTime gameTime)
        {
            // Render the 3D scene into the tiny internal buffer
            GraphicsDevice.SetRenderTarget(_renderTarget);
            GraphicsDevice.Clear(BackgroundColor);
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            GraphicsDevice.BlendState = BlendState.Opaque;

            _effect.World = Matrix.CreateRotationY(_yaw) * Matrix.CreateRotationX(_pitch);
            _effect.View = Matrix.CreateLookAt(new Vector3(0, 0, _cameraDistance), Vector3.Zero, Vector3.Up);
            _effect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.PiOver4, (float)VirtualWidth / VirtualHeight, 0.1f, 100f);

            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();

                // Grid
                GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, _gridLines, 0, _gridLines.Length / 2);

                // Bars
                for (int i = 0; i < BarCount; i++)
                    DrawBar(in _bars[i]);

                // Bird
                DrawBird();

                // Car
                DrawCar();
            }

            // Point-sample upscale to the window: fat pixels, hard stair-stepped edges
            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(BackgroundColor);

            var vp = GraphicsDevice.Viewport;
            int scale = Math.Max(1, Math.Min(vp.Width / VirtualWidth, vp.Height / VirtualHeight));
            int w = VirtualWidth * scale, h = VirtualHeight * scale;
            var dest = new Rectangle((vp.Width - w) / 2, (vp.Height - h) / 2, w, h);

            _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone);
            _spriteBatch.Draw(_renderTarget, dest, Color.White);
            _spriteBatch.End();

            base.Draw(gameTime);
        }

        private void DrawBar(in Bar bar)
        {
            // Height follows a half-sine: grows from 0 to max, shrinks back to 0
            float phase = bar.Time / bar.Duration;
            float height = BarMaxHeight * (float)Math.Sin(phase * Math.PI);
            if (height < 0.01f)
                return;

            float x0 = -GridExtent + bar.CellX * CellSize + BarInset;
            float x1 = x0 + CellSize - 2 * BarInset;
            float z0 = -GridExtent + bar.CellZ * CellSize + BarInset;
            float z1 = z0 + CellSize - 2 * BarInset;
            const float y0 = 0.001f; // lift base slightly to avoid z-fighting with grid lines
            float y1 = height;

            // 8 corners of the box
            var a = new Vector3(x0, y0, z0);
            var b = new Vector3(x1, y0, z0);
            var c = new Vector3(x1, y0, z1);
            var d = new Vector3(x0, y0, z1);
            var e = new Vector3(x0, y1, z0);
            var f = new Vector3(x1, y1, z0);
            var g = new Vector3(x1, y1, z1);
            var h = new Vector3(x0, y1, z1);

            // Flat-shaded faces: sides in the bar colour, top brighter, for that Starglider solid look.
            // With colours off the faces are filled with the background colour instead — the bar
            // still occludes whatever is behind it, but reads as a hollow wireframe outline.
            Color side, sideDim, top;
            if (_colorsOn)
            {
                side = bar.Color;
                sideDim = new Color((int)(side.R * 0.55f), (int)(side.G * 0.55f), (int)(side.B * 0.55f));
                top = Color.Lerp(side, Color.White, 0.35f);
            }
            else
            {
                side = sideDim = top = BackgroundColor;
            }

            var tris = new VertexPositionColor[30];
            int v = 0;
            void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color col)
            {
                tris[v++] = new VertexPositionColor(p0, col);
                tris[v++] = new VertexPositionColor(p1, col);
                tris[v++] = new VertexPositionColor(p2, col);
                tris[v++] = new VertexPositionColor(p0, col);
                tris[v++] = new VertexPositionColor(p2, col);
                tris[v++] = new VertexPositionColor(p3, col);
            }
            Quad(a, b, f, e, side);     // front  (-Z)
            Quad(c, d, h, g, side);     // back   (+Z)
            Quad(b, c, g, f, sideDim);  // right  (+X)
            Quad(d, a, e, h, sideDim);  // left   (-X)
            Quad(e, f, g, h, top);      // top

            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, tris, 0, 10);

            // White wireframe edges over the solid box — the vector-graphics signature
            var edges = new VertexPositionColor[24];
            v = 0;
            void Edge(Vector3 p0, Vector3 p1)
            {
                edges[v++] = new VertexPositionColor(p0, Color.White);
                edges[v++] = new VertexPositionColor(p1, Color.White);
            }
            Edge(a, b); Edge(b, c); Edge(c, d); Edge(d, a); // base
            Edge(e, f); Edge(f, g); Edge(g, h); Edge(h, e); // top rim
            Edge(a, e); Edge(b, f); Edge(c, g); Edge(d, h); // verticals

            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, edges, 0, 12);
        }

        private void DrawBird()
        {
            // Slow orbit above the grid; the bird always faces along its flight path
            float angle = _birdTime * BirdOrbitSpeed;
            Vector3 center = new Vector3(
                (float)Math.Cos(angle) * BirdOrbitRadius,
                BirdHeight,
                (float)Math.Sin(angle) * BirdOrbitRadius);

            Vector3 forward = new Vector3(-(float)Math.Sin(angle), 0f, (float)Math.Cos(angle));
            Vector3 up = Vector3.Up;
            Vector3 right = Vector3.Cross(forward, up);

            // Body: a small flattened diamond — nose, tail, and two flanks
            const float bodyLen = 0.5f * BirdScale;
            const float bodyWidth = 0.14f * BirdScale;
            Vector3 nose = center + forward * (bodyLen * 0.6f);
            Vector3 tail = center - forward * (bodyLen * 0.4f);
            Vector3 flankL = center - right * bodyWidth;
            Vector3 flankR = center + right * bodyWidth;

            Color bodyColor = Color.White;
            var bodyTris = new VertexPositionColor[6];
            bodyTris[0] = new VertexPositionColor(nose, bodyColor);
            bodyTris[1] = new VertexPositionColor(flankR, bodyColor);
            bodyTris[2] = new VertexPositionColor(tail, bodyColor);
            bodyTris[3] = new VertexPositionColor(nose, bodyColor);
            bodyTris[4] = new VertexPositionColor(tail, bodyColor);
            bodyTris[5] = new VertexPositionColor(flankL, bodyColor);
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, bodyTris, 0, 2);

            var bodyEdges = new[]
            {
                new VertexPositionColor(nose, Color.White), new VertexPositionColor(flankR, Color.White),
                new VertexPositionColor(flankR, Color.White), new VertexPositionColor(tail, Color.White),
                new VertexPositionColor(tail, Color.White), new VertexPositionColor(flankL, Color.White),
                new VertexPositionColor(flankL, Color.White), new VertexPositionColor(nose, Color.White),
            };
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, bodyEdges, 0, 4);

            Color wingColor = _colorsOn ? new Color(140, 210, 230) : BackgroundColor;
            DrawWing(center, forward, right, up, +1, wingColor);
            DrawWing(center, forward, right, up, -1, wingColor);
        }

        // Wing built from several segments so a traveling sine wave ripples from root to tip,
        // giving a slow, undulating flap rather than a rigid hinge
        private void DrawWing(Vector3 center, Vector3 forward, Vector3 right, Vector3 up, int side, Color color)
        {
            var pts = new Vector3[WingSegments + 1];
            pts[0] = center;
            for (int i = 1; i <= WingSegments; i++)
            {
                float t = (float)i / WingSegments;               // 0..1 root to tip
                float spanDist = t * WingSpan * BirdScale;
                float sweepBack = t * t * WingSweep * BirdScale;  // curves backward toward the tip
                float wave = (float)Math.Sin(_birdTime * WingFlapFreq * MathHelper.TwoPi
                    - t * WingWaveCount * MathHelper.TwoPi);
                float flap = wave * WingFlapAmp * BirdScale * t;  // amplitude grows toward the tip
                pts[i] = center + right * (spanDist * side) - forward * sweepBack + up * flap;
            }

            var tris = new VertexPositionColor[WingSegments * 3];
            int v = 0;
            for (int i = 0; i < WingSegments; i++)
            {
                tris[v++] = new VertexPositionColor(center, color);
                tris[v++] = new VertexPositionColor(pts[i], color);
                tris[v++] = new VertexPositionColor(pts[i + 1], color);
            }
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, tris, 0, WingSegments);

            var edges = new VertexPositionColor[WingSegments * 2];
            v = 0;
            for (int i = 0; i < WingSegments; i++)
            {
                edges[v++] = new VertexPositionColor(pts[i], Color.White);
                edges[v++] = new VertexPositionColor(pts[i + 1], Color.White);
            }
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, edges, 0, WingSegments);
        }

        private void DrawCar()
        {
            // Interpolate between the current and next grid intersection
            float x0 = -GridExtent + _car.GridX * CellSize;
            float z0 = -GridExtent + _car.GridZ * CellSize;
            float x1 = -GridExtent + (_car.GridX + _car.DirX) * CellSize;
            float z1 = -GridExtent + (_car.GridZ + _car.DirZ) * CellSize;
            Vector3 pos = Vector3.Lerp(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), _car.Progress);
            pos.Y = 0.015f; // lift slightly above the grid to avoid z-fighting

            Vector3 forward = new Vector3(_car.DirX, 0f, _car.DirZ); // already unit length
            Vector3 right = Vector3.Cross(forward, Vector3.Up);

            Color bodyColor = _colorsOn ? CarColor : BackgroundColor;
            Color wheelColor = _colorsOn ? WheelColor : BackgroundColor;

            // Body: a flat rectangle lying on the grid plane, like a painted vector decal
            Vector3 bf = forward * (CarLength * 0.5f);
            Vector3 br = right * (CarWidth * 0.5f);
            DrawFlatQuad(pos + bf + br, pos + bf - br, pos - bf - br, pos - bf + br, bodyColor);

            // Wheels: small rectangles poking out past the body's four corners
            Vector3 frontAxle = pos + forward * (CarLength * 0.32f);
            Vector3 rearAxle = pos - forward * (CarLength * 0.32f);
            DrawWheel(frontAxle, forward, right, +1, wheelColor);
            DrawWheel(frontAxle, forward, right, -1, wheelColor);
            DrawWheel(rearAxle, forward, right, +1, wheelColor);
            DrawWheel(rearAxle, forward, right, -1, wheelColor);
        }

        private void DrawWheel(Vector3 axleCenter, Vector3 forward, Vector3 right, int side, Color color)
        {
            float outerEdge = CarWidth * 0.5f + WheelOutset;
            Vector3 wheelCenter = axleCenter + right * (side * (outerEdge - WheelTrack * 0.5f));
            Vector3 wf = forward * (WheelLength * 0.5f);
            Vector3 wr = right * (WheelTrack * 0.5f);
            DrawFlatQuad(wheelCenter + wf + wr, wheelCenter + wf - wr, wheelCenter - wf - wr, wheelCenter - wf + wr, color);
        }

        // Flat filled quad plus a white wireframe outline — the vector-graphics signature
        private void DrawFlatQuad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Color fill)
        {
            var tris = new[]
            {
                new VertexPositionColor(p0, fill), new VertexPositionColor(p1, fill), new VertexPositionColor(p2, fill),
                new VertexPositionColor(p0, fill), new VertexPositionColor(p2, fill), new VertexPositionColor(p3, fill),
            };
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, tris, 0, 2);

            var edges = new[]
            {
                new VertexPositionColor(p0, Color.White), new VertexPositionColor(p1, Color.White),
                new VertexPositionColor(p1, Color.White), new VertexPositionColor(p2, Color.White),
                new VertexPositionColor(p2, Color.White), new VertexPositionColor(p3, Color.White),
                new VertexPositionColor(p3, Color.White), new VertexPositionColor(p0, Color.White),
            };
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, edges, 0, 4);
        }
    }
}
