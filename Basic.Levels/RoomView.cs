using MeshLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Basic.Levels
{
    // A room ready to draw: its shell and furniture as instances borrowing meshes from the cache
    // (so a RoomView must not outlive it).
    public sealed class RoomView
    {
        private readonly List<MeshInstance> _instances = new List<MeshInstance>();

        public RoomSpec Spec { get; }

        public RoomView(RoomSpec spec, GraphicsDevice device, MeshCache cache)
        {
            Spec = spec;

            var shell = cache.GetOrAdd(device, "room:" + spec.Id, d => RoomMesh.Build(d, spec));
            _instances.Add(Place(new MeshInstance(shell, RoomMesh.Palette(spec)), spec.WorldOffset, 0f));

            foreach (var prop in spec.Props)
            {
                var mesh = cache.GetOrAdd(device, prop.Key, prop.Build);
                _instances.Add(Place(new MeshInstance(mesh, prop.Palette), spec.WorldOffset + prop.Position, prop.YawDegrees));
            }
        }

        // Nothing here moves: no tumbling, just a fixed position and heading.
        private static MeshInstance Place(MeshInstance instance, Vector3 position, float yawDegrees)
        {
            instance.Position = position;
            instance.Pitch = 0f;
            instance.Yaw = MathHelper.ToRadians(yawDegrees);
            instance.YawSpeed = 0f;
            instance.PitchSpeed = 0f;
            return instance;
        }

        public void Draw(GameTime gameTime, GraphicsDevice device, BasicEffect effect, Color background, bool colorsOn)
        {
            foreach (var instance in _instances)
            {
                instance.ColorsOn = colorsOn;
                instance.Draw(gameTime, device, effect, background);
            }
        }

        // Pushes a walker (a circle of `radius` on the floor) out of any furniture it overlaps. The position is in the room's own coordinates.
        public Vector3 PushOutOfProps(Vector3 position, float radius)
        {
            var p = new Vector2(position.X, position.Z);
            foreach (var prop in Spec.Props)
            {
                if (!prop.Blocks)
                    continue;
                p = PushOutOfBox(p, new Vector2(prop.Position.X, prop.Position.Z), prop.Half, radius);
            }
            return new Vector3(p.X, position.Y, p.Y);
        }

        private static Vector2 PushOutOfBox(Vector2 p, Vector2 centre, Vector2 half, float radius)
        {
            var d = p - centre;
            var nearest = Vector2.Clamp(d, -half, half);
            var gap = d - nearest;
            var distance = gap.Length();
            if (distance >= radius)
                return p;

            if (distance > 1e-6f)
                return centre + nearest + gap / distance * radius;

            // The walker's centre is inside the box: leave by whichever side is nearest
            var toX = half.X - System.MathF.Abs(d.X);
            var toZ = half.Y - System.MathF.Abs(d.Y);
            if (toX < toZ)
                return new Vector2(centre.X + (d.X < 0f ? -1f : 1f) * (half.X + radius), p.Y);
            return new Vector2(p.X, centre.Y + (d.Y < 0f ? -1f : 1f) * (half.Y + radius));
        }
    }
}
