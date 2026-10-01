using Microsoft.Xna.Framework;
using System;
using System.Collections;
using World.Core;
using World.Maps.Files;

namespace MapStudio
{
    public enum EntryKind { Pad, Pool, Building, Prop, Thing, Start, Portal }

    // One entry in a district file: which file (its path from the map file), which list, which one in it.
    public readonly record struct EntryRef(string File, EntryKind Kind, int Index);

    // What the studio does with any entry of a district file, whatever kind it is: find its list, where it is, move it,
    // turn it, and the box it fills. Entries are records, which can't change: moving one makes a new one (`with`), which
    // replaces the old in its list. That's what makes undo simple (see Edits): an edit is the entry before and after.
    public static class Entries
    {
        // Its list in the file, as a plain IList, so one piece of code can work on any kind
        public static IList List(DistrictFile file, EntryKind kind) => kind switch
        {
            EntryKind.Pad => file.Pads,
            EntryKind.Pool => file.Pools,
            EntryKind.Building => file.Buildings,
            EntryKind.Prop => file.Props,
            EntryKind.Thing => file.Things,
            EntryKind.Start => file.Starts,
            EntryKind.Portal => file.Portals,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };

        public static EntryKind KindOf(object entry) => entry switch
        {
            PadEntry => EntryKind.Pad,
            PoolEntry => EntryKind.Pool,
            BuildingEntry => EntryKind.Building,
            PropEntry => EntryKind.Prop,
            ThingEntry => EntryKind.Thing,
            StartEntry => EntryKind.Start,
            PortalEntry => EntryKind.Portal,
            _ => throw new ArgumentException("Not an entry of a district file.", nameof(entry)),
        };

        // Where it is on the ground: its middle
        public static Vector2 Where(object entry) => entry switch
        {
            PadEntry p => p.Centre,
            PoolEntry p => p.Centre,
            BuildingEntry b => b.At,
            PropEntry p => p.At,
            ThingEntry t => t.At,
            StartEntry s => s.At,
            PortalEntry p => (p.A + p.B) / 2f,
            _ => throw new ArgumentException("Not an entry of a district file.", nameof(entry)),
        };

        // The same, somewhere else: a portal's door and where it takes you move together
        public static object MovedTo(object entry, Vector2 to)
        {
            var by = to - Where(entry);
            return entry switch
            {
                PadEntry p => p with { Centre = to },   // a pad levelled with somewhere else stays level with it
                PoolEntry p => p with { Centre = to },
                BuildingEntry b => b with { At = to },
                PropEntry p => p with { At = to },
                ThingEntry t => t with { At = to },
                StartEntry s => s with { At = to },
                PortalEntry p => p with { A = p.A + by, B = p.B + by, To = p.To + new Vector3(by.X, 0f, by.Y) },
                _ => entry,
            };
        }

        // Which way it faces, in degrees clockwise from north, for those that face a way
        public static float? Facing(object entry) => entry switch
        {
            PropEntry p => p.Turn,
            ThingEntry t => t.Turn,
            StartEntry s => s.Yaw,
            _ => null,
        };

        public static object TurnedBy(object entry, float degrees)
        {
            static float Round(float d) => MathF.Round(((d % 360f) + 360f) % 360f, 3);
            return entry switch
            {
                PropEntry p => p with { Turn = Round(p.Turn + degrees) },
                ThingEntry t => t with { Turn = Round(MathF.Round(t.Turn / 90f) * 90f + MathF.CopySign(90f, degrees)) },   // a body faces four ways
                StartEntry s => s with { Yaw = Round(s.Yaw + degrees) },
                _ => entry,
            };
        }

        public const float StartWidth = 0.6f, StartHeight = 1.8f;   // a start's marker: about the size of someone standing there

        // Where someone starting at `at` stands: on the ground, or, with `above`, on the highest floor under the point
        // that far above it, which is where the game drops them (see Start). Without `ground` (the buildings' floors),
        // in the air there.
        public static float StartFloor(Vector2 at, float above, Terrain terrain, IGround? ground)
        {
            var height = terrain.HeightAt(at.X, at.Y) + above;
            return above > 0f && ground?.GroundBelow(new Vector3(at.X, height, at.Y), 0f) is { } floor ? floor : height;
        }

        // The box it fills, for drawing round it and picking it with the mouse: placed by `place` (its foot at y = 0,
        // centred), `size` big. A pad or a pool is a thin slab at its level.
        public static (Matrix place, Vector3 size) Box(object entry, Terrain terrain, MapLibrary library, IGround? ground = null)
        {
            float Ground(Vector2 at) => terrain.HeightAt(at.X, at.Y);
            Matrix At(Vector2 at, float y, float turn = 0f) => Matrix.CreateRotationY(turn) * Matrix.CreateTranslation(at.X, y, at.Y);
            switch (entry)
            {
                case PadEntry p:
                    return (At(p.Centre, Ground(p.Centre) - 0.05f), new Vector3(p.Half.X * 2f, 0.1f, p.Half.Y * 2f));
                case PoolEntry p:
                    var across = p.Half is { } half ? half * 2f : new Vector2(p.Radius * 2f);
                    return (At(p.Centre, p.Level - 0.05f), new Vector3(across.X, 0.1f, across.Y));
                case BuildingEntry b:
                    var footprint = library.BuildingKind(b.Kind).HalfOf(b);
                    return (At(b.At, Ground(b.At)), new Vector3(footprint.X * 2f, 6f, footprint.Y * 2f));
                case PropEntry p:
                    return (FileDistrict.PropPlace(p, terrain), library.Item(p.Item).Size);
                case ThingEntry t:
                    var (size, turn) = FileDistrict.ThingBox(library.Item(t.Item).Size, t.Turn);
                    // Its body's box, which doesn't turn; the mesh turns inside it
                    return (At(t.At, Ground(t.At) + t.Above), size);
                case StartEntry s:
                    return (At(s.At, StartFloor(s.At, s.Above, terrain, ground), FileDistrict.MeshTurn(s.Yaw)), new Vector3(StartWidth, StartHeight, StartWidth));
                case PortalEntry p:
                    var along = p.B - p.A;
                    return (At((p.A + p.B) / 2f, p.Floor, MathF.Atan2(-along.Y, along.X)),   // its width along the door
                            new Vector3(along.Length(), 2.1f, 0.2f));
                default:
                    throw new ArgumentException("Not an entry of a district file.", nameof(entry));
            }
        }

        // What it is, in a few words, for a panel or the status line
        public static string Describe(object entry) => entry switch
        {
            PadEntry p => $"pad, {p.Half.X * 2f:0.#} x {p.Half.Y * 2f:0.#} m",
            PoolEntry p => p.Half is { } h ? $"pool, {h.X * 2f:0.#} x {h.Y * 2f:0.#} m" : $"pool, {p.Radius:0.#} m round",
            BuildingEntry b => $"{b.Kind} '{b.Name ?? b.Id}'",
            PropEntry p => $"prop: {p.Item}",
            ThingEntry t => $"thing: {t.Name ?? t.Item}",
            StartEntry s => $"start '{s.Name}'",
            PortalEntry => "portal",
            _ => entry.ToString() ?? "",
        };
    }
}
