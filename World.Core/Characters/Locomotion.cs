using System;
using System.Collections.Generic;
using World.Core.Movement;

namespace World.Core.Characters
{
    // The ways the droid can get about: what it stands on, under the broom (see DroidBases). Each is a part to swap
    // (GameDesign.md 3.1), and each moves its own way (its Gait).
    public enum Locomotion
    {
        Segway,    // the hoverboard wheels it starts on: balances, turns on the spot, no stairs, no going sideways
        Tracks,    // a tank's tracks, with the broom on a turntable that turns by itself; big and heavy, kerbs but no stairs
        TriStar,   // a cluster of three wheels each side, like a sack truck's, that tips over each step: stairs
    }

    public static class Locomotions
    {
        public static readonly IReadOnlyList<Locomotion> All = new[] { Locomotion.Segway, Locomotion.Tracks, Locomotion.TriStar };

        // Its name, to pick it by
        public static string NameOf(Locomotion locomotion) => locomotion switch
        {
            Locomotion.Tracks => "tracks",
            Locomotion.TriStar => "tri-star",
            _ => "segway",
        };

        // Its place in All
        public static int IndexOf(Locomotion locomotion) => (int)locomotion;

        public static Locomotion? Named(string name)
        {
            foreach (var locomotion in All)
                if (string.Equals(NameOf(locomotion), name, StringComparison.OrdinalIgnoreCase))
                    return locomotion;
            return null;
        }

        public static string About(Locomotion locomotion) => locomotion switch
        {
            Locomotion.Tracks =>
                "Tank tracks: slow and heavy, it turns on the spot, pushes what the others can't and gets up a kerb, but it's " +
                "too big for narrow gaps and its tracks can't climb stairs. The body turns on a turntable by itself (sideways keys).",
            Locomotion.TriStar =>
                "Tri-star wheels: three wheels on a spider each side, two on the ground. At a step the spider tips over onto the " +
                "next one, so it climbs stairs (not steep ones), slowly. No going sideways, no jumping.",
            _ =>
                "Hoverboard wheels, balancing like a Segway: quick, turns on the spot, but only up the lowest kerb, and no stairs, " +
                "no going sideways, no jumping.",
        };

        // How each gets about. Height is the droid's own (it changes with what it stands on); the speeds are its walking
        // pace, hurried by RunMultiplier.
        public static Gait GaitOf(Locomotion locomotion) => locomotion switch
        {
            Locomotion.Tracks => new Gait("tracks", StepUp: 0.15f, Radius: DroidBases.TankRadius, Height: DroidRig.HeightOn(locomotion),
                Speed: 1.6f, RunMultiplier: 1.75f, TurnSpeed: 1.2f, Acceleration: 4f, Strafes: false, Jumps: false, Ladders: false,
                Mass: 150f, PushForce: 1200f, PushPower: 600f),
            Locomotion.TriStar => new Gait("tri-star", StepUp: 0.22f, Radius: 0.3f, Height: DroidRig.HeightOn(locomotion),
                Speed: 1.6f, RunMultiplier: 1.6f, TurnSpeed: 1.6f, Acceleration: 8f, Strafes: false, Jumps: false, Ladders: false,
                Mass: 45f, PushForce: 300f, PushPower: 200f),
            _ => new Gait("segway", StepUp: 0.07f, Radius: 0.25f, Height: DroidRig.HeightOn(locomotion),
                Speed: 2.5f, RunMultiplier: 1.8f, TurnSpeed: 2f, Acceleration: 8f, Strafes: false, Jumps: false, Ladders: false,
                Mass: 40f, PushForce: 250f, PushPower: 150f),
        };
    }
}
