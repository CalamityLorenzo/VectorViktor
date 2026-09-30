using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;

namespace World.Core.Animation
{
    // One part of a rig: a joint, and what's drawn there, if anything (Part names a mesh; see World.Rendering's
    // RigScene). Its Pose is where it is now, relative to its parent; Rest is where it goes back to.
    public sealed class RigNode
    {
        public string Name { get; }
        public int Parent { get; internal set; }   // its parent's index in the rig, or -1 for a root
        public Pose Rest { get; internal set; }     // changes only when it's hung somewhere else (see Rig.Reparent)
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
    //
    // Limbs come and go (see Attach, Detach): a limb is a small rig of its own, fitted at a socket (a bare joint). And a
    // part can be hung from another without jumping (see Reparent): an arm carried in the hand, then fitted at the shoulder.
    // Parts' indexes can change when they do, so hold on to names rather than indexes across them.
    public sealed class Rig
    {
        private readonly List<RigNode> _nodes = new List<RigNode>();
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>();
        private readonly List<Cable> _cables = new List<Cable>();
        private Matrix[] _world = Array.Empty<Matrix>();
        private Matrix _placement = Matrix.Identity;   // as of the last Solve

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
            _placement = placement;
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

        // Fits a limb (a rig of its own) at `socket`: every part of it, posed as it is in the limb, its roots hung from the
        // socket, and its cables. Its parts' names must all be new here (a limb builder takes a prefix for that). The limb
        // itself is left as it was; parts are added, not shared.
        public void Attach(Rig limb, string socket)
        {
            ArgumentNullException.ThrowIfNull(limb);
            if (!Has(socket))
                throw new ArgumentException($"The rig has no socket '{socket}'.", nameof(socket));
            for (var i = 0; i < limb.Count; i++)
                if (Has(limb[i].Name))
                    throw new ArgumentException($"The rig already has a part '{limb[i].Name}': give the limb's parts names of their own.", nameof(limb));
            for (var i = 0; i < limb.Count; i++)
            {
                var node = limb[i];
                Add(node.Name, node.Parent < 0 ? socket : limb[node.Parent].Name, node.Rest, node.Part);
                this[node.Name].Pose = node.Pose;
            }
            foreach (var cable in limb.Cables)
                AddCable(cable);
            Solve(_placement);
        }

        // Takes a part off, with everything hung from it: a rig of its own, its root resting as it did (relative to where
        // it was fitted), to be fitted again elsewhere. The cables fixed only to what came off go with it; any that also ran
        // to what's left (the wiring to a limb) are cut, and dropped.
        public Rig Detach(string name)
        {
            var root = IndexOf(name);
            if (root < 0)
                throw new KeyNotFoundException($"The rig has no part '{name}'.");
            var off = new HashSet<RigNode>();
            for (var i = root; i < _nodes.Count; i++)   // parents come before children, so one pass finds them all
                if (i == root || (_nodes[i].Parent >= 0 && off.Contains(_nodes[_nodes[i].Parent])))
                    off.Add(_nodes[i]);

            var limb = new Rig();
            foreach (var node in _nodes)
                if (off.Contains(node))
                {
                    limb.Add(node.Name, node == _nodes[root] ? null : _nodes[node.Parent].Name, node.Rest, node.Part);
                    limb[node.Name].Pose = node.Pose;
                }
            foreach (var cable in _cables)
                if (cable.Points.All(p => limb.Has(p.Node)))
                    limb.AddCable(cable);
            _cables.RemoveAll(c => c.Points.Any(p => limb.Has(p.Node)));

            Rebuild(_nodes.Where(n => !off.Contains(n)).Select(n => (n, n.Parent < 0 ? null : _nodes[n.Parent])).ToList());
            Solve(_placement);
            return limb;
        }

        // Hangs a part, and all that hangs from it, from `parent` instead, where it is now (as of the last Solve): it rests
        // there, relative to its new parent, until something moves it - a clip easing it into its socket, say. So an arm
        // carried in the hand can be let go of at the shoulder, and not jump.
        public void Reparent(string name, string parent)
        {
            var node = this[name];
            var to = IndexOf(parent);
            if (to < 0)
                throw new KeyNotFoundException($"The rig has no part '{parent}'.");
            for (var p = to; p >= 0; p = _nodes[p].Parent)
                if (_nodes[p] == node)
                    throw new ArgumentException($"'{name}' can't hang from '{parent}', which hangs from it.", nameof(parent));

            var local = World(IndexOf(name)) * Matrix.Invert(World(to));
            local.Decompose(out var scale, out var rotation, out var translation);
            var pose = new Pose(translation, Quaternion.Normalize(rotation), scale);
            node.Rest = pose;
            node.Pose = pose;

            var newParent = _nodes[to];
            Rebuild(_nodes.Select(n => (n, n == node ? newParent : n.Parent < 0 ? null : _nodes[n.Parent])).ToList());
            Solve(_placement);
        }

        // Where a part rests from now on, relative to its parent: once a clip has eased it into place (a limb into its
        // socket), so it stays there when the clip goes.
        public void SetRest(string name, Pose rest) => this[name].Rest = rest;

        // The rig made again from these parts and their parents, parents first (each root, then what hangs from it, in the
        // order they were), so Solve can still work down the list once.
        private void Rebuild(List<(RigNode node, RigNode? parent)> parts)
        {
            var children = new Dictionary<RigNode, List<RigNode>>();
            foreach (var (node, parent) in parts)
                if (parent != null)
                {
                    if (!children.TryGetValue(parent, out var list))
                        children[parent] = list = new List<RigNode>();
                    list.Add(node);
                }

            _nodes.Clear();
            _index.Clear();
            void Visit(RigNode node, int parent)
            {
                node.Parent = parent;
                _nodes.Add(node);
                var at = _nodes.Count - 1;
                _index[node.Name] = at;
                if (children.TryGetValue(node, out var hung))
                    foreach (var child in hung)
                        Visit(child, at);
            }
            foreach (var (node, parent) in parts)
                if (parent == null)
                    Visit(node, -1);
        }

        public Matrix World(string name) => _world[IndexOf(name) is var i and >= 0 ? i : throw new KeyNotFoundException($"The rig has no part '{name}'.")];
    }
}
