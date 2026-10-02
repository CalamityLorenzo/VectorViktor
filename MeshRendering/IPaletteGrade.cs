using Microsoft.Xna.Framework;

namespace MeshRendering
{
    // Colours changed as they're drawn, the whole world's at once (see PaletteEffect.Grade): colour drained out of the world,
    // and coming back. Every palette's graded three ways: as the world shows it, as a place shows it, and as it shows behind a
    // front spreading out from the place's centre; and each pixel takes whichever its place on the ground says (see Zones).
    //
    // Each instance keeps what it was last graded to, and grades again only when Version changes (or its own colours do):
    // so a grade that's still costs nothing, and one fading costs a palette or three a mesh a frame.
    public interface IPaletteGrade
    {
        // Changes whenever Grade would give anything different.
        int Version { get; }

        // Where each grading shows.
        GradeZones Zones { get; }

        // `colours` (a palette, as the shader takes it) as the world shows them, as the place does, and behind the front.
        // `state` is the instance's own, for the grade to keep what it likes from one grading of it to the next (what its
        // colours are made of, say): null the first time, and again whenever the instance's colours change.
        void Grade(ReadOnlySpan<Vector4> colours, ref object? state, Span<Vector4> world, Span<Vector4> place, Span<Vector4> front);
    }

    // Where a grade's colourings show, by how far a point is from Centre across the ground (its height doesn't count):
    // within Place, the place's, or within Front the front's, fading into the world's over PlaceEdge; beyond, the world's.
    // Rim (0 to 1) is how bright a line marks the front, while it's moving.
    public readonly record struct GradeZones(Vector3 Centre, float Place, float PlaceEdge, float Front, float Rim)
    {
        // The world's colouring everywhere
        public static readonly GradeZones WorldOnly = new GradeZones(Vector3.Zero, -1f, 1f, -1f, 0f);
    }
}
