using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace MeshRendering
{
    // A camera that flies anywhere, belonging to nothing in the world: W, A, S, D to fly, Space and Ctrl to rise and
    // sink, Shift for speed, and the mouse, with its right button held, to look. On a controller: the left stick flies,
    // the right stick looks, the right and left shoulders rise and sink, the right trigger for speed.
    public sealed class FreeCamera
    {
        public const float Speed = 6f, FastSpeed = 24f;   // metres per second
        public const float MouseTurn = 0.004f;            // radians per pixel
        public const float PadTurn = 2.5f;                // radians per second, the right stick pushed all the way

        public Vector3 Position { get; set; }
        public float Yaw { get; set; }     // 0 faces north (-Z), as the walker's does
        public float Pitch { get; set; }   // up is more

        public Vector3 Forward => new Vector3(MathF.Sin(Yaw) * MathF.Cos(Pitch), MathF.Sin(Pitch), -MathF.Cos(Yaw) * MathF.Cos(Pitch));
        public Vector3 Right => new Vector3(MathF.Cos(Yaw), 0f, MathF.Sin(Yaw));

        // Looking from `eye` at `target`.
        public void LookFrom(Vector3 eye, Vector3 target)
        {
            Position = eye;
            var along = target - eye;
            Yaw = MathF.Atan2(along.X, -along.Z);
            Pitch = MathF.Atan2(along.Y, new Vector2(along.X, along.Z).Length());
        }

        // `turn` is how far the mouse moved (pixels) with the right button held, or nothing.
        public void Step(KeyboardState keys, GamePadState pad, Vector2 turn, float dt)
        {
            var look = pad.ThumbSticks.Right;
            Yaw += turn.X * MouseTurn + look.X * PadTurn * dt;
            Pitch = Math.Clamp(Pitch - turn.Y * MouseTurn + look.Y * PadTurn * dt, -1.5f, 1.5f);

            float Axis(Keys plus, Keys minus) => (keys.IsKeyDown(plus) ? 1f : 0f) - (keys.IsKeyDown(minus) ? 1f : 0f);
            float Pair(Buttons plus, Buttons minus) => (pad.IsButtonDown(plus) ? 1f : 0f) - (pad.IsButtonDown(minus) ? 1f : 0f);
            var stick = pad.ThumbSticks.Left;
            var move = Forward * (Axis(Keys.W, Keys.S) + stick.Y) + Right * (Axis(Keys.D, Keys.A) + stick.X) +
                       Vector3.Up * (Axis(Keys.Space, Keys.LeftControl) + Pair(Buttons.RightShoulder, Buttons.LeftShoulder));
            if (move.LengthSquared() > 1f)
                move.Normalize();   // so going diagonally isn't faster; a stick pushed part way goes slower
            var fast = keys.IsKeyDown(Keys.LeftShift) || pad.Triggers.Right > 0.5f;
            Position += move * (fast ? FastSpeed : Speed) * dt;
        }
    }
}
