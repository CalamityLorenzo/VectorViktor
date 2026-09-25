using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using World.Buildings;
using World.Core;
using World.Rendering;

namespace Basic.Levels
{
    // Walk round a small house in the first person: a corridor with a door either side halfway along,
    // each door leading to a room of its own. Drawn to a small render target and scaled up with hard
    // pixels, for the chunky look of an 8/16 bit machine.
    // Up / W and Down / S walk (hold Shift to run), Left / Right turn, A / D sidestep. Walk into a door to go through it.
    // Space toggles colours / wireframe, L toggles the low-resolution look, F11 full screen, Escape exits.
    // BASIC_WORLD_SHOT takes a screenshot (see RetroGame).
    public class Game1 : RetroGame
    {
        private const int WindowWidth = 1440;
        private const int WindowHeight = 810;
        private const int LowResWidth = 480;      // 16:9, so 3 x fits the window and 4 x fills 1920 x 1080 (see RetroGame)
        private const int LowResHeight = 270;

        private const float EyeHeight = WorldConstants.EyeHeight;
        private const float PlayerRadius = WorldConstants.WalkerRadius;
        private const float PlayerHeight = WorldConstants.WalkerHeight;
        private const float WalkSpeed = WorldConstants.WalkSpeed;
        private const float RunMultiplier = WorldConstants.RunMultiplier;
        private const float TurnSpeed = WorldConstants.TurnSpeed;
        private const float DoorReach = 0.02f;    // how much closer than the player's radius to a wall still counts as walking into it
        private const float ArrivalDistance = 1.0f;   // how far in from a door you appear when you come through it

        private readonly Dictionary<string, RoomView> _rooms = new Dictionary<string, RoomView>();
        private readonly HouseLevel _level = HouseLevel.Create();
        private readonly string _startRoomOverride;
        private BasicEffect _basicEffect;

        private RoomView _room;                                       // the room you are standing in
        private List<RoomView> _visibleRooms = new List<RoomView>();  // that room and all those joined to it by openings
        private Vector3 _position;       // in the world, on the floor; the eye is EyeHeight above it
        private float _yaw;              // 0 looks north (-Z); increasing turns right

        // startRoom lets you begin at a room's first door instead of the corridor's end.
        public Game1(string startRoom = null) : base(WindowWidth, WindowHeight, LowResWidth, LowResHeight, colorsKey: Keys.Space)
        {
            _startRoomOverride = startRoom;
        }

        protected override void LoadWorld()
        {
            _basicEffect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true, World = Matrix.Identity };

            foreach (var spec in _level.Rooms.Values)
                _rooms[spec.Id] = new RoomView(spec, GraphicsDevice, MeshCache);

            if (_startRoomOverride != null && _level.Rooms.TryGetValue(_startRoomOverride, out var start))
            {
                if (start.Doors.Length > 0)
                    Arrive(start.Id, start.Doors[0].Id);
                else
                {
                    // No door to arrive by (a room only reached by stairs or a ladder): stand a little north
                    // of the middle, facing south - the middle itself is where the octagons' ladder and hatch are
                    EnterRoom(_rooms[start.Id]);
                    _position = start.WorldOffset + new Vector3(0f, 0f, -2f);
                    _yaw = MathHelper.Pi;
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

        // Up or down through a hatch. Rooms stacked on one footprint both Contain you, so UpdateCurrentRoom
        // can't tell them apart; instead, inside a hatch's outline you belong to whichever room's floor
        // you're standing on. You go up once the stair has brought you within a step of the floor above,
        // and down once the floor below you (the stair, or the floor far beneath) is more than a step
        // lower. Both tests compare the lower room's walk height against the upper floor, so they can't
        // flip back and forth.
        private void ClimbThroughHatches()
        {
            var spec = _room.Spec;
            var local = _position - spec.WorldOffset;

            foreach (var hatch in spec.CeilingHatches)
                if (hatch.Contains(local) && local.Y >= spec.Height + hatch.SlabThickness - RoomSpec.DefaultMaxStepUp)
                {
                    EnterRoom(_rooms[hatch.TargetRoom]);
                    SnapToFloor();
                    return;
                }

            foreach (var hatch in spec.FloorHatches)
            {
                if (!hatch.Contains(local))
                    continue;
                var below = _rooms[hatch.TargetRoom];
                var belowLocal = _position - below.Spec.WorldOffset;
                if (below.Spec.WalkHeightAt(belowLocal, belowLocal.Y) < belowLocal.Y - RoomSpec.DefaultMaxStepUp)
                {
                    EnterRoom(below);
                    SnapToFloor();
                    return;
                }
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

        protected override void UpdateWorld(GameTime gameTime, KeyboardState keyboard)
        {
            if (IsActive)
                Walk(keyboard, (float)gameTime.ElapsedGameTime.TotalSeconds);
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
            p = spec.PushOutOfProps(p, PlayerRadius);
            p = spec.KeepOutOfRamps(p, PlayerRadius, PlayerHeight);   // a staircase's side

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
            ClimbThroughHatches();
        }

        protected override void DrawWorld(GameTime gameTime)
        {
            var viewport = GraphicsDevice.Viewport;
            var eye = _position + Vector3.Up * EyeHeight;
            var heading = new Vector3(MathF.Sin(_yaw), 0f, -MathF.Cos(_yaw));
            _basicEffect.View = Matrix.CreateLookAt(eye, eye + heading, Vector3.Up);
            _basicEffect.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(70f), viewport.AspectRatio, 0.1f, 400f);   // the hangar is 140 m long

            foreach (var room in _visibleRooms)
                room.Draw(gameTime, GraphicsDevice, _basicEffect, BackgroundColor, ColorsOn);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _basicEffect?.Dispose();
            base.Dispose(disposing);
        }
    }
}
