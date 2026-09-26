using Microsoft.Xna.Framework;

namespace Basic.World
{
    // Where to start, and which way to face. With Above, you're dropped from that far above the ground and land
    // on the highest floor below that: to start up in a building rather than on the ground under it.
    public readonly record struct Start(Vector2 At, float Yaw, float Above = 0f);
}
