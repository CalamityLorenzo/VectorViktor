using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Core.Animation
{
    // One part of a rig: a joint, and what's drawn there, if anything (Part names a mesh; see World.Rendering's
    // RigScene). Its Pose is where it is now, relative to its parent; Rest is where it goes back to.
    public sealed class RigNode
    {
        public string Name { get; }
        public int Parent { get; }            // its parent's index in the rig, or -1 for a root
        public Pose Rest { get; }
        public Pose Pose { get; set; }

        // The part drawn at this joint, or null for a bare joint (a pivot, a socket with nothing in it).
        // Swapping it swaps what's fitted there: a new arm, a new set of wheels.
        public string? Part { get; set; }

        internal RigNode(string name, int parent, Pose rest, string? part)
        {
            Name = name;
            Parent = parent;
            Rest = rest;
            Pose = rest;
            Part = part;
        }
    }

    // A thing made of parts that move relative to each other - the droid, a cupboard and its doors, a car and its
    // wheels - as a tree of named joints (glTF's node hierarchy). It knows nothing of meshes or drawing, so it's
    // tested like the rest of the simulation.
    //
    // Each tick: Reset, then play clips onto it (see Animator, Clip.Apply), then set whatever's worked out in code
    // (a wheel's roll, where a camera's looking), then Solve, and read each part's World.
    public sealed class Rig
    {
        private readonly List<RigNode> _nodes = new List<RigNode>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>();
        private readonly List<Cable> _cables = new List<Cable>();
        private Matrix[] _world = Array.Empty<Matrix>();

        public int Count => _nodes.Count;
        public IReadOnlyList<Cable> Cables => _cables;
        public RigNode this[int index] => _nodes[index];
        public RigNode this[string name] => _nodes[IndexOf(name) is var i and >= 0 ? i : throw new KeyNotFoundException($"The rig has no part '{name}'.")];

        // A part, hung from `parent` (null for a root), its rest pose relative to it. Parents go in before their
        // children, so Solve can work down the list once. Its index in the rig.
        public int Add(string name, string? parent, Pose rest, string? part = null)
        {
            ArgumentNullException.ThrowIfNull(name);
            if (_index.ContainsKey(name))
                throw new ArgumentException($"The rig already has a part '{name}'.", nameof(name));
            var parentIndex = -1;
            if (parent != null && !_index.TryGetValue(parent, out parentIndex))
                throw new ArgumentException($"'{name}' hangs from '{parent}', which isn't in the rig yet.", nameof(parent));
            _nodes.Add(new RigNode(name, parentIndex, rest, part));
            _index[name] = _nodes.Count - 1;
            return _nodes.Count - 1;
        }

        // A cable strung between its parts (see Cable): every part it's fixed to must be in the rig already.
        public Cable AddCable(Cable cable)
        {
            ArgumentNullException.ThrowIfNull(cable);
            foreach (var point in cable.Points)
                if (!Has(point.Node))
                    throw new ArgumentException($"The cable '{cable.Name}' is fixed to '{point.Node}', which isn't in the rig.", nameof(cable));
            _cables.Add(cable);
            return cable;
        }

        public int IndexOf(string name) => _index.TryGetValue(name, out var i) ? i : -1;
        public bool Has(string name) => _index.ContainsKey(name);

        // Every part back where it rests.
        public void Reset()
        {
            foreach (var node in _nodes)
                node.Pose = node.Rest;
        }

        // Sets a part's pose from what it is now: `rig.Change("lid", p => p with { Rotation = ... })`.
        public void Change(string name, Func<Pose, Pose> change)
        {
            var node = this[name];
            node.Pose = change(node.Pose);
        }

        // Works out where every part is, the whole rig placed by `placement` (in the world, or wherever it's drawn).
        public void Solve(Matrix placement)
        {
            if (_world.Length != _nodes.Count)
                _world = new Matrix[_nodes.Count];
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];
                _world[i] = node.Pose.Matrix * (node.Parent < 0 ? placement : _world[node.Parent]);
            }
        }

        // Where a part is, as of the last Solve: its joint's own space, placed.
        public Matrix World(int index) => _world[index];
        // Where a stretch of a cable is, as of the last Solve: a matrix taking a unit cable - radius 1 round the z axis,
        // from z = 0 to z = 1 - onto the stretch from its point `stretch` to the next, the cable's radius round.
        public Matrix Span(Cable cable, int stretch)
        {
            var from = cable.Points[stretch];
            var to = cable.Points[stretch + 1];
            var start = Vector3.Transform(from.At, World(from.Node));
            var along = Vector3.Transform(to.At, World(to.Node)) - start;
            var length = along.Length();
            var z = length > 1e-6f ? along / length : Vector3.UnitZ;
            var x = Vector3.Normalize(Vector3.Cross(MathF.Abs(z.Y) < 0.9f ? Vector3.Up : Vector3.UnitX, z));
            var y = Vector3.Cross(z, x);
            x *= cable.Radius;
            y *= cable.Radius;
            z *= length;
            return new Matrix(
                x.X, x.Y, x.Z, 0f,
                y.X, y.Y, y.Z, 0f,
                z.X, z.Y, z.Z, 0f,
                start.X, start.Y, start.Z, 1f);
        }

        public Matrix World(string name) => _world[IndexOf(name) is var i and >= 0 ? i : throw new KeyNotFoundException($"The rig has no part '{name}'.")];
    }
}
