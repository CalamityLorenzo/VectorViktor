using MeshCore.Library;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace MeshRendering
{
    // Owns every MeshData (and so every GPU buffer) it builds.
    // Lifetime rule: MeshInstances borrow meshes from here and must not outlive the cache.
    //
    // A key names one mesh: ask twice with the same key and the second gets the first one's mesh, whatever it
    // says to build. So a key has to say everything that decides the mesh's shape (see LadderMesh.Source, say).
    // In a debug build, asking again with the same key but a different way of building it - another method, or
    // the same lambda holding different values - is an error, rather than quietly drawing the wrong mesh.
    public class MeshCache : IDisposable
    {
        private readonly Dictionary<string, MeshData> _meshDataCache = new();
        private readonly Dictionary<string, Func<GraphicsDevice, MeshData>> _builtWith = new();

        public MeshData GetOrAdd(GraphicsDevice device, string key, Func<GraphicsDevice, MeshData> build)
        {
            if (!_meshDataCache.TryGetValue(key, out var meshData))
            {
                meshData = build(device);
                _meshDataCache[key] = meshData;
                _builtWith[key] = build;
            }
            else
                CheckSameBuild(key, build);
            return meshData;
        }

        // A new instance of the source's mesh (built if it's not been asked for before), in the source's colours.
        public MeshInstance CreateInstance(GraphicsDevice device, MeshSource source) =>
            new MeshInstance(GetOrAdd(device, source.Key, source.Build), source.Palette);

        [Conditional("DEBUG")]
        private void CheckSameBuild(string key, Func<GraphicsDevice, MeshData> build)
        {
            var first = _builtWith[key];
            if (first.Method != build.Method)
                throw new InvalidOperationException(
                    $"Mesh key '{key}' was first built by {Describe(first)} and is now asked for with {Describe(build)}: " +
                    "give meshes that can differ keys that differ.");
            if (!SameCapture(first.Target, build.Target))
                throw new InvalidOperationException(
                    $"Mesh key '{key}' is asked for again with {Describe(build)} holding different values from the first time: " +
                    "put everything that decides the mesh's shape in its key.");
        }

        // A lambda's captured values (the fields of the compiler's closure class) compared one by one; any other
        // target (an instance whose method builds the mesh) is compared with Equals.
        private static bool SameCapture(object? a, object? b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (a == null || b == null || a.GetType() != b.GetType())
                return false;
            if (!a.GetType().IsDefined(typeof(CompilerGeneratedAttribute)))
                return a.Equals(b);
            foreach (var field in a.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                if (!Equals(field.GetValue(a), field.GetValue(b)))
                    return false;
            return true;
        }

        private static string Describe(Delegate build) => $"{build.Method.DeclaringType?.Name}.{build.Method.Name}";

        public void Dispose()
        {
            foreach (var meshData in _meshDataCache.Values)
                meshData.Dispose();
            _meshDataCache.Clear();
            _builtWith.Clear();
        }
    }
}
