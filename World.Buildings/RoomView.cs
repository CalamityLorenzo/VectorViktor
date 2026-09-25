using MeshLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace World.Buildings
{
    // A room ready to draw: its shell and furniture as instances borrowing meshes from the cache
    // (so a RoomView must not outlive it).
    public sealed class RoomView
    {
        private readonly List<MeshInstance> _instances = new List<MeshInstance>();

        public RoomSpec Spec { get; }

        // Its shell and furniture, placed in the world.
        public IReadOnlyList<MeshInstance> Instances => _instances;

        public RoomView(RoomSpec spec, GraphicsDevice device, MeshCache cache)
        {
            Spec = spec;

            var shell = cache.GetOrAdd(device, "room:" + spec.Id, d => RoomMesh.Build(d, spec));
            _instances.Add(new MeshInstance(shell, RoomMesh.Palette(spec)) { Position = spec.WorldOffset });

            // Nothing here moves: just a fixed position and heading.
            foreach (var prop in spec.Props)
            {
                var mesh = cache.GetOrAdd(device, prop.Key, prop.Build);
                _instances.Add(new MeshInstance(mesh, prop.Palette)
                {
                    Position = spec.WorldOffset + prop.Position,
                    Yaw = MathHelper.ToRadians(prop.YawDegrees),
                });
            }
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
                p = RoomSpec.PushOutOfBox(p, new Vector2(prop.Position.X, prop.Position.Z), prop.Half, radius);
            }
            return new Vector3(p.X, position.Y, p.Y);
        }
    }
}
