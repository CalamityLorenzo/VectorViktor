using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace World.Maps.Files
{
    // How map and district files are written as JSON: names in camelCase, points as short arrays ([x, z] on the ground,
    // [x, y, z] in the air), colours as "#rrggbb", and each entry of a list (a prop, a pad, a start) on a line of its own,
    // with only the values that aren't their defaults - so a file reads as a list of what's in it, and a change to one
    // thing is a change to one line, which is what makes a diff of it (or a merge of two people's work) easy to follow.
    //
    // Reading is forgiving of what people do by hand (comments, a trailing comma) and strict about what they might get
    // wrong: a name it doesn't know ("trun" for "turn") is an error, not quietly ignored.
    public static class MapJson
    {
        // The entries written a line each in their lists (see OneEach)
        private static readonly Type[] Entries =
        {
            typeof(PadEntry), typeof(PoolEntry), typeof(BuildingEntry), typeof(PropEntry), typeof(ThingEntry),
            typeof(StartEntry), typeof(PortalEntry), typeof(DistrictRef),
        };

        // For each entry on its own: the same, but not indented, and without the OneEach converters (which would
        // otherwise call themselves for ever).
        private static readonly JsonSerializerOptions Inner = Make(indented: false);

        public static readonly JsonSerializerOptions Options = WithEntries(Make(indented: true));

        private static JsonSerializerOptions Make(bool indented)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = indented,
                IndentSize = 2,
                NewLine = "\n",   // the same file on every machine, whatever its line endings
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
                UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                RespectRequiredConstructorParameters = true,   // an entry without its item, or where it is, is an error
                TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { LeaveOutEmptyLists } },
            };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            options.Converters.Add(new Vector2Converter());
            options.Converters.Add(new Vector3Converter());
            options.Converters.Add(new ColorConverter());
            return options;
        }

        // A list with nothing in it isn't written: a district with no water has no "pools" at all.
        private static void LeaveOutEmptyLists(JsonTypeInfo info)
        {
            foreach (var property in info.Properties)
                if (typeof(System.Collections.ICollection).IsAssignableFrom(property.PropertyType))
                    property.ShouldSerialize = (_, value) => value is System.Collections.ICollection { Count: > 0 };
        }

        private static JsonSerializerOptions WithEntries(JsonSerializerOptions options)
        {
            foreach (var type in Entries)
                options.Converters.Add((JsonConverter)Activator.CreateInstance(typeof(OneEach<>).MakeGenericType(type)));
            return options;
        }

        // A file's contents, read; any mistake in it named with the file, and where in it.
        public static T Read<T>(string path)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
                    ?? throw new InvalidDataException($"{path}: the file is empty.");
            }
            catch (JsonException e)
            {
                throw new InvalidDataException($"{path}: {e.Message}", e);
            }
        }

        public static string Write<T>(T value) => JsonSerializer.Serialize(value, Options) + "\n";

        // Written to a file, only if that changes it: so saving what hasn't changed leaves the file alone (and anything
        // watching it - see MapWatcher - isn't told it's changed).
        public static bool Save<T>(string path, T value)
        {
            var text = Write(value);
            if (File.Exists(path) && File.ReadAllText(path) == text)
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, text);
            return true;
        }

        // A number as short as it can be and still read back exactly: 0.1, not 0.100000001.
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static float[] ReadNumbers(ref Utf8JsonReader reader, int count, string what)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
                throw new JsonException($"A {what} is written as an array of {count} numbers.");
            var numbers = new List<float>();
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                numbers.Add(reader.TokenType == JsonTokenType.Number ? reader.GetSingle()
                    : throw new JsonException($"A {what} is written as an array of {count} numbers."));
            if (numbers.Count != count)
                throw new JsonException($"A {what} has {count} numbers, not {numbers.Count}.");
            return numbers.ToArray();
        }

        // A point on the ground: [x, z]
        private sealed class Vector2Converter : JsonConverter<Vector2>
        {
            public override Vector2 Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                var n = ReadNumbers(ref reader, 2, "point on the ground ([x, z])");
                return new Vector2(n[0], n[1]);
            }

            public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options) =>
                writer.WriteRawValue($"[{Number(value.X)},{Number(value.Y)}]");   // spaced by OneEach
        }

        // A point anywhere: [x, y, z]
        private sealed class Vector3Converter : JsonConverter<Vector3>
        {
            public override Vector3 Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                var n = ReadNumbers(ref reader, 3, "point ([x, y, z])");
                return new Vector3(n[0], n[1], n[2]);
            }

            public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options) =>
                writer.WriteRawValue($"[{Number(value.X)},{Number(value.Y)},{Number(value.Z)}]");
        }

        // A colour: "#rrggbb", as in a web page
        private sealed class ColorConverter : JsonConverter<Color>
        {
            public override Color Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                var text = reader.GetString();
                if (text is { Length: 7 } && text[0] == '#' &&
                    uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
                    return new Color((int)(rgb >> 16) & 255, (int)(rgb >> 8) & 255, (int)rgb & 255);
                throw new JsonException($"A colour is written \"#rrggbb\", not \"{text}\".");
            }

            public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) =>
                writer.WriteStringValue($"#{value.R:x2}{value.G:x2}{value.B:x2}");
        }

        // A list of entries, each on a line of its own: each written without indenting, then the list put in the file as
        // it is, indented to where it is in the file.
        private sealed class OneEach<T> : JsonConverter<List<T>>
        {
            // What the list is called in a file: "props" for PropEntry
            private static readonly string Called = typeof(T) == typeof(DistrictRef) ? "districts" : typeof(T).Name.Replace("Entry", "").ToLowerInvariant() + "s";

            // Each entry read on its own, so a mistake in one can say which it is
            public override List<T> Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartArray)
                    throw new JsonException($"The {Called} are a list: [ ... ].");
                var list = new List<T>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    try
                    {
                        list.Add(JsonSerializer.Deserialize<T>(ref reader, Inner));
                    }
                    catch (JsonException e)
                    {
                        throw new JsonException($"The {Called}, number {list.Count + 1}: {e.Message}", e);
                    }
                return list;
            }

            public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
            {
                var indent = new string(' ', options.IndentSize * writer.CurrentDepth);
                var lines = value.Select(entry => indent + new string(' ', options.IndentSize) + Spaced(JsonSerializer.Serialize(entry, Inner)));
                writer.WriteRawValue($"[\n{string.Join(",\n", lines)}\n{indent}]");
            }
        }

        // A space after each comma and colon that isn't inside a string, so a line reads {"at": [1, 2], "turn": 90}.
        private static string Spaced(string json)
        {
            var spaced = new System.Text.StringBuilder(json.Length + 16);
            var (inString, escaped) = (false, false);
            foreach (var c in json)
            {
                spaced.Append(c);
                if (inString)
                    (inString, escaped) = (escaped || c != '"', !escaped && c == '\\');
                else if (c == '"')
                    inString = true;
                else if (c is ',' or ':')
                    spaced.Append(' ');
            }
            return spaced.ToString();
        }

        // Nothing in `known` is called `name`: a message saying so, and what there is.
        public static string Unknown(string what, string name, IEnumerable<string> known) =>
            $"There's no {what} called '{name}'. There are: {string.Join(", ", known.OrderBy(k => k, StringComparer.Ordinal))}.";
    }
}
