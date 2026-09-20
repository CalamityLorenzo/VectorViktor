# Mesh sharing refactor plan

Goal: one GPU copy of the ingot geometry, many `IngotFrustrum` instances that each carry only their own transform.

Files involved: `BasicTests/Meshes/IngotFrustrumMeshBuilder.cs`, `BasicTests/Meshes/MeshData.cs`, `BasicTests/IngotFrustrum.cs`, `BasicTests/Game1.cs`.

## Problems with the current code

1. **Sharing only works if everyone shares one builder.** `Game1.cs:56` does `new IngotFrustrumMeshBuilder(...)` inline per ingot, so each ingot gets its own cache and its own buffers. Nothing holds or disposes that builder, so the buffers are never freed.
2. **The builder is really a cache that owns GPU buffers**, not a build-and-dispose object. It has to outlive every ingot that uses the meshes.
3. **Colour is baked into the vertices**, so each `(top, side, other)` combination gets its own pair of `VertexBuffer`s. Identical geometry is duplicated per colour scheme.
4. **`MeshData` holds redundant CPU copies** (`Vertices`, `EdgeVertex`, `SolidVertex`) of data already in the `WriteOnly` buffers. Nothing reads them.
5. **`MeshData` is a struct that holds two `IDisposable` buffers.** Copies look independent but share the same GPU resources.

## Steps

### 1. Make `MeshData` a disposable class (fixes 4, 5)
- `sealed class MeshData : IDisposable` owning `SolidBuffer` and `EdgeBuffer`.
- Remove the CPU-side arrays (`Vertices`, `EdgeVertex`, `SolidVertex`).
- `Dispose()` disposes both buffers.

### 2. Split the builder into a factory and an owner (fixes 1, 2)
- Factory: static function that takes the raw vertices, colours and a `GraphicsDevice`, and returns a `MeshData`. Keep `BuildEdges`, `BuildSolid`, `AddLine`, `AddQuad`.
- Owner: `sealed class MeshCache : IDisposable`, holding the `Dictionary<(Color, Color, Color), MeshData>`, with `GetIngot(device, top, side, other)` that returns the cached mesh or builds one.
- `Game1` creates one `MeshCache`, keeps it in a field, and disposes it in `UnloadContent`/`Dispose`.
- `MeshCache.Dispose()` is just `foreach (var m in cache.Values) m.Dispose()`.

### 3. Simplify `IngotFrustrum` (fixes the two-step setup)
- Constructor takes a finished `MeshData` (no builder, no colours, no device).
- Delete `Configure()` and the `_builder` / colour fields.
- Ingots are then created with `new IngotFrustrum(cache.GetIngot(GraphicsDevice, top, side, other))`.

### 4. Share the render state
- Make `_depthOnlyBlend` and `_occluderRasterizer` `static readonly` (they are identical for every ingot and currently never disposed).

### 5. Fix bugs and cleanup
- `IngotFrustrum.Draw`: the solid-only path returns early (line ~70) without restoring `basicEffect.World`. Use `try/finally` or restore before returning.
- `BuildEdges` takes `OtherColor` but never uses it; drop the parameter.
- Remove the finalizer and `Dispose(bool)` boilerplate from the builder/cache (it owns no unmanaged resources directly, and GPU objects must not be touched from the finalizer thread). Use a plain `Dispose()`.
- Replace hard-coded primitive counts (`12`) with `buffer.VertexCount / 3` (triangles) and `buffer.VertexCount / 2` (lines).

## Optional: stop duplicating geometry per colour (fixes 3)
Only worth doing if you use many colour schemes. If you only use a handful, the cache from step 2 is fine.
- Store geometry only (`VertexPosition`, or position + normal).
- Draw in per-face-group ranges (top, bottom, sides), setting `basicEffect.DiffuseColor` before each range, with `VertexColorEnabled = false`.
- The cache is then keyed on shape alone.

## Suggested order
Steps 1 -> 2 -> 3 first (they fix the actual sharing and ownership problem), then 4 and 5, then the optional colour change.

## Target shape

```csharp
sealed class MeshData : IDisposable { public VertexBuffer Solid, Edges; /* Dispose both */ }

sealed class MeshCache : IDisposable   // owned by Game1, disposed in UnloadContent
{
    public MeshData GetIngot(GraphicsDevice d, Color top, Color side, Color other) { /* cached or built */ }
}

class IngotFrustrum
{
    public IngotFrustrum(MeshData mesh) { /* position + rotation only */ }
}
```
