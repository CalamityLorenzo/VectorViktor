using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using World.Core;
using World.Core.Movement;
using World.Core.Physics;

namespace World.Buildings
{
    // The terrain with buildings standing on it, as one ground to walk on (see IGround): outdoors it's the
    // terrain; indoors, the floor and ramps of whichever room you're in, and its walls.
    //
    // Rooms stacked one on another are just surfaces at different heights here, so there's no need to
    // know which room a walker is "in": the ground under their feet is the highest surface within a
    // step of them, whichever room it belongs to. A hatch is a hole in the upper floor, so climbing a
    // stair up through it carries you from the lower room's stair onto the upper floor, and walking over
    // it from above drops you through, to the stair or floor below.
    //
    // The furniture in a room (a PropSpec with a Half) blocks you up to PropHeight above its floor.
    //
    // Free-standing walls outside - garden fences, a billboard's posts - can be given as well (see
    // WallSegment); they block walkers, bodies and the drone's line of sight the same way. And ledges (see
    // Ledge): level-topped strips to stand on, blocking walkers at their sides.
    //
    // Its doors (see Door) are walls too, wherever they've swung to. They swing on in StepDoors, stopping
    // against anything in their way, and Interact opens or shuts the one a walker is facing. So are its chests of
    // drawers and cupboards (see Cabinet): their carcasses always, their drawers when they're out, their doors wherever
    // they are.
    public sealed class BuildingGround : IGround
    {
        public const float PropHeight = 1f;
        public const float DoorReach = 1.2f;       // how near a door's leaf you must be to open or shut it
        private const float SightMargin = 0.3f;   // how far short of a wall or ceiling a clear line stops

        private readonly IGround _terrain;
        private readonly List<Placed> _rooms = new List<Placed>();
        private readonly List<WallSegment> _walls = new List<WallSegment>();
        private readonly WallGrid _grid;   // the walls (not the doors, which swing), sorted by where they are
        private readonly Ledge[] _ledges;
        private readonly WallGrid _ledgeGrid;   // the ledges' middle lines, in the same order, sorted by where they are
        private readonly float _ledgeReach;     // the widest of them, either side of its line
        private readonly List<Door> _doors = new List<Door>();
        private readonly Dictionary<Building, List<Door>> _doorsOf = new Dictionary<Building, List<Door>>();
        private readonly List<Cabinet> _cabinets = new List<Cabinet>();
        private readonly Dictionary<Building, List<Cabinet>> _cabinetsOf = new Dictionary<Building, List<Cabinet>>();
        private readonly List<Door> _swinging = new List<Door>();       // the doors, and the cabinets' doors
        private readonly List<Drawer> _drawers = new List<Drawer>();    // the cabinets' drawers
        // The box round each room's roof (or the room over it) in the world, overhang and all, and how high it goes
        private readonly List<(Vector2 min, Vector2 max, float top)> _roofs = new List<(Vector2, Vector2, float)>();

        public IReadOnlyList<Door> Doors => _doors;

        // The doors hung in one of its buildings.
        public IReadOnlyList<Door> DoorsOf(Building building) => _doorsOf[building];

        public IReadOnlyList<Cabinet> Cabinets => _cabinets;

        // The chests of drawers and cupboards in one of its buildings.
        public IReadOnlyList<Cabinet> CabinetsOf(Building building) => _cabinetsOf[building];

        // A room, and the box round its floor plan in the world, to rule most rooms out quickly.
        private sealed record Placed(RoomSpec Spec, Vector2 Min, Vector2 Max)
        {
            public Vector3 Offset => Spec.WorldOffset;
            public bool Near(Vector3 p, float margin) =>
                p.X >= Min.X - margin && p.X <= Max.X + margin && p.Z >= Min.Y - margin && p.Z <= Max.Y + margin;
        }

        public IReadOnlyList<Building> Buildings { get; }

