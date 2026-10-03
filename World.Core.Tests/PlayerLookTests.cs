using Microsoft.Xna.Framework;
using World.Core.Characters;
using Xunit;

namespace World.Core.Tests
{
    // Tilting your own view up and down (see Player.LookUp)
    public class PlayerLookTests
    {
        private static Player Player() => new Player(Vector3.Zero, 0f, Grounds.Flat());

        [Fact]
        public void LevelYouLookAlongYourHeading()
        {
            var player = Player();
            Assert.True(Vector3.Distance(player.Body.Heading, player.Looking) < 1e-5f);
        }

        [Fact]
        public void TiltedUpYouLookUpAndStillAhead()
        {
            var player = Player();
            player.LookUp = 0.5f;
            Assert.True(player.Looking.Y > 0.4f);
            Assert.True(Vector3.Dot(player.Looking, player.Body.Heading) > 0.8f);
            Assert.Equal(1f, player.Looking.Length(), 4);
        }

        [Fact]
        public void TheTiltStopsShortOfStraightUpOrDown()
        {
            var player = Player();
            player.LookUp = 10f;
            Assert.Equal(World.Core.Characters.Player.MaxLookUp, player.LookUp);
            player.LookUp = -10f;
            Assert.Equal(-World.Core.Characters.Player.MaxLookDown, player.LookUp);
            Assert.True(Vector3.Dot(player.Looking, player.Body.Heading) > 0.1f);   // still facing somewhere, not straight down
        }
    }
}
