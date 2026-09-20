using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using VectorViktor.Models;

namespace VectorViktor
{
    public class Game1 : Game
    {

        private float _modelRotation = 0f;  // Add this with your other fields
        float speed = 0f;
        private GraphicsDeviceManager _graphics;
        private BasicEffect _effect;
        private SpriteBatch _spriteBatch;
        private RenderTarget2D _renderTarget;

        // Internal low-res buffer, point-upscaled for chunky 8/16-bit pixels
        private const int VirtualWidth = 640; //400;
        private const int VirtualHeight = 256; //180;

        // Grid layout
        private const int GridSquares = 28;      // squares per side
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

        // Camera view: cycle between the free orbit camera and chase cams riding behind the car/bird
        private enum CameraMode { Orbit, ChaseCar, ChaseBird }
        private CameraMode _cameraMode = CameraMode.Orbit;
        private const float CarChaseDistance = 1.4f;
        private const float CarChaseHeight = 0.5f;
        private const float CarChaseLookAhead = 0.6f;
        private const float CarChaseTargetHeight = 0.15f;
        private const float BirdChaseDistance = 1.6f;
        private const float BirdChaseHeight = 0.4f;
        private const float BirdChaseLookAhead = 1.0f;

        // Chase camera rotation offsets
        private float _chaseYaw = 0f;
        private float _chasePitch = 0f;

        // Windowed size to restore when leaving fullscreen
        private const int WindowedWidth = 1280;
        private const int WindowedHeight = 720;

        // Animated bars
        private const int BarCount = 12;
        private const float BarMaxHeight = 3.0f;
        private const float BarInset = 0f;       // bars fill their square edge-to-edge
        private readonly Bar[] _bars = new Bar[BarCount];
        private readonly Random _rng = new Random();

        // Houses: static structures placed at random grid locations
        private const int HouseCount = 5;
        private readonly House[] _houses = new House[HouseCount];
        private HouseGeometry[] _houseGeometryColorsOn;
        private HouseGeometry[] _houseGeometryColorsOff;

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

        // Bird flight variation: periodic dives toward the grid, and random cruising-speed changes
        private const float BirdDiveIntervalMin = 4f;
        private const float BirdDiveIntervalMax = 9f;
        private const float BirdDiveDuration = 1.5f;
        private const float BirdDiveLowHeight = 0.4f;   // altitude at the bottom of the dive
        private const float BirdHeightVariance = 0.10f; // resting height settles within ±10% of BirdHeight
        private const float BirdDivePitchSensitivity = 0.2f; // tilts the nose with vertical speed
        private const float BirdDiveMaxPitch = 0.85f;        // ceiling the tilt saturates toward
        private const float BirdSpeedChangeIntervalMin = 3f;
        private const float BirdSpeedChangeIntervalMax = 7f;
        private const float BirdSpeedMultMin = 0.6f;
        private const float BirdSpeedMultMax = 1.8f;
        private float _birdAngle;
        private float _birdSpeedMult = 1f;
        private float _birdSpeedTimer;
        private float _birdBaseHeight = BirdHeight;
        private float _birdDiveTimer;
        private float _birdDiveElapsed = -1f;   // < 0 means not currently diving
        private float _birdDiveStartHeight;
        private float _birdDiveEndHeight;
        private float _birdPrevHeight = BirdHeight;
        private float _birdVerticalVelocity;

        // Car: drives along the grid lines, turning at intersections
        private Car _car;
        // The car's shape only depends on its heading (one of 4 axis-aligned directions) and the
        // colour toggle, so — like houses — that geometry is built once per combination up front.
        // Only the translation (its position along the current grid segment) changes every frame.
        private CarGeometry[] _carGeometryColorsOn;
        private CarGeometry[] _carGeometryColorsOff;
        private const float CarSpeed = 1.1f;      // grid cells per second
        private const float CarLength = 0.92f;    // 0.8f * 1.15
        private const float CarWidth = 0.483f;    // 0.42f * 1.15
        private const float CarBodyHeight = 0.115f; // 0.10f * 1.15
        private const float CarCabinHeight = 0.161f; // 0.14f * 1.15
        private const float CarCabinLength = CarLength * 0.55f;
        private const float CarCabinWidth = CarWidth * 0.72f;
        private const float CarCabinSetback = CarLength * 0.06f;  // glasshouse sits toward the rear
        private const float WheelLength = 0.253f;  // 0.22f * 1.15
        private const float WheelTrack = 0.115f;   // 0.1f * 1.15; wheel thickness across the car's width
        private const float WheelHeight = 0.184f;  // 0.16f * 1.15; also doubles as the body's ground clearance
        private const float WheelOutset = 0.092f;  // 0.08f * 1.15; how far the wheels poke out past the body sides
        private static readonly int[] DirX4 = { 1, -1, 0, 0 };
        private static readonly int[] DirZ4 = { 0, 0, 1, -1 };
        private static readonly Color CarColor = new Color(34, 85, 34);      // Dark green
        private static readonly Color CarCabinColor = new Color(120, 200, 230);
        private static readonly Color WheelColor = new Color(40, 40, 40);

