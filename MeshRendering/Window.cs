using Microsoft.Xna.Framework;

namespace MeshRendering
{
    // A window onto somewhere else, drawn as if it were behind it (see WindowPortals), whatever's really there:
    // Width by Height, its bottom Sill above Frame's origin. Frame places the window and what's seen through it in the
    // world - the window upright in its x-y plane, centred on x, facing its -Z, and what's beyond it, Beyond, in its
    // own space, running back along its +Z. Beyond Reach of it (where you stand, across the ground) it's shut, its
    // Pane solid; coming in, the pane thins, and it's gone at Clear. However close you are, the glass tints what's
    // seen through it: TintStrength of the way to Tint, by default a lighter shade of the pane.
    //
    // With Onto, it looks out onto the world itself instead, from somewhere else in it: Onto is a frame like Frame,
    // there, and what's seen through the window is the world beyond it, as if you stood as far in front of it as you
    // do of this window (see Through). Beyond's left empty.
    public sealed record Window(Matrix Frame, float Width, float Sill, float Height, Color Pane, IReadOnlyList<ScenePart> Beyond)
    {
        public float Reach { get; init; } = 5f;
        public float Clear { get; init; } = 1.5f;
        public Color Tint { get; init; } = Color.Lerp(Pane, new Color(170, 210, 255), 0.6f);
        public float TintStrength { get; init; } = 0.35f;
        public Matrix? Onto { get; init; }

        // What's seen through it, placed in the world: Beyond's space, or the world beyond Onto, brought round to it
        public Matrix Scene => Onto is { } onto ? Matrix.Invert(onto) * Frame : Frame;

        // Where a point in front of it would be in front of Onto: where you'd be standing, and where the eye'd be,
        // to see out of it there what you see through this one.
        public Vector3 Through(Vector3 p) => Onto is { } onto ? Vector3.Transform(p, Matrix.Invert(Frame) * onto) : p;

        public Vector3 Centre => Vector3.Transform(new Vector3(0f, Sill + Height / 2f, 0f), Frame);

        // Round the window, in the world
        public Vector3[] Corners()
        {
            float half = Width / 2f, top = Sill + Height;
            return Array.ConvertAll(new[] { new Vector3(-half, Sill, 0f), new Vector3(half, Sill, 0f), new Vector3(half, top, 0f), new Vector3(-half, top, 0f) },
                p => Vector3.Transform(p, Frame));
        }

        // Whether the eye's out in front of it, where it can be seen from
        public bool Faces(Vector3 eye) => Vector3.Dot(eye - Centre, Vector3.TransformNormal(Vector3.UnitZ, Frame)) < 0f;

        public bool Open(Vector3 you) => Across(you) <= Reach;

        public float Opacity(Vector3 you) => MathHelper.Clamp((Across(you) - Clear) / (Reach - Clear), 0f, 1f);

        private float Across(Vector3 you)
        {
            var centre = Centre;
            return Vector2.Distance(new Vector2(you.X, you.Z), new Vector2(centre.X, centre.Z));
        }
    }
}
