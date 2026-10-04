using Hexa.NET.ImGui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using World.Story;
using Num = System.Numerics;

namespace Droid.Playground
{
    // Conversations written in ink (NarrativePlan.md), run by World.Story's StoryRunner and shown in a Dear ImGui window
    // as a stand-in for the real thing (the droid's console in the picture, the operator's crisp text over it: neither
    // built yet). It reads "Story and scripts/opening.ink" and reloads it whenever a script there is saved, so a
    // conversation can be written in Inky or any editor and tried here at once.
    //
    // Lines are revealed at their style's pace: the droid's by its text quality, from a broken typewriter that slips and
    // strikes over to a console's all-at-once. Each voice has its own face (Windows' own fonts, standing in for the pixel
    // glyph sets of NarrativePlan.md step 1): the drone and the console in Consolas, people (the operator, Mask) in
    // Segoe UI, and the droid in Courier New Bold, struck unevenly while it's a typewriter, and Consolas once repaired.
    // The sizes grow and shrink with the window. Cut scenes the script plays are pretended (a couple of seconds' wait, K
    // ends it); calls and events are shown as what the game would do. Choices are buttons, and everything said and
    // decided goes into the record shown below the conversation, which can be saved and loaded.
    public sealed class Talk : Experiment
    {
        private const string ScriptName = "opening";
        private const float CutSceneTime = 2f;   // how long a pretend cut scene holds the conversation

        // What's been shown in the conversation window: a beat, and how long ago it appeared (world time)
        private sealed class Shown
        {
            public Shown(Beat beat, int seed) { Beat = beat; Seed = seed; }
            public Beat Beat { get; }
            public int Seed { get; }   // for its slips and uneven typing, the same every time it's drawn
            public float Since { get; set; }
        }

        // An option the operator picked, shown in the conversation
        private sealed record Picked(string Text, string Id) : Beat;

        private readonly List<Shown> _shown = new List<Shown>();
        private Script? _script;
        private StoryRunner? _runner;
        private List<string> _errors = new List<string>();
        private string _status = "";
        private DateTime _scriptsChanged;
        private DateTime _lastLook;
        private int _conversation;
        private string _started = "opening";
        private bool _playOn = true, _next, _scrollDown, _overrideQuality;
        private int _quality;
        private float _cutScene;   // seconds left of a pretend cut scene
        private string? _call;     // the video call going on, if any
        private int _seeds;

        public override string Name => "talk";
        public override string About =>
            "Conversations written in ink, from 'Story and scripts/opening.ink' (reloaded when it's saved). Lines appear at " +
            "their style's pace, the droid's by its text quality; cut scenes are pretended (K ends one); choices are buttons. " +
            "Everything said and decided is kept in the record below, which can be saved and loaded.";

        private static string SavePath => Path.Combine(AppContext.BaseDirectory, "Saves", "talk.save.json");

        public override void Start(Session session)
        {
            Faces.Load();
            _scriptsChanged = ScriptsChanged();
            Load(new StoryRecord());
            Begin(_started, session);
        }

        // (Re)compiles the script and makes a runner for it carrying on `record`. Keeps the old ones if it doesn't compile.
        private bool Load(StoryRecord record)
        {
            try
            {
                _script = Script.FromFile(ScriptFolder.PathOf(ScriptName));
                _errors = _script.Warnings.Select(w => "warning: " + w).ToList();
            }
            catch (Exception e) when (e is ScriptException or IOException)
            {
                _errors = e is ScriptException s ? s.Errors.ToList() : new List<string> { e.Message };
                return false;
            }
            try
            {
                _runner = new StoryRunner(_script, record);
            }
            catch (Exception)   // ink's saved state doesn't fit the changed script: keep the rest of the record
            {
                record.Ink = null;
                _runner = new StoryRunner(_script, record);
                _status = "the script changed too much to keep ink's place in it: its memory of what's been seen starts again";
            }
            _conversation = Math.Max(0, _runner.Conversations.ToList().IndexOf(_started));
            return true;
        }

