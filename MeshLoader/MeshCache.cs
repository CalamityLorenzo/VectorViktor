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

        public void Dispose()
        {
            foreach (var meshData in _meshDataCache.Values)
                meshData.Dispose();
            _meshDataCache.Clear();
        }
    }
}
