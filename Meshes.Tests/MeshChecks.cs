using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using Xunit;

namespace Meshes.Tests
{
    // What every mesh has to be, whichever builder made it: built with no graphics device (see MeshData.Headless).
    internal static class MeshChecks
    {
        public static void IsSound(string name, MeshData mesh, Color[] palette = null)
        {
            Assert.True(mesh.IsHeadless, name);
            Assert.True(mesh.SolidVertexCount % 3 == 0, $"{name}: {mesh.SolidVertexCount} face vertices isn't whole triangles");
            Assert.True(mesh.EdgeVertexCount % 2 == 0, $"{name}: {mesh.EdgeVertexCount} edge vertices isn't whole lines");
            Assert.True(mesh.SolidVertexCount + mesh.EdgeVertexCount > 0 || mesh.Outline != null, $"{name} has nothing in it");

            // Nothing NaN or infinite, which would vanish or smear across the screen
            foreach (var vertex in mesh.HeadlessSolids.Concat(mesh.HeadlessEdges))
                Assert.True(IsFinite(vertex.Position), $"{name} has a vertex at {vertex.Position}");
            Assert.True(IsFinite(mesh.Bounds.Min) && IsFinite(mesh.Bounds.Max), $"{name}'s bounds are {mesh.Bounds}");
            Assert.True(mesh.Bounds.Min.X <= mesh.Bounds.Max.X && mesh.Bounds.Min.Y <= mesh.Bounds.Max.Y && mesh.Bounds.Min.Z <= mesh.Bounds.Max.Z, $"{name}'s bounds are {mesh.Bounds}");

            // Every vertex is inside the box culling uses, or it would be culled while still in view
            foreach (var vertex in mesh.HeadlessSolids.Concat(mesh.HeadlessEdges))
                Assert.True(mesh.Bounds.Contains(vertex.Position) != ContainmentType.Disjoint, $"{name}'s vertex {vertex.Position} is outside its bounds {mesh.Bounds}");

            // Not every triangle flat: that's a mesh built on top of itself
            if (mesh.SolidVertexCount > 0)
                Assert.True(Enumerable.Range(0, mesh.SolidVertexCount / 3).Any(t => Area(mesh, t) > 1e-9f), $"{name}'s faces all have no area");

            if (palette != null)
                Assert.True(palette.Length >= mesh.PaletteSize, $"{name}: its faces use {mesh.PaletteSize} colours, its palette has {palette.Length}");
        }

        private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);

        private static float Area(MeshData mesh, int triangle)
        {
            var s = mesh.HeadlessSolids;
            return Vector3.Cross(s[triangle * 3 + 1].Position - s[triangle * 3].Position, s[triangle * 3 + 2].Position - s[triangle * 3].Position).LengthSquared();
        }
    }
}
