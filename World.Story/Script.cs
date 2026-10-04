using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ink;

namespace World.Story
{
    // An ink script, compiled. ink is written as text (.ink files, in Inky or any editor) and compiled to ink's own JSON,
    // which its runtime plays. Compiling here, in the game, rather than with inklecate beforehand, means a script can be
    // changed and reloaded while the game runs (the playground's `talk` does). Each StoryRunner gets its own story from
    // it (NewStory), so one script can be run afresh any number of times.
    public sealed class Script
    {
        public string Name { get; }
        public string Json { get; }
        public IReadOnlyList<string> Warnings { get; }

        // The conversations a game can start: every knot that isn't a function ("opening"), and its stitches ("mask.briefing")
        public IReadOnlyList<string> Conversations { get; }

        private Script(string name, string json, IReadOnlyList<string> warnings, IReadOnlyList<string> conversations)
        {
            Name = name;
            Json = json;
            Warnings = warnings;
            Conversations = conversations;
        }

        // Compiles ink source. INCLUDEd files are looked for in `folder` (the working directory if null). Throws
        // ScriptException, listing every error, if it doesn't compile.
        public static Script Compile(string source, string name = "script", string? folder = null)
        {
            var errors = new List<string>();
            var warnings = new List<string>();
            var options = new Compiler.Options
            {
                sourceFilename = name,
                fileHandler = new FolderFiles(folder ?? Directory.GetCurrentDirectory()),
                errorHandler = (message, type) => (type == ErrorType.Error ? errors : warnings).Add(message),
            };
            var compiler = new Compiler(source, options);
            var story = compiler.Compile();
            if (errors.Count > 0 || story == null)
                throw new ScriptException(name, errors.Count > 0 ? errors : new List<string> { "It didn't compile, and ink didn't say why." });
            var conversations = compiler.parsedStory.FindAll<Ink.Parsed.Knot>(k => !k.isFunction)
                .SelectMany(k => new[] { k.name }.Concat(k.FindAll<Ink.Parsed.Stitch>().Select(s => k.name + "." + s.name)))
                .ToList();
            return new Script(name, story.ToJson(), warnings, conversations);
        }

        public static Script FromFile(string path) =>
            Compile(File.ReadAllText(path), Path.GetFileName(path), Path.GetDirectoryName(Path.GetFullPath(path)));

        internal Ink.Runtime.Story NewStory() => new Ink.Runtime.Story(Json);

        // INCLUDE "game.ink" is found beside the script that includes it
        private sealed class FolderFiles : IFileHandler
        {
            private readonly string _folder;

            public FolderFiles(string folder) => _folder = folder;

            public string ResolveInkFilename(string includeName) => Path.Combine(_folder, includeName);

            public string LoadInkFileContents(string fullFilename) => File.ReadAllText(fullFilename);
        }
    }

    public sealed class ScriptException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public ScriptException(string script, IReadOnlyList<string> errors)
            : base($"{script}: {errors.Count} error(s). The first: {errors.FirstOrDefault()}") => Errors = errors;
    }

    // Where the scripts are: "Story and scripts" beside the solution when run from inside the repository (so a script
    // saved there is the one the game reads), otherwise the copy in "Story" beside the app.
    public static class ScriptFolder
    {
        public const string Extension = ".ink";
        private const string Solution = "VectorViktor.slnx", InRepository = "Story and scripts", BesideApp = "Story";

        private static string? _found;

        public static string Find() => _found ??= Search();

        private static string Search()
        {
            for (var folder = new DirectoryInfo(AppContext.BaseDirectory); folder != null; folder = folder.Parent)
                if (File.Exists(Path.Combine(folder.FullName, Solution)) && Directory.Exists(Path.Combine(folder.FullName, InRepository)))
                    return Path.Combine(folder.FullName, InRepository);
            return Path.Combine(AppContext.BaseDirectory, BesideApp);
        }

        public static string PathOf(string scriptName) => Path.Combine(Find(), scriptName + Extension);
    }
}
