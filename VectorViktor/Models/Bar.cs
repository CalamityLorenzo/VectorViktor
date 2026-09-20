using Microsoft.Xna.Framework;

namespace VectorViktor.Models
{
    internal struct Bar
    {
        public int CellX, CellZ;   // 0..GridSquares-1w
        public Color Color;
        public float Time;         // seconds elapsed in this cycle
        public float Duration;     // full grow+shrink cycle length
    }
}
