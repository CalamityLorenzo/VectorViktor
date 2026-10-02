using MeshRendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using World.Core.Animation;
using World.Core.Characters;
using World.Maps;
using World.Rendering;

namespace Droid.Playground
{
    // What an experiment has to work with: the world, the droid in it (moved as the walker is, drawn as its rig), and
    // the harness's clock, which stops when it's paused and slows when it's slowed; and what to draw anything more with.
    public sealed class Session
    {
        public Session(BuiltWorld world, Player player, Rig rig, DroidMotion motion, RigView view, GraphicsDevice device, MeshCache meshes)
        {
            World = world;
            Player = player;
            Rig = rig;
            Motion = motion;
            View = view;
            Device = device;
            Meshes = meshes;
        }

        public BuiltWorld World { get; }

        // The droid's body: where it is and how it moves (for now the walker's; see ToolsPlan.md step 2), and its drone
        public Player Player { get; }

        // How it's drawn: its rig, and how that follows the body about
        public Rig Rig { get; }
        public DroidMotion Motion { get; }
        public RigView View { get; }

        // For an experiment's own meshes (see Experiment.Add): made with these, they last as long as the playground
        public GraphicsDevice Device { get; }
        public MeshCache Meshes { get; }

        // Seconds of the world's time so far
        public float Clock { get; internal set; }

        public Vector3 Feet => Player.Body.Position;

        // Where the droid's rig is placed in the world: at its feet, facing the way it's heading (the rig faces +Z; a yaw of 0
        // faces -Z, north)
        public Matrix Placement => Matrix.CreateRotationY(MathHelper.Pi - Player.Body.Yaw) * Matrix.CreateTranslation(Player.Body.Position);
    }
}
