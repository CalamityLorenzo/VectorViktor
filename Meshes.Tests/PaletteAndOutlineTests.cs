using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using System.Text;
using Xunit;

namespace Meshes.Tests
{
    // The palette shader is built in (see PaletteEffect), and outlines hide what's behind them only where it shows (see
    // OutlineData, OutlineView).
    public class PaletteAndOutlineTests
    {
        [Fact]
        public void The_compiled_palette_shader_is_built_in()
        {
            var shader = PaletteEffect.CompiledShader();
            Assert.True(shader.Length > 100);
            Assert.Equal("MGFX", Encoding.ASCII.GetString(shader, 0, 4));   // MonoGame's compiled effect
        }

        // Two balls, one in front of the other as seen from +Z
        private static OutlineData TwoBalls()
        {
            var mesh = new MeshBuilder();
            foreach (var centre in new[] { new Vector3(0f, 0f, 1f), new Vector3(0.6f, 0f, -1f) })
            {
                const int around = 16, up = 8;
                Vector3 At(int k, int j)
                {
                    var (a, b) = (k * MathHelper.TwoPi / around, (j / (float)up - 0.5f) * MathHelper.Pi);
                    return centre + new Vector3(MathF.Cos(b) * MathF.Cos(a), MathF.Sin(b), MathF.Cos(b) * MathF.Sin(a));
                }
                for (var k = 0; k < around; k++)
                    for (var j = 0; j < up; j++)
                        foreach (var (x, y, z) in new[] { (At(k, j), At(k + 1, j), At(k + 1, j + 1)), (At(k, j), At(k + 1, j + 1), At(k, j + 1)) })
                            if (Vector3.Cross(y - x, z - x).LengthSquared() > 1e-10f)
                            {
                                mesh.AddTri(0, x, y, z);
                                mesh.AddOutlineTri(x, y, z, centre);
                            }
            }
            return mesh.Build(null).Outline!;
        }

        [Fact]
        public void Close_to_the_front_ball_hides_the_back_balls_outline_where_it_crosses_it()
        {
            var balls = TwoBalls();
            Assert.Equal(2, balls.ConvexBlobs);
            var eye = new Vector3(0f, 0f, 6f);
            var (hidden, all) = (new VertexPosition[balls.MaxVertices], new VertexPosition[balls.MaxVertices]);
            var kept = balls.GetOutline(eye, hidden, hiding: true);
            var every = balls.GetOutline(eye, all, hiding: false);
            Assert.True(kept < every, $"{kept / 2} lines kept of {every / 2}: none hidden");

            // Close to, a view tests what it hides; far off, it doesn't
            var view = balls.CreateView();
            view.Update(eye);
            Assert.Equal(kept, view.VertexCount);
            var far = eye * 40f;
            view.Update(far);
            Assert.Equal(balls.GetOutline(far, all, hiding: false), view.VertexCount);
        }

        [Fact]
        public void A_view_works_its_outline_out_again_only_when_the_eye_has_moved_enough_to_show()
        {
            var view = TwoBalls().CreateView();
            view.Update(new Vector3(0f, 0f, 20f));
            var version = view.Version;
            view.Update(new Vector3(0.01f, 0f, 20f));   // a centimetre, at twenty metres
            Assert.Equal(version, view.Version);
            view.Update(new Vector3(1f, 0f, 20f));
            Assert.True(view.Version > version);
        }
    }
}
