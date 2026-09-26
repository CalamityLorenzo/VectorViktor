using MeshCore.Library;
using World.Core.Physics;

namespace Basic.World
{
    // A body and how it's drawn: its mesh, and how far it's turned about the vertical inside its box - only by a
    // half turn, so the box it fills is the same.
    public record Thing(Body Body, MeshSource Mesh, float Turn = 0f);
}
