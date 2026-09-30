using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using World.Core;
using World.Core.Characters;
using World.Core.Vehicles;

namespace World.Maps
{
    // Draws the built world as seen by the player, from their own eyes or from the drone, or from the camera chasing
    // the bird (see Bird.Chase): the fog, the camera, the world (see WorldView), the player or the drone (or both, from
    // the bird), the bird, and the windows that look onto elsewhere - into whatever render
    // target is set. It has no input and no clock of its own, so a game and the benchmark (see Basic.World.Benchmark)
    // draw exactly the same frame. DrawFrom is the map alone from any camera, with whatever else is to be in it added:
    // for the playtest harness (its droid) and the map studio.
    public sealed class WorldRenderer : IDisposable
    {
        // Distance fades everything into the sky: otherwise, a long way off, the terrain's grid lines are closer
        // together than the pixels are and the far hills turn solid white
        public const float FogStart = 20f, FogEnd = 95f;   // metres

        // The terrain's built out to where the fog has hidden it all, and a little beyond, so it's there before it shows
        public const float DrawDistance = FogEnd + 15f;

        private const float FieldOfView = 70f, NearPlane = 0.1f;

        // Wet clothes are darker: the legs first, as you wade in, then the body and the head (see PlayerMesh's
        // heights), as high as you've been soaked.
        private static readonly (int part, float bottom, float top)[] Soakable =
            { (PlayerMesh.Legs, 0f, 0.85f), (PlayerMesh.Body, 0.85f, 1.5f), (PlayerMesh.Head, 1.5f, 1.8f) };

        private readonly GraphicsDevice _device;
        private readonly BasicEffect _effect;
        private readonly MeshBatch _batch = new MeshBatch();
        private readonly MeshInstance _playerView, _droneView;
        private readonly MeshInstance _cockpit, _steeringWheel;   // what you see of the car from its driver's seat
        private readonly MeshInstance[] _needles = new MeshInstance[2];   // its dials' (see CockpitMesh.NeedleAt)
        private readonly MeshBatch _cockpitBatch = new MeshBatch();
        private readonly MeshInstance[] _birdFrames = new MeshInstance[BirdMesh.Frames];   // one for each step through a flap
        private readonly IGround _ground;
        private readonly Color[] _playerColors;   // dry: as drawn, they're darker where wet

        public WorldView View { get; }

        // The camera the last frame was drawn with: for drawing more over it (lines, markers) or picking with the mouse.
        public Matrix ViewMatrix { get; private set; }
        public Matrix Projection { get; private set; }

        // How many meshes the last frame drew, left out and made draw calls of.
        public MeshBatch Batch => _batch;

        public WorldRenderer(BuiltWorld built, GraphicsDevice device, MeshCache cache)
        {
            _device = device;
            _ground = built.Ground;
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true, World = Matrix.Identity,
                FogEnabled = true, FogColor = RetroStyle.Background.ToVector3(), FogStart = FogStart, FogEnd = FogEnd,
            };
            // The terrain's built a chunk at a time round the camera, out to where the fog has hidden it all
            View = new WorldView(built, device, cache, DrawDistance);
            _playerColors = PlayerMesh.Palette(new Color(50, 60, 120), new Color(200, 60, 40), new Color(230, 180, 140));
            _playerView = cache.CreateInstance(device, new MeshSource("player", PlayerMesh.Build, _playerColors));
            _droneView = cache.CreateInstance(device, new MeshSource("drone", DroneMesh.Build,
                DroneMesh.Palette(new Color(90, 90, 100), new Color(60, 60, 65), new Color(40, 40, 45), new Color(120, 220, 230))));
            _droneView.Scale = 1.5f;   // so it reads at low resolution, even a few metres off
            _cockpit = cache.CreateInstance(device, new MeshSource("cockpit", CockpitMesh.Build,
                CockpitMesh.Palette(new Color(50, 50, 60), new Color(150, 30, 28), new Color(170, 165, 150), new Color(60, 90, 120), new Color(90, 35, 35))));
            _steeringWheel = cache.CreateInstance(device, new MeshSource("steering wheel", CockpitMesh.BuildWheel,
                CockpitMesh.WheelPalette(new Color(35, 35, 40), new Color(85, 85, 95))));
            var needle = new MeshSource("needle", CockpitMesh.BuildNeedle, CockpitMesh.NeedlePalette(new Color(240, 120, 30)));
            for (var i = 0; i < _needles.Length; i++)
                _needles[i] = cache.CreateInstance(device, needle);
            var birdPalette = BirdMesh.Palette(Color.White, new Color(140, 210, 230));
            for (var frame = 0; frame < BirdMesh.Frames; frame++)
                _birdFrames[frame] = cache.CreateInstance(device, BirdMesh.Source(frame, birdPalette));
        }

        // Builds all the terrain round the player at once, rather than a few chunks a frame.
        public void BuildTerrain(Player player) => View.Update(_device, player.Eye, player.Body.Position, all: true);

