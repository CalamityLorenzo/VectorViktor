using Microsoft.Xna.Framework;
using System;

namespace World.Core.Colour
{
    // What a colour turns to with its hue gone: the background (the wireframe look, as with colours off), or its own grey (an
    // old photograph).
    public enum DrainedTo { Background, Grey }

    // Whites, greys and blacks have no hue to come back with: they're never missing, or they come back as the colours do (bit
    // by bit with every one, with the first, or only once they're all back).
    public enum Neutrals { NeverMissing, BitByBit, WithTheFirst, WithTheLast }

    // How the world looks with colour drained out of it, and how a colour comes back: by how much of its hue is back (0 to 1
    // for each of Hues.All), from what it's drained to, plus Left of the way to its own (a little colour left in everything).
    public readonly record struct Drained(DrainedTo To, Vector3 Background, float Left, Neutrals Neutrals)
    {
        // A colour's chroma (see Hues.Of) below which it's a grey, and above which it's all its hue; it's a mix between
        private const float GreyBelow = 0.05f, HueAbove = 0.2f;

        // What a colour's made of, for bringing it back over and over as more of its hue comes back: what it is, what it's
        // drained to, the two hues it lies between and how far from the first (see Hues.Of), and how much it's of them rather
        // than a grey (0 to 1). Worked out once (Of), it's a few multiplications to restore.
        public readonly record struct Makeup(Vector3 Rgb, Vector3 Gone, Hue From, Hue To, float Towards, float Hued);

        public Makeup Of(Vector3 rgb)
        {
            var (from, to, towards, chroma) = Hues.Of(rgb);
            var t = MathHelper.Clamp((chroma - GreyBelow) / (HueAbove - GreyBelow), 0f, 1f);
            var gone = To == DrainedTo.Grey ? new Vector3(Vector3.Dot(rgb, new Vector3(0.299f, 0.587f, 0.114f))) : Background;
            return new Makeup(rgb, Vector3.Lerp(gone, rgb, Left), from, to, towards, t * t * (3f - 2f * t));
        }

        // How much of `rgb` is back, 0 to 1, with `back` of each hue back
        public float Back(Vector3 rgb, ReadOnlySpan<float> back) => Back(Of(rgb), back);

        public float Back(in Makeup colour, ReadOnlySpan<float> back) => Back(colour, back, Grey(back));

        // Given how much of a grey's back with `back` of each hue (see Grey): for many colours at once, worked out once
        public float Back(in Makeup colour, ReadOnlySpan<float> back, float grey) =>
            MathHelper.Lerp(grey, MathHelper.Lerp(back[(int)colour.From], back[(int)colour.To], colour.Towards), colour.Hued);

        // How much of a white, grey or black is back with `back` of each hue back (see Neutrals)
        public float Grey(ReadOnlySpan<float> back) => Neutrals switch
        {
            Neutrals.NeverMissing => 1f,
            Neutrals.BitByBit => Average(back),
            Neutrals.WithTheFirst => Most(back),
            _ => Least(back),
        };

        // `rgb` as it shows, with `back` of each hue back
        public Vector3 Restore(Vector3 rgb, ReadOnlySpan<float> back) => Restore(Of(rgb), back, Grey(back));

        public Vector3 Restore(in Makeup colour, ReadOnlySpan<float> back, float grey) =>
            Vector3.Lerp(colour.Gone, colour.Rgb, Back(colour, back, grey));

        private static float Average(ReadOnlySpan<float> back)
        {
            var sum = 0f;
            foreach (var b in back)
                sum += b;
            return sum / back.Length;
        }

        private static float Most(ReadOnlySpan<float> back)
        {
            var most = 0f;
            foreach (var b in back)
                most = MathF.Max(most, b);
            return most;
        }

        private static float Least(ReadOnlySpan<float> back)
        {
            var least = 1f;
            foreach (var b in back)
                least = MathF.Min(least, b);
            return least;
        }
    }
}
