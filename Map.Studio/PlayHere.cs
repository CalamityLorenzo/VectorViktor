using Microsoft.Xna.Framework;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MapStudio
{
    // "Play here": the playtest harness (Droid.Playground) started as a program of its own, on the map open in the studio,
    // dropping the droid at a point, facing a way. It watches the map's files (see MapWatcher), so each save in the studio
    // is in the running harness a moment later: edit, save, look, edit again.
    public static class PlayHere
    {
        private const string Harness = "Droid.Playground";

        // The harness's program: built beside the studio's own, in the same configuration (Debug or Release), found from
        // the repository's folder. Null if it hasn't been built.
        public static string? Find()
        {
            var studio = new DirectoryInfo(AppContext.BaseDirectory);
            // ...\Map.Studio\bin\Debug\net10.0-windows\ : the configuration and framework are the two folders below bin
            var framework = studio.Name;
            var configuration = studio.Parent?.Name;
            for (var folder = studio; folder != null; folder = folder.Parent)
            {
                var exe = Path.Combine(folder.FullName, Harness, "bin", configuration ?? "Debug", framework, Harness + ".exe");
                if (File.Exists(exe))
                    return exe;
            }
            return null;
        }

        // Starts it: on the map in `mapFile`, at `at` on the ground, facing `yaw` (radians, as the camera's). Says what
        // happened, for the status line.
        public static string Start(string mapFile, Vector2 at, float yaw)
        {
            var exe = Find();
            if (exe == null)
                return $"Play here needs {Harness} built first (dotnet build {Harness}).";
            static string N(float x) => x.ToString("0.##", CultureInfo.InvariantCulture);
            var degrees = MathHelper.ToDegrees(MathHelper.WrapAngle(yaw));
            var arguments = new[] { mapFile, $"at={N(at.X)},{N(at.Y)}", $"yaw={N(degrees)}" };
            var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) };
            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);
            Process.Start(start);
            return $"Playing at {N(at.X)}, {N(at.Y)}: {Harness} {string.Join(' ', arguments.Skip(1))}";
        }
    }
}
