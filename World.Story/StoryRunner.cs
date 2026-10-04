using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ink;
using Ink.Runtime;

namespace World.Story
{
    // Runs a script for the game: a conversation started by name (Start: an ink knot), then its beats one at a time
    // (Next) - lines, and cues for the game to act on - until it stops at a choice (Choices, Choose) or ends (Ended).
    // Everything said and decided goes into its StoryRecord as it happens.
    //
    // The script talks to the game through external functions, which it declares (see "Story and scripts/game.ink",
    // which also gives each a stand-in, so a script still runs in Inky):
    //
    //   set(name, value)  get(name)        a story flag, which the world reads too
    //   give(part)  take(part)  has(part)  the droid's parts
    //   decided(id)                        whether a choice with that id has been made
    //   text_quality()                     the droid's text quality, 0 (broken typewriter) to 4 (console)
    //   play(scene)  start(scene)          a cut scene: play holds the conversation till it's done, start doesn't
    //   call(caller, video)  hangup()      a video call to the operator
    //
    // and through tags: "style:x" on a line (how it looks), "event:x" (a one-off for the game: a flash, a sound),
    // "at:seconds" (when, in a call's video, it's spoken), "id:x" on a choice (what the decision log calls it).
    public sealed class StoryRunner
    {
        private readonly Ink.Runtime.Story _story;
        private readonly Queue<Beat> _pending = new Queue<Beat>();
        private string _speaker = "", _name = "";

        public StoryRecord Record { get; }
        public Script Script { get; }
        public List<string> Warnings { get; } = new List<string>();

        // Kept with each line and choice in the record: seconds played, and where (map.district), both the game's to set
        public float Clock { get; set; }
        public string Place { get; set; } = "";

        // The conversation last started, or null
        public string? Conversation { get; private set; }

        // Starts from where `record` left off (its ink state), or a new record if none.
        public StoryRunner(Script script, StoryRecord? record = null)
        {
            Script = script;
            Record = record ?? new StoryRecord();
            _story = script.NewStory();
            _story.onError += (message, type) =>
            {
                if (type == ErrorType.Error)
                    throw new ScriptException(script.Name, new[] { message });
                Warnings.Add(message);
            };
            Bind();
            if (Record.Ink != null)
                _story.state.LoadJson(Record.Ink);
        }

        private void Bind()
        {
            // Ones that only read are safe for ink to call while it looks ahead to the end of a line; ones that change
            // something must wait until the line before them has been given out (lookaheadSafe false).
            _story.BindExternalFunction<string, object>("set", (name, value) => { Record.Flags[name] = value; });
            _story.BindExternalFunction<string>("get", name => Record.Flags.TryGetValue(name, out var value) ? value : 0, lookaheadSafe: true);
            _story.BindExternalFunction<string>("give", part => { Record.Parts.Add(part); });
            _story.BindExternalFunction<string>("take", part => { Record.Parts.Remove(part); });
            _story.BindExternalFunction<string>("has", part => (object)Record.Parts.Contains(part), lookaheadSafe: true);
            _story.BindExternalFunction<string>("decided", id => (object)Record.Decided(id), lookaheadSafe: true);
            _story.BindExternalFunction("text_quality", () => (object)Record.TextQuality, lookaheadSafe: true);
            _story.BindExternalFunction<string>("play", scene => { Ask(World.Story.Cue.Play, true, scene); });
            _story.BindExternalFunction<string>("start", scene => { Ask(World.Story.Cue.Start, false, scene); });
            _story.BindExternalFunction<string, string>("call", (caller, video) => { Ask(World.Story.Cue.Call, false, caller, video); });
            _story.BindExternalFunction("hangup", () => { Ask(World.Story.Cue.HangUp, false); });
        }

        private void Ask(string kind, bool wait, params string[] arguments) => _pending.Enqueue(new Cue(kind, arguments, wait));

        public IReadOnlyList<string> Conversations => Script.Conversations;

        // Runs a conversation from its start (whatever was being said is dropped). A knot's stitch is "knot.stitch".
        public void Start(string conversation)
        {
            _pending.Clear();
            _speaker = _name = "";
            _story.ChoosePathString(conversation);
            Conversation = conversation;
        }

        // The next beat, or null when it's waiting for a choice or has ended.
        public Beat? Next()
        {
            while (_pending.Count == 0 && _story.canContinue)
            {
                var text = _story.Continue().Trim();   // external functions called on the way add their cues first
                var tags = _story.currentTags.ToList();
                foreach (var e in Values(tags, "event"))
                    _pending.Enqueue(new Cue(World.Story.Cue.Event, new[] { e }, false));
                if (text.Length > 0)
                    _pending.Enqueue(Say(text, tags));
            }
            return _pending.Count > 0 ? _pending.Dequeue() : null;
        }

        private Line Say(string text, List<string> tags)
        {
            if (Speakers.Split(text) is { } said)
                (_speaker, _name, text) = said;
            var style = Values(tags, "style").FirstOrDefault() ?? Speakers.StyleOf(_speaker);
            int? quality = _speaker == Speakers.Droid ? Record.TextQuality : null;
            float? at = float.TryParse(Values(tags, "at").FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) ? seconds : null;
            Record.Transcript.Add(new TranscriptEntry(Clock, Place, _speaker, style, quality, text));
            return new Line(_speaker, _name, text, style, quality, at, tags);
        }

        // The values of tags "key:value" with this key
        private static IEnumerable<string> Values(IEnumerable<string>? tags, string key) =>
            (tags ?? Enumerable.Empty<string>())
                .Where(t => t.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase))
                .Select(t => t[(key.Length + 1)..].Trim());

        // The options, once it's said everything up to them (empty while there's more to say, or when it's ended).
        public IReadOnlyList<StoryChoice> Choices =>
            _pending.Count > 0 || _story.canContinue
                ? Array.Empty<StoryChoice>()
                : _story.currentChoices.Select(c => new StoryChoice(c.index, c.text, Values(c.tags, "id").FirstOrDefault() ?? c.text)).ToList();

        public bool Ended => _pending.Count == 0 && !_story.canContinue && _story.currentChoices.Count == 0;

        // Picks an option (by its Index), and keeps the decision.
        public void Choose(int index)
        {
            var choice = Choices.FirstOrDefault(c => c.Index == index) ?? throw new ArgumentOutOfRangeException(nameof(index), index, "Not one of the choices on offer.");
            Record.Decisions.Add(new Decision(choice.Id, choice.Text, Clock, Place));
            _story.ChooseChoiceIndex(index);
        }

        // The record with ink's state brought up to date, ready to save. Best done between beats: beats already taken
        // from ink but not yet given out aren't in ink's state.
        public StoryRecord Save()
        {
            Record.Ink = _story.state.ToJson();
            return Record;
        }

        // An ink variable's value (a VAR in the script), or null if there's no such variable
        public object? Variable(string name) => _story.variablesState.GlobalVariableExistsWithName(name) ? _story.variablesState[name] : null;
    }
}
