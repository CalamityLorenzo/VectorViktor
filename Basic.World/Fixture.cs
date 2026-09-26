using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;

namespace Basic.World
{
    // Something built into the world: its mesh, and where it goes. With ShownTo, it's only drawn while that's true
    // of where you are (your feet): to swap one thing for another as you come up to it.
    public readonly record struct Fixture(MeshSource Mesh, Matrix Transform, Func<Vector3, bool> ShownTo = null);
}
