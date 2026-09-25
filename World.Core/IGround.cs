using Microsoft.Xna.Framework;

namespace World.Core
{
    // Whatever a walker can stand on: the terrain, and the floors, stairs and walls of buildings on it. The
    // character controller only ever asks these questions, so it walks on any of them the same way.
    //
    // The first three are all a heightfield needs. The rest are for things with walls and more than one
    // level (see World.Buildings); their defaults are what open ground with nothing standing on it says.
    public interface IGround
    {
        // The height of the ground a walker with its feet at `feet` would stand on at (feet.X, feet.Z):
        // the highest surface no more than `reach` above feet.Y, so a building can tell the floor you're
        // on from the one overhead. A heightfield has only one surface and returns it whatever the reach.
        // A ladder may lift you further than `reach` (see StepUpAt). Null where there's nothing to stand
        // on at all (off the edge of the world).
        float? GroundBelow(Vector3 feet, float reach);

        // Which way that ground faces: Vector3.Up where it's level.
        Vector3 NormalAt(Vector3 feet);

        // Whether that ground is gentle enough to stand on and walk up. Steeper than this is a cliff.
        bool IsWalkable(Vector3 feet);

        // How far up a walker at `feet` may step in one go: `step` almost everywhere, but more on a ladder,
        // which climbs steeply enough that each stride rises further than a step.
        float StepUpAt(Vector3 feet, float step) => step;

        // A walker - an upright cylinder `radius` round and `height` tall, feet at `feet` - pushed back out of
        // any wall it's walked into, the shortest way. It slides along a wall it walks into at an angle.
        Vector3 KeepOut(Vector3 feet, float radius, float height) => feet;

        // The underside of whatever is overhead of a walker at `feet` (a ceiling), or null in the open.
        float? CeilingAbove(Vector3 feet) => null;

        // Whether an upright box standing on `bottomCentre` would be partly inside a wall.
        bool Obstructs(Vector3 bottomCentre, Vector3 size) => false;

        // Looking from `from` towards `to`: `to`, or if a wall or ceiling is in the way, the furthest point
        // along the line short of it that can still be seen from `from`.
        Vector3 ClearLine(Vector3 from, Vector3 to) => to;

        // The height of the water's surface at (point.X, point.Z), if there's water there; null where it's dry.
        float? WaterAt(Vector3 point) => null;
    }
}
