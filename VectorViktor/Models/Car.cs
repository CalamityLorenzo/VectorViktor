using Microsoft.Xna.Framework.Graphics;

namespace VectorViktor.Models
{
    internal struct Car
    {
        public int GridX, GridZ;   // current intersection, 0..GridSquares
        public int DirX, DirZ;     // travel direction: one of (±1,0) or (0,±1)
        public float Progress;     // 0..1 progress toward the next intersection
    }

    // Precomputed vertex/edge data for the car's body panels, built once per heading (the car
    // only ever faces one of 4 axis-aligned directions) since only its position — not its
    // shape — changes every frame. Cached separately per colour state, same as houses.
    internal struct CarGeometry
    {
        public VertexPositionColor[] BodyTris, BodyEdges;
        public VertexPositionColor[] CabinTris, CabinEdges;
        public VertexPositionColor[] WheelFrontATris, WheelFrontAEdges;
        public VertexPositionColor[] WheelFrontBTris, WheelFrontBEdges;
        public VertexPositionColor[] WheelRearATris, WheelRearAEdges;
        public VertexPositionColor[] WheelRearBTris, WheelRearBEdges;
    }

}
