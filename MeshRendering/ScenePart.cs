using MeshCore.Library;
using Microsoft.Xna.Framework;

namespace MeshRendering
{
    // Something drawn that can move by itself: its mesh, and where it is so many seconds in - in the world, or in
    // its own space, for what's seen through a window (see Window). SeenWithin: how far off it can be seen at all
    // (see MeshInstance.SeenWithin).
    public readonly record struct ScenePart(MeshSource Mesh, Func<float, Matrix> At, float SeenWithin = float.PositiveInfinity)
    {
        // A part that never moves
        public static ScenePart Still(MeshSource mesh, Matrix at) => new ScenePart(mesh, _ => at);
    }
}