        private void Begin(string conversation, Session session)
        {
            if (_runner == null)
                return;
            _started = conversation;
            _shown.Clear();
            _cutScene = 0f;
            _call = null;
            _runner.Place = "playground." + conversation;
            _runner.Clock = session.Clock;
            try
            {
                _runner.Start(conversation);
                _next = true;
            }
            catch (Exception e)   // a conversation the script no longer has
            {
                _status = e.Message;
            }
        }

        public override void AfterTick(Session session, float dt)
        {
            foreach (var shown in _shown)
                shown.Since += dt;
            if (_runner == null)
                return;
            if (_cutScene > 0f)
            {
                _cutScene -= dt;
                if (_cutScene > 0f)
                    return;
                _next = true;   // the scene's over: carry on
            }
            var last = _shown.LastOrDefault();
            if (last != null && !Revealed(last))
                return;
            var holdOver = last == null || last.Beat is not Line line || last.Since >= RevealTime(last) + 0.8f + 0.03f * line.Text.Length;
            if (!_next && !(_playOn && holdOver))
                return;
            _next = false;
            _runner.Clock = session.Clock;
            if (_runner.Next() is not { } beat)
                return;
            _shown.Add(new Shown(beat, ++_seeds));
            _scrollDown = true;
            if (beat is Cue cue)
            {
                if (cue.Kind == Cue.Play && cue.Wait)
                    _cutScene = CutSceneTime;
                else if (cue.Kind == Cue.Call)
                    _call = $"{cue.Argument(0)} ({cue.Argument(1)})";
                else if (cue.Kind == Cue.HangUp)
                    _call = null;
                _next = _cutScene <= 0f;   // a cue isn't read: straight on to the next beat
            }
        }

        // K: finish the line being typed, or the cut scene being pretended
        public override void Skip(Session session)
        {
            if (_shown.LastOrDefault() is { } last && !Revealed(last))
                last.Since = RevealTime(last);
            else if (_cutScene > 0f)
                _cutScene = 0.0001f;
            else
                _next = true;
        }

        // How a line is revealed: letters a second for its style (the droid's by its text quality; NarrativePlan.md 6)
        private static float Rate(Line line) => line.Style switch
        {
            Speakers.DroidStyle => new[] { 14f, 24f, 50f, 110f, 2000f }[Math.Clamp(line.Quality ?? 0, 0, Speakers.Best)],
            Speakers.Console => 2000f,
            Speakers.OperatorStyle => 2000f,
            "code" => 160f,
            Speakers.CallStyle => 30f,   // as fast as it's spoken
            "ether" => 40f,
            _ => 80f,
        };

        // A number from 0 to 1 for letter `i` of a beat, the same every time
        private static float Noise(int seed, int i)
        {
            var h = (uint)(seed * 73856093) ^ (uint)(i * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xffff) / 65535f;
        }

        // How long after it appeared letter `i` is struck: evenly at a good quality, unevenly on a broken typewriter
        private static float StruckAt(Shown shown, int i)
        {
            if (shown.Beat is not Line line)
                return 0f;
            var unevenness = (line.Quality ?? Speakers.Best) switch { 0 => 1.2f, 1 => 0.5f, _ => 0f };
            var t = 0f;
            for (var k = 0; k < i; k++)
                t += (1f + unevenness * (Noise(shown.Seed, k) - 0.5f)) / Rate(line);
            return t;
        }

        private static float RevealTime(Shown shown) => shown.Beat is Line line ? StruckAt(shown, line.Text.Length) : 0f;

        private static bool Revealed(Shown shown) => shown.Since >= RevealTime(shown);

