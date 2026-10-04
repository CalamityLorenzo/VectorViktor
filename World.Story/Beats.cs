using System;
using System.Collections.Generic;

namespace World.Story
{
    // What a conversation gives the game, one at a time (see StoryRunner.Next): something said, or something to do.
    public abstract record Beat;

    // One thing said. `Speaker` is who (a key in Speakers: "droid", "drone", "mask"; "" for no one in particular), `Name`
    // how they're shown, `Style` how it looks (see Speakers.StyleOf), `Quality` the droid's text quality when it was said
    // (null for anyone else's), `At` when in a call it's spoken (an "at:" tag), and `Tags` all its ink tags.
    public sealed record Line(string Speaker, string Name, string Text, string Style, int? Quality, float? At, IReadOnlyList<string> Tags) : Beat;

    // Something for the game to do: play a cut scene, start a call, fire an event. `Wait` says whether the conversation
    // should hold its next beat until the game says it's done (a cut scene started with play, not start).
    public sealed record Cue(string Kind, IReadOnlyList<string> Arguments, bool Wait) : Beat
    {
        public const string Play = "play", Start = "start", Call = "call", HangUp = "hangup", Event = "event";

        public string Argument(int index) => index < Arguments.Count ? Arguments[index] : "";

        public override string ToString() => Arguments.Count == 0 ? Kind : $"{Kind}({string.Join(", ", Arguments)})";
    }

    // An option the player can pick. `Id` is what the decision log keeps (its "id:" tag; its text if it has none).
    public sealed record StoryChoice(int Index, string Text, string Id);

    // Who's in the story and how their lines look. An ink line starting "Name:" with a name here is theirs; any other
    // line (say "Memory: 30%" in the droid's diagnostics) carries on with whoever spoke last.
    public static class Speakers
    {
        public const string Droid = "droid", Drone = "drone", Mask = "mask", Operator = "operator";

        // Styles (see NarrativePlan.md section 6): the droid's own changes with its text quality
        public const string DroidStyle = "droid", Console = "console", CallStyle = "call", OperatorStyle = "operator", Narration = "narration";

        private static readonly Dictionary<string, (string key, string style)> Known = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Droid"] = (Droid, DroidStyle),
            ["Drone"] = (Drone, Console),
            ["Mask"] = (Mask, CallStyle),
            ["Kensington Mask"] = (Mask, CallStyle),
            ["Operator"] = (Operator, OperatorStyle),
        };

        // The speaker a line starts with, and the rest of it; or null if it doesn't start with a known name.
        public static (string key, string name, string rest)? Split(string text)
        {
            var colon = text.IndexOf(':');
            if (colon <= 0)
                return null;
            var name = text[..colon].Trim();
            return Known.TryGetValue(name, out var known) ? (known.key, name, text[(colon + 1)..].Trim()) : null;
        }

        public static string StyleOf(string speaker)
        {
            foreach (var (key, style) in Known.Values)
                if (key == speaker)
                    return style;
            return Narration;
        }

        // The droid's text quality stages (NarrativePlan.md section 6), 0 to Best
        public const int Best = 4;
        public static readonly string[] QualityNames = { "broken typewriter", "typewriter", "teleprinter", "dot matrix", "console" };
    }
}
