namespace MeshCore.Library
{
    // A contiguous run of primitives in a vertex buffer, drawn with one palette colour.
    public readonly record struct DrawRange(int Start, int Primitives, int ColorSlot);
}