        // What a line shows `Since` in: the letters struck so far, the last a wrong one for a moment if it's a slip
        private static string Visible(Shown shown, Line line)
        {
            var count = 0;
            while (count < line.Text.Length && StruckAt(shown, count + 1) <= shown.Since)
                count++;
            var text = line.Text[..count];
            if (count > 0 && SlipAt(shown, line, count - 1) is { } wrong && shown.Since - StruckAt(shown, count) < 0.2f)
                text = text[..^1] + wrong;   // struck wrong, about to be struck over
            return text;
        }

        // Whether letter `i` of a line is struck wrong first, and the wrong letter (shown for a moment, then struck over)
        private static char? SlipAt(Shown shown, Line line, int i)
        {
            var slipRate = (line.Quality ?? Speakers.Best) switch { 0 => 0.08f, 1 => 0.03f, _ => 0f };
            return char.IsLetter(line.Text[i]) && Noise(shown.Seed + 7, i + 1) < slipRate
                ? (char)('a' + (int)(Noise(shown.Seed + 11, i + 1) * 26) % 26)
                : null;
        }

        private static Num.Vector4 ColourOf(string speaker, string style) => style switch
        {
            "code" => new Num.Vector4(0.45f, 1f, 0.55f, 1f),
            _ => speaker switch
            {
                Speakers.Droid => new Num.Vector4(1f, 0.38f, 0.42f, 1f),     // the ruby visor
                Speakers.Drone => new Num.Vector4(0.45f, 0.85f, 1f, 1f),
                Speakers.Mask => new Num.Vector4(1f, 0.82f, 0.35f, 1f),
                Speakers.Operator => new Num.Vector4(0.95f, 0.95f, 0.95f, 1f),
                _ => new Num.Vector4(0.75f, 0.75f, 0.75f, 1f),
            },
        };

        private static readonly Num.Vector4 Grey = new Num.Vector4(0.55f, 0.55f, 0.55f, 1f);

