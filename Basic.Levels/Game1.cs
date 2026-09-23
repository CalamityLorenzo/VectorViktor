using MeshLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;

namespace Basic.Levels
{
    // Walk round a small house in the first person: a corridor with a door either side halfway along,
    // each door leading to a room of its own. Drawn to a small render target and scaled up with hard
    // pixels, for the chunky look of an 8/16 bit machine.
    // Up / W and Down / S walk (hold Shift to run), Left / Right turn, A / D sidestep. Walk into a door to go through it.
    // Space toggles colours / wireframe, L toggles the low-resolution look, F11 full screen, Escape exits.
    public class Game1 : Game
    {
        private const int WindowWidth = 1440;
        private const int WindowHeight = 810;
        private const int LowResWidth = 480;      // 16:9, so 3 x fits the window and 4 x fills 1920 x 1080
        private const int LowResHeight = 270;

        private const float EyeHeight = 1.6f;
        private const float PlayerRadius = 0.3f;
        private const float WalkSpeed = 2.5f;     // metres per second
        private const float RunMultiplier = 2.5f;
        private const float TurnSpeed = 2.0f;     // radians per second
        private const float DoorReach = 0.02f;    // how much closer than the player's radius to a wall still counts as walking into it
        private const float ArrivalDistance = 1.0f;   // how far in from a door you appear when you come through it

        private static readonly Color BackgroundColor = new Color(27, 13, 120);

        private readonly GraphicsDeviceManager _graphics;
        private readonly MeshCache _meshCache = new MeshCache();
        private readonly Dictionary<string, RoomView> _rooms = new Dictionary<string, RoomView>();
        private readonly HouseLevel _level = HouseLevel.Create();
        private readonly string _startRoomOverride;
        private BasicEffect _basicEffect;
        private RasterizerState _rasterizerState;
        private RenderTarget2D _lowRes;
        private SpriteBatch _spriteBatch;
        private KeyboardState _previousKeyboard;
        private bool _colorsOn = true;   // off = faces drawn in the background colour (wireframe look)
        private bool _lowResOn = true;

        private RoomView _room;                                       // the room you are standing in
        private List<RoomView> _visibleRooms = new List<RoomView>();  // that room and all those joined to it by openings
        private Vector3 _position;       // in the world, on the floor; the eye is EyeHeight above it
        private float _yaw;              // 0 looks north (-Z); increasing turns right

        // startRoom lets you begin at a room's first door instead of the corridor's end.
        public Game1(string startRoom = null)
        {
            _startRoomOverride = startRoom;
            _graphics = new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = WindowWidth,
                PreferredBackBufferHeight = WindowHeight,
                // Exclusive fullscreen leaves the process running (window gone, exe alive) after exit; use borderless.
                HardwareModeSwitch = false,
            };
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            _basicEffect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true, World = Matrix.Identity };
            _rasterizerState = new RasterizerState { CullMode = CullMode.None };
            _lowRes = new RenderTarget2D(GraphicsDevice, LowResWidth, LowResHeight, false, SurfaceFormat.Color, DepthFormat.Depth24);
            _spriteBatch = new SpriteBatch(GraphicsDevice);

            foreach (var spec in _level.Rooms.Values)
                _rooms[spec.Id] = new RoomView(spec, GraphicsDevice, _meshCache);

