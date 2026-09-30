using Microsoft.Xna.Framework;

namespace World.Maps
{
    // Where to start, and which way to face. With Above, you're dropped from that far above the ground and land
    // on the highest floor below that: to start up in a building rather than on the ground under it. InCar, you
    // start in the driver's seat of the car parked there, or of one brought there for you if none is.
    public readonly record struct Start(Vector2 At, float Yaw, float Above = 0f, bool InCar = false);

    // A car left somewhere (see World.Core.Vehicles.Car), facing `Yaw`: to get into and drive.
    public readonly record struct ParkedCar(Vector2 At, float Yaw);
}
