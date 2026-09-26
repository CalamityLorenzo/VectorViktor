using MeshCore.Library;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using Xunit;

namespace Meshes.Tests
{
    public class MeshBuilderTests
    {
        private static readonly Vector3 A = new(0f, 0f, 0f), B = new(1f, 0f, 0f), C = new(1f, 1f, 0f), D = new(0f, 1f, 0f);

        [Fact]
        public void Each_colour_is_one_run_of_faces_however_the_faces_were_added()
        {
            var mesh = new MeshBuilder();
            mesh.AddTri(2, A, B, C);
            mesh.AddQuad(0, A, B, C, D);
            mesh.AddTri(2, A, C, D);
            var built = mesh.Build(null);

            Assert.Equal(new[] { (0, 2), (2, 2) }, built.SolidRanges.Select(r => (r.ColorSlot, r.Primitives)));
            Assert.Equal(6 + 6, built.SolidVertexCount);
            Assert.Equal(3, built.PaletteSize);
        }

        [Fact]
        public void Bounds_go_round_the_faces_and_the_edges()
        {
            var mesh = new MeshBuilder();
            mesh.AddTri(0, A, B, C);
            mesh.AddLine(new Vector3(-2f, 0f, 0f), new Vector3(0f, 5f, 3f));
            var built = mesh.Build(null);

            Assert.Equal(new Vector3(-2f, 0f, 0f), built.Bounds.Min);
            Assert.Equal(new Vector3(1f, 5f, 3f), built.Bounds.Max);
        }

        [Fact]
        public void A_mesh_with_nothing_in_it_has_no_buffers_and_no_bounds_to_speak_of()
        {
            var built = new MeshBuilder().Build(null);

            Assert.Equal(0, built.SolidVertexCount);
            Assert.Equal(0, built.EdgeVertexCount);
            Assert.Equal(0, built.PaletteSize);
            Assert.Equal(BoundingBox.CreateFromPoints(new[] { Vector3.Zero }), built.Bounds);
        }

        [Fact]
        public void A_box_is_five_faces_three_shades_and_twelve_edges()
        {
            var mesh = new MeshBuilder();
            mesh.AddBox(0, Vector3.Zero, 2f, 1f, 1f);
            var built = mesh.Build(null);

            Assert.Equal(new[] { 0, 1, 2 }, built.SolidRanges.Select(r => r.ColorSlot));
            Assert.Equal(5 * 6, built.SolidVertexCount);
            Assert.Equal(12 * 2, built.EdgeVertexCount);
        }

        [Fact]
        public void A_footprint_is_kept_only_if_asked_for()
        {
            var mesh = new MeshBuilder();
            mesh.AddQuad(0, new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f), new Vector3(1f, 0f, 1f), new Vector3(0f, 0f, 1f));   // lying flat, as the ground does
            Assert.Throws<InvalidOperationException>(() => mesh.Build(null).Covers(0.5f, 0.5f));
            Assert.True(mesh.Build(null, keepFootprint: true).Covers(0.5f, 0.5f));
            Assert.False(mesh.Build(null, keepFootprint: true).Covers(1.5f, 0.5f));
        }

#if DEBUG
        // Checked in debug builds only: the check costs, and the meshes are all known to pass it (see MeshCatalogueTests)
        [Fact]
        public void A_polygon_whose_fan_folds_back_is_refused()
        {
            var dart = new[] { new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 0f), new Vector3(1f, 1f, 0f), new Vector3(3f, 2f, 0f) };   // reflex at the third point, which the first can't see past

            Assert.Throws<ArgumentException>(() => new MeshBuilder().AddPolygon(0, dart));
        }

        [Fact]
        public void A_convex_polygon_and_a_polygon_seen_from_its_first_point_are_accepted()
        {
            var mesh = new MeshBuilder();
            mesh.AddPolygon(0, A, B, C, D);
            mesh.AddPolygon(0, new Vector3(0f, 0f, 0f), new Vector3(2f, 0f, 0f), new Vector3(2f, 2f, 0f), new Vector3(1f, 1f, 0f), new Vector3(0f, 2f, 0f));   // concave, but a fan from its first point still covers it
            mesh.AddPolygon(0, A, A, B);   // no area at all: nothing to fold
        }
#endif
    }
}