        // Every wall there is to walk into (the buildings', and the free-standing ones), for looking at: tools draw them.
        public IReadOnlyList<WallSegment> Walls => _walls;

        public IReadOnlyList<Ledge> Ledges => _ledges;

        public BuildingGround(IGround terrain, IReadOnlyList<Building> buildings, IEnumerable<WallSegment>? walls = null,
                              IEnumerable<Ledge>? ledges = null)
        {
            _terrain = terrain;
            Buildings = buildings;
            if (walls != null)
                _walls.AddRange(walls);
            foreach (var building in buildings)
            {
                foreach (var room in building.Rooms)
                {
                    var min = new Vector2(float.MaxValue);
                    var max = new Vector2(float.MinValue);
                    foreach (var p in room.Outline)
                    {
                        min = Vector2.Min(min, p);
                        max = Vector2.Max(max, p);
                    }
                    var offset = new Vector2(room.WorldOffset.X, room.WorldOffset.Z);
                    _rooms.Add(new Placed(room, min + offset, max + offset));
                    var eaves = new Vector2(building.WallThickness + building.RoofOverhang);
                    // A flat roof's top is the shell's; a pitched one's is the roof's thickness over its ridge's underside
                    var top = building.ShellSpan(room).top + (building.RoofOf(room) != null ? building.RoofThickness : 0f);
                    _roofs.Add((min + offset - eaves, max + offset + eaves, top));
                }
                _walls.AddRange(building.Walls());
                var doors = building.HangDoors();
                _doorsOf[building] = doors;
                _doors.AddRange(doors);
                var cabinets = building.FitCabinets();
                _cabinetsOf[building] = cabinets;
                _cabinets.AddRange(cabinets);
                foreach (var cabinet in cabinets)
                {
                    _walls.AddRange(cabinet.Carcass());
                    _swinging.AddRange(cabinet.Leaves);
                    _drawers.AddRange(cabinet.Drawers);
                }
            }
            _swinging.AddRange(_doors);
            _grid = new WallGrid(_walls);
            _ledges = ledges?.ToArray() ?? Array.Empty<Ledge>();
            _ledgeGrid = new WallGrid(_ledges.Select(l => new WallSegment(l.A, l.B, l.Bottom, l.Highest)).ToList());
            _ledgeReach = _ledges.Length == 0 ? 0f : _ledges.Max(l => l.HalfWidth);
        }

        // The indexes of the ledges that might be within `margin` (past their own widths) of (x, z)
        private IReadOnlyList<int> LedgesNear(Vector2 p, float margin)
        {
            var reach = new Vector2(_ledgeReach + margin);
            return _ledgeGrid.Near(p - reach, p + reach);
        }

        private static float DistanceFromLine(Vector2 p, Ledge ledge) =>
            Vector2.Distance(p, Geometry2D.NearestOnSegment(p, ledge.A, ledge.B));

        // Swings the doors on, and the cabinets' doors and drawers, each stopping short of any body in its way, or any
        // walker (feet, radius, height).
        public void StepDoors(float dt, IEnumerable<Body> bodies, IEnumerable<(Vector3 feet, float radius, float height)> walkers)
        {
            foreach (var door in _swinging)
                if (!door.AtRest)   // nearly always: and so nothing to make a closure of, or to enumerate the bodies for
                    door.Step(dt, (hinge, tip) => InTheWay(hinge, tip, door.Bottom, door.Bottom + door.Height, bodies, walkers));
            foreach (var drawer in _drawers)
                if (!drawer.AtRest)
                    drawer.Step(dt, (a, b) => InTheWay(a, b, drawer.Bottom, drawer.Bottom + drawer.Height, bodies, walkers));
        }

