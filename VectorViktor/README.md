# VectorViktor: where it started

The first program in the solution, and the origin of everything else in it: a low-resolution, 8/16-bit-style vector
world with hidden-line wireframes and flat-shaded faces on a deep blue background. The house, the grid, the car and
the bird here are what the libraries (`MeshCore.Library`, `MeshProps`, `MeshRendering`) and the games
(`Basic.World`, `Basic.Levels`, `Basic.Models`) were later grown out of.

It is kept as it was, on purpose, and kept **isolated**:

- It references no other project in the solution, and no project references it. Its own package references are
  MonoGame and SharpGLTF, and it shares only the build settings in `Directory.Build.props` and the package versions in
  `Directory.Packages.props`.
- Nothing shared is to be refactored *into* it, or to wait on it: when a library changes, this project doesn't move.
- It duplicates things the libraries now do better (the low-res target, box building, input, edge drawing). That's
  deliberate, so it stays a self-contained record of the starting point. Don't "clean it up" onto the shared code.
- `LoadingModelMeshes` (loading `.glb` models and drawing their hard edges) is the other prototype, kept the same way.

Both sit in the solution's `Prototypes` folder, and the CI build builds them along with everything else.
