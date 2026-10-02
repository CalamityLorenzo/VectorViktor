using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace World.Core.Colour
{
    // The colours that can go missing from the world and be brought back (see GameDesign.md 3.3), round the colour wheel.
    public enum Hue { Red, Orange, Yellow, Green, Blue, Purple }

    // Sorting colours by hue: any colour lies somewhere between two of them round the wheel (a brown between red and orange,
    // nearer orange), and is more or less colourful (a grey not at all, so it's no hue's).
    public static class Hues
    {
        public const int Count = 6;

        public static readonly IReadOnlyList<Hue> All = (Hue[])Enum.GetValues(typeof(Hue));

        // Where each is round the wheel, in degrees from red: not evenly spaced, as the eye doesn't see them evenly (there's
        // much more wheel that looks green or blue than looks orange)
        private static readonly float[] Middle = { 0f, 30f, 58f, 120f, 225f, 285f };

        // How much of the way from one hue to the next is all the one, and all the next, at each end
        private const float Plain = 0.25f;

        public static string NameOf(Hue hue) => hue.ToString().ToLowerInvariant();

        // A strong, plain example of each: what a drop of it looks like
        public static Color Example(Hue hue) => hue switch
        {
            Hue.Red => new Color(220, 40, 35),
            Hue.Orange => new Color(240, 130, 30),
            Hue.Yellow => new Color(245, 215, 40),
            Hue.Green => new Color(50, 190, 60),
            Hue.Blue => new Color(40, 90, 230),
            _ => new Color(150, 60, 200),
        };

        // Which two hues `rgb` (each 0 to 1) lies between round the wheel, how far it is from the first to the second (0 to 1:
        // all the first near its middle, all the second near the second's, a mix only between), and how colourful it is (its
        // chroma: the most of red, green or blue less the least, 0 for a grey, 1 for a pure hue).
        public static (Hue from, Hue to, float towards, float chroma) Of(Vector3 rgb)
        {
            var most = MathF.Max(rgb.X, MathF.Max(rgb.Y, rgb.Z));
            var least = MathF.Min(rgb.X, MathF.Min(rgb.Y, rgb.Z));
            var chroma = most - least;
            if (chroma <= 1e-5f)
                return (Hue.Red, Hue.Red, 0f, 0f);

            // The hue in degrees, as HSV has it
            float degrees;
            if (most == rgb.X)
                degrees = 60f * ((rgb.Y - rgb.Z) / chroma);
            else if (most == rgb.Y)
                degrees = 60f * ((rgb.Z - rgb.X) / chroma + 2f);
            else
                degrees = 60f * ((rgb.X - rgb.Y) / chroma + 4f);
            if (degrees < 0f)
                degrees += 360f;

            for (var k = 0; k < Count; k++)
            {
                var start = Middle[k];
                var end = k + 1 < Count ? Middle[k + 1] : 360f;
                if (degrees >= start && degrees < end)
                    return ((Hue)k, (Hue)((k + 1) % Count), Math.Clamp(((degrees - start) / (end - start) - Plain) / (1f - 2f * Plain), 0f, 1f), chroma);
            }
            return (Hue.Red, Hue.Red, 0f, chroma);   // 360 exactly
        }

        // The three paints the others are mixed from, and what each of the others is mixed of
        public static readonly IReadOnlyList<Hue> Primaries = new[] { Hue.Red, Hue.Yellow, Hue.Blue };

        // How much of each hue's back (into `back`, Count of them) when only the primaries are brought back (`red`, `yellow`,
        // `blue`): each of the others is a mix of two, so it's half back with one of them and all back with both.
        public static void FromPrimaries(float red, float yellow, float blue, Span<float> back)
        {
            back[(int)Hue.Red] = red;
            back[(int)Hue.Yellow] = yellow;
            back[(int)Hue.Blue] = blue;
            back[(int)Hue.Orange] = (red + yellow) / 2f;
            back[(int)Hue.Green] = (yellow + blue) / 2f;
            back[(int)Hue.Purple] = (red + blue) / 2f;
        }
    }
}
