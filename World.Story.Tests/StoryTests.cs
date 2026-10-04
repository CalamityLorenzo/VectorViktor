using System.Collections.Generic;
using System.IO;
using System.Linq;
using World.Story;
using Xunit;

namespace World.Story.Tests
{
    // The story runner on the real script ("Story and scripts/opening.ink"), and on small scripts of its own.
    public class StoryTests
    {
        private static readonly Script Opening = Script.FromFile(ScriptFolder.PathOf("opening"));

        // Everything it gives out until it stops at a choice or ends
        private static List<Beat> RunOn(StoryRunner runner)
        {
            var beats = new List<Beat>();
            while (runner.Next() is { } beat)
                beats.Add(beat);
            return beats;
        }

        private static List<Line> Lines(IEnumerable<Beat> beats) => beats.OfType<Line>().ToList();

        private static Script Inline(string ink) => Script.Compile(ink, "test.ink", ScriptFolder.Find());

        [Fact]
        public void TheOpeningCompilesWithoutWarnings()
        {
            Assert.Empty(Opening.Warnings);
            var runner = new StoryRunner(Opening);
            foreach (var name in new[] { "opening", "mask.briefing", "junction", "scrapyard", "vocal_processor", "remarks.wet", "remarks.bump" })
                Assert.Contains(name, runner.Conversations);
            foreach (var function in new[] { "set", "give", "play", "text_quality" })   // game.ink's stand-ins aren't conversations
                Assert.DoesNotContain(function, runner.Conversations);
        }

        [Fact]
        public void ALineWithNoKnownNameCarriesOnWithWhoeverSpokeLast()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("opening");
            var lines = Lines(RunOn(runner));

            var memory = lines.Single(l => l.Text == "Memory: 30%");   // "Memory" isn't a speaker: still the droid
            Assert.Equal(Speakers.Droid, memory.Speaker);
            Assert.Equal(Speakers.DroidStyle, memory.Style);
            var retry = lines.Single(l => l.Text.StartsWith("Retry IFF"));
            Assert.Equal(Speakers.Drone, retry.Speaker);
            Assert.Equal(Speakers.Console, retry.Style);
            Assert.Equal("Physical connection: complete.", lines[0].Text);   // the name is taken off
        }

        [Fact]
        public void EscapedHashesAndStarsAreText()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("opening");
            var lines = Lines(RunOn(runner));

