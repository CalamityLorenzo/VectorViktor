using Microsoft.Xna.Framework;
using System;
using World.Core.Characters;
using Xunit;

namespace World.Core.Tests
{
    // The bird roaming overhead (see Bird): wandering round its home, never too low over what's under it, diving now
    // and then, and never jerking.
    public class BirdTests
    {
        private const float StepTime = 1f / 60f;

        private static readonly Func<float, float, float, float> Flat = (x, z, margin) => 0f;

        // A town of blocks `height` tall, 10 m square with 10 m streets between them, everywhere
        private static Func<float, float, float, float> Blocks(float height) => (x, z, margin) =>
        {
            static bool Near(float v, float margin)
            {
                var m = ((v % 20f) + 20f) % 20f;   // 0..10 is a block, 10..20 a street
                return m <= 10f + margin || m >= 20f - margin;
            }
            return Near(x, margin) && Near(z, margin) ? height : 0f;
        };

        private static void Fly(Bird bird, float seconds, Action eachTick)
        {
            for (var t = 0f; t < seconds; t += StepTime)
            {
                bird.Step(StepTime);
                eachTick();
            }
        }

        [Fact]
        public void It_roams_far_and_wide_but_no_further_than_its_range_from_home_facing_the_way_it_goes()
        {
            var home = new Vector2(10f, -20f);
            var bird = new Bird(home, Flat);
            var furthest = 0f;
            var (left, right) = (0f, 0f);
            var previousYaw = bird.Yaw;
            Fly(bird, 600f, () =>
            {
                var from = Vector2.Distance(new Vector2(bird.Position.X, bird.Position.Z), home);
                furthest = MathF.Max(furthest, from);
                Assert.True(from <= Bird.RoamRadius * 1.2f, $"strayed {from} m from home");

                var along = new Vector2(bird.Velocity.X, bird.Velocity.Z);
                Assert.True(Vector2.Dot(Vector2.Normalize(along), Vector2.Normalize(new Vector2(bird.Forward.X, bird.Forward.Z))) > 0.99f);
                var turned = MathHelper.WrapAngle(bird.Yaw - previousYaw);
                (left, right) = (left + MathF.Max(0f, -turned), right + MathF.Max(0f, turned));
                previousYaw = bird.Yaw;
            });
            Assert.True(furthest > Bird.RoamRadius * 0.5f, $"it only got {furthest} m from home");
            Assert.True(left > MathHelper.TwoPi && right > MathHelper.TwoPi, $"it turned {left} left and {right} right: it isn't wandering");
        }

        [Fact]
        public void It_dives_down_near_the_ground_and_climbs_back_up()
        {
            var bird = new Bird(Vector2.Zero, Flat);
            var lowest = float.MaxValue;
            var steepest = (down: 0f, up: 0f);
            var backUp = false;
            Fly(bird, 30f, () =>
            {
                lowest = MathF.Min(lowest, bird.Position.Y);
                steepest = (MathF.Min(steepest.down, bird.Pitch), MathF.Max(steepest.up, bird.Pitch));
                backUp |= lowest < Bird.Clearance + 0.5f && bird.Position.Y >= Bird.CruiseHeight * (1f - Bird.HeightVariance) - 0.1f;
                Assert.True(bird.Position.Y >= Bird.Clearance - 1e-3f, $"at {bird.Position}");
            });
            Assert.True(lowest < Bird.Clearance + 0.5f, $"lowest {lowest}: it never dived");
            Assert.True(backUp, "it never climbed back up after diving");
            Assert.True(steepest.down < -0.3f && steepest.up > 0.3f, $"its nose went no further down than {steepest.down} or up than {steepest.up}");
        }

        [Fact]
        public void Over_a_town_of_blocks_it_keeps_nearly_its_clearance_and_goes_up_and_down_smoothly()
        {
            const float height = 8f;
            const float fastest = 20f;   // metres per second: a dive's steepest
            var skyline = Blocks(height);
            var bird = new Bird(new Vector2(5f, 5f), skyline);
            var previous = bird.Position.Y;
            var lowestOverBlock = float.MaxValue;
            Fly(bird, 300f, () =>
            {
                var under = skyline(bird.Position.X, bird.Position.Z, Bird.WingReach);
                // It follows a roof's edge a little late (see Bird.Lead), so it may come a little under
                Assert.True(bird.Position.Y >= under + Bird.Clearance - 0.5f, $"at {bird.Position}, over {under}");
                if (under > 0f)
                    lowestOverBlock = MathF.Min(lowestOverBlock, bird.Position.Y);
                Assert.True(MathF.Abs(bird.Position.Y - previous) <= fastest * StepTime,
                    $"went {bird.Position.Y - previous} in a tick, at {bird.Position}");
                previous = bird.Position.Y;
            });
            Assert.True(lowestOverBlock < height + Bird.Clearance + 1.5f, $"it never dived over a block: lowest {lowestOverBlock}");
        }

        // The camera chasing it tips with it, so a jolt in its tilt shakes the whole view: it may turn its nose no
        // faster than the first VectorViktor's bird did at the bottom of its dives (0.085 radians a tick)
        [Fact]
        public void Its_nose_tips_smoothly_even_where_a_dive_meets_a_roof()
        {
            var bird = new Bird(new Vector2(5f, 5f), Blocks(8f));
            var previous = bird.Pitch;
            var dived = false;
            Fly(bird, 300f, () =>
            {
                Assert.True(MathF.Abs(bird.Pitch - previous) <= 0.09f, $"its nose turned {bird.Pitch - previous} in a tick, at {bird.Position}");
                dived |= bird.Pitch < -0.3f;
                previous = bird.Pitch;
            });
            Assert.True(dived);
        }

        [Fact]
        public void Its_flap_goes_round_from_0_up_to_1()
        {
            var bird = new Bird(Vector2.Zero, Flat);
            Fly(bird, 10f, () => Assert.InRange(bird.FlapPhase, 0f, 1f - 1e-6f));
        }
    }
}
