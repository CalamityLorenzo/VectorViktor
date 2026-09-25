using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using World.Core;
using World.Rendering;

namespace Basic.World
{
    // A small road layout on flat ground, every road piece in it, to look at through an orbit camera. The
    // pieces sit on the 10 m grid the ground's lines are drawn on (see RoadMesh), and join up into a loop:
    // out of the roundabout's east road, round a bend, down to a T-junction, back west along its side
    // road and round another bend into the roundabout's south road. A second loop goes west out of the
    // roundabout and back into its north road round three wide, gentle bends, past a pedestrian island.
    // A spur goes on south from the T-junction, going nowhere yet.
    // Left-drag or the arrow keys orbit, the mouse wheel or W / S zoom, A / D slide sideways across the map. C toggles colours / wireframe,
    // L the low-resolution look, F11 full screen, Escape exits. BASIC_WORLD_SHOT takes a screenshot (see RetroGame).
    public class Game2 : RetroGame
    {
        private const int WindowWidth = 1440;
        private const int WindowHeight = 810;
        private const int LowResWidth = 640;
        private const int LowResHeight = 256;

        private const int GroundCells = 40;
        private const float GroundCellSize = 5f;   // grid lines every other cell: every 10 m, on the road grid

        // The orbit camera starts looking at the middle of the layout; A / D slide it across the ground
        private static readonly Vector3 StartTarget = new Vector3(-10f, 0f, -5f);
        private const float KeySlideSpeed = 0.8f;              // camera distances per second, so it feels the same near and far
        private const float MinDistance = 10f, MaxDistance = 250f;
        private const float MinPitch = 5f, MaxPitch = 89f;     // degrees; stops it going under the ground or over the top
        private const float MouseOrbitSpeed = 0.005f;          // radians per pixel dragged
        private const float KeyOrbitSpeed = 1.5f;              // radians per second
        private const float WheelZoom = 0.9f;                  // distance multiplier per wheel notch
        private const float KeyZoomSpeed = 2.0f;               // zoom factor per second

        private static readonly Color[] RoadPalette =
            RoadMesh.Palette(new Color(70, 70, 75), new Color(170, 165, 155), Color.White, new Color(60, 140, 50));

        // Each piece: its mesh, where its centre goes, and how far it's turned (quarter turns, from +Z towards +X)
        private record struct Piece(MeshSource Mesh, float X, float Z, float Turn);

        private static MeshSource Road(string key, Func<GraphicsDevice, MeshData> build) => new MeshSource(key, build, RoadPalette);

        // Each kind of piece once, however many of it the layout has
        private static readonly MeshSource StraightRoad = Road("road-straight", d => RoadMesh.Straight(d));
        private static readonly MeshSource Corner = Road("road-corner", d => RoadMesh.Corner(d));
        private static readonly MeshSource WideCorner = Road("road-corner-wide", d => RoadMesh.Corner(d, radius: 20f));

        private static Piece Straight(float x, float z, bool alongX) =>
            new(StraightRoad, x, z, alongX ? MathHelper.PiOver2 : 0f);

        private static readonly Piece[] Layout =
        {
            // The roundabout, 40 x 40, its roads leaving at (±20, 0) and (0, ±20)
            new(Road("road-roundabout", d => RoadMesh.Roundabout(d)), 0f, 0f, 0f),

            // East, round a bend (in from the west, out to the south) and south to the T-junction
            Straight(25f, 0f, alongX: true),
            Straight(35f, 0f, alongX: true),
            new(Corner, 50f, 0f, MathHelper.Pi),
            Straight(50f, 15f, alongX: false),
            Straight(50f, 25f, alongX: false),

            // The T-junction: through road north-south, its side road off to the west
            new(Road("road-tjunction", d => RoadMesh.TJunction(d)), 50f, 40f, MathHelper.Pi),

            // West along the side road, round a bend (in from the east, out to the north) back to the roundabout
            Straight(35f, 40f, alongX: true),
            Straight(25f, 40f, alongX: true),
            Straight(15f, 40f, alongX: true),
            new(Corner, 0f, 40f, 0f),
            Straight(0f, 25f, alongX: false),

            // A spur on south from the T-junction
            Straight(50f, 55f, alongX: false),
            Straight(50f, 65f, alongX: false),

            // The second loop, with gentle bends (radius 20, each filling 40 x 40): west out of the roundabout,
            // bending north, past a pedestrian island, bending east, and bending south into its north road
            Straight(-25f, 0f, alongX: true),
            Straight(-35f, 0f, alongX: true),
            new(WideCorner, -60f, 0f, 0f),
            new(Road("road-island", d => RoadMesh.StraightWithIsland(d)), -60f, -30f, 0f),
            new(WideCorner, -60f, -60f, -MathHelper.PiOver2),
            Straight(-35f, -60f, alongX: true),
            Straight(-25f, -60f, alongX: true),
            new(WideCorner, 0f, -60f, MathHelper.Pi),
            Straight(0f, -25f, alongX: false),
            Straight(0f, -35f, alongX: false),
        };

