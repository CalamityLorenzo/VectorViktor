using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace World.Story
{
    // A line as it was shown: when (seconds played), where, who, in what style, at what quality (the droid's), and what.
    public sealed record TranscriptEntry(float Time, string Place, string Speaker, string Style, int? Quality, string Text);

    // A choice as it was made: which (its id), what the option said, when and where.
    public sealed record Decision(string Id, string Text, float Time, string Place);

    // What the story keeps (NarrativePlan.md section 7, "The record"): the flags conversations set and the world reads,
    // the droid's parts, every line shown, every choice made, and ink's own state (where it is in the script, its
    // variables, what's been seen). Saved and loaded as one JSON file. The start of the game state of GameDesign.md
    // section 4: parts and flags will move to it when it exists.
    public sealed class StoryRecord
    {
        public const int Version = 1;

        // How much each part adds to the droid's text quality. A stand-in: which repairs move it on is still open
        // (NarrativePlan.md question 11).
        public static readonly IReadOnlyDictionary<string, int> TextParts = new Dictionary<string, int>
        {
            ["vocal-processor"] = 2,
            ["memory-card"] = 1,
            ["language-chip"] = 1,
        };

        public Dictionary<string, object> Flags { get; } = new Dictionary<string, object>();
        public SortedSet<string> Parts { get; } = new SortedSet<string>();
        public List<TranscriptEntry> Transcript { get; } = new List<TranscriptEntry>();
        public List<Decision> Decisions { get; } = new List<Decision>();
        public string? Ink { get; set; }   // ink's state, as ink's own JSON

        // For trying things out: the droid's text quality whatever its parts say, or null to go by its parts
        public int? QualityOverride { get; set; }

        public int TextQuality => QualityOverride ?? Math.Min(Speakers.Best, Parts.Sum(p => TextParts.TryGetValue(p, out var q) ? q : 0));

        public bool Decided(string id) => Decisions.Any(d => d.Id == id);

        // Saving: ink's values are ints, floats, strings and booleans, and so are the flags
        private sealed record Saved(int Version, Dictionary<string, JsonElement>? Flags, List<string>? Parts,
                                    List<TranscriptEntry>? Transcript, List<Decision>? Decisions, string? Ink);

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public string ToJson() => JsonSerializer.Serialize(new Saved(Version,
            Flags.ToDictionary(f => f.Key, f => JsonSerializer.SerializeToElement(f.Value)),
            Parts.ToList(), Transcript, Decisions, Ink), Json);

        public static StoryRecord FromJson(string json)
        {
            var saved = JsonSerializer.Deserialize<Saved>(json, Json) ?? throw new InvalidDataException("Not a saved story.");
            if (saved.Version != Version)
                throw new InvalidDataException($"A saved story of version {saved.Version}; this game reads version {Version}.");
            var record = new StoryRecord { Ink = saved.Ink };
            foreach (var (name, value) in saved.Flags ?? new Dictionary<string, JsonElement>())
                record.Flags[name] = FromJson(value);
            record.Parts.UnionWith(saved.Parts ?? new List<string>());
            record.Transcript.AddRange(saved.Transcript ?? new List<TranscriptEntry>());
            record.Decisions.AddRange(saved.Decisions ?? new List<Decision>());
            return record;
        }

        private static object FromJson(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetInt32(out var i) => i,
            JsonValueKind.Number => (float)value.GetDouble(),
            _ => value.GetString() ?? "",
        };

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllText(path, ToJson());
        }

        public static StoryRecord Load(string path) => FromJson(File.ReadAllText(path));
    }
}