        private static readonly Color BackgroundColor = new Color(66, 37, 251); // deep blue-purple

        private static readonly Color[] Palette =
        {
            Color.Cyan, Color.Magenta, Color.Yellow,
            Color.Lime, Color.Red, Color.Orange, Color.DeepSkyBlue
        };

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

            // Initialize houses at random grid locations, each facing a random 90° step, with no
            // two houses sharing a grid cell.
            _houseGeometryColorsOn = new HouseGeometry[HouseCount];
            _houseGeometryColorsOff = new HouseGeometry[HouseCount];
            var occupiedCells = new HashSet<(int x, int z)>();
            for (int i = 0; i < HouseCount; i++)
            {
                int rotation, gridX, gridZ;
                var footprint = new List<(int x, int z)>();
                int attempts = 0;
                do
                {
                    rotation = _rng.Next(4);
                    var (footprintWidth, footprintDepth) = GetHouseFootprint(rotation);
                    gridX = _rng.Next(1, GridSquares - footprintWidth - 1);
                    gridZ = _rng.Next(1, GridSquares - footprintDepth - 1);

                    footprint.Clear();
                    for (int dx = 0; dx < footprintWidth; dx++)
                        for (int dz = 0; dz < footprintDepth; dz++)
                            footprint.Add((gridX + dx, gridZ + dz));

                    attempts++;
                } while (attempts < 200 && footprint.Exists(occupiedCells.Contains));

                foreach (var cell in footprint)
                    occupiedCells.Add(cell);

                _houses[i].GridX = gridX;
                _houses[i].GridZ = gridZ;
                _houses[i].Rotation = rotation;

                // Houses are static, so their vertex/edge data is built once here rather than
                // every frame in Draw(). Both colour states are cached since colours can be
                // toggled live (C key).
                _houseGeometryColorsOn[i] = BuildHouseGeometry(_houses[i], colorsOn: true);
                _houseGeometryColorsOff[i] = BuildHouseGeometry(_houses[i], colorsOn: false);
            }

            _car.GridX = GridSquares / 2;
            _car.GridZ = 0;
            _car.DirX = 0;
            _car.DirZ = 1;
            _car.Progress = 0f;

            // The car can only ever face one of 4 axis-aligned headings, so its local-space shape
            // is built once per heading here (both colour states) instead of every frame in
            // Draw() — DrawCar() then just points the effect's World matrix at the car's current
            // position and lets the GPU place the cached geometry, which is the only thing that
            // actually changes frame to frame.
            _carGeometryColorsOn = new CarGeometry[4];
            _carGeometryColorsOff = new CarGeometry[4];
            for (int heading = 0; heading < 4; heading++)
            {
                _carGeometryColorsOn[heading] = BuildCarGeometry(heading, colorsOn: true);
                _carGeometryColorsOff[heading] = BuildCarGeometry(heading, colorsOn: false);
            }

            _birdDiveTimer = BirdDiveIntervalMin + (float)_rng.NextDouble() * (BirdDiveIntervalMax - BirdDiveIntervalMin);
            _birdSpeedTimer = BirdSpeedChangeIntervalMin + (float)_rng.NextDouble() * (BirdSpeedChangeIntervalMax - BirdSpeedChangeIntervalMin);

            Window.Title = $"Vector Viktor — {CameraModeName(_cameraMode)}";

            base.Initialize();
        }

        private static string CameraModeName(CameraMode mode) => mode switch
        {
            CameraMode.Orbit => "Orbit View",
            CameraMode.ChaseCar => "Chase Car",
            CameraMode.ChaseBird => "Chase Bird",
            _ => "",
        };

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
            _modelRotation += (float)gameTime.ElapsedGameTime.TotalSeconds * 1.5f;  // 3 radians/sec (adjust speed as needed)
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
            if (keys.IsKeyDown(Keys.V) && _prevKeys.IsKeyUp(Keys.V))
            {
                _cameraMode = (CameraMode)(((int)_cameraMode + 1) % 3);
                _chaseYaw = 0f;
                _chasePitch = 0f;
                Window.Title = $"Vector Viktor — {CameraModeName(_cameraMode)}";
            }

