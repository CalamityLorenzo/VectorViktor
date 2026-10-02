using Microsoft.Xna.Framework;
using System;
using World.Core.Colour;
using Xunit;

namespace World.Core.Tests
{
    // Colours sorted by hue, and brought back into a drained world (see Hues, Drained)
    public class ColourTests
    {
        private static Vector3 Rgb(int r, int g, int b) => new Color(r, g, b).ToVector3();

        [Theory]
        [InlineData(255, 0, 0, Hue.Red)]
        [InlineData(240, 130, 30, Hue.Orange)]
        [InlineData(245, 215, 40, Hue.Yellow)]
        [InlineData(50, 190, 60, Hue.Green)]
        [InlineData(40, 90, 230, Hue.Blue)]
        [InlineData(150, 60, 200, Hue.Purple)]
        public void Each_example_is_mostly_its_own_hue(int r, int g, int b, Hue hue)
        {
            var (from, to, towards, chroma) = Hues.Of(Rgb(r, g, b));
            Assert.Equal(hue, towards < 0.5f ? from : to);
            Assert.True(chroma > 0.5f);
            Assert.Equal(hue, Mostly(Hues.Example(hue)));
        }

        private static Hue Mostly(Color colour)
        {
            var (from, to, towards, _) = Hues.Of(colour.ToVector3());
            return towards < 0.5f ? from : to;
        }

        [Fact]
        public void A_brown_is_between_red_and_orange_and_a_grey_is_no_hue()
        {
            var (from, to, _, chroma) = Hues.Of(Rgb(110, 70, 40));
            Assert.Equal((Hue.Red, Hue.Orange), (from, to));
            Assert.True(chroma > 0.2f);
            Assert.Equal(0f, Hues.Of(Rgb(128, 128, 128)).chroma);
        }

        private static readonly Vector3 Night = Rgb(27, 13, 120);

        private static float[] Back(Hue hue, float amount)
        {
            var back = new float[Hues.Count];
            back[(int)hue] = amount;
            return back;
        }

        [Fact]
        public void A_colour_comes_back_with_its_own_hue_and_not_with_another()
        {
            var drained = new Drained(DrainedTo.Background, Night, 0f, Neutrals.NeverMissing);
            var red = Rgb(220, 40, 35);
            Assert.Equal(Night, drained.Restore(red, new float[Hues.Count]));
            Assert.Equal(Night, drained.Restore(red, Back(Hue.Blue, 1f)));
            Assert.True(Vector3.Distance(red, drained.Restore(red, Back(Hue.Red, 1f))) < 1e-5f);
            var half = drained.Restore(red, Back(Hue.Red, 0.5f));
            Assert.Equal(Vector3.Lerp(Night, red, 0.5f).X, half.X, 3);
        }

        [Fact]
        public void Drained_to_grey_it_keeps_its_brightness()
        {
            var drained = new Drained(DrainedTo.Grey, Night, 0f, Neutrals.NeverMissing);
            var green = Rgb(50, 190, 60);
            var grey = drained.Restore(green, new float[Hues.Count]);
            Assert.Equal(grey.X, grey.Y, 4);
            Assert.Equal(grey.Y, grey.Z, 4);
            Assert.InRange(grey.X, 0.5f, 0.55f);   // green's bright
        }

        [Fact]
        public void Greys_come_back_as_the_neutrals_say()
        {
            var white = Vector3.One;
            var oneBack = Back(Hue.Red, 1f);
            Assert.Equal(white, new Drained(DrainedTo.Background, Night, 0f, Neutrals.NeverMissing).Restore(white, new float[Hues.Count]));
            Assert.Equal(1f / Hues.Count, new Drained(DrainedTo.Background, Night, 0f, Neutrals.BitByBit).Back(white, oneBack), 4);
            Assert.Equal(1f, new Drained(DrainedTo.Background, Night, 0f, Neutrals.WithTheFirst).Back(white, oneBack));
            Assert.Equal(0f, new Drained(DrainedTo.Background, Night, 0f, Neutrals.WithTheLast).Back(white, oneBack));
        }

        [Fact]
        public void With_only_the_primaries_purple_needs_red_and_blue()
        {
            var back = new float[Hues.Count];
            Hues.FromPrimaries(1f, 0f, 0f, back);
            Assert.Equal(0.5f, back[(int)Hue.Purple]);
            Assert.Equal(0f, back[(int)Hue.Green]);
            Hues.FromPrimaries(1f, 0f, 1f, back);
            Assert.Equal(1f, back[(int)Hue.Purple]);
        }
    }
}
