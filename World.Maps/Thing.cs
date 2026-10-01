using MeshCore.Library;
using World.Core.Physics;

namespace World.Maps
{
    // A body and how it's drawn: its mesh, and how far it's turned about the vertical inside its box - only by a
    // half turn, so the box it fills is the same. From is the district it came from, which WorldBuilder notes (for
    // a tool to tell one district's things from another's).
    public record Thing(Body Body, MeshSource Mesh, float Turn = 0f)
    {
        public IDistrict From { get; init; }
    }
}