        // Whether a wall from a to b, from `bottom` up to `top`, would be in any of the bodies or walkers.
        private static bool InTheWay(Vector2 a, Vector2 b, float bottom, float top, IEnumerable<Body> bodies,
                                     IEnumerable<(Vector3 feet, float radius, float height)> walkers)
        {
            foreach (var body in bodies)
                if (body.Bottom < top && body.Top > bottom + 0.01f &&
                    Geometry2D.SegmentHitsBox(a, b, body.Footprint - body.Half, body.Footprint + body.Half))
                    return true;
            foreach (var (feet, radius, height) in walkers)
                if (feet.Y < top && feet.Y + height > bottom &&
                    Vector2.Distance(new Vector2(feet.X, feet.Z), Geometry2D.NearestOnSegment(new Vector2(feet.X, feet.Z), a, b)) < radius)
                    return true;
            return false;
        }

        // Opens or shuts the nearest door whose leaf is within DoorReach of a walker at `feet`, in front of
        // them as they face along `heading`, and at the height of their floor - or the nearest cabinet, if that's nearer,
        // its front within reach and them standing before it (see Cabinet.Toggle). Returns it, or null if none is.
        public IOpenable? Interact(Vector3 feet, Vector3 heading)
        {
            var p = new Vector2(feet.X, feet.Z);
            var ahead = new Vector2(heading.X, heading.Z);
            IOpenable? best = null;
            var bestDistance = DoorReach;
            foreach (var cabinet in _cabinets)
            {
                if (MathF.Abs(feet.Y - cabinet.Floor) > 0.5f || Vector2.Dot(p - cabinet.FrontLeft, cabinet.Facing) <= 0f)
                    continue;
                var nearest = Geometry2D.NearestOnSegment(p, cabinet.FrontLeft, cabinet.FrontRight);
                var distance = Vector2.Distance(p, nearest);
                if (distance > bestDistance || Vector2.Dot(nearest - p, ahead) <= 0f)
                    continue;
                best = cabinet;
                bestDistance = distance;
            }
            foreach (var door in _doors)
            {
                if (MathF.Abs(feet.Y - door.Bottom) > 0.5f)
                    continue;
                var nearest = Geometry2D.NearestOnSegment(p, door.Hinge, door.Tip);
                var distance = Vector2.Distance(p, nearest);
                if (distance > bestDistance || (distance > CharacterController.Radius + 0.05f && Vector2.Dot(nearest - p, ahead) <= 0f))
                    continue;
                best = door;
                bestDistance = distance;
            }
            best?.Toggle();
            return best;
        }

        public float? GroundBelow(Vector3 feet, float reach) => Surface(feet, reach).height;

        // How high whatever stands at (x, z), or within `margin` of it, reaches: the ground, or the water over it,
        // or a roof or the top of a wall - a fence, a billboard - if that's higher. For something flying over them
        // all (see Bird), which mustn't go through any of them. Off the edge of the world it's 0.
        public float SkylineAt(float x, float z, float margin)
        {
            var at = new Vector3(x, 0f, z);
            var top = _terrain.GroundBelow(at with { Y = 1e6f }, 0f) ?? 0f;
            if (_terrain.WaterAt(at) is { } water)
                top = MathF.Max(top, water);
            var p = new Vector2(x, z);
            foreach (var (min, max, roof) in _roofs)
                if (p.X >= min.X - margin && p.X <= max.X + margin && p.Y >= min.Y - margin && p.Y <= max.Y + margin)
                    top = MathF.Max(top, roof);
            var near = _grid.Near(p - new Vector2(margin), p + new Vector2(margin));
            for (var k = 0; k < near.Count; k++)   // not foreach: over the interface, that'd make garbage every call
            {
                var wall = _grid[near[k]];
                if (wall.Top > top && Vector2.DistanceSquared(p, Geometry2D.NearestOnSegment(p, wall.A, wall.B)) <= margin * margin)
                    top = wall.Top;
            }
            var ledges = LedgesNear(p, margin);
            for (var k = 0; k < ledges.Count; k++)
            {
                var ledge = _ledges[ledges[k]];
                if (ledge.Highest > top && DistanceFromLine(p, ledge) <= ledge.HalfWidth + margin)
                    top = MathF.Max(top, ledge.TopAt(p));
            }
            return top;
        }