        // One frame: `clock` is the seconds drawn so far (for what moves by itself). With `followBird`, it's seen from
        // the camera chasing the bird, and whatever depends on where you are (a window near enough to be open) goes
        // by where that camera is instead.
        public void Draw(Player player, float clock, bool colorsOn, Bird bird = null, bool followBird = false)
        {
            Dampen(player);

            var body = player.Body;
            var you = body.Position;
            Vector3 eye, lookAt, up = Vector3.Up;
            var driving = player.Driving;
            View.HiddenCar = !followBird && driving != null && player.View == ViewMode.FirstPerson ? driving : null;
            if (followBird && bird != null)
            {
                (eye, lookAt) = bird.Chase(_ground);
                you = eye;
            }
            else if (driving != null)
            {
                // From the driver's seat, tipping as the car does, or from the car's chase camera
                eye = player.View == ViewMode.FirstPerson ? driving.Eye : driving.Chase.Position;
                lookAt = player.View == ViewMode.FirstPerson ? eye + driving.Forward : driving.Position + driving.Up * 1.4f + driving.Heading * 2f;
                if (player.View == ViewMode.FirstPerson)
                    up = driving.Up;
            }
            else if (player.View == ViewMode.FirstPerson)
            {
                eye = player.Eye;
                lookAt = eye + body.Heading;
            }
            else
            {
                eye = player.Drone.Position;
                lookAt = player.Eye;
            }
            // The meshes face +Z; a yaw of 0 here faces -Z (north), hence Pi - yaw
            _playerView.Position = body.Position;
            _playerView.Yaw = MathHelper.Pi - body.Yaw;
            _droneView.Position = player.Drone.Position;
            _droneView.Yaw = MathHelper.Pi - player.Drone.Yaw;

            // Neither camera sees the thing it's in: from inside your own head (or the drone), you'd only
            // see the inside of it. Turn round in your own view, though, and the drone's there, following.
            // From the bird's chase camera, both are.
            DrawFrom(eye, lookAt, up, you, clock, colorsOn, batch =>
            {
                // Driving, you're in the car, and your drone's put away
                if (driving == null && (followBird || player.View == ViewMode.Drone))
                    batch.Add(_playerView);
                if (driving == null && (followBird || player.View == ViewMode.FirstPerson))
                    batch.Add(_droneView);
                if (bird != null)
                {
                    var birdView = _birdFrames[BirdMesh.FrameAt(bird.FlapPhase)];
                    birdView.Position = bird.Position;
                    birdView.Yaw = MathHelper.Pi - bird.Yaw;
                    birdView.Pitch = -bird.Pitch;   // the mesh's X turn tips its nose down
                    batch.Add(birdView);
                }
            });
            if (View.HiddenCar is { } seat)
                DrawCockpit(seat, colorsOn);
        }

        // The car's inside, from the driver's seat (see CockpitMesh): over everything, so nothing outside it - a wall
        // you're scraping along - comes through, and from the eye's own frame, so it stays put on screen as the view
        // outside turns and tips.
        private void DrawCockpit(Car car, bool colorsOn)
        {
            _device.Clear(ClearOptions.DepthBuffer, RetroStyle.Background, 1f, 0);
            var view = Matrix.CreateLookAt(Vector3.Zero, Vector3.Backward, Vector3.Up);
            _steeringWheel.Transform = CockpitMesh.WheelAt(car.SteerAngle);
            _needles[CockpitMesh.Speedometer].Transform = CockpitMesh.NeedleAt(CockpitMesh.Speedometer, CockpitMesh.SpeedReading(car.Speed));
            _needles[CockpitMesh.RevCounter].Transform = CockpitMesh.NeedleAt(CockpitMesh.RevCounter, CockpitMesh.RevReading(car.Speed, !car.Flooded));
            _effect.View = view;
            _cockpitBatch.Begin(view, Projection);
            _cockpitBatch.Add(_cockpit);
            _cockpitBatch.Add(_steeringWheel);
            foreach (var needle in _needles)
                _cockpitBatch.Add(needle);
            _cockpitBatch.Draw(_device, _effect, RetroStyle.Background, colorsOn);
            _effect.View = ViewMatrix;
        }

        // The map from a camera at `eye`, looking at `lookAt` with `up` its up, with whatever `extra` adds to the batch
        // (characters, markers). `you` is where you are, for what depends on it (see WorldView.Collect).
        public void DrawFrom(Vector3 eye, Vector3 lookAt, Vector3 up, Vector3 you, float clock, bool colorsOn, Action<MeshBatch> extra = null)
        {
            // The far plane is where the fog ends: past it everything's the background's colour anyway, and the
            // batch leaves out whatever's beyond it
            _effect.View = ViewMatrix = Matrix.CreateLookAt(eye, lookAt, up);
            _effect.Projection = Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(FieldOfView), _device.Viewport.AspectRatio, NearPlane, FogEnd);

            View.Update(_device, eye, you);
            _batch.Begin(_effect.View, _effect.Projection);
            View.Collect(_batch, eye, you, clock);
            extra?.Invoke(_batch);
            View.DrawWindows(_device, _effect, eye, you, clock, RetroStyle.Background, colorsOn);
            _batch.Draw(_device, _effect, RetroStyle.Background, colorsOn);
        }

        private void Dampen(Player player)
        {
            var soaked = player.Wetness * Player.Height;
            foreach (var (part, bottom, top) in Soakable)
            {
                var wet = MathHelper.Clamp((soaked - bottom) / (top - bottom), 0f, 1f);
                for (var shade = 0; shade < 3; shade++)
                {
                    var dry = _playerColors[part + shade];
                    _playerView.SetColor(part + shade, Color.Lerp(dry, Color.Lerp(dry, Color.Black, 0.45f), wet));
                }
            }
        }

        public void Dispose()
        {
            View.Dispose();
            _effect.Dispose();
        }
    }
}
