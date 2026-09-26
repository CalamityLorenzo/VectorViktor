using MeshCore.Library;
using MeshProps;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using World.Core;
using World.Core.Characters;

namespace Basic.World
{
    // Draws the built world as seen by the player, from their own eyes or from the drone, or from the camera chasing
    // the bird (see Bird.Chase): the fog, the camera, the world (see WorldView), the player or the drone (or both, from
    // the bird), the bird, and the windows that look onto elsewhere - into whatever render
    // target is set. It has no input and no clock of its own, so a game and the benchmark (see Basic.World.Benchmark)
    // draw exactly the same frame.
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
        private readonly MeshInstance[] _birdFrames = new MeshInstance[BirdMesh.Frames];   // one for each step through a flap
        private readonly IGround _ground;
        private readonly Color[] _playerColors;   // dry: as drawn, they're darker where wet

        public WorldView View { get; }

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
            Vector3 eye, lookAt;
            if (followBird && bird != null)
            {
                (eye, lookAt) = bird.Chase(_ground);
                you = eye;
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
            // The far plane is where the fog ends: past it everything's the background's colour anyway, and the
            // batch leaves out whatever's beyond it
            _effect.View = Matrix.CreateLookAt(eye, lookAt, Vector3.Up);
            _effect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(FieldOfView), _device.Viewport.AspectRatio, NearPlane, FogEnd);

            // The meshes face +Z; a yaw of 0 here faces -Z (north), hence Pi - yaw
            _playerView.Position = body.Position;
            _playerView.Yaw = MathHelper.Pi - body.Yaw;
            _droneView.Position = player.Drone.Position;
            _droneView.Yaw = MathHelper.Pi - player.Drone.Yaw;

            // Neither camera sees the thing it's in: from inside your own head (or the drone), you'd only
            // see the inside of it. Turn round in your own view, though, and the drone's there, following.
            // From the bird's chase camera, both are.
            View.Update(_device, eye, you);
            _batch.Begin(_effect.View, _effect.Projection);
            View.Collect(_batch, eye, you, clock);
            if (followBird || player.View == ViewMode.Drone)
                _batch.Add(_playerView);
            if (followBird || player.View == ViewMode.FirstPerson)
                _batch.Add(_droneView);
            if (bird != null)
            {
                var birdView = _birdFrames[BirdMesh.FrameAt(bird.FlapPhase)];
                birdView.Position = bird.Position;
                birdView.Yaw = MathHelper.Pi - bird.Yaw;
                birdView.Pitch = -bird.Pitch;   // the mesh's X turn tips its nose down
                _batch.Add(birdView);
            }
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