        // The ground a walker's feet are on, and whether it's a building's (a floor or a stair) or a ledge's rather
        // than the terrain's: always walkable, and level but for a sloping ledge (a ramp), which way that faces.
        private (float? height, bool built, Vector3 normal) Surface(Vector3 feet, float reach)
        {
            var best = _terrain.GroundBelow(feet, reach);
            var built = false;
            var normal = Vector3.Up;
            foreach (var room in _rooms)
            {
                if (!room.Near(feet, 0f))
                    continue;
                var surface = room.Spec.SurfaceAt(feet - room.Offset, reach);
                if (!surface.HasValue)
                    continue;
                var height = room.Offset.Y + surface.Value;
                if (!best.HasValue || height >= best.Value)
                {
                    best = height;
                    built = true;
                    normal = Vector3.Up;
                }
            }
            var p = new Vector2(feet.X, feet.Z);
            var ledges = LedgesNear(p, 0f);
            for (var k = 0; k < ledges.Count; k++)
            {
                var ledge = _ledges[ledges[k]];
                var top = ledge.TopAt(p);
                if ((!best.HasValue || top >= best.Value) && top <= feet.Y + reach && DistanceFromLine(p, ledge) <= ledge.HalfWidth)
                {
                    best = top;
                    built = true;
                    normal = ledge.NormalAt(p);
                }
            }
            return (best, built, normal);
        }

        // Floors, stairs and ledges are level (but for a sloping ledge), as far as standing on them goes, and always walkable.
        private const float LookReach = CharacterController.MaxStepUp + CharacterController.Radius;
        public Vector3 NormalAt(Vector3 feet) => Surface(feet, LookReach) is { built: true } surface ? surface.normal : _terrain.NormalAt(feet);
        public bool IsWalkable(Vector3 feet) => Surface(feet, LookReach).built || _terrain.IsWalkable(feet);

        // On a ladder (a ramp with a MaxStepUp above the usual), at the height of it: its own.
        public float StepUpAt(Vector3 feet, float step)
        {
            foreach (var room in _rooms)
            {
                if (!room.Near(feet, 0f))
                    continue;
                var local = feet - room.Offset;
                foreach (var ramp in room.Spec.Ramps)
                {
                    if (ramp.MaxStepUp <= step || !ramp.Contains(local))
                        continue;
                    if (MathF.Abs(ramp.HeightAt(local) - local.Y) <= ramp.MaxStepUp)
                        step = ramp.MaxStepUp;
                }
            }
            return step;
        }

        // On a stepped ramp (stairs), each step's height; on a ladder, as high as it lets you step, since it's rungs.
        public float RiserAt(Vector3 feet)
        {
            var riser = 0f;
            foreach (var room in _rooms)
            {
                if (!room.Near(feet, 0f))
                    continue;
                var local = feet - room.Offset;
                foreach (var ramp in room.Spec.Ramps)
                {
                    var ladder = ramp.MaxStepUp > RoomSpec.DefaultMaxStepUp;
                    if ((ramp.Steps <= 0 && !ladder) || !ramp.Contains(local) || MathF.Abs(ramp.HeightAt(local) - local.Y) > RiserReach)
                        continue;
                    riser = MathF.Max(riser, ladder ? ramp.MaxStepUp : MathF.Abs(ramp.End.Y - ramp.Start.Y) / ramp.Steps);
                }
            }
            return riser;
        }

        private const float RiserReach = 0.5f;   // how far from a stair's slope your feet can be and still be on it

        // How far past a walker's radius to look for walls that might push it: once pushed out of one, it can be up against another
        private const float PushMargin = 2f;

