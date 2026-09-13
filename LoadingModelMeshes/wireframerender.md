# Wireframe renderer: how it works

An overview of the retro (Elite / Mercenary / Tau Ceti style) hidden-line-removal wireframe
renderer in this project, and how it's structured to support many placed model instances.

## Files

- **`LoadingModelMeshesGame1.cs`** — the `Game` loop: content loading, the scene list, the
  orbit camera, input handling, and the two-pass draw.
- **`WireframeModel.cs`** — one placed instance of a `Model` in the scene (its own position and
  spin), plus the precomputed rendering data it needs.
- **`WireframeGeometry.cs`** — static helpers that turn a loaded `Model` into hard-edge line
  data and a bounding sphere, by reading its vertex/index buffers directly.
- **`Helpers.cs`** — fullscreen toggle helper (unrelated to rendering).

## Content loading

The FBX model is imported via the standard MonoGame content pipeline
(`Content/Content.mgcb`, `FbxImporter` → `ModelProcessor`) and loaded with
`Content.Load<Model>("EastGermanCar")`. The model's vertex data has no normals, so
`BasicEffect.LightingEnabled` is always left `false` — enabling default lighting would throw,
since the lighting shader requires a `NORMAL0` vertex element that isn't present.

## Scene: many instances of a model

Rather than a single hardcoded model, the game keeps:

- `List<WireframeModel> _sceneModels` — every instance to draw this frame.
- `Dictionary<Model, ModelRenderData> _renderDataByModel` — a cache keyed by the loaded
  `Model` asset, holding its hard-edge line lists (per `ModelMesh`) and local bounding sphere.

`AddModelInstance(model, position)` builds (or reuses) that cached render data, then creates a
`WireframeModel` wrapping the `Model` + cache + a world position. Multiple instances can point
at the *same* `Model` (e.g. several cars) without repeating the (relatively expensive) edge
extraction — it only runs once per unique asset.

`WireframeModel.GetWorldMatrix()` rotates the model around **its own** bounding-sphere center,
then translates it to its scene `Position`:

```
World = Translate(-LocalCenter) * RotateY(RotationY) * Translate(LocalCenter) * Translate(Position)
```

This means each instance spins in place wherever it's placed, instead of orbiting the world
origin (which would happen if the model's local origin isn't at its geometric center).

## Camera

A simple spherical-coordinates orbit camera (`_cameraYaw`, `_cameraPitch`, `_cameraDistance`
around `_cameraTarget`). On load, the camera is framed using the **combined** bounding sphere
of every instance in the scene (`ComputeSceneBounds`, merging each `WireframeModel`'s
`GetWorldBounds()`), so the initial distance and near/far clip planes adapt automatically
regardless of how many models are in the scene or how they're scaled/exported.

Controls:
- **Shift + drag** (left or right mouse button) — orbit (changes yaw/pitch).
- **Scroll wheel** — zoom (changes distance; scroll forward = in, back = out).
- **Space** — pause/resume rotation for every model in the scene.
- **F11 / Alt+Enter** — toggle fullscreen.
- **Escape** — quit.

## Rendering: two passes, done scene-wide

`DrawScene` first computes each instance's world matrix and bone transforms once per frame,
then runs two full passes over *all* instances (not one instance at a time):

1. **Fill pass** — every instance drawn solid, in the background colour, with backface culling
   (`_fillRasterizerState`). This is invisible against the background, but writes the depth
   buffer for the whole scene.
2. **Edge pass** — every instance's precomputed hard-edge lines are drawn on top via
   `GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, ...)`, through a dedicated
   `BasicEffect` (`_edgeEffect`).

Because pass 1 writes depth for the *entire* scene before pass 2 draws *any* edges, an edge
gets correctly hidden whether it's occluded by its own mesh or by a completely different
instance in front of it. (Interleaving fill+edges per instance, one at a time, would get this
wrong for overlapping objects — a farther instance's edges could poke through a nearer
instance that hadn't been fill-passed yet.)

## Hard-edge extraction (`WireframeGeometry`)

Plain GPU wireframe fill mode (`FillMode.WireFrame`) draws every triangle edge, including the
diagonal MonoGame adds to triangulate a flat quad face — so a cube would show a seam across
each face. `BuildHardEdgeVertices` avoids this by reading the actual geometry:

1. For each `ModelMeshPart`, read the raw vertex buffer (extracting just the `POSITION0`
   element via its `VertexDeclaration`, since the exact vertex struct type isn't assumed) and
   the raw index buffer (handling both 16-bit and 32-bit indices).
2. Walk every triangle, compute its face normal, and record that normal against each of its
   three edges (keyed by the edge's two vertex indices, order-independent).
3. An edge is kept as a "hard" edge if:
   - it borders only **one** triangle (a mesh boundary), or
   - any two of its adjacent triangles' normals diverge past a threshold (`dot < 0.999`,
     i.e. the surface actually creases there).

   A triangulation diagonal on a flat face has two adjacent triangles with the *same* normal
   (dot ≈ 1), so it's correctly excluded — only the real edges of the shape remain.
4. The surviving edges become a flat `VertexPosition[]` (pairs of endpoints) per mesh, ready
   to hand straight to `DrawUserPrimitives(PrimitiveType.LineList, ...)`.

## Adding more meshes/models

To add another model to the scene:

1. Add its asset to `Content.mgcb` (or via the MGCB Editor) so it's processed by the content
   pipeline, and give it a `.fbx`/etc. file under `Content/`.
2. In `LoadContent`, load it (`Content.Load<Model>("YourAssetName")`) and call
   `AddModelInstance(model, position)` for each placed copy you want.
3. Nothing else needs to change — the render-data cache, camera framing, rotation toggle, and
   two-pass draw all already operate over the full `_sceneModels` list.

Currently three instances of the same car asset are placed side by side (spaced by three times
the model's bounding radius) purely to demonstrate the scene supporting multiple instances;
swap in different assets/positions as needed.
