using MeshCore.Library;
using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using World.Core.Animation;

namespace World.Rendering
{
    // A rig (see Rig) as drawn, while it's being moved by the simulation: after the rig's been posed and solved, Add puts
    // a mesh at each part and a length of cable on each stretch of its cables. It follows the rig as it changes: a
    // part's mesh swapped (RigNode.Part), a part or cable added. For a rig moved only by the clock, RigScene is simpler.
    public sealed class RigView
    {
        private readonly Rig _rig;
        private readonly IReadOnlyDictionary<string, MeshSource> _meshes;
        private readonly MeshSource? _cable;
        private readonly GraphicsDevice _device;
        private readonly MeshCache _cache;
        private readonly List<(string? part, MeshInstance? view)> _parts = new List<(string?, MeshInstance?)>();   // by the node's index
        private readonly List<MeshInstance> _stretches = new List<MeshInstance>();

        public RigView(Rig rig, IReadOnlyDictionary<string, MeshSource> meshes, MeshSource? cable, GraphicsDevice device, MeshCache cache)
        {
            _rig = rig;
            _meshes = meshes;
            _cable = cable;
            _device = device;
            _cache = cache;
        }

        // Every part (and cable) where the rig's last Solve put it, into the batch; `shown` can leave parts out (the head
        // camera's own mesh, seen from inside it).
        public void Add(MeshBatch batch, Func<RigNode, bool>? shown = null)
        {
            for (var i = 0; i < _rig.Count; i++)
            {
                var node = _rig[i];
                var view = ViewOf(i, node.Part);
                if (view == null || (shown != null && !shown(node)))
                    continue;
                view.Transform = _rig.World(i);
                batch.Add(view);
            }

            if (_cable == null)
                return;
            var stretch = 0;
            foreach (var cable in _rig.Cables)
                for (var k = 0; k < cable.Stretches; k++, stretch++)
                {
                    if (stretch == _stretches.Count)
                        _stretches.Add(_cache.CreateInstance(_device, _cable));
                    _stretches[stretch].Transform = _rig.Span(cable, k);
                    batch.Add(_stretches[stretch]);
                }
        }

        // The node's mesh, made again if its part has changed since last time (null for a bare joint).
        private MeshInstance? ViewOf(int index, string? part)
        {
            while (_parts.Count <= index)
                _parts.Add((null, null));
            var (shown, view) = _parts[index];
            if (shown == part)
                return view;
            view = part == null ? null
                : _meshes.TryGetValue(part, out var mesh) ? _cache.CreateInstance(_device, mesh)
                : throw new KeyNotFoundException($"No mesh for the rig's part '{part}' (at '{_rig[index].Name}').");
            _parts[index] = (part, view);
            return view;
        }
    }
}