        // `p`, pushed out of `wall` if it's within `radius` of it and within its height.
        private static Vector3 PushOutOfWall(Vector3 p, WallSegment wall, float radius, float height)
        {
            if (wall.Bottom >= p.Y + height || wall.Top <= p.Y + 0.01f)
                return p;
            var q = new Vector2(p.X, p.Z);
            var nearest = Geometry2D.NearestOnSegment(q, wall.A, wall.B);
            var gap = q - nearest;
            var distance = gap.Length();
            if (distance >= radius)
                return p;
            var away = distance > 1e-6f ? gap / distance : Geometry2D.Outward(wall.A, wall.B);
            var pushed = nearest + away * radius;
            return new Vector3(pushed.X, p.Y, pushed.Y);
        }

        public Vector3 KeepOut(Vector3 feet, float radius, float height)
        {
            var p = feet;
            // Twice round, so being pushed out of one wall into another (in a corner) settles
            for (var pass = 0; pass < 2; pass++)
            {
                // The walls near it (a push moves it by less than a radius, and it's pushed only by walls within one), then the doors
                var reach = radius + PushMargin;
                foreach (var index in _grid.Near(new Vector2(p.X - reach, p.Z - reach), new Vector2(p.X + reach, p.Z + reach)))
                    p = PushOutOfWall(p, _grid[index], radius, height);
                foreach (var door in _swinging)
                    p = PushOutOfWall(p, door.Panel, radius, height);
                foreach (var drawer in _drawers)
                    for (var k = 0; k < drawer.Panels; k++)
                        p = PushOutOfWall(p, drawer.Panel(k), radius, height);
                // Out from the side of a ledge too high to step up onto, as from a wall as thick as it is
                foreach (var index in LedgesNear(new Vector2(p.X, p.Z), radius))
                {
                    var ledge = _ledges[index];
                    var top = ledge.TopAt(new Vector2(p.X, p.Z));   // where it's beside you, if it slopes
                    if (ledge.Bottom < p.Y + height && top > p.Y + CharacterController.MaxStepUp)
                        p = PushOutOfWall(p, new WallSegment(ledge.A, ledge.B, ledge.Bottom, top), ledge.HalfWidth + radius, height);
                }

                foreach (var room in _rooms)
                {
                    if (!room.Near(p, radius))
                        continue;
                    var local = room.Spec.KeepUnderRoof(room.Spec.KeepOutOfRamps(p - room.Offset, radius, height), radius, height);
                    if (local.Y < PropHeight && local.Y + height > 0f)
                    {
                        var q = new Vector2(local.X, local.Z);
                        foreach (var prop in room.Spec.Props)
                            if (prop.Blocks)
                                q = Geometry2D.PushOutOfBox(q, new Vector2(prop.Position.X, prop.Position.Z), prop.Half, radius);
                        local = new Vector3(q.X, local.Y, q.Y);
                    }
                    p = local + room.Offset;
                }
            }
            return p;
        }

        // The lowest ceiling over a walker standing in a room (not under a hatch in it).
        public float? CeilingAbove(Vector3 feet)
        {
            float? lowest = _terrain.CeilingAbove(feet);
            foreach (var room in _rooms)
            {
                if (!room.Near(feet, 0f))
                    continue;
                var local = feet - room.Offset;
                if (!room.Spec.Contains(local) || local.Y < -0.05f || HatchSpec.AnyContain(room.Spec.CeilingHatches, local))
                    continue;
                var ceiling = room.Spec.CeilingHeightAt(local);
                if (local.Y >= ceiling)
                    continue;
                if (!lowest.HasValue || room.Offset.Y + ceiling < lowest.Value)
                    lowest = room.Offset.Y + ceiling;
            }
            return lowest;
        }

