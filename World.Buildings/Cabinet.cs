using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Buildings
{
    public enum CabinetKind { Drawers, Cupboard }

    // A chest of drawers or a cupboard standing in a room (see RoomSpec.Cabinets): Position is the middle of its footprint
    // on the floor, in the room's own coordinates, and it's turned YawDegrees as a PropSpec is, its front facing +Z before
    // it's turned. Width across its front, Depth front to back. A chest has Count drawers, one above another, the top one
    // first; a cupboard has Count doors (1 or 2) across its front, hinged at its sides.
    public record CabinetSpec(CabinetKind Kind, Vector3 Position, float YawDegrees, float Width, float Depth, float Height, int Count, Color Color)
    {
        public const float Plinth = 0.06f;    // the base under the drawers or doors
        public const float Board = 0.02f;     // the sides', top's and back's thickness
        public const float Gap = 0.006f;      // between one drawer front, or door, and the next

        // How far a drawer slides out: most of the way, so what's in it can be got at
        public float Travel => Depth * 0.75f;

        // The part of the front the drawers or doors fill, up from the floor
        public float FrontBottom => Plinth;
        public float FrontTop => Height - Board;

        // Drawer `i`'s front, from the top: its bottom and height, up from the floor.
        public (float bottom, float height) DrawerSpan(int i)
        {
            var each = (FrontTop - FrontBottom) / Count;
            return (FrontTop - (i + 1) * each + Gap / 2f, each - Gap);
        }

        // Its own space in the room's: turned, then moved to where it stands.
        public Matrix Placement(Vector3 roomOffset) =>
            Matrix.CreateRotationY(MathHelper.ToRadians(YawDegrees)) * Matrix.CreateTranslation(roomOffset + Position);
    }

    // Something that opens and shuts when you use it (see BuildingGround.Interact): a door, a cabinet.
    public interface IOpenable
    {
        void Toggle();
    }

    // A drawer: its front shut flush with its cabinet's, from Left to Right, sliding out along Out as far as Travel, at
    // Speed, from Bottom up Height. It stops against anything in its way, as a door does (see Step); out, its front and
    // sides are walls (see Panels).
    public sealed class Drawer
    {
        public const float Speed = 1.2f;   // metres a second: out in about a third of a second

        public Vector2 Left { get; }
        public Vector2 Right { get; }
        public Vector2 Out { get; }
        public float Travel { get; }
        public float Bottom { get; }
        public float Height { get; }

        public float Extent { get; private set; }     // how far out it is: 0 shut, Travel all the way out
        public bool Opening { get; private set; }
        public float Fraction => Extent / Travel;
        public bool IsShut => Extent <= 0f && !Opening;
        public bool AtRest => Extent == (Opening ? Travel : 0f);

        public Drawer(Vector2 left, Vector2 right, Vector2 @out, float travel, float bottom, float height)
        {
            Left = left;
            Right = right;
            Out = Vector2.Normalize(@out);
            Travel = travel;
            Bottom = bottom;
            Height = height;
        }

        public void Toggle() => Opening = !Opening;

        // Side `k` of the three that stick out of the cabinet, out `extent`: its front, then its left and right sides.
        private (Vector2 a, Vector2 b) Side(int k, float extent)
        {
            var out1 = Out * extent;
            return k switch
            {
                0 => (Left + out1, Right + out1),
                1 => (Left, Left + out1),
                _ => (Right, Right + out1),
            };
        }

        // As walls, where it is now: none while it's shut, inside the cabinet; out, its front and sides (Panel(0 to 2)).
        public int Panels => Extent > 0.005f ? 3 : 0;

        public WallSegment Panel(int k)
        {
            var (a, b) = Side(k, Extent);
            return new WallSegment(a, b, Bottom, Bottom + Height);
        }

        // Slides it on, unless it would end up in something (`blocked` says whether a wall from a to b would).
        public void Step(float dt, Func<Vector2, Vector2, bool> blocked)
        {
            var target = Opening ? Travel : 0f;
            if (Extent == target)
                return;
            var change = Speed * dt;
            var next = MathF.Abs(target - Extent) <= change ? target : Extent + MathF.Sign(target - Extent) * change;
            if (next > Extent)   // only coming out can it run into anything
                for (var k = 0; k < 3; k++)
                {
                    var (a, b) = Side(k, next);
                    if (blocked(a, b))
                        return;
                }
            Extent = next;
        }
    }

    // A cabinet as it stands in the world, from its spec (see CabinetSpec): its carcass, which never moves, and its drawers
    // or doors (Door, the same as a doorway's, only small), in the world's coordinates. Using it (Toggle) opens the top
    // drawer still shut, and once they're all open, shuts them all; or opens a cupboard's doors together, or shuts them.
    public sealed class Cabinet : IOpenable
    {
        private readonly Drawer[] _drawers;
        private readonly Door[] _leaves;

        public CabinetSpec Spec { get; }
        public Matrix Placement { get; }
        public float Floor { get; }
        public IReadOnlyList<Drawer> Drawers => _drawers;
        public IReadOnlyList<Door> Leaves => _leaves;

        // The front of its carcass, across the world, and the way it faces
        public Vector2 FrontLeft { get; }
        public Vector2 FrontRight { get; }
        public Vector2 Facing { get; }

        public Cabinet(CabinetSpec spec, Vector3 roomOffset)
        {
            Spec = spec;
            Placement = spec.Placement(roomOffset);
            Floor = roomOffset.Y + spec.Position.Y;
            Vector2 At(float x, float z)
            {
                var p = Vector3.Transform(new Vector3(x, 0f, z), Placement);
                return new Vector2(p.X, p.Z);
            }
            var (w, d) = (spec.Width / 2f, spec.Depth / 2f);
            FrontLeft = At(-w, d);
            FrontRight = At(w, d);
            Facing = Vector2.Normalize(At(0f, 1f) - At(0f, 0f));

            if (spec.Count < 1 || (spec.Kind == CabinetKind.Cupboard && spec.Count > 2))
                throw new ArgumentOutOfRangeException(nameof(spec), spec.Count, "A chest needs a drawer at least; a cupboard one door or two.");
            var inner = w - CabinetSpec.Board;
            if (spec.Kind == CabinetKind.Drawers)
            {
                _drawers = new Drawer[spec.Count];
                for (var i = 0; i < spec.Count; i++)
                {
                    var (bottom, height) = spec.DrawerSpan(i);
                    _drawers[i] = new Drawer(At(-inner, d), At(inner, d), Facing, spec.Travel, Floor + bottom, height);
                }
                _leaves = Array.Empty<Door>();
            }
            else
            {
                _drawers = Array.Empty<Drawer>();
                var (bottom, height) = (Floor + spec.FrontBottom, spec.FrontTop - spec.FrontBottom);
                var across = Vector2.Normalize(FrontRight - FrontLeft);
                var width = spec.Count == 1 ? 2f * w : w - CabinetSpec.Gap / 2f;
                _leaves = spec.Count == 1
                    ? new[] { new Door(FrontLeft, across, Facing, width, bottom, height, spec.Color) }
                    : new[]
                    {
                        new Door(FrontLeft, across, Facing, width, bottom, height, spec.Color),
                        new Door(FrontRight, -across, Facing, width, bottom, height, spec.Color),
                    };
            }
        }

        public bool AtRest
        {
            get
            {
                foreach (var drawer in _drawers)
                    if (!drawer.AtRest)
                        return false;
                foreach (var leaf in _leaves)
                    if (!leaf.AtRest)
                        return false;
                return true;
            }
        }

        public bool IsShut => Array.TrueForAll(_drawers, d => d.IsShut) && Array.TrueForAll(_leaves, l => l.IsShut);

        public void Toggle()
        {
            if (_leaves.Length > 0)
            {
                var open = !Array.Exists(_leaves, l => l.Opening);
                foreach (var leaf in _leaves)
                    if (leaf.Opening != open)
                        leaf.Toggle();
                return;
            }
            var next = Array.Find(_drawers, d => !d.Opening);
            if (next != null)
                next.Toggle();
            else
                foreach (var drawer in _drawers)
                    drawer.Toggle();
        }

        // Its carcass's four sides as walls: it stands in the way whatever's open.
        public IEnumerable<WallSegment> Carcass()
        {
            var (w, d) = (Spec.Width / 2f, Spec.Depth / 2f);
            var corners = new[] { new Vector3(-w, 0f, -d), new Vector3(w, 0f, -d), new Vector3(w, 0f, d), new Vector3(-w, 0f, d) };
            for (var i = 0; i < 4; i++)
            {
                var a = Vector3.Transform(corners[i], Placement);
                var b = Vector3.Transform(corners[(i + 1) % 4], Placement);
                yield return new WallSegment(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z), Floor, Floor + Spec.Height);
            }
        }

        // What sticks out of it as walls, where it is now: open drawers, and its doors wherever they've swung to.
        public IEnumerable<WallSegment> Moving()
        {
            foreach (var drawer in _drawers)
                for (var k = 0; k < drawer.Panels; k++)
                    yield return drawer.Panel(k);
            foreach (var leaf in _leaves)
                yield return leaf.Panel;
        }
    }
}
