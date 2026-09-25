using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
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
    // WallSegment); they block walkers, bodies and the drone's line of sight the same way.
    //
    // Its doors (see Door) are walls too, wherever they've swung to. They swing on in StepDoors, stopping
    // against anything in their way, and Interact opens or shuts the one a walker is facing.
    public sealed class BuildingGround : IGround
    {
        public const float PropHeight = 1f;
        public const float DoorReach = 1.2f;       // how near a door's leaf you must be to open or shut it
        private const float SightMargin = 0.3f;   // how far short of a wall or ceiling a clear line stops

        private readonly IGround _terrain;
        private readonly List<Placed> _rooms = new List<Placed>();
        private readonly List<WallSegment> _walls = new List<WallSegment>();
        private readonly List<Door> _doors = new List<Door>();

        public IReadOnlyList<Door> Doors => _doors;

        // A room, and the box round its floor plan in the world, to rule most rooms out quickly.
        private sealed record Placed(RoomSpec Spec, Vector2 Min, Vector2 Max)
        {
            public Vector3 Offset => Spec.WorldOffset;
            public bool Near(Vector3 p, float margin) =>
                p.X >= Min.X - margin && p.X <= Max.X + margin && p.Z >= Min.Y - margin && p.Z <= Max.Y + margin;
        }

        public IReadOnlyList<Building> Buildings { get; }

        public BuildingGround(IGround terrain, IReadOnlyList<Building> buildings, IEnumerable<WallSegment> walls = null)
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
                }
                _walls.AddRange(building.Walls());
                _doors.AddRange(building.HangDoors());
            }
        }

        // The walls, and every door's leaf where it is now.
        private IEnumerable<WallSegment> AllWalls()
        {
            foreach (var wall in _walls)
                yield return wall;
            foreach (var door in _doors)
                yield return door.Panel;
        }

        // Swings the doors on, each stopping short of any body in its way, or any walker (feet, radius, height).
        public void StepDoors(float dt, IEnumerable<Body> bodies, IEnumerable<(Vector3 feet, float radius, float height)> walkers)
        {
            foreach (var door in _doors)
            {
                var bottom = door.Bottom;
                var top = door.Bottom + door.Height;
                door.Step(dt, (hinge, tip) =>
                {
                    foreach (var body in bodies)
                        if (body.Bottom < top && body.Top > bottom + 0.01f &&
                            SegmentHitsBox(hinge, tip, body.Footprint - body.Half, body.Footprint + body.Half))
                            return true;
                    foreach (var (feet, radius, height) in walkers)
                        if (feet.Y < top && feet.Y + height > bottom &&
                            Vector2.Distance(new Vector2(feet.X, feet.Z), Nearest(new Vector2(feet.X, feet.Z), hinge, tip)) < radius)
                            return true;
                    return false;
                });
            }
        }

        // Opens or shuts the nearest door whose leaf is within DoorReach of a walker at `feet`, in front of
        // them as they face along `heading`, and at the height of their floor. Returns it, or null if none is.
        public Door Interact(Vector3 feet, Vector3 heading)
        {
            var p = new Vector2(feet.X, feet.Z);
            var ahead = new Vector2(heading.X, heading.Z);
            Door best = null;
            var bestDistance = DoorReach;
            foreach (var door in _doors)
            {
                if (MathF.Abs(feet.Y - door.Bottom) > 0.5f)
                    continue;
                var nearest = Nearest(p, door.Hinge, door.Tip);
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

        // The ground a walker's feet are on, and whether it's a building's (a floor or a stair) rather than the terrain's.
        private (float? height, bool indoors) Surface(Vector3 feet, float reach)
        {
            var best = _terrain.GroundBelow(feet, reach);
            var indoors = false;
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
                    indoors = true;
                }
            }
            return (best, indoors);
        }

        // Floors and stairs are level, as far as standing on them goes, and always walkable.
        private const float LookReach = CharacterController.MaxStepUp + CharacterController.Radius;
        public Vector3 NormalAt(Vector3 feet) => Surface(feet, LookReach).indoors ? Vector3.Up : _terrain.NormalAt(feet);
        public bool IsWalkable(Vector3 feet) => Surface(feet, LookReach).indoors || _terrain.IsWalkable(feet);

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

        public Vector3 KeepOut(Vector3 feet, float radius, float height)
        {
            var p = feet;
            // Twice round, so being pushed out of one wall into another (in a corner) settles
            for (var pass = 0; pass < 2; pass++)
            {
                foreach (var wall in AllWalls())
                {
                    if (wall.Bottom >= p.Y + height || wall.Top <= p.Y + 0.01f)
                        continue;
                    var q = new Vector2(p.X, p.Z);
                    var nearest = Nearest(q, wall.A, wall.B);
                    var gap = q - nearest;
                    var distance = gap.Length();
                    if (distance >= radius)
                        continue;
                    var away = distance > 1e-6f ? gap / distance : Building.Outward(wall.A, wall.B);
                    var pushed = nearest + away * radius;
                    p = new Vector3(pushed.X, p.Y, pushed.Y);
                }

                foreach (var room in _rooms)
                {
                    if (!room.Near(p, radius))
                        continue;
                    var local = room.Spec.KeepOutOfRamps(p - room.Offset, radius, height);
                    if (local.Y < PropHeight && local.Y + height > 0f)
                    {
                        var q = new Vector2(local.X, local.Z);
                        foreach (var prop in room.Spec.Props)
                            if (prop.Blocks)
                                q = RoomSpec.PushOutOfBox(q, new Vector2(prop.Position.X, prop.Position.Z), prop.Half, radius);
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
                if (!room.Spec.Contains(local) || local.Y < -0.05f || Array.Exists(room.Spec.CeilingHatches, h => h.Contains(local)))
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
            foreach (var wall in AllWalls())
                if (wall.Bottom < bottomCentre.Y + size.Y && wall.Top > bottomCentre.Y + shrink && SegmentHitsBox(wall.A, wall.B, min, max))
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
            foreach (var wall in AllWalls())
            {
                var s = wall.B - wall.A;
                var denominator = Cross(r, s);
                if (MathF.Abs(denominator) < 1e-9f)
                    continue;
                var t = Cross(wall.A - p, s) / denominator;
                var u = Cross(wall.A - p, r) / denominator;
                if (t < 0f || t >= hit || u < 0f || u > 1f)
                    continue;
                var y = from.Y + d.Y * t;
                if (y >= wall.Bottom && y <= wall.Top)
                    hit = t;
            }

            // Ceilings and floors it passes through, where there's no hatch
            foreach (var room in _rooms)
            {
                foreach (var (height, hatches) in new[] { (room.Spec.Height, room.Spec.CeilingHatches), (0f, room.Spec.FloorHatches) })
                {
                    var y = room.Offset.Y + height;
                    if ((from.Y - y) * (to.Y - y) >= 0f)
                        continue;
                    var t = (y - from.Y) / d.Y;
                    if (t >= hit)
                        continue;
                    var local = from + d * t - room.Offset;
                    if (room.Spec.Contains(local) && !Array.Exists(hatches, h => h.Contains(local)))
                        hit = t;
                }
            }

            if (hit >= 1f)
                return to;
            var length = d.Length();
            return from + d * MathF.Max(0f, hit - SightMargin / MathF.Max(length, 1e-6f));
        }

        // The terrain's lakes and ponds; buildings keep dry, standing clear of them.
        public float? WaterAt(Vector3 point) => _terrain.WaterAt(point);

        private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        private static Vector2 Nearest(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.LengthSquared();
            if (lengthSq < 1e-12f)
                return a;
            return a + ab * MathHelper.Clamp(Vector2.Dot(p - a, ab) / lengthSq, 0f, 1f);
        }

        // Whether the segment a-b passes through the box: clipped to it one axis at a time (Liang-Barsky).
        private static bool SegmentHitsBox(Vector2 a, Vector2 b, Vector2 min, Vector2 max)
        {
            var d = b - a;
            float t0 = 0f, t1 = 1f;
            bool Clip(float p, float q)
            {
                if (MathF.Abs(p) < 1e-12f)
                    return q >= 0f;
                var t = q / p;
                if (p < 0f)
                {
                    if (t > t1) return false;
                    if (t > t0) t0 = t;
                }
                else
                {
                    if (t < t0) return false;
                    if (t < t1) t1 = t;
                }
                return true;
            }
            return Clip(-d.X, a.X - min.X) && Clip(d.X, max.X - a.X) && Clip(-d.Y, a.Y - min.Y) && Clip(d.Y, max.Y - a.Y);
        }
    }
}