        private readonly List<MeshInstance> _instances = new List<MeshInstance>();
        private readonly MeshBatch _batch = new MeshBatch();
        private BasicEffect _basicEffect;
        private MouseState _previousMouse;

        // Where the camera is, as spherical coordinates round the point it looks at: looking north-west from the south-east
        private Vector3 _cameraTarget = StartTarget;
        private float _cameraYaw = MathHelper.ToRadians(30f);
        private float _cameraPitch = MathHelper.ToRadians(40f);
        private float _cameraDistance = 110f;

        public Game2() : base(WindowWidth, WindowHeight, LowResWidth, LowResHeight, colorsKey: Keys.C)
        {
            Window.Title = "Basic.World - roads";
        }

        protected override void LoadWorld()
        {
            _basicEffect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true, World = Matrix.Identity, FogEnabled = true, FogColor = BackgroundColor.ToVector3() };

            // The pieces first, so the ground can leave its grid lines out from under them: they'd show through
            // the tarmac, 2 cm above them, from a low angle (the faces are drawn a little deeper than they are, so
            // their own edges show - see MeshInstance)
            var pieces = new List<(MeshInstance view, Matrix toPiece)>();
            foreach (var piece in Layout)
            {
                var view = MeshCache.CreateInstance(GraphicsDevice, piece.Mesh);
                view.Transform = Matrix.CreateRotationY(piece.Turn) * Matrix.CreateTranslation(piece.X, 0f, piece.Z);
                _instances.Add(view);
                pieces.Add((view, Matrix.Invert(view.World)));
            }
            bool UnderRoad(float x, float z) => pieces.Exists(p =>
            {
                var local = Vector3.Transform(new Vector3(x, 0f, z), p.toPiece);
                return p.view.Mesh.Covers(local.X, local.Z);
            });

            var ground = Terrain.FromFunction(GroundCells, GroundCells, GroundCellSize, (x, z) => 0f);
            _instances.Add(MeshCache.CreateInstance(GraphicsDevice,
                new MeshSource("ground", d => TerrainMesh.Build(d, ground, 0f, 0, 0, ground.Width, ground.Depth, UnderRoad), TerrainMesh.Palette())));
            UpdateCamera();
        }

        protected override void UpdateWorld(GameTime gameTime, KeyboardState keyboard)
        {
            var mouse = Mouse.GetState();
            if (IsActive)
                UpdateOrbitCamera(keyboard, mouse, (float)gameTime.ElapsedGameTime.TotalSeconds);
            _previousMouse = mouse;
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

            // Slide: A / D move the point looked at sideways across the screen, along the ground, staying over it
            var right = new Vector3(MathF.Cos(_cameraYaw), 0f, -MathF.Sin(_cameraYaw));
            var slide = (keyboard.IsKeyDown(Keys.D) ? 1f : 0f) - (keyboard.IsKeyDown(Keys.A) ? 1f : 0f);
            _cameraTarget += right * (slide * KeySlideSpeed * _cameraDistance * dt);
            var edge = GroundCells * GroundCellSize / 2f;
            _cameraTarget.X = MathHelper.Clamp(_cameraTarget.X, -edge, edge);
            _cameraTarget.Z = MathHelper.Clamp(_cameraTarget.Z, -edge, edge);

            UpdateCamera();
        }

        private void UpdateCamera()
        {
            var offset = new Vector3(
                MathF.Sin(_cameraYaw) * MathF.Cos(_cameraPitch),
                MathF.Sin(_cameraPitch),
                MathF.Cos(_cameraYaw) * MathF.Cos(_cameraPitch));
            _basicEffect.View = Matrix.CreateLookAt(_cameraTarget + offset * _cameraDistance, _cameraTarget, Vector3.Up);
            // The fog keeps its distance from the layout, however far out the camera is, and fades the ground's edge
            _basicEffect.FogStart = _cameraDistance + 40f;
            _basicEffect.FogEnd = _cameraDistance + 140f;
        }

        protected override void DrawWorld(GameTime gameTime)
        {
            // Near plane well out: the tarmac is only 2 cm above the ground, and this keeps them apart in the depth buffer
            _basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(60f), GraphicsDevice.Viewport.AspectRatio, 1f, 500f);

            _batch.Begin(_basicEffect.View, _basicEffect.Projection);
            foreach (var instance in _instances)
                _batch.Add(instance);
            _batch.Draw(GraphicsDevice, _basicEffect, BackgroundColor, ColorsOn);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _basicEffect?.Dispose();
            base.Dispose(disposing);
        }
    }
}
