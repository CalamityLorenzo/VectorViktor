using MeshCore.Library;

using Microsoft.Xna.Framework;

namespace MeshProps.Helpers
{
    // Shared by RobotHeadMesh and RobotArmMesh: solid pieces cut out of a side view, closed all round.
    public static class SlabBuilder
    {
        // A slab from x0 to x1 whose side view, in (z, y), is `profile` - convex, in order round its edge: both sides
        // (in the base slot's Side shade), the faces joining them (Top where they face more up or down than along,
        // Dim otherwise), and all its edges
        public static void AddSlab(MeshBuilder mesh, int baseSlot, float x0, float x1, params Vector2[] profile)
        {
            var near = new Vector3[profile.Length];
            var far = new Vector3[profile.Length];
            for (var i = 0; i < profile.Length; i++)
            {
                near[i] = new Vector3(x0, profile[i].Y, profile[i].X);
                far[i] = new Vector3(x1, profile[i].Y, profile[i].X);
            }
            mesh.AddPolygon(baseSlot + MeshBuilder.Side, near);
            mesh.AddPolygon(baseSlot + MeshBuilder.Side, far);
            for (var i = 0; i < profile.Length; i++)
            {
                var j = (i + 1) % profile.Length;
                var along = profile[j] - profile[i];
                var shade = MathF.Abs(along.X) > MathF.Abs(along.Y) ? MeshBuilder.Top : MeshBuilder.Dim;
                mesh.AddQuad(baseSlot + shade, near[i], near[j], far[j], far[i]);
            }
            mesh.AddLineLoop(near);
            mesh.AddLineLoop(far);
            for (var i = 0; i < profile.Length; i++)
                mesh.AddLine(near[i], far[i]);
        }

        // A panel line on a slab's side at x (a little proud of it): the side view's outline, drawn in towards its
        // middle, to `scale` of its size
        public static void AddPanel(MeshBuilder mesh, float x, float scale, params Vector2[] profile)
        {
            var centre = Vector2.Zero;
            foreach (var p in profile)
                centre += p / profile.Length;
            var loop = new Vector3[profile.Length];
            for (var i = 0; i < profile.Length; i++)
            {
                var p = centre + (profile[i] - centre) * scale;
                loop[i] = new Vector3(x, p.Y, p.X);
            }
            mesh.AddLineLoop(loop);
        }

        // A round bar across from x0 to x1, `sides` sided, round (z, y): a slab whose side view's a regular polygon,
        // a flat at the bottom
        public static void AddRoundBar(MeshBuilder mesh, int baseSlot, float x0, float x1, Vector2 centre, float radius, int sides)
        {
            var profile = new Vector2[sides];
            for (var k = 0; k < sides; k++)
            {
                var angle = -MathHelper.PiOver2 + (k + 0.5f) * MathHelper.TwoPi / sides;
                profile[k] = centre + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            }
            AddSlab(mesh, baseSlot, x0, x1, profile);
        }
    }
}
