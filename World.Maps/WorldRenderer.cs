using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Core.Animation;
using World.Core.Characters;
using World.Core.Vehicles;
using World.Rendering;

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

        public const float FieldOfView = 70f, NearPlane = 0.1f;   // degrees up and down, and metres

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

        // The televisions' pictures (see ScreenSpec): static, and the feeds, each what a camera sees, drawn into a small
        // render target of its own before each frame (see Feed, DrawFeeds)
        public const int FeedWidth = 96, FeedHeight = 72;
        public const string DroneChannel = "drone";   // what the player's drone sees, where a game feeds it
        private readonly BasicEffect _screenEffect;
        private readonly StaticPicture _static;
        private readonly Dictionary<string, (RenderTarget2D target, Func<CameraView?> camera, Action<MeshBatch> extra)> _feeds =
            new Dictionary<string, (RenderTarget2D, Func<CameraView?>, Action<MeshBatch>)>();
        private bool _feeding;   // drawing a feed: then the screens are left out, or one would be drawing into itself

        public WorldView View { get; }

        // The camera the last frame was drawn with: for drawing more over it (lines, markers) or picking with the mouse.
        public Matrix ViewMatrix { get; private set; }
        public Matrix Projection { get; private set; }

        // How many meshes the last frame drew, left out and made draw calls of.
        public MeshBatch Batch => _batch;

        // For a tool that wants to see the map differently from the game (the map studio): without the fog; out to a
        // farther far plane; through a projection of its own (an orthographic one, for flat views); and with the terrain
        // built round a point of its own choosing rather than round the camera (a flat view's camera stands well back).
        public bool Fog { get; set; } = true;
        public float FarPlane { get; set; } = FogEnd;
        public Matrix? ProjectionOverride { get; set; }
        public Vector3? TerrainCentre { get; set; }

        // `drawDistance` is how far out the terrain's built: as far as the fog lets you see, unless a tool wants to see further.
        // `terrain`: the terrain as another renderer drew it, if the ground's the same (see WorldView.ReleaseTerrain).
        public WorldRenderer(BuiltWorld built, GraphicsDevice device, MeshCache cache, float drawDistance = DrawDistance, TerrainView terrain = null)
        {
            _device = device;
            _ground = built.Ground;
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true, World = Matrix.Identity,
                FogEnabled = true, FogColor = RetroStyle.Background.ToVector3(), FogStart = FogStart, FogEnd = FogEnd,
            };
            // The terrain's built a chunk at a time round the camera, out to where the fog has hidden it all
            View = new WorldView(built, device, cache, drawDistance, terrain);
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
            _screenEffect = new BasicEffect(device)
            {
                TextureEnabled = true, VertexColorEnabled = false, LightingEnabled = false, World = Matrix.Identity,
                FogEnabled = true, FogColor = RetroStyle.Background.ToVector3(), FogStart = FogStart, FogEnd = FogEnd,
            };
            _static = new StaticPicture(device);
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
                lookAt = eye + player.Looking;   // tilted, if you've tilted it (see Player.LookUp)
            }
            else
            {
                eye = player.Drone.Position;
                lookAt = player.Eye;
            }
            PlacePlayer(player);
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

        // The walker where the player is, for a batch: seen from the drone, the bird, or a feed (see Feed).
        public void AddPlayer(MeshBatch batch, Player player)
        {
            PlacePlayer(player);
            batch.Add(_playerView);
        }

        private void PlacePlayer(Player player)
        {
            // The meshes face +Z; a yaw of 0 here faces -Z (north), hence Pi - yaw
            _playerView.Position = player.Body.Position;
            _playerView.Yaw = MathHelper.Pi - player.Body.Yaw;
        }

        // A channel (see ScreenSpec.Channel) that shows what a camera sees, drawn afresh before each frame by DrawFeeds, with
        // whatever `extra` adds (characters: the world alone has none). Screens tuned to a channel with no feed show static.
        public void Feed(string channel, Func<CameraView?> camera, Action<MeshBatch> extra = null)
        {
            if (_feeds.TryGetValue(channel, out var old))
                old.target.Dispose();
            var target = new RenderTarget2D(_device, FeedWidth, FeedHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8,
                                            0, RenderTargetUsage.PreserveContents);
            _feeds[channel] = (target, camera, extra);
        }

        // Before a frame: each feed's picture, into its render target; then back to the render target that was set,
        // cleared. `you` is where you are (see WorldView.Collect).
        public void DrawFeeds(Vector3 you, float clock, bool colorsOn)
        {
            if (_feeds.Count == 0)
                return;
            var previous = _device.GetRenderTargets();
            _feeding = true;
            foreach (var (target, camera, extra) in _feeds.Values)
            {
                if (camera() is not { } view)
                    continue;
                _device.SetRenderTarget(target);
                _device.Clear(RetroStyle.Background);
                DrawFrom(view.Eye, view.Eye + view.Forward, view.Up, you, clock, colorsOn, extra);
            }
            _feeding = false;
            _device.SetRenderTargets(previous);
            _device.Clear(RetroStyle.Background);
        }

        // What a screen tuned to `channel` shows: its feed's latest picture, or static.
        private Texture2D Picture(string channel) =>
            _feeds.TryGetValue(channel, out var feed) ? feed.target : _static.Texture;

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
            _effect.Projection = Projection = ProjectionOverride ?? Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(FieldOfView), _device.Viewport.AspectRatio, NearPlane, FarPlane);
            _effect.FogEnabled = _screenEffect.FogEnabled = Fog;

            View.Update(_device, TerrainCentre ?? eye, you);
            _batch.Begin(_effect.View, _effect.Projection);
            // Nothing narrower than a pixel of the picture it's drawn into (see MeshBatch.SmallestSeen)
            _batch.SmallestSeen = 2f * MathF.Tan(MathHelper.ToRadians(FieldOfView) / 2f) / _device.Viewport.Height;
            View.Collect(_batch, eye, you, clock);
            extra?.Invoke(_batch);
            View.DrawWindows(_device, _effect, eye, you, clock, RetroStyle.Background, colorsOn);
            _batch.Draw(_device, _effect, RetroStyle.Background, colorsOn);
            if (!_feeding)
            {
                _static.Update(clock);
                _screenEffect.View = _effect.View;
                _screenEffect.Projection = _effect.Projection;
                View.DrawScreens(_device, _screenEffect, eye, Picture);
            }
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
            _screenEffect.Dispose();
            _static.Dispose();
            foreach (var (target, _, _) in _feeds.Values)
                target.Dispose();
        }
    }
}