            // F11 or Alt+Enter toggles fullscreen
            bool altEnter = keys.IsKeyDown(Keys.Enter) && _prevKeys.IsKeyUp(Keys.Enter)
                && (keys.IsKeyDown(Keys.LeftAlt) || keys.IsKeyDown(Keys.RightAlt));
            if ((keys.IsKeyDown(Keys.F11) && _prevKeys.IsKeyUp(Keys.F11)) || altEnter)
                ToggleFullscreen();

            _prevKeys = keys;

            if (_autoSpin)
                _yaw += 0.25f * dt;

            // Kept just short of ±90°: at exactly ±90° the orbit camera's eye sits directly
            // above/below the origin, which makes the up vector in CreateLookAt degenerate.
            const float maxPitch = MathHelper.PiOver2 - 0.01f;
            _pitch = MathHelper.Clamp(_pitch, -maxPitch, maxPitch);

            // Advance bar animations; respawn each bar elsewhere when its cycle ends
            for (int i = 0; i < BarCount; i++)
            {
                _bars[i].Time += dt;
                if (_bars[i].Time >= _bars[i].Duration)
                    SpawnBar(ref _bars[i]);
            }

            _birdTime += dt;
            UpdateBird(dt);
            UpdateCar(dt);

            base.Update(gameTime);
        }

        private void UpdateBird(float dt)
        {
            // Cruising speed drifts up or down at random intervals
            _birdSpeedTimer -= dt;
            if (_birdSpeedTimer <= 0f)
            {
                _birdSpeedMult = BirdSpeedMultMin + (float)_rng.NextDouble() * (BirdSpeedMultMax - BirdSpeedMultMin);
                _birdSpeedTimer = BirdSpeedChangeIntervalMin + (float)_rng.NextDouble() * (BirdSpeedChangeIntervalMax - BirdSpeedChangeIntervalMin);
            }
            _birdAngle += BirdOrbitSpeed * _birdSpeedMult * dt;

            // Periodically dive toward the grid, then climb back to a height near the original ±10%
            if (_birdDiveElapsed < 0f)
            {
                _birdDiveTimer -= dt;
                if (_birdDiveTimer <= 0f)
                {
                    _birdDiveStartHeight = _birdBaseHeight;
                    float variance = 1f + (float)(_rng.NextDouble() * 2 - 1) * BirdHeightVariance;
                    _birdDiveEndHeight = BirdHeight * variance;
                    _birdDiveElapsed = 0f;
                }
            }
            else
            {
                _birdDiveElapsed += dt;
                if (_birdDiveElapsed >= BirdDiveDuration)
                {
                    _birdDiveElapsed = -1f;
                    _birdBaseHeight = _birdDiveEndHeight;
                    _birdDiveTimer = BirdDiveIntervalMin + (float)_rng.NextDouble() * (BirdDiveIntervalMax - BirdDiveIntervalMin);
                }
            }

            float currentHeight = GetBirdHeight();
            _birdVerticalVelocity = dt > 0f ? (currentHeight - _birdPrevHeight) / dt : 0f;
            _birdPrevHeight = currentHeight;
        }

        // Height follows a down-then-up smoothstep during a dive; otherwise holds at the resting height
        private float GetBirdHeight()
        {
            if (_birdDiveElapsed < 0f)
                return _birdBaseHeight;

            float phase = _birdDiveElapsed / BirdDiveDuration;
            if (phase < 0.5f)
                return MathHelper.Lerp(_birdDiveStartHeight, BirdDiveLowHeight, Smooth(phase / 0.5f));
            return MathHelper.Lerp(BirdDiveLowHeight, _birdDiveEndHeight, Smooth((phase - 0.5f) / 0.5f));
        }

        // Quintic smootherstep: unlike a cubic smoothstep, this also zeroes out at both ends,
        // so the dive eases in and out with no kink in acceleration — no harsh snap at the bottom.
        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

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

            switch (_cameraMode)
            {
                case CameraMode.ChaseCar:
                {
                    var (carPos, carForward) = GetCarTransform();
                    Vector3 carRight = Vector3.Cross(carForward, Vector3.Up);

                    // Build offset in car's local coordinate frame using spherical coordinates
                    float r = CarChaseDistance;
                    float baseHeight = CarChaseHeight;

                    // Local offset: (side, height, back)
                    float localX = r * (float)Math.Sin(_chaseYaw) * (float)Math.Cos(_chasePitch);
                    float localY = baseHeight + r * (float)Math.Sin(_chasePitch);
                    float localZ = -r * (float)Math.Cos(_chaseYaw) * (float)Math.Cos(_chasePitch);

                    // Transform to world space using car's basis
                    Vector3 offsetInWorld = carRight * localX + Vector3.Up * localY + carForward * localZ;

                    Vector3 eye = carPos + offsetInWorld;
                    Vector3 target = carPos + carForward * CarChaseLookAhead + Vector3.Up * CarChaseTargetHeight;
                    _effect.World = Matrix.Identity;
                    _effect.View = Matrix.CreateLookAt(eye, target, Vector3.Up);
                    break;
                }
                case CameraMode.ChaseBird:
                {
                    var (birdPos, birdForward) = GetBirdTransform();
                    Vector3 birdRight = Vector3.Cross(birdForward, Vector3.Up);

                    // Build offset in bird's local coordinate frame using spherical coordinates
                    float r = BirdChaseDistance;
                    float baseHeight = BirdChaseHeight;

                    // Local offset: (side, height, back)
                    float localX = r * (float)Math.Sin(_chaseYaw) * (float)Math.Cos(_chasePitch);
                    float localY = baseHeight + r * (float)Math.Sin(_chasePitch);
                    float localZ = -r * (float)Math.Cos(_chaseYaw) * (float)Math.Cos(_chasePitch);

                    // Transform to world space using bird's basis
                    Vector3 offsetInWorld = birdRight * localX + Vector3.Up * localY + birdForward * localZ;

                    Vector3 eye = birdPos + offsetInWorld;
                    Vector3 target = birdPos + birdForward * BirdChaseLookAhead;
                    _effect.World = Matrix.Identity;
                    _effect.View = Matrix.CreateLookAt(eye, target, Vector3.Up);
                    break;
                }
                default:
                {
                    // Orbit camera: the eye moves around the origin on a sphere of radius
                    // _cameraDistance, driven by yaw/pitch; the world itself stays fixed,
                    // matching the chase cams' convention of a moving camera + identity world.
                    float eyeX = _cameraDistance * (float)Math.Sin(_yaw) * (float)Math.Cos(_pitch);
                    float eyeY = _cameraDistance * (float)Math.Sin(_pitch);
                    float eyeZ = _cameraDistance * (float)Math.Cos(_yaw) * (float)Math.Cos(_pitch);
                    _effect.World = Matrix.Identity;
                    _effect.View = Matrix.CreateLookAt(new Vector3(eyeX, eyeY, eyeZ), Vector3.Zero, Vector3.Up);
                    break;
                }
            }
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

                // Houses (static; geometry built once in Initialize rather than rebuilt per frame)
                var houseGeometry = _colorsOn ? _houseGeometryColorsOn : _houseGeometryColorsOff;
                for (int i = 0; i < HouseCount; i++)
                    DrawHouseGeometry(in houseGeometry[i]);

                // Bird
                DrawBird();

                // Car
                DrawCar(pass);
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

        // Orbits above the grid at a speed and height that drift over time; the bird's nose tilts
        // with its vertical speed so dives and climbs read as actual diving rather than floating
        private (Vector3 pos, Vector3 forward) GetBirdTransform()
        {
            float angle = _birdAngle;
            float height = GetBirdHeight();
            Vector3 center = new Vector3(
                (float)Math.Cos(angle) * BirdOrbitRadius,
                height,
                (float)Math.Sin(angle) * BirdOrbitRadius);

            // tanh saturates smoothly at speed rather than hard-clamping, so the tilt itself
            // eases toward its max instead of snapping flat the instant velocity peaks
            float pitch = (float)Math.Tanh(_birdVerticalVelocity * BirdDivePitchSensitivity) * BirdDiveMaxPitch;
            Vector3 forward = Vector3.Normalize(new Vector3(
                -(float)Math.Sin(angle), pitch, (float)Math.Cos(angle)));
            return (center, forward);
        }

        private void DrawBird()
        {
            var (center, forward) = GetBirdTransform();
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

        // Interpolate between the current and next grid intersection
        private (Vector3 pos, Vector3 forward) GetCarTransform()
        {
            float x0 = -GridExtent + _car.GridX * CellSize;
            float z0 = -GridExtent + _car.GridZ * CellSize;
            float x1 = -GridExtent + (_car.GridX + _car.DirX) * CellSize;
            float z1 = -GridExtent + (_car.GridZ + _car.DirZ) * CellSize;
            Vector3 pos = Vector3.Lerp(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z1), _car.Progress);
            pos.Y = 0.015f; // lift slightly above the grid to avoid z-fighting
            Vector3 forward = new Vector3(_car.DirX, 0f, _car.DirZ); // already unit length
            return (pos, forward);
        }

        // Maps the car's current (DirX, DirZ) to the matching index into the 4 precomputed
        // headings (see BuildCarGeometry) — heading 0 is +Z, then 90° steps the same way house
        // rotation does.
        private static int CarHeadingIndex(int dirX, int dirZ)
        {
            if (dirZ == 1) return 0;
            if (dirX == 1) return 1;
            if (dirZ == -1) return 2;
            return 3; // dirX == -1
        }

        // The car's cached geometry is local space with heading already baked in (see
        // BuildCarGeometry) — same shape, every frame. Rather than re-transforming its vertices on
        // the CPU each frame (as any moving mesh would otherwise need to), position is applied via
        // the effect's World matrix and left to the GPU, so this does zero per-frame CPU work
        // beyond picking the cached array and issuing the draw calls.
        private void DrawCar(EffectPass pass)
        {
            var (pos, _) = GetCarTransform();
            int heading = CarHeadingIndex(_car.DirX, _car.DirZ);
            var geo = _colorsOn ? _carGeometryColorsOn[heading] : _carGeometryColorsOff[heading];

            _effect.World = Matrix.CreateTranslation(pos);
            pass.Apply();

            DrawBoxGeometry(geo.WheelFrontATris, geo.WheelFrontAEdges);
            DrawBoxGeometry(geo.WheelFrontBTris, geo.WheelFrontBEdges);
            DrawBoxGeometry(geo.WheelRearATris, geo.WheelRearAEdges);
            DrawBoxGeometry(geo.WheelRearBTris, geo.WheelRearBEdges);
            DrawBoxGeometry(geo.BodyTris, geo.BodyEdges);
            DrawBoxGeometry(geo.CabinTris, geo.CabinEdges);

            // Everything else this frame assumes World == Identity (its vertices are already in
            // world space), so restore it before the next draw call relies on that.
            _effect.World = Matrix.Identity;
            pass.Apply();
        }

        // Builds the car's local-space geometry (centred on its ground-contact point, at the
        // origin) for one of its 4 possible headings. Called once per heading per colour state at
        // startup and cached, since heading only changes at intersections and colour only on the
        // C key — DrawCar() then just translates these cached vertices to the car's live position.
        private CarGeometry BuildCarGeometry(int heading, bool colorsOn)
        {
            Vector3 forward = Vector3.Transform(Vector3.UnitZ, Matrix.CreateRotationY(heading * MathHelper.PiOver2));
            Vector3 right = Vector3.Cross(forward, Vector3.Up);

            Color bodyColor = colorsOn ? CarColor : BackgroundColor;
            Color cabinColor = colorsOn ? CarCabinColor : BackgroundColor;
            Color wheelColor = colorsOn ? WheelColor : BackgroundColor;

            var geo = new CarGeometry();

            // Wheels: low-poly boxes at the four corners, giving the body its ground clearance
            Vector3 frontAxle = forward * (CarLength * 0.32f);
            Vector3 rearAxle = -forward * (CarLength * 0.32f);
            (geo.WheelFrontATris, geo.WheelFrontAEdges) = BuildWheelGeometry(frontAxle, forward, right, +1, wheelColor, colorsOn);
            (geo.WheelFrontBTris, geo.WheelFrontBEdges) = BuildWheelGeometry(frontAxle, forward, right, -1, wheelColor, colorsOn);
            (geo.WheelRearATris, geo.WheelRearAEdges) = BuildWheelGeometry(rearAxle, forward, right, +1, wheelColor, colorsOn);
            (geo.WheelRearBTris, geo.WheelRearBEdges) = BuildWheelGeometry(rearAxle, forward, right, -1, wheelColor, colorsOn);

            // Body: a low chassis box riding on top of the wheels
            Vector3 bodyBottom = Vector3.Up * WheelHeight;
            (geo.BodyTris, geo.BodyEdges) = BuildBoxGeometry(bodyBottom, forward, right, CarLength, CarWidth, CarBodyHeight, bodyColor, colorsOn);

            // Cabin: a shorter, narrower glasshouse set back toward the rear — hatchback roofline
            Vector3 cabinBottom = bodyBottom + Vector3.Up * CarBodyHeight - forward * CarCabinSetback;
            (geo.CabinTris, geo.CabinEdges) = BuildBoxGeometry(cabinBottom, forward, right, CarCabinLength, CarCabinWidth, CarCabinHeight, cabinColor, colorsOn);

            return geo;
        }

        private (VertexPositionColor[] tris, VertexPositionColor[] edges) BuildWheelGeometry(Vector3 axleCenter, Vector3 forward, Vector3 right, int side, Color color, bool colorsOn)
        {
            float outerEdge = CarWidth * 0.5f + WheelOutset;
            Vector3 wheelCenter = axleCenter + right * (side * (outerEdge - WheelTrack * 0.5f));
            return BuildBoxGeometry(wheelCenter, forward, right, WheelLength, WheelTrack, WheelHeight, color, colorsOn);
        }

        // Builds the vertex/edge arrays for a shaded+outlined box without drawing it, so callers
        // with static geometry (e.g. houses) can build once and cache the result instead of
        // rebuilding it every frame.r
        private (VertexPositionColor[] tris, VertexPositionColor[] edges) BuildBoxGeometry(
            Vector3 bottomCenter, Vector3 forward, Vector3 right, float length, float width, float height, Color color, bool colorsOn)
        {
            Vector3 hf = forward * (length * 0.5f);
            Vector3 hr = right * (width * 0.5f);
            Vector3 hu = Vector3.Up * height;

            Vector3 a = bottomCenter - hf - hr;
            Vector3 b = bottomCenter + hf - hr;
            Vector3 c = bottomCenter + hf + hr;
            Vector3 d = bottomCenter - hf + hr;
            Vector3 e = a + hu;
            Vector3 f = b + hu;
            Vector3 g = c + hu;
            Vector3 h = d + hu;

            Color side, sideDim, top;
            if (colorsOn)
            {
                side = color;
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
            Quad(a, b, f, e, side);     // flank (-right)
            Quad(c, d, h, g, side);     // flank (+right)
            Quad(b, c, g, f, sideDim);  // nose  (+forward)
            Quad(d, a, e, h, sideDim);  // tail  (-forward)
            Quad(e, f, g, h, top);      // roof

            var edges = new VertexPositionColor[24];
            v = 0;
            void Edge(Vector3 p0, Vector3 p1)
            {
                edges[v++] = new VertexPositionColor(p0, Color.White);
                edges[v++] = new VertexPositionColor(p1, Color.White);
            }
            Edge(a, b); Edge(b, c); Edge(c, d); Edge(d, a); // base
            Edge(e, f); Edge(f, g); Edge(g, h); Edge(h, e); // roof rim
            Edge(a, e); Edge(b, f); Edge(c, g); Edge(d, h); // verticals

            return (tris, edges);
        }

        private void DrawBoxGeometry(VertexPositionColor[] tris, VertexPositionColor[] edges)
        {
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, tris, 0, 10);
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, edges, 0, 12);
        }

        // A house's unrotated footprint is 2 grid cells wide (local X) by 1 deep (local Z).
        // Rotating it by 90°/270° swaps which world axis is wide vs. deep.
        private static (int width, int depth) GetHouseFootprint(int rotation) =>
            rotation % 2 == 0 ? (2, 1) : (1, 2);

        // World-space centre of a house's occupied footprint, accounting for its rotation.
        private Vector3 GetHouseWorldCenter(House house)
        {
            var (footprintWidth, footprintDepth) = GetHouseFootprint(house.Rotation);
            float x = -GridExtent + house.GridX * CellSize + footprintWidth * CellSize * 0.5f;
            float z = -GridExtent + house.GridZ * CellSize + footprintDepth * CellSize * 0.5f;
            return new Vector3(x, 0.02f, z); // y: slightly above grid to avoid z-fighting
        }

        // Rotates each vertex's position about the origin, then translates it — used to place a
        // house's local-space geometry (built facing its default orientation) at its actual
        // world position and 90°-step rotation.
        private static VertexPositionColor[] TransformVerts(VertexPositionColor[] verts, Matrix rotation, Vector3 translation)
        {
            var result = new VertexPositionColor[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 pos = Vector3.Transform(verts[i].Position, rotation) + translation;
                result[i] = new VertexPositionColor(pos, verts[i].Color);
            }
            return result;
        }

        // Builds all vertex/edge data for a house without drawing it, so it can be computed once
        // (houses never move) and cached rather than rebuilt every frame. Shape is built in local
        // space (centred on the origin, facing its default 0° orientation) and rotated/placed at
        // the end, so the same math produces a house facing any of the four 90° steps.
        private HouseGeometry BuildHouseGeometry(House house, bool colorsOn)
        {
            // House dimensions: 2 grid cells wide (X), 1 grid cell deep (Z), 1 story tall
            float houseWidth = 2.0f * CellSize;    // 2 grid squares
            float houseDepth = 1.0f * CellSize;    // 1 grid square
            float wallHeight = 0.6f;               // 1 story
            float roofPeakHeight = 0.3f;           // roof adds this much height

            Vector3 houseCenter = Vector3.Zero;    // local space; rotated + placed at the end

            // Colors
            Color wallColor = colorsOn ? new Color(210, 140, 80) : BackgroundColor;      // Terracotta
            Color roofColor = colorsOn ? new Color(150, 80, 200) : BackgroundColor;      // Purple
            Color doorColor = colorsOn ? Color.White : BackgroundColor;
            Color windowColor = colorsOn ? Color.Blue : BackgroundColor;
            Color chimneyColor = colorsOn ? new Color(80, 40, 20) : BackgroundColor;    // Dark brown

            var geo = new HouseGeometry();

            // Main walls (box)
            (geo.WallTris, geo.WallEdges) = BuildBoxGeometry(houseCenter, Vector3.UnitZ, Vector3.UnitX, houseDepth, houseWidth, wallHeight, wallColor, colorsOn);

            // Roof (pyramid-like shape - four triangular faces)
            Vector3 roofBase = houseCenter + Vector3.Up * wallHeight;
            Vector3 roofPeak = roofBase + Vector3.Up * roofPeakHeight;

            float roofHalfWidth = houseWidth * 0.5f;
            float roofHalfDepth = houseDepth * 0.5f;

            Vector3 roofFrontLeft = roofBase - Vector3.UnitZ * roofHalfDepth - Vector3.UnitX * roofHalfWidth;
            Vector3 roofFrontRight = roofBase - Vector3.UnitZ * roofHalfDepth + Vector3.UnitX * roofHalfWidth;
            Vector3 roofBackLeft = roofBase + Vector3.UnitZ * roofHalfDepth - Vector3.UnitX * roofHalfWidth;
            Vector3 roofBackRight = roofBase + Vector3.UnitZ * roofHalfDepth + Vector3.UnitX * roofHalfWidth;

            geo.RoofTris = new VertexPositionColor[12];
            int v = 0;

            // Front roof slope
            geo.RoofTris[v++] = new VertexPositionColor(roofFrontLeft, roofColor);
            geo.RoofTris[v++] = new VertexPositionColor(roofFrontRight, roofColor);
            geo.RoofTris[v++] = new VertexPositionColor(roofPeak, roofColor);

            // Back roof slope
            geo.RoofTris[v++] = new VertexPositionColor(roofBackRight, roofColor);
            geo.RoofTris[v++] = new VertexPositionColor(roofBackLeft, roofColor);
            geo.RoofTris[v++] = new VertexPositionColor(roofPeak, roofColor);

            // Left roof slope
            geo.RoofTris[v++] = new VertexPositionColor(roofFrontLeft, roofColor);
            geo.RoofTris[v++] = new VertexPositionColor(roofBackLeft, roofColor);
            geo.RoofTris[v++] = new VertexPositionColor(roofPeak, roofColor);

            // Right roof slope
            geo.RoofTris[v++] = new VertexPositionColor(roofBackRight, roofColor);
            geo.RoofTris[v++] = new VertexPositionColor(roofFrontRight, roofColor);
            geo.RoofTris[v++] = new VertexPositionColor(roofPeak, roofColor);

            geo.RoofEdges = new VertexPositionColor[12];
            v = 0;
            geo.RoofEdges[v++] = new VertexPositionColor(roofFrontLeft, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofPeak, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofFrontRight, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofPeak, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofBackLeft, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofPeak, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofBackRight, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofPeak, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofFrontLeft, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofBackLeft, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofFrontRight, Color.White);
            geo.RoofEdges[v++] = new VertexPositionColor(roofBackRight, Color.White);

            // Front door on one side of the facade
            float doorWidth = 0.24f;
            float doorHeight = wallHeight * 0.85f; // slightly shorter than the wall
            // BuildBoxGeometry treats its position argument as the box's bottom, not its center,
            // so no vertical offset here — houseCenter.Y is already the wall's base, and the door
            // should sit flush with it rather than floating half its height above the ground.
            Vector3 doorCenter = houseCenter
                + Vector3.UnitX * (houseWidth * -0.22f)
                - Vector3.UnitZ * (houseDepth * 0.5f + 0.01f);
            (geo.DoorTris, geo.DoorEdges) = BuildBoxGeometry(doorCenter, Vector3.UnitZ, Vector3.UnitX, 0.01f, doorWidth, doorHeight, doorColor, colorsOn);

            // A larger front window on the opposite side, with a plus symbol to suggest a peek-through pane
            float windowWidth = 0.52f;
            float windowHeight = 0.33f;
            Vector3 windowCenter = houseCenter
                + Vector3.UnitX * (houseWidth * 0.22f)
                - Vector3.UnitZ * (houseDepth * 0.5f + 0.02f)
                + Vector3.Up * (wallHeight * 0.45f);
            (geo.WindowTris, geo.WindowEdges) = BuildBoxGeometry(windowCenter, Vector3.UnitZ, Vector3.UnitX, 0.01f, windowWidth, windowHeight, windowColor, colorsOn);

            // windowCenter is the window box's bottom (BuildBoxGeometry's bottomCenter convention),
            // so the true mid-height point is windowHeight/2 above it. The cross spans the full
            // pane edge to edge: the horizontal bar runs the full width at mid-height, the
            // vertical bar runs the full height from the window's bottom to its top.
            // Pulled 0.01 further out (-Z, the same direction the window already projects out
            // from the wall) so it clears the pane's own front face — at the box's exact center
            // Z it was sandwiched inside the 0.01-thick pane and z-fighting against it.
            Vector3 crossOrigin = windowCenter - Vector3.UnitZ * 0.01f;
            Vector3 windowMid = crossOrigin + Vector3.Up * (windowHeight * 0.5f);
            geo.WindowPlusEdges = new[]
            {
                new VertexPositionColor(windowMid - Vector3.UnitX * (windowWidth * 0.5f), Color.White),
                new VertexPositionColor(windowMid + Vector3.UnitX * (windowWidth * 0.5f), Color.White),
                new VertexPositionColor(crossOrigin, Color.White),
                new VertexPositionColor(crossOrigin + Vector3.Up * windowHeight, Color.White),
            };

            // Chimney (on roof right side)
            float chimneyWidth = 0.15f;
            float chimneyDepth = 0.1f;
            float chimneyHeight = 0.3f;
            Vector3 chimneyBase = roofBase + Vector3.UnitX * (roofHalfWidth - chimneyWidth * 0.5f) - Vector3.UnitZ * (roofHalfDepth * 0.5f);
            Vector3 chimneyCenter = chimneyBase; // + Vector3.Up * (chimneyHeight * 0.5f);
            (geo.ChimneyTris, geo.ChimneyEdges) = BuildBoxGeometry(chimneyCenter, Vector3.UnitZ, Vector3.UnitX, chimneyDepth, chimneyWidth, chimneyHeight, chimneyColor, colorsOn);

            // Rotate the local-space shape to the house's facing and place it at its actual grid position.
            Matrix rotation = Matrix.CreateRotationY(house.Rotation * MathHelper.PiOver2);
            Vector3 worldCenter = GetHouseWorldCenter(house);

            geo.WallTris = TransformVerts(geo.WallTris, rotation, worldCenter);
            geo.WallEdges = TransformVerts(geo.WallEdges, rotation, worldCenter);
            geo.RoofTris = TransformVerts(geo.RoofTris, rotation, worldCenter);
            geo.RoofEdges = TransformVerts(geo.RoofEdges, rotation, worldCenter);
            geo.DoorTris = TransformVerts(geo.DoorTris, rotation, worldCenter);
            geo.DoorEdges = TransformVerts(geo.DoorEdges, rotation, worldCenter);
            geo.WindowTris = TransformVerts(geo.WindowTris, rotation, worldCenter);
            geo.WindowEdges = TransformVerts(geo.WindowEdges, rotation, worldCenter);
            geo.WindowPlusEdges = TransformVerts(geo.WindowPlusEdges, rotation, worldCenter);
            geo.ChimneyTris = TransformVerts(geo.ChimneyTris, rotation, worldCenter);
            geo.ChimneyEdges = TransformVerts(geo.ChimneyEdges, rotation, worldCenter);

            return geo;
        }

        private void DrawHouseGeometry(in HouseGeometry geo)
        {
            DrawBoxGeometry(geo.WallTris, geo.WallEdges);

            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, geo.RoofTris, 0, 4);
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, geo.RoofEdges, 0, 6);

            DrawBoxGeometry(geo.DoorTris, geo.DoorEdges);
            DrawBoxGeometry(geo.WindowTris, geo.WindowEdges);
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, geo.WindowPlusEdges, 0, 2);

            DrawBoxGeometry(geo.ChimneyTris, geo.ChimneyEdges);
        }
   }
}

 