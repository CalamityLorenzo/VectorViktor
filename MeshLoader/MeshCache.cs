using MeshCore.Library;
using Microsoft.Xna.Framework.Graphics;

namespace MeshLoader
{
    // Owns every MeshData (and so every GPU buffer) it builds.
    // Lifetime rule: MeshInstances borrow meshes from here and must not outlive the cache.
    public class MeshCache : IDisposable
    {
        private readonly Dictionary<string, MeshData> _meshDataCache = new();

        public MeshData GetOrAdd(GraphicsDevice device, string key, Func<GraphicsDevice, MeshData> build)
        {
            if (!_meshDataCache.TryGetValue(key, out var meshData))
            {
                meshData = build(device);
                _meshDataCache[key] = meshData;
            }
            return meshData;
        }

        // A new instance of the source's mesh (built if it's not been asked for before), in the source's colours.
        public MeshInstance CreateInstance(GraphicsDevice device, MeshSource source) =>
            new MeshInstance(GetOrAdd(device, source.Key, source.Build), source.Palette);

        public void Dispose()
        {
            foreach (var meshData in _meshDataCache.Values)
                meshData.Dispose();
            _meshDataCache.Clear();
        }
    }
}
