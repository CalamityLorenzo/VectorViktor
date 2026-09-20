using Microsoft.Xna.Framework.Graphics;

namespace VectorViktor.Models
{
    internal struct House
    {
        public int GridX, GridZ;   // corner of the house's footprint, in grid cells
        public int Rotation;       // 0..3, one 90° step each: 0°, 90°, 180°, 270°
    }

    // Precomputed vertex/edge data for a single house, built once since houses never move.
    // Cached separately per colour state so toggling colours (C key) still works without
    // rebuilding geometry every frame.
    internal struct HouseGeometry
    {
        public VertexPositionColor[] WallTris, WallEdges;
        public VertexPositionColor[] RoofTris, RoofEdges;
        public VertexPositionColor[] DoorTris, DoorEdges;
        public VertexPositionColor[] WindowTris, WindowEdges;
        public VertexPositionColor[] WindowPlusEdges;
        public VertexPositionColor[] ChimneyTris, ChimneyEdges;
    }
}
