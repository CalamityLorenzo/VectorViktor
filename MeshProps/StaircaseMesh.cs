using MeshCore.Library;
using MeshProps.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshProps
{
    // An open-riser wooden staircase (see StaircaseBuilder): treads on two diagonal stringers, no
    // risers, no banister, with an optional carpet runner inset on each tread. Climbs from the
    // origin along +Z. For anything beyond a straight run or a single quarter turn — a longer
    // dog-leg, custom width/rise/run — call StaircaseBuilder.Build directly with your own Flight[].
    public static class StaircaseMesh
    {
        public static Color[] Palette(Color wood, Color carpet) => StaircaseBuilder.Palette(wood, carpet);

        public static MeshData BuildStraight(GraphicsDevice device, int steps, bool carpet = true) =>
            StaircaseBuilder.Build(device, new[] { new StaircaseBuilder.Flight(steps) }, carpet: carpet);

        public static MeshData BuildQuarterTurn(GraphicsDevice device, int stepsBeforeTurn, int stepsAfterTurn, StairTurn turn, bool carpet = true) =>
            StaircaseBuilder.Build(device, new[]
            {
                new StaircaseBuilder.Flight(stepsBeforeTurn, turn),
                new StaircaseBuilder.Flight(stepsAfterTurn),
            }, carpet: carpet);
    }
}