        public override void Panel(Session session)
        {
            LookForChanges(session);
            if (_runner != null)
                _runner.Record.QualityOverride = _overrideQuality ? _quality : null;
            ConversationWindow(session);

            if (ImGui.CollapsingHeader("Script", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextWrapped(ScriptFolder.PathOf(ScriptName));
                if (_errors.Count == 0)
                    ImGui.TextDisabled("compiled, no warnings (reloads when saved)");
                foreach (var error in _errors)
                {
                    ImGui.PushTextWrapPos(0f);
                    ImGui.TextColored(new Num.Vector4(1f, 0.5f, 0.4f, 1f), error);
                    ImGui.PopTextWrapPos();
                }
                if (_runner != null)
                {
                    var names = _runner.Conversations.ToArray();
                    ImGui.Combo("conversation", ref _conversation, names, names.Length);
                    if (ImGui.Button("start it"))
                        Begin(names[_conversation], session);
                }
                ImGui.Checkbox("play on by itself", ref _playOn);
                ImGui.SameLine();
                if (ImGui.Button("next (K)"))
                    Skip(session);
                if (_status.Length > 0)
                    ImGui.TextWrapped(_status);
            }

            if (_runner == null)
                return;
            var record = _runner.Record;

            if (ImGui.CollapsingHeader("The droid's text", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Text($"text quality {record.TextQuality}: {Speakers.QualityNames[record.TextQuality]}");
                ImGui.Checkbox("whatever its parts say", ref _overrideQuality);
                if (_overrideQuality)
                    ImGui.SliderInt("quality", ref _quality, 0, Speakers.Best);
                ImGui.TextDisabled("from parts: " + string.Join(", ", StoryRecord.TextParts.Select(p => $"{p.Key} +{p.Value}")));
            }

            if (ImGui.CollapsingHeader("The record", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.Text("parts: " + (record.Parts.Count == 0 ? "-" : string.Join(", ", record.Parts)));
                ImGui.Text("flags: " + (record.Flags.Count == 0 ? "-" : string.Join(", ", record.Flags.Select(f => $"{f.Key} = {f.Value}"))));
                ImGui.SeparatorText($"Decisions ({record.Decisions.Count})");
                foreach (var d in record.Decisions)
                    ImGui.TextWrapped($"{d.Time:F0} s, {d.Place}: {d.Id} \"{d.Text}\"");
                ImGui.SeparatorText($"Transcript ({record.Transcript.Count} lines)");
                foreach (var t in record.Transcript.TakeLast(8))
                    ImGui.TextWrapped($"{t.Time:F0} s {t.Speaker}{(t.Quality is { } q ? $" [q{q}]" : "")}: {t.Text}");
                if (ImGui.Button("save"))
                {
                    _runner.Save().Save(SavePath);
                    _status = "saved to " + SavePath;
                }
                ImGui.SameLine();
                if (ImGui.Button("load") && File.Exists(SavePath))
                {
                    Load(StoryRecord.Load(SavePath));
                    _shown.Clear();
                    _status = "loaded: run a conversation to see it carry on (try remarks.wet, or junction)";
                }
                ImGui.SameLine();
                if (ImGui.Button("forget everything"))
                {
                    Load(new StoryRecord());
                    Begin(_started, session);
                    _status = "a new record";
                }
            }
        }

        // The conversation itself, in a window of its own along the bottom
        private void ConversationWindow(Session session)
        {
            var display = ImGui.GetIO().DisplaySize;
            ImGui.SetNextWindowPos(new Num.Vector2(350f, display.Y - 340f * Faces.Scale), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Num.Vector2(display.X - 740f, 330f * Faces.Scale), ImGuiCond.FirstUseEver);
            ImGui.Begin("Conversation");
            Faces.Push(Faces.Person, Faces.Note);   // what's going on, and the cues: smaller, but grown with the window too
            if (_call != null)
                ImGui.TextColored(ColourOf(Speakers.Mask, ""), $"VIDEO CALL from {_call}: the video would play here, from outside the droid's broadcast");
            else if (_cutScene > 0f)
                ImGui.TextColored(Grey, $"a cut scene is playing ({_cutScene:F1} s; K ends it)");
            else
                ImGui.TextDisabled(_runner == null ? "no script" : _runner.Ended ? "(the conversation has ended)" : _started);
            ImGui.Separator();
            Faces.Pop(Faces.Person);

            var choices = _runner != null && (_shown.Count == 0 || Revealed(_shown[^1])) && _cutScene <= 0f ? _runner.Choices : Array.Empty<StoryChoice>();
            Faces.Push(Faces.Person);
            var buttonHeight = ImGui.GetFrameHeightWithSpacing();
            Faces.Pop(Faces.Person);
            ImGui.BeginChild("lines", new Num.Vector2(0f, -(choices.Count * buttonHeight + 4f)));
            foreach (var shown in _shown)
                Draw(shown);
            if (_scrollDown)
            {
                ImGui.SetScrollHereY(1f);
                _scrollDown = false;
            }
            ImGui.EndChild();

            Faces.Push(Faces.Person);   // the operator's choices, in the operator's own face
            foreach (var choice in choices)
                if (ImGui.Button($"{choice.Index + 1}. {choice.Text}"))
                {
                    _runner!.Clock = session.Clock;
                    _runner.Choose(choice.Index);
                    _shown.Add(new Shown(new Picked(choice.Text, choice.Id), ++_seeds));
                    _scrollDown = _next = true;
                }
            Faces.Pop(Faces.Person);
            ImGui.End();
        }

        private static void Draw(Shown shown)
        {
            ImGui.PushTextWrapPos(0f);
            switch (shown.Beat)
            {
                case Line line:
                    var who = line.Speaker == Speakers.Droid
                        ? $"{line.Name} ({Speakers.QualityNames[Math.Clamp(line.Quality ?? 0, 0, Speakers.Best)]})"
                        : line.Name.Length > 0 ? line.Name : "-";
                    var at = line.At is { } seconds ? $"[{(int)seconds / 60}:{(int)seconds % 60:00}] " : "";
                    var cursor = Revealed(shown) ? "" : "_";
                    var face = Faces.Of(line);
                    Faces.Push(face);
                    if (line.Style == Speakers.DroidStyle && (line.Quality ?? 0) <= 1)
                        Struck(shown, line, $"{who}: {at}", cursor);
                    else
                        ImGui.TextColored(ColourOf(line.Speaker, line.Style), $"{who}: {at}{Visible(shown, line)}{cursor}");
                    Faces.Pop(face);
                    break;
                case Cue cue:
                    Faces.Push(Faces.Person, Faces.Note);
                    ImGui.TextColored(Grey, cue.Kind switch
                    {
                        Cue.Play => $"[cut scene: {cue.Argument(0)}, the conversation waits for it]",
                        Cue.Start => $"[cut scene starts: {cue.Argument(0)}]",
                        Cue.Call => $"[video call from {cue.Argument(0)}: {cue.Argument(1)}.mp4]",
                        Cue.HangUp => "[the call ends]",
                        Cue.Event => $"[event: {cue.Argument(0)}]",
                        _ => $"[{cue}]",
                    });
                    Faces.Pop(Faces.Person);
                    break;
                case Picked picked:
                    Faces.Push(Faces.Person);
                    ImGui.TextColored(ColourOf(Speakers.Operator, ""), $"> {picked.Text}   (decided: {picked.Id})");
                    Faces.Pop(Faces.Person);
                    break;
            }
            ImGui.PopTextWrapPos();
        }

        // A typewriter line, letter by letter: each a pixel or two high or low, some faint, some over-inked (struck twice,
        // a little apart), and a slip's wrong letter left faintly under the right one struck over it. The label
        // ("Droid (broken typewriter): ") is typed evenly. Wraps at words to the window's width.
        private static void Struck(Shown shown, Line line, string label, string cursor)
        {
            var (lift, faint, heavy) = (line.Quality ?? 0) == 0 ? (1.6f, 0.4f, 0.25f) : (0.7f, 0.15f, 0.1f);
            var pixel = MathF.Max(1f, MathF.Round(ImGui.GetFontSize() / 18f));   // the unevenness grows with the letters
            lift *= pixel;
            var colour = ColourOf(line.Speaker, line.Style);
            var visible = Visible(shown, line);
            var draw = ImGui.GetWindowDrawList();
            var origin = ImGui.GetCursorScreenPos();
            var width = Math.Max(ImGui.GetContentRegionAvail().X, 40f);
            var advance = ImGui.CalcTextSize("M").X;   // Courier's letters are all as wide
            var lineHeight = ImGui.GetTextLineHeightWithSpacing();
            var text = label + visible + cursor;
            var x = 0f;
            var y = 0f;
            for (var c = 0; c < text.Length; c++)
            {
                if (text[c] == ' ' && x > 0f)   // a word that won't fit starts a new line
                {
                    var end = text.IndexOf(' ', c + 1);
                    var word = (end < 0 ? text.Length : end) - c - 1;
                    if (x + (word + 1) * advance > width)
                    {
                        x = 0f;
                        y += lineHeight;
                        continue;
                    }
                }
                var i = c - label.Length;   // which letter of the line this is (negative for the label)
                var at = origin + new Num.Vector2(x, y);
                var tint = colour;
                if (i >= 0 && i < line.Text.Length)
                {
                    at += new Num.Vector2(MathF.Round((Noise(shown.Seed + 3, i) - 0.5f) * lift * 0.6f),
                                          MathF.Round((Noise(shown.Seed + 5, i) - 0.5f) * 2f * lift));
                    tint.W *= 1f - faint * Noise(shown.Seed + 13, i);
                    if (i < visible.Length - 1 && SlipAt(shown, line, i) is { } wrong)
                        draw.AddText(at + new Num.Vector2(pixel, pixel), ImGui.GetColorU32(colour with { W = 0.3f }), wrong.ToString());
                    if (Noise(shown.Seed + 17, i) < heavy)
                        draw.AddText(at + new Num.Vector2(pixel, 0f), ImGui.GetColorU32(tint), text[c].ToString());
                }
                draw.AddText(at, ImGui.GetColorU32(tint), text[c].ToString());
                x += advance;
            }
            ImGui.Dummy(new Num.Vector2(width, y + lineHeight - ImGui.GetStyle().ItemSpacing.Y));
        }

        // The faces lines are drawn in: Windows' own fonts, added to Dear ImGui's atlas once (it makes each size's glyphs
        // as they're first drawn). One that's missing leaves its lines in Dear ImGui's own font. A font's size in pixels
        // doesn't say how big it looks: Courier's letters are wide and evenly spaced, so the others need a bigger size
        // to look as big beside it (sizes set by eye). The sizes are for a window 720 pixels high, and grow and shrink
        // with it. The typewriter is Courier New Bold: inked, not a thin hairline.
        private static class Faces
        {
            private const float ConsoleSize = 20f, PersonSize = 22f, TypewriterSize = 20f;
            public const float Note = 0.8f;   // a size for notes beside the lines, as a part of the face's own

            // How much bigger than at 720 pixels high the window is (never less than three quarters)
            public static float Scale => MathF.Max(0.75f, ImGui.GetIO().DisplaySize.Y / 720f);
            public static ImFontPtr Console, Person, Typewriter;
            private static bool _loaded;

            public static unsafe void Load()
            {
                if (_loaded)
                    return;
                _loaded = true;
                var fonts = ImGui.GetIO().Fonts;
                if (fonts.Fonts.Size == 0)   // the first font added becomes the panels' own: keep Dear ImGui's for them
                    ImGui.AddFontDefault(fonts);
                var folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                ImFontPtr Add(string file) =>
                    File.Exists(Path.Combine(folder, file)) ? fonts.AddFontFromFileTTF(Path.Combine(folder, file), 16f) : default;
                Console = Add("consola.ttf");
                Person = Add("segoeui.ttf");
                Typewriter = Add("courbd.ttf");
            }

            // A line's face: the droid's by its text quality (typewriter and teleprinter, then console)
            public static ImFontPtr Of(Line line) => line.Style switch
            {
                Speakers.DroidStyle when (line.Quality ?? 0) <= 2 => Typewriter,
                Speakers.DroidStyle or Speakers.Console or "code" => Console,
                _ => Person,
            };

            // Draws in `face` from here to Pop, at its size for the window (or `part` of it), in whole pixels
            public static unsafe void Push(ImFontPtr face, float part = 1f)
            {
                if (face.Handle == null)
                    return;
                var size = face.Handle == Typewriter.Handle ? TypewriterSize : face.Handle == Person.Handle ? PersonSize : ConsoleSize;
                ImGui.PushFont(face, MathF.Round(size * part * Scale));
            }

            public static unsafe void Pop(ImFontPtr face)
            {
                if (face.Handle != null)
                    ImGui.PopFont();
            }
        }

        // Reloads the script when any .ink file beside it has been saved (looked for twice a second)
        private void LookForChanges(Session session)
        {
            if ((DateTime.UtcNow - _lastLook).TotalSeconds < 0.5)
                return;
            _lastLook = DateTime.UtcNow;
            var changed = ScriptsChanged();
            if (changed == _scriptsChanged)
                return;
            _scriptsChanged = changed;
            if (_runner == null || Load(_runner.Save()))
            {
                _status = $"reloaded at {DateTime.Now:HH:mm:ss}";
                Begin(_started, session);
            }
        }

        private static DateTime ScriptsChanged()
        {
            var folder = ScriptFolder.Find();
            return Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*" + ScriptFolder.Extension).Select(File.GetLastWriteTimeUtc).DefaultIfEmpty().Max()
                : default;
        }
    }
}
