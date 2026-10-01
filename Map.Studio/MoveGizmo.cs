using MeshRendering;
using Microsoft.Xna.Framework;
using System;

namespace MapStudio
{
    public enum GizmoAxis { None, X, Z }

    // The handles drawn on what's selected, to move it by: an arrow east (X, red) and one south (Z, blue), from its foot.
    // Grab an arrow and it moves only along that; grab the thing itself and it slides anywhere over the ground. The
    // arrows are a size on the screen, not in the world, so they're as easy to grab near or far.
    public static class MoveGizmo
    {
        public const float ScreenLength = 70f;   // pixels, about
        public const float GrabReach = 8f;        // pixels from an arrow that still grabs it

        // How long the arrows are in the world, standing at `at`, for them to look ScreenLength long: from how much of the
        // world one pixel covers there
        public static float Length(Vector3 at, Vector3 eye, StudioCamera camera, int pictureHeight)
        {
            var across = camera.Flat ? camera.Height : 2f * Vector3.Distance(at, eye) * MathF.Tan(MathHelper.ToRadians(World.Maps.WorldRenderer.FieldOfView) / 2f);
            return across / pictureHeight * ScreenLength;
        }

        public static Vector3 Direction(GizmoAxis axis) => axis == GizmoAxis.X ? Vector3.UnitX : Vector3.UnitZ;

        // Which arrow `mouse` is on, if either: each tested as a line on the screen
        public static GizmoAxis Over(Vector2 mouse, Vector3 at, float length, Func<Vector3, Vector2?> toScreen)
        {
            var best = (axis: GizmoAxis.None, distance: GrabReach);
            foreach (var axis in new[] { GizmoAxis.X, GizmoAxis.Z })
                if (toScreen(at) is { } a && toScreen(at + Direction(axis) * length) is { } b &&
                    Picking.ToSegment(mouse, a, b) is var d && d < best.distance)
                    best = (axis, d);
            return best.axis;
        }

        // How far along an arrow the mouse has dragged it, in metres: its movement along the arrow as drawn on the screen,
        // in proportion to how long the arrow looks there.
        public static float Dragged(Vector2 moved, Vector3 at, GizmoAxis axis, float length, Func<Vector3, Vector2?> toScreen)
        {
            if (toScreen(at) is not { } a || toScreen(at + Direction(axis) * length) is not { } b)
                return 0f;
            var onScreen = b - a;
            var pixels = onScreen.Length();
            if (pixels < 4f)
                return 0f;   // looking straight along the arrow: no telling which way along it you mean
            return Vector2.Dot(moved, onScreen / pixels) / pixels * length;
        }

        public static void Draw(DebugLines lines, Vector3 at, float length, GizmoAxis hot)
        {
            foreach (var axis in new[] { GizmoAxis.X, GizmoAxis.Z })
            {
                var colour = axis == hot ? Color.Yellow : axis == GizmoAxis.X ? Color.Red : Color.DeepSkyBlue;
                var along = Direction(axis);
                var side = Vector3.Cross(along, Vector3.Up);
                var tip = at + along * length;
                lines.Line(at, tip, colour);
                lines.Line(tip, tip - along * length * 0.2f + side * length * 0.08f, colour);
                lines.Line(tip, tip - along * length * 0.2f - side * length * 0.08f, colour);
            }
        }
    }
}