            if (_startRoomOverride != null && _level.Rooms.TryGetValue(_startRoomOverride, out var start))
            {
                if (start.Doors.Length > 0)
                    Arrive(start.Id, start.Doors[0].Id);
                else
                {
                    // No door to arrive by (a room only reached by stairs): stand in the middle, facing north
                    EnterRoom(_rooms[start.Id]);
                    _position = start.WorldOffset;
                    _yaw = 0f;
                    SnapToFloor();
                }
            }
            else
            {
                EnterRoom(_rooms[_level.StartRoom]);
                _position = _room.Spec.WorldOffset + _level.StartPosition;
                _yaw = _level.StartYaw;
                SnapToFloor();
            }
        }

        // Makes this the room you are in, and works out which others can be seen from it.
        private void EnterRoom(RoomView room)
        {
            _room = room;
            _visibleRooms = new List<RoomView> { room };
            for (var i = 0; i < _visibleRooms.Count; i++)
                foreach (var neighbour in _visibleRooms[i].Spec.Neighbours())
                {
                    var next = _rooms[neighbour];
                    if (!_visibleRooms.Contains(next))
                        _visibleRooms.Add(next);
                }
            Window.Title = "Basic.Levels - " + room.Spec.Name;
        }

        // Stands you on the floor: the room's, the step under your feet, or a ramp you've climbed onto.
        private void SnapToFloor()
        {
            var spec = _room.Spec;
            var local = _position - spec.WorldOffset;
            _position.Y = spec.WorldOffset.Y + spec.WalkHeightAt(local, local.Y);
        }

        // Walking through an opening takes you into the room beyond it
        private void UpdateCurrentRoom()
        {
            if (_room.Spec.Contains(_position - _room.Spec.WorldOffset))
                return;
            foreach (var room in _visibleRooms)
                if (room != _room && room.Spec.Contains(_position - room.Spec.WorldOffset))
                {
                    EnterRoom(room);
                    return;
                }
        }

        // Puts you just inside a room's door, facing into the room.
        private void Arrive(string roomId, string doorId)
        {
            EnterRoom(_rooms[roomId]);
            var door = _room.Spec.FindDoor(doorId);
            var inward = _room.Spec.Inward(door.WallIndex);
            _position = _room.Spec.WorldOffset + _room.Spec.WallPoint(door.WallIndex, door.Offset) + inward * ArrivalDistance;
            _yaw = MathF.Atan2(inward.X, -inward.Z);
            SnapToFloor();
        }

        protected override void Update(GameTime gameTime)
        {
            var keyboard = Keyboard.GetState();
            var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || keyboard.IsKeyDown(Keys.Escape))
                Exit();

            if (keyboard.IsKeyDown(Keys.F11) && _previousKeyboard.IsKeyUp(Keys.F11))
                _graphics.ToggleFullScreen();
            if (keyboard.IsKeyDown(Keys.Space) && _previousKeyboard.IsKeyUp(Keys.Space))
                _colorsOn = !_colorsOn;
            if (keyboard.IsKeyDown(Keys.L) && _previousKeyboard.IsKeyUp(Keys.L))
                _lowResOn = !_lowResOn;

            if (IsActive)
                Walk(keyboard, dt);

            _previousKeyboard = keyboard;
            base.Update(gameTime);
        }

        private void Walk(KeyboardState keyboard, float dt)
        {
            var turn = Axis(keyboard, Keys.Right, Keys.Left);
            var forward = Axis(keyboard, Keys.Up, Keys.Down) + Axis(keyboard, Keys.W, Keys.S);
            var sideways = Axis(keyboard, Keys.D, Keys.A);

            _yaw = MathHelper.WrapAngle(_yaw + turn * TurnSpeed * dt);

            var heading = new Vector3(MathF.Sin(_yaw), 0f, -MathF.Cos(_yaw));
            var right = new Vector3(MathF.Cos(_yaw), 0f, MathF.Sin(_yaw));
            var step = heading * MathHelper.Clamp(forward, -1f, 1f) + right * sideways;
            if (step.LengthSquared() > 1f)
                step.Normalize();   // so going diagonally isn't faster
            if (step == Vector3.Zero)
                return;

            var spec = _room.Spec;
            var speed = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift) ? WalkSpeed * RunMultiplier : WalkSpeed;

            // Collide in the room's own coordinates
            var p = _position - spec.WorldOffset + step * speed * dt;
            p = spec.KeepInside(p, PlayerRadius);
            p = _room.PushOutOfProps(p, PlayerRadius);

            // Pressed against a wall within a door's width: go through
            foreach (var door in spec.Doors)
            {
                if (spec.DistanceToWall(door.WallIndex, p) <= PlayerRadius + DoorReach &&
                    MathF.Abs(spec.AlongWall(door.WallIndex, p) - door.Offset) < RoomSpec.DoorWidth / 2f)
                {
                    Arrive(door.TargetRoom, door.TargetDoor);
                    return;
                }
            }

            _position = p + spec.WorldOffset;
            UpdateCurrentRoom();
            SnapToFloor();
        }

        private static float Axis(KeyboardState keyboard, Keys positive, Keys negative) =>
            (keyboard.IsKeyDown(positive) ? 1f : 0f) - (keyboard.IsKeyDown(negative) ? 1f : 0f);

        protected override void Draw(GameTime gameTime)
        {
            var target = _lowResOn ? _lowRes : null;
            GraphicsDevice.SetRenderTarget(target);
            GraphicsDevice.Clear(BackgroundColor);

            // SpriteBatch leaves these changed, so set them each frame
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.RasterizerState = _rasterizerState;

            var viewport = GraphicsDevice.Viewport;
            var eye = _position + Vector3.Up * EyeHeight;
            var heading = new Vector3(MathF.Sin(_yaw), 0f, -MathF.Cos(_yaw));
            _basicEffect.View = Matrix.CreateLookAt(eye, eye + heading, Vector3.Up);
            _basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(70f), viewport.AspectRatio, 0.1f, 400f);   // the hangar is 140 m long

            foreach (var room in _visibleRooms)
                room.Draw(gameTime, GraphicsDevice, _basicEffect, BackgroundColor, _colorsOn);

            if (_lowResOn)
            {
                GraphicsDevice.SetRenderTarget(null);
                GraphicsDevice.Clear(BackgroundColor);

                // Largest whole-number scale that fits, centred, so every low-res pixel is the same size
                var back = GraphicsDevice.PresentationParameters;
                var scale = Math.Max(1, Math.Min(back.BackBufferWidth / LowResWidth, back.BackBufferHeight / LowResHeight));
                var width = LowResWidth * scale;
                var height = LowResHeight * scale;
                var destination = new Rectangle((back.BackBufferWidth - width) / 2, (back.BackBufferHeight - height) / 2, width, height);

                _spriteBatch.Begin(samplerState: SamplerState.PointClamp);
                _spriteBatch.Draw(_lowRes, destination, Color.White);
                _spriteBatch.End();
            }

            base.Draw(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _meshCache.Dispose();
                _basicEffect?.Dispose();
                _rasterizerState?.Dispose();
                _lowRes?.Dispose();
                _spriteBatch?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