            var code = lines.Single(l => l.Text.StartsWith("SendWakeUp"));
            Assert.Equal("SendWakeUp: LDX #$00", code.Text);
            Assert.Equal("code", code.Style);
            Assert.Contains(lines, l => l.Text.StartsWith("*** ALERT"));
        }

        [Fact]
        public void ACutSceneComesBeforeTheLineAfterItAndAsksToBeWaitedFor()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("opening");
            var beats = RunOn(runner);

            var first = Assert.IsType<Cue>(beats[0]);
            Assert.Equal(Cue.Play, first.Kind);
            Assert.Equal("opening.arrive", first.Argument(0));
            Assert.True(first.Wait);

            var fall = beats.FindIndex(b => b is Cue { Kind: Cue.Play } c && c.Argument(0) == "opening.fall");
            Assert.StartsWith("A-HA!", Assert.IsType<Line>(beats[fall - 1]).Text);
            Assert.Equal("Switching to Ether broadcast.", Assert.IsType<Line>(beats[fall + 1]).Text);
        }

        [Fact]
        public void AnEventTagIsACueJustBeforeItsLine()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("opening");
            var beats = RunOn(runner);

            var zap = beats.FindIndex(b => b is Cue { Kind: Cue.Event } c && c.Argument(0) == "zap");
            Assert.Equal("Sharing charge.", Assert.IsType<Line>(beats[zap + 1]).Text);
            Assert.False(((Cue)beats[zap]).Wait);
        }

        [Fact]
        public void TheOpeningRunsOnIntoMasksCallAndStopsAtHisChoice()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("opening");
            var beats = RunOn(runner);

            var call = beats.OfType<Cue>().Single(c => c.Kind == Cue.Call);
            Assert.Equal(new[] { "mask", "mask-briefing-01" }, call.Arguments);
            var mask = Lines(beats).Where(l => l.Speaker == Speakers.Mask).ToList();
            Assert.Equal(Speakers.CallStyle, mask[0].Style);
            Assert.Equal(0.5f, mask[0].At);
            Assert.Equal(new[] { "mask.accept", "mask.whats-in-it", "mask.why-expensive" }, runner.Choices.Select(c => c.Id));
            Assert.False(runner.Ended);
        }

        [Fact]
        public void AChoiceIsKeptInTheDecisionLogAndItsFlagIsSet()
        {
            var runner = new StoryRunner(Opening) { Clock = 12.5f, Place = "coast.station" };
            runner.Start("junction");
            RunOn(runner);
            var left = runner.Choices.Single(c => c.Id == "junction.left");
            runner.Choose(left.Index);
            var after = Lines(RunOn(runner));

            Assert.Equal("Rolling.", after.Last().Text);
            Assert.True(runner.Ended);
            Assert.Equal("left", runner.Record.Flags["route"]);
            var decision = Assert.Single(runner.Record.Decisions);
            Assert.Equal(new Decision("junction.left", "Left", 12.5f, "coast.station"), decision);
        }

        [Fact]
        public void WhatWasDecidedBeforeChangesWhatIsSaidNextTime()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("junction");
            Assert.DoesNotContain(Lines(RunOn(runner)), l => l.Text.StartsWith("We have been here before"));
            runner.Choose(runner.Choices.Single(c => c.Id == "junction.right").Index);
            RunOn(runner);

            runner.Start("junction");
            var again = Lines(RunOn(runner));
            Assert.Contains(again, l => l.Text == "We have been here before. Last time we went right.");
        }

        [Fact]
        public void TakingThePartGivesItToTheDroidAndTheOptionGoes()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("scrapyard");
            RunOn(runner);
            runner.Choose(runner.Choices.Single(c => c.Id == "scrapyard.take-leg").Index);
            var beats = RunOn(runner);

            Assert.Contains("leg-left", runner.Record.Parts);
            Assert.Equal("fit-leg", beats.OfType<Cue>().Single(c => c.Kind == Cue.Play).Argument(0));

            runner.Start("scrapyard");
            Assert.Contains(Lines(RunOn(runner)), l => l.Text.StartsWith("I already have one"));
            Assert.Equal(new[] { "scrapyard.leave-leg" }, runner.Choices.Select(c => c.Id));
        }

        [Fact]
        public void TheDroidsTextQualityComesFromItsPartsAndIsKeptWithEachLine()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("vocal_processor");
            var before = Lines(RunOn(runner)).Single();
            Assert.Equal(0, before.Quality);
            Assert.StartsWith("Fnd smthng", before.Text);   // the script chooses to say it worse, too

            runner.Choose(runner.Choices.Single(c => c.Id == "vocal.fit").Index);
            var after = Lines(RunOn(runner)).Single();
            Assert.Equal(2, runner.Record.TextQuality);
            Assert.Equal(2, after.Quality);
            Assert.StartsWith("Testing, testing.", after.Text);
            Assert.Equal(new int?[] { 0, 2 }, runner.Record.Transcript.Select(t => t.Quality));
        }

        [Fact]
        public void OnlyTheDroidsLinesHaveAQuality()
        {
            var runner = new StoryRunner(Opening);
            runner.Start("opening");
            foreach (var line in Lines(RunOn(runner)))
                Assert.Equal(line.Speaker == Speakers.Droid, line.Quality.HasValue);
        }

        [Fact]
        public void AStoppingSequenceSaysSomethingNewEachTimeThenKeepsToItsLast()
        {
            var runner = new StoryRunner(Opening);
            var said = new List<string>();
            for (var i = 0; i < 7; i++)
            {
                runner.Start("remarks.wet");
                said.Add(Lines(RunOn(runner)).Single().Text);
            }
            Assert.Equal(new[] { "Water. Interesting.", "Wet again.", "Still wet.", "I was not built for this.", "...", "...", "..." }, said);
        }

        [Fact]
        public void SavedAndLoadedItCarriesOnFromTheSamePlace()
        {
            var runner = new StoryRunner(Opening) { Clock = 3f, Place = "house.bedroom1" };
            runner.Start("junction");
            RunOn(runner);
            runner.Choose(runner.Choices.Single(c => c.Id == "junction.left").Index);
            RunOn(runner);
            runner.Start("remarks.wet");
            RunOn(runner);
            runner.Start("scrapyard");
            RunOn(runner);
            runner.Choose(runner.Choices.Single(c => c.Id == "scrapyard.take-leg").Index);
            RunOn(runner);
            var json = runner.Save().ToJson();

            var loaded = new StoryRunner(Opening, StoryRecord.FromJson(json));
            Assert.Equal("left", loaded.Record.Flags["route"]);
            Assert.Contains("leg-left", loaded.Record.Parts);
            Assert.Equal(runner.Record.Decisions, loaded.Record.Decisions);
            Assert.Equal(runner.Record.Transcript, loaded.Record.Transcript);

            loaded.Start("remarks.wet");   // ink's own memory came back too: this is its second time
            Assert.Equal("Wet again.", Lines(RunOn(loaded)).Single().Text);
            loaded.Start("junction");
            Assert.Contains(Lines(RunOn(loaded)), l => l.Text == "We have been here before. Last time we went left.");
        }

        [Fact]
        public void FlagsKeepTheirKindThroughASave()
        {
            var record = new StoryRecord();
            record.Flags["done"] = true;
            record.Flags["count"] = 3;
            record.Flags["speed"] = 1.5f;
            record.Flags["route"] = "left";

            var loaded = StoryRecord.FromJson(record.ToJson());
            Assert.Equal(true, loaded.Flags["done"]);
            Assert.Equal(3, loaded.Flags["count"]);
            Assert.Equal(1.5f, loaded.Flags["speed"]);
            Assert.Equal("left", loaded.Flags["route"]);
        }

        [Fact]
        public void AScriptThatDoesntCompileSaysWhy()
        {
            var error = Assert.Throws<ScriptException>(() => Inline("=== broken ===\n-> nowhere\n"));
            Assert.Contains(error.Errors, e => e.Contains("nowhere"));
        }

        [Fact]
        public void AStyleTagOverridesTheSpeakersOwn()
        {
            var runner = new StoryRunner(Inline("INCLUDE game.ink\n=== talk ===\nDrone: Droid report! #style:ether\nDrone: Again.\n-> DONE\n"));
            runner.Start("talk");
            var lines = Lines(RunOn(runner));
            Assert.Equal(new[] { "ether", Speakers.Console }, lines.Select(l => l.Style));
        }

        [Fact]
        public void TheScriptsAreCopiedBesideTheApp()
        {
            Assert.True(File.Exists(Path.Combine(System.AppContext.BaseDirectory, "Story", "opening.ink")));
            Assert.True(File.Exists(Path.Combine(System.AppContext.BaseDirectory, "Story", "game.ink")));
        }
    }
}
