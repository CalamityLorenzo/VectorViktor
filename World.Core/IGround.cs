using Microsoft.Xna.Framework;

namespace World.Core
{
    // Whatever a walker can stand on: the terrain now, a room's floor and ramps later. The character
    // controller only ever asks these questions, so it walks on either the same way.
    public interface IGround
    {
        // The height of the ground a walker with its feet at `feet` would stand on at (feet.X, feet.Z):
        // the highest surface no more than `reach` above feet.Y, so a room can tell a ramp you're on from
        // one passing overhead. A heightfield has only one surface and returns it whatever the reach.
        // Null where there's nothing to stand on at all (off the edge of the world).
        float? GroundBelow(Vector3 feet, float reach);

        // Which way that ground faces: Vector3.Up where it's level.
        Vector3 NormalAt(Vector3 feet);

        // Whether that ground is gentle enough to stand on and walk up. Steeper than this is a cliff.
        bool IsWalkable(Vector3 feet);
    }
}
