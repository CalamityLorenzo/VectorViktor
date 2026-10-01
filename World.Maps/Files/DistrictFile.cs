using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace World.Maps.Files
{
    // A district as a file (Maps/districts/*.district.json): what's in it, every piece by name and where, never an
    // object - "a wooden crate at (3, 4)", not the crate itself. A FileDistrict turns it into an IDistrict, looking the
    // names up in a MapLibrary (catalogue items, kinds of building), so it joins the districts built in code through the
    // same WorldBuilder. Everything's in metres and degrees, the way you'd write it by hand; turns and yaws go clockwise
    // seen from above, 0 facing north (-Z), as everywhere else in the world.
    //
    // A change to what a file can hold is a new Version, and Load upgrades an older file as it reads it, so files
    // already written keep loading (see Load).
    public sealed class DistrictFile
    {
        public const int CurrentVersion = 1;

        [JsonRequired]
        public int Version { get; set; } = CurrentVersion;

        // What it is, for whoever opens the file
        public string About { get; set; }

        public List<PadEntry> Pads { get; set; } = new List<PadEntry>();
        public List<PoolEntry> Pools { get; set; } = new List<PoolEntry>();
        public List<BuildingEntry> Buildings { get; set; } = new List<BuildingEntry>();
        public List<PropEntry> Props { get; set; } = new List<PropEntry>();
        public List<ThingEntry> Things { get; set; } = new List<ThingEntry>();
        public List<StartEntry> Starts { get; set; } = new List<StartEntry>();
        public List<PortalEntry> Portals { get; set; } = new List<PortalEntry>();

        public static DistrictFile Load(string path)
        {
            var file = MapJson.Read<DistrictFile>(path);
            Versions.Check(path, file.Version, CurrentVersion);
            // Upgrades from older versions go here, oldest first, each taking a file from one version to the next:
            // if (file.Version == 1) { ...; file.Version = 2; }
            file.Pads ??= new List<PadEntry>();
            file.Pools ??= new List<PoolEntry>();
            file.Buildings ??= new List<BuildingEntry>();
            file.Props ??= new List<PropEntry>();
            file.Things ??= new List<ThingEntry>();
            file.Starts ??= new List<StartEntry>();
            file.Portals ??= new List<PortalEntry>();
            return file;
        }

        // Written to `path` if that changes it (see MapJson.Save): whether it did.
        public bool Save(string path) => MapJson.Save(path, this);

        // A copy that shares nothing that can change with this one (its entries can't change).
        public DistrictFile Copy() => new DistrictFile
        {
            Version = Version, About = About,
            Pads = new List<PadEntry>(Pads), Pools = new List<PoolEntry>(Pools), Buildings = new List<BuildingEntry>(Buildings),
            Props = new List<PropEntry>(Props), Things = new List<ThingEntry>(Things), Starts = new List<StartEntry>(Starts),
            Portals = new List<PortalEntry>(Portals),
        };
    }

    // Ground levelled (see TerrainGenerator.Pad): `Half` either side of `Centre`, an `Apron` of level ground round that
    // (2 m if not given) blending into the hills over `Blend` (6 m). `Raise` lifts it (or sinks it, below zero) from the
    // hills' height at its centre, or at `LevelWith`.
    public sealed record PadEntry(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 Centre,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 Half,
        float? Apron = null, float? Blend = null, float Raise = 0f, Vector2? LevelWith = null);

    // Standing water (see Pool), its surface at `Level`: in the circle `Radius` round `Centre`, or with a `Half`, a
    // rectangle (a swimming pool). It fills whatever ground's below its level there, so it needs a hollow: dig one
    // with a pad with a Raise below zero.
    public sealed record PoolEntry(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 Centre,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] float Level,
        float Radius = 0f, Vector2? Half = null, float? Shore = null);

    public enum Side { North, East, South, West }

    // A building from a generator (see BuildingKind): `Kind` names it ("house.bungalow"), `Id` names its rooms and
    // must be unique on the map, `At` is the middle of its ground floor. The ground under it is levelled for it. The
    // rest are the generator's to use or not (the kind says which: see BuildingKind): which side its door's in, its
    // colours, a flat roof rather than a pitched one, an attic, what its window looks onto.
    public sealed record BuildingEntry(string Kind, string Id,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 At,
        string Name = null, Side Door = Side.North, Color? Walls = null, Color? Roof = null, bool Flat = false, bool Attic = false,
        string View = null);

    // Something from the catalogue (see CatalogueItem) built into the world, standing on the ground (or `Above` it),
    // turned `Turn` degrees: it never moves, and you walk into its box (if the catalogue says it's solid).
    public sealed record PropEntry(string Item,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 At,
        float Turn = 0f, float Above = 0f);

    // Something from the catalogue lying about, to push, stack and knock over (see Thing): dropped onto the ground,
    // or onto what's under it from `Above` (1 m up on a 0.8 m crate stacks it). A body's box can only face the four
    // ways, so its Turn is to the nearest quarter turn. `Mass` in kilograms, or the catalogue's if not given; `Name`
    // for the overlays, or one's made up.
    public sealed record ThingEntry(string Item,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 At,
        float Turn = 0f, float Above = 0f, string Name = null, float Mass = 0f);

    // A named place to start (see Start), facing `Yaw` degrees.
    public sealed record StartEntry(string Name,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 At,
        float Yaw = 0f, float Above = 0f, bool InCar = false);

    // A door that takes you elsewhere (see Portal): walk into the wall from A to B, your feet near Floor, and you're at
    // To, facing Yaw degrees.
    public sealed record PortalEntry(
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 A,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector2 B,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] float Floor,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Vector3 To,
        float Yaw = 0f);

    internal static class Versions
    {
        public static void Check(string path, int version, int current)
        {
            if (version <= 0)
                throw new InvalidDataException($"{path}: it has no \"version\", so there's no knowing how to read it.");
            if (version > current)
                throw new InvalidDataException($"{path}: it's version {version}, written by something newer than this, " +
                                               $"which reads up to version {current}.");
        }
    }
}
