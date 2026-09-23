using Microsoft.Xna.Framework;
using System;
using World.Core;
using World.Core.Movement;

namespace World.Core.Tests
{
    // Terrains and grounds shaped for one thing each, and a way to run a walker over them.
    internal static class Grounds
    {
        public const float Tick = 1f / 60f;

        public const float East = MathHelper.PiOver2;   // a yaw facing +X

        public static Terrain Flat() => Terrain.FromFunction(64, 64, 1f, (x, z) => 0f);

        // Level for x < 0, then rising east at `degrees`, up to `cap` metres.
        public static Terrain SlopeFrom(float degrees, float cap = 30f) =>
            Terrain.FromFunction(64, 64, 1f, (x, z) => MathF.Min(cap, MathF.Max(0f, x) * MathF.Tan(MathHelper.ToRadians(degrees))));

        // A plateau `height` high for x < 0, dropping sheer (within one cell) to the level ground east of it.
        public static Terrain PlateauEdge(float height) => Terrain.FromFunction(64, 64, 1f, (x, z) => x < 0f ? height : 0f);

        public static void Run(CharacterController walker, MoveInput input, float seconds, IGround ground, Action<CharacterController> eachTick = null)
        {
            for (var t = 0; t < (int)MathF.Round(seconds / Tick); t++)
            {
                walker.Step(input, Tick, ground);
                eachTick?.Invoke(walker);
            }
        }

        public static MoveInput Forward(bool run = false) => new MoveInput(new Vector2(0f, 1f), Run: run);
    }

    // A true step, not a slope: level at 0 west of x = 0 and level at Height east of it. A heightfield can't
    // make one of these (its cells turn any step into a slope), but rooms will.
    internal sealed class StepGround : IGround
    {
        public float Height { get; }
        public StepGround(float height) => Height = height;
        public float? GroundBelow(Vector3 feet, float reach) => feet.X < 0f ? 0f : Height;
        public Vector3 NormalAt(Vector3 feet) => Vector3.Up;
        public bool IsWalkable(Vector3 feet) => true;
    }
}
