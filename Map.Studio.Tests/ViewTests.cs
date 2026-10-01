using MapStudio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using World.Core;
using Xunit;

namespace Map.Studio.Tests
{
    // The camera's views, picking with the mouse, and the move arrows
    public class ViewTests
    {
        private static void Arrive(StudioCamera camera)
        {
            for (var t = 0f; t < StudioCamera.MoveTime + 0.1f; t += 1f / 60f)
                camera.Step(1f / 60f);
        }

        private static void Near(Vector3 expected, Vector3 actual, float within = 1e-3f) =>
            Assert.True(Vector3.Distance(expected, actual) < within, $"expected {expected}, was {actual}");

        [Fact]
        public void OverheadIsFlatWithNorthUp()
        {
            var camera = new StudioCamera { Target = new Vector3(5f, 2f, 7f), Yaw = 1f, Pitch = -0.3f, Distance = 40f };
            camera.SnapTo(ViewKind.Overhead, camera.Target);
            Assert.False(camera.Flat);   // seen in perspective while it moves
            Arrive(camera);
            Assert.True(camera.Flat);
            Near(Vector3.Down, camera.Forward);
            Near(-Vector3.UnitZ, camera.Up);   // north, up the screen
            Near(new Vector3(5f, 2f + StudioCamera.FlatBack, 7f), camera.Eye, 0.01f);
        }

        [Fact]
        public void TurningAFlatViewMakesItPerspective()
        {
            var camera = new StudioCamera();
            camera.SnapTo(ViewKind.Front, camera.Target);
            Arrive(camera);
            Assert.True(camera.Flat);
            camera.Orbit(0.1f, 0f);
            Assert.False(camera.Flat);
            Assert.Equal(ViewKind.Free, camera.Kind);
        }

        [Fact]
        public void AFlatViewShowsWhatThePerspectiveDidAtTheTarget()
        {
            // A point at the target's distance, at the top edge of the perspective picture, is at the top edge of the flat one
            var camera = new StudioCamera { Target = Vector3.Zero, Yaw = 0f, Pitch = 0f, Distance = 25f };
            var viewport = new Viewport(0, 0, 800, 450);
            var edge = new Vector3(0f, camera.Height / 2f, 0f);
            var perspective = viewport.Project(edge, camera.Projection(viewport.AspectRatio, 1000f), camera.View, Matrix.Identity);
            camera.SnapTo(ViewKind.Front, Vector3.Zero);
            Arrive(camera);
            var flat = viewport.Project(edge, camera.Projection(viewport.AspectRatio, 1000f), camera.View, Matrix.Identity);
            Assert.Equal(0f, perspective.Y, 0.5f);
            Assert.Equal(0f, flat.Y, 0.5f);
        }

        [Fact]
        public void ASnapTurnsTheShortWayRound()
        {
            var camera = new StudioCamera { Yaw = MathHelper.ToRadians(170f) };
            camera.MoveTo(new StudioCamera.Pose(Vector3.Zero, MathHelper.ToRadians(-170f), 0f, 30f), flat: false);
            camera.Step(StudioCamera.MoveTime / 2f);
            Assert.True(MathF.Abs(MathHelper.WrapAngle(camera.Yaw - MathHelper.Pi)) < 0.01f, $"half way should be due south, was {MathHelper.ToDegrees(camera.Yaw)}");
        }

        [Fact]
        public void APickedBoxIsHitWhereItsTurnedTo()
        {
            // A box 4 long (along its own X) and 1 deep, turned a quarter: it now lies along Z
            var place = Matrix.CreateRotationY(MathHelper.PiOver2) * Matrix.CreateTranslation(10f, 0f, 0f);
            var size = new Vector3(4f, 1f, 1f);
            var down = new Vector3(0f, -1f, 0f);
            Assert.NotNull(Picking.Box(new Ray(new Vector3(10f, 5f, 1.8f), down), place, size));
            Assert.Null(Picking.Box(new Ray(new Vector3(11.8f, 5f, 0f), down), place, size));
            Assert.Equal(4f, Picking.Box(new Ray(new Vector3(10f, 5f, 0f), down), place, size)!.Value, 0.001f);
        }

        [Fact]
        public void TheGroundIsFoundUnderARay()
        {
            var terrain = Terrain.FromFunction(64, 64, 1f, (x, z) => 2f + MathF.Sin(x * 0.3f));
            var ray = new Ray(new Vector3(-10f, 30f, 3f), Vector3.Normalize(new Vector3(1f, -1.5f, 0.2f)));
            var hit = Picking.Ground(ray, terrain, 200f)!.Value;
            Assert.Equal(terrain.HeightAt(hit.X, hit.Z), hit.Y, 0.01f);
            // and nothing nearer along it was under the ground
            for (var t = 0f; t < Vector3.Distance(ray.Position, hit) - 0.05f; t += 0.05f)
            {
                var p = ray.Position + ray.Direction * t;
                Assert.True(p.Y > terrain.HeightAt(p.X, p.Z));
            }
        }

        [Fact]
        public void DraggingAlongAnArrowMovesInProportion()
        {
            // An arrow drawn from (100, 100) to (100 + 70, 100) on the screen, 2 m long: 35 pixels along it is a metre
            Vector2? ToScreen(Vector3 p) => new Vector2(100f + p.X * 35f, 100f + p.Z * 35f);
            Assert.Equal(1f, MoveGizmo.Dragged(new Vector2(35f, 0f), Vector3.Zero, GizmoAxis.X, 2f, ToScreen), 0.001f);
            Assert.Equal(1f, MoveGizmo.Dragged(new Vector2(35f, 50f), Vector3.Zero, GizmoAxis.X, 2f, ToScreen), 0.001f);   // across it doesn't count
            Assert.Equal(GizmoAxis.Z, MoveGizmo.Over(new Vector2(103f, 140f), Vector3.Zero, 2f, ToScreen));
            Assert.Equal(GizmoAxis.None, MoveGizmo.Over(new Vector2(150f, 150f), Vector3.Zero, 2f, ToScreen));
        }
    }
}
