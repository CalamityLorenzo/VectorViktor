using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using World.Core;
using World.Buildings;

namespace World.Rendering
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

            var shell = cache.CreateInstance(device, new MeshSource("room:" + spec.Id, d => RoomMesh.Build(d, spec), RoomMesh.Palette(spec)));
            shell.Position = spec.WorldOffset;
            _instances.Add(shell);

            // Nothing here moves: just a fixed position and heading.
            foreach (var prop in spec.Props)
            {
                var instance = cache.CreateInstance(device, prop.Mesh);
                instance.Position = spec.WorldOffset + prop.Position;
                instance.Yaw = MathHelper.ToRadians(prop.YawDegrees);
                _instances.Add(instance);
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
    }
}
