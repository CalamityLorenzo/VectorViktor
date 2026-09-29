using Microsoft.Xna.Framework;
using World.Core.Animation;
using World.Core.Characters;
using World.Maps;

namespace Droid.Playground
{
    // What an experiment has to work with: the world, the droid in it (moved as the walker is, drawn as its rig), and
    // the harness's clock, which stops when it's paused and slows when it's slowed.
    public sealed class Session
    {
        public Session(BuiltWorld world, Player player, Rig rig, DroidMotion motion)
        {
            World = world;
            Player = player;
            Rig = rig;
            Motion = motion;
        }

        public BuiltWorld World { get; }

        // The droid's body: where it is and how it moves (for now the walker's; see ToolsPlan.md step 2), and its drone
        public Player Player { get; }

        // How it's drawn: its rig, and how that follows the body about
        public Rig Rig { get; }
        public DroidMotion Motion { get; }

        // Seconds of the world's time so far
        public float Clock { get; internal set; }

        public Vector3 Feet => Player.Body.Position;
    }
}
