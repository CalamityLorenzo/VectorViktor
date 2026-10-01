using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using World.Core;

namespace MapStudio
{
    // Finding what's under the mouse: a ray from the camera through the mouse's point on the picture (Unproject does the
    // camera's sums backwards: from a point on the screen at the near and far planes back into the world), then the
    // nearest thing it goes through. Boxes are tested in their own frame, where they're square to the axes; the ground,
    // which has no shape to test against, is found by stepping along the ray until it's gone under.
    public static class Picking
    {
        // The ray under `pixel` (in `viewport`, the picture), through a camera with this view and projection.
        public static Ray RayAt(Vector2 pixel, Viewport viewport, Matrix view, Matrix projection)
        {
            var near = viewport.Unproject(new Vector3(pixel, 0f), projection, view, Matrix.Identity);
            var far = viewport.Unproject(new Vector3(pixel, 1f), projection, view, Matrix.Identity);
            return new Ray(near, Vector3.Normalize(far - near));
        }

        // How far along the ray it enters the box `size` big, standing on the origin of `place` (its foot on y = 0,
        // centred), if it does.
        public static float? Box(Ray ray, Matrix place, Vector3 size)
        {
            // Into the box's own frame, where it's an ordinary box square to the axes. Its frame has no scaling, so a
            // distance along the ray is the same there as here.
            var into = Matrix.Invert(place);
            var local = new Ray(Vector3.Transform(ray.Position, into), Vector3.TransformNormal(ray.Direction, into));
            var half = new Vector3(size.X / 2f, 0f, size.Z / 2f);
            return local.Intersects(new BoundingBox(-half, half + Vector3.Up * size.Y));
        }

        // Where the ray first goes under the ground, if it does within `reach`: stepping along it (the steps longer the
        // further past `from` - where the camera's looking - since a metre or two out matters less far off), then halving
        // the last step until it's close enough.
        public static Vector3? Ground(Ray ray, Terrain terrain, float reach, float from = 0f)
        {
            bool Below(float t)
            {
                var p = ray.Position + ray.Direction * t;
                return p.Y <= terrain.HeightAt(p.X, p.Z);
            }

            var t0 = 0f;
            if (Below(0f))
                return null;   // starting underground: nothing sensible to find
            while (t0 < reach)
            {
                var t1 = t0 + MathF.Max(0.25f, (t0 - from) * 0.01f);
                if (Below(t1))
                {
                    for (var i = 0; i < 16; i++)
                    {
                        var middle = (t0 + t1) / 2f;
                        if (Below(middle)) t1 = middle; else t0 = middle;
                    }
                    return ray.Position + ray.Direction * t1;
                }
                t0 = t1;
            }
            return null;
        }

        // Where the ray crosses the level plane at `height`, if it does (ahead of it).
        public static Vector3? Level(Ray ray, float height)
        {
            if (MathF.Abs(ray.Direction.Y) < 1e-5f)
                return null;
            var t = (height - ray.Position.Y) / ray.Direction.Y;
            return t > 0f ? ray.Position + ray.Direction * t : null;
        }

        // How far `point` (pixels) is from the line on the screen between `a` and `b`.
        public static float ToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            var along = b - a;
            var t = along.LengthSquared() < 1e-6f ? 0f : Math.Clamp(Vector2.Dot(point - a, along) / along.LengthSquared(), 0f, 1f);
            return Vector2.Distance(point, a + along * t);
        }
    }
}
