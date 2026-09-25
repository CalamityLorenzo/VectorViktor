using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MeshCore.Library
{
    // A mesh to draw, as the cache knows it: its key (the same key is the same mesh, built once), how to build it
    // the first time it's asked for, and the colours to draw it in (see MeshData.PaletteSize).
    public sealed record MeshSource(string Key, Func<GraphicsDevice, MeshData> Build, Color[] Palette);
}
