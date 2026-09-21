namespace BasicTests.Meshes
{
    // A contiguous run of primitives in a vertex buffer, drawn with one palette colour.
    readonly record struct DrawRange(int Start, int Primitives, int ColorSlot);
}