        public bool Obstructs(Vector3 bottomCentre, Vector3 size)
        {
            const float shrink = 0.01f;   // touching a wall isn't being in it
            var min = new Vector2(bottomCentre.X - size.X / 2f + shrink, bottomCentre.Z - size.Z / 2f + shrink);
            var max = new Vector2(bottomCentre.X + size.X / 2f - shrink, bottomCentre.Z + size.Z / 2f - shrink);
            bool Blocks(WallSegment wall) =>
                wall.Bottom < bottomCentre.Y + size.Y && wall.Top > bottomCentre.Y + shrink && Geometry2D.SegmentHitsBox(wall.A, wall.B, min, max);
            foreach (var index in _grid.Near(min, max))
                if (Blocks(_grid[index]))
                    return true;
            foreach (var door in _swinging)
                if (Blocks(door.Panel))
                    return true;
            foreach (var drawer in _drawers)
                for (var k = 0; k < drawer.Panels; k++)
                    if (Blocks(drawer.Panel(k)))
                        return true;
            return _terrain.Obstructs(bottomCentre, size);
        }

        public Vector3 ClearLine(Vector3 from, Vector3 to)
        {
            to = _terrain.ClearLine(from, to);
            var d = to - from;
            var hit = 1f;

            // Walls it passes through, at a height where there's wall
            var p = new Vector2(from.X, from.Z);
            var r = new Vector2(d.X, d.Z);
            void Cross(WallSegment wall)
            {
                var s = wall.B - wall.A;
                var denominator = Geometry2D.Cross(r, s);
                if (MathF.Abs(denominator) < 1e-9f)
                    return;
                var t = Geometry2D.Cross(wall.A - p, s) / denominator;
                var u = Geometry2D.Cross(wall.A - p, r) / denominator;
                if (t < 0f || t >= hit || u < 0f || u > 1f)
                    return;
                var y = from.Y + d.Y * t;
                if (y >= wall.Bottom && y <= wall.Top)
                    hit = t;
            }
            var end = p + r;
            foreach (var index in _grid.Near(Vector2.Min(p, end), Vector2.Max(p, end)))
                Cross(_grid[index]);
            foreach (var door in _swinging)
                Cross(door.Panel);
            foreach (var drawer in _drawers)
                for (var k = 0; k < drawer.Panels; k++)
                    Cross(drawer.Panel(k));

            // Floors it passes through, where there's no hatch
            foreach (var room in _rooms)
            {
                var y = room.Offset.Y;
                if ((from.Y - y) * (to.Y - y) >= 0f)
                    continue;
                var t = (y - from.Y) / d.Y;
                if (t >= hit)
                    continue;
                var local = from + d * t - room.Offset;
                if (room.Spec.Contains(local) && !HatchSpec.AnyContain(room.Spec.FloorHatches, local))
                    hit = t;
            }

            // And ceilings, which may slope (under a pitched roof, over a stair): found by walking along the
            // line in short steps for where it goes from under a room's ceiling to over it, or back
            var steps = Math.Max(8, (int)MathF.Ceiling(d.Length() / 0.25f));
            foreach (var room in _rooms)
            {
                float? Above(float t)
                {
                    var local = from + d * t - room.Offset;
                    if (!room.Spec.Contains(local) || local.Y < 0f || HatchSpec.AnyContain(room.Spec.CeilingHatches, local))
                        return null;
                    return local.Y - room.Spec.CeilingHeightAt(local);
                }
                var before = Above(0f);
                for (var k = 1; k <= steps && k / (float)steps < hit; k++)
                {
                    var t = k / (float)steps;
                    var now = Above(t);
                    if (before.HasValue && now.HasValue && (before.Value < 0f) != (now.Value < 0f))
                    {
                        hit = t - 1f / steps;
                        break;
                    }
                    before = now;
                }
            }

            if (hit >= 1f)
                return to;
            var length = d.Length();
            return from + d * MathF.Max(0f, hit - SightMargin / MathF.Max(length, 1e-6f));
        }

        // The terrain's lakes and ponds; buildings keep dry, standing clear of them.
        public float? WaterAt(Vector3 point) => _terrain.WaterAt(point);
        public Vector2 CurrentAt(Vector3 point) => _terrain.CurrentAt(point);

    }
}
