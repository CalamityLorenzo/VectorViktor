# Mesh generalisation plan

Goal: keep the ownership/sharing pattern from `MeshRefactorPlan.md` (cache owns GPU buffers, instances borrow a mesh and carry only transform + colours) but make it work for **any** mesh, not just the ingot.

Files involved: `BasicTests/Meshes/MeshData.cs`, `BasicTests/Meshes/MeshCache.cs`, `BasicTests/IngotFrustrum.cs`, `BasicTests/Game1.cs`. New: `BasicTests/Meshes/DrawRange.cs`, `BasicTests/Meshes/IngotMesh.cs`, `BasicTests/MeshInstance.cs` (replaces `IngotFrustrum.cs`).

## What stays as is
- Cache owns buffers; `Game1` owns the cache and disposes it in `Dispose(bool)`.
- Instances hold only transform + palette + `EdgesOnly` / `NoEdgeColor`.
- Shared static `BlendState` / `RasterizerState` for the hidden-line pass.
- `Draw` saves and restores `World`, `DiffuseColor`, `VertexColorEnabled`.

## Problems being fixed
1. `MeshCache` is really an ingot builder (takes the ingot's `Vector3[]`, hard-codes 8 corners / 24 / 36 vertices, key is the literal `"ingot"`).
2. Draw ranges are `const`s on `MeshData`, so every mesh would share the ingot's numbers.
3. `IngotFrustrum` mixes generic code (transform, hidden-line passes, `DrawRange`) with ingot-specific code (`_topColor` / `_sideColor` / `_otherColor` and the mapping in `DrawSolid` / `DrawEdges`).
4. `MeshData` has public mutable fields and `Dispose()` throws if a buffer is null.
5. Leftovers: `IEnumerable<MeshData>` on `MeshCache` (unused), `BuildIngot` is really get-or-build.

## Steps

### 1. Describe draw ranges as data (fixes 2)
- New `readonly record struct DrawRange(int Start, int Primitives, int ColorSlot)`.
- `MeshData` becomes immutable and self-describing:
  - `VertexBuffer Solids, Edges`
  - `DrawRange[] SolidRanges, EdgeRanges`
  - `int PaletteSize` (highest `ColorSlot` + 1), used to validate instances.
- Constructor takes all of the above; delete the `Solid*` / `Edge*` consts.
- `Dispose()` null-safe (`Solids?.Dispose()`).
- The occluder pass still draws the whole `Solids` buffer in one call (`VertexCount / 3`), so it needs no ranges.

### 2. Move the ingot geometry into its own builder (fixes 1)
- New `static class IngotMesh` with `public static MeshData Build(GraphicsDevice device)`.
- Move `BuildEdges`, `BuildSolid`, `AddLine`, `AddQuad` and the `RawData.Basic_Ingot_Frustrum` reference here.
- Declare the palette slots next to the vertex ordering: `public const int Top = 0, Side = 1, Other = 2;`.
- The ranges live here too, because this file defines the vertex order:
  - Solids: `(0, 2, Top)`, `(6, 2, Other)`, `(12, 8, Side)`.
  - Edges: `(0, 4, Top)`, `(8, 8, Side)`.
- Add a convenience `public static Color[] Palette(Color top, Color side, Color other)` so callers don't have to remember slot order.

### 3. Make `MeshCache` only cache (fixes 1, 5)
- Parameterless constructor; no raw vertex data.
- `public MeshData GetOrAdd(string key, Func<GraphicsDevice, MeshData> build)`.
- `Dispose()` disposes every value and clears the dictionary.
- Delete `IEnumerable<MeshData>`, `GetEnumerator`, `BuildIngot`, `BuildMeshData` and all vertex-building code.
- Document the lifetime rule in a comment: instances must not outlive their cache.

### 4. Generic instance replaces `IngotFrustrum` (fixes 3)
- Rename `IngotFrustrum` to `MeshInstance` (file rename too).
- Constructor: `MeshInstance(MeshData mesh, Color[] palette)`; throw if `mesh` is null or `palette.Length < mesh.PaletteSize`.
- Remove `_topColor` / `_sideColor` / `_otherColor`.
- `DrawSolid` / `DrawEdges` loop over `mesh.SolidRanges` / `mesh.EdgeRanges`, calling `DrawRange` with `NoEdgeColor ? Color.White : palette[range.ColorSlot]` for edges.
- Keep `Position`, `Scale`, `Pitch`, `Yaw`, speeds, `World`, `EdgesOnly`, `NoEdgeColor`, `Update`, the try/finally state restore, and the static blend/rasterizer states unchanged.
- Delete the now-unused `DrawBuffer` overload if nothing else calls it (the occluder pass still does, so keep it).

### 5. Update `Game1`
- `_meshCache = new MeshCache();`
- In `LoadContent`: `var ingot = _meshCache.GetOrAdd("ingot", IngotMesh.Build);`
- `_ingots` becomes `List<MeshInstance>`; construct with `new MeshInstance(ingot, IngotMesh.Palette(scheme.Top, scheme.Side, scheme.Other)) { ... }`.
- Keep the random-call order in `BuildIngots` exactly the same so the fixed-seed scene is unchanged.

### 6. Prove it with a second mesh
- Add a small second builder (e.g. `PyramidMesh.Build`) with different vertex counts, a different number of ranges and a different palette size.
- Spawn a few instances of it next to the ingots via `GetOrAdd("pyramid", PyramidMesh.Build)`.
- This is the real test that nothing ingot-specific is left in the cache, `MeshData` or `MeshInstance`. Remove or keep it as a demo afterwards.

## Verification
- `dotnet build BasicTests` is clean after each step.
- After step 5 the 40-ingot scene must look identical to before (same seed, positions, colours, speeds). Space toggles edges-only and K toggles white edges on every instance.
- After step 6, pyramids draw correctly in solid and edges-only modes, and the red/green/blue triangle still keeps its vertex colours.
- Closing the game disposes the cache once, with no exceptions.

## Suggested order
1 -> 2 -> 3 -> 4 -> 5 (one commit each is fine; the build is only expected to pass again once 5 is done), then 6.

## Optional / later
- Record the vertex declaration on `MeshData` (and support `VertexPositionNormal`) once a mesh needs lighting.
- Add an `IndexBuffer` for meshes where vertex duplication matters; the ingot's 36 vertices for 8 corners is fine at this size.
- Sort or group instances by mesh so `SetVertexBuffer` only changes when the mesh changes. A performance tweak, only worth it with many meshes or thousands of instances.

## Target shape

```csharp
readonly record struct DrawRange(int Start, int Primitives, int ColorSlot);

sealed class MeshData : IDisposable
{
    public VertexBuffer Solids { get; }  public DrawRange[] SolidRanges { get; }
    public VertexBuffer Edges  { get; }  public DrawRange[] EdgeRanges  { get; }
    public int PaletteSize { get; }
}

sealed class MeshCache : IDisposable   // owned by Game1
{
    public MeshData GetOrAdd(string key, Func<GraphicsDevice, MeshData> build);
}

static class IngotMesh
{
    public const int Top = 0, Side = 1, Other = 2;
    public static MeshData Build(GraphicsDevice device);
    public static Color[] Palette(Color top, Color side, Color other);
}

class MeshInstance
{
    public MeshInstance(MeshData mesh, Color[] palette) { /* transform + palette only */ }
}
```
