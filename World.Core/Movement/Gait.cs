namespace World.Core.Movement
{
    // How something moving under its own power gets about (see CharacterController): its size, how fast it goes and
    // turns, how quickly it gets up to speed, how high a step it can get up, and what it can't do at all. The walker's
    // is Walker; each of the droid's ways of moving has its own (see World.Core.Characters.Locomotions).
    //
    //   StepUp      the highest kerb or step it can get up. Stairs are climbed a step at a time, so stairs with
    //               steps higher than this are as good as a wall (see IGround.RiserAt)
    //   Radius      how far out from its middle it's kept from walls: an upright cylinder Radius round, Height tall
    //   Speed       metres per second, going; RunMultiplier times that, hurrying
    //   TurnSpeed   radians per second, turned flat out
    //   Acceleration how quickly it reaches the speed asked for, or stops: m/s²
    //   Strafes     whether it can go sideways; without, a sideways ask does nothing (or something else: see the tank)
    //   Jumps       whether it can jump
    //   Ladders     whether it can climb a ladder (see IGround.StepUpAt): only with hands and feet
    //   Mass, PushForce, PushPower   against bodies, as the walker's (see CharacterController)
    public sealed record Gait(string Name, float StepUp, float Radius, float Height, float Speed, float RunMultiplier, float TurnSpeed,
                              float Acceleration, bool Strafes, bool Jumps, bool Ladders, float Mass, float PushForce, float PushPower)
    {
        public static readonly Gait Walker = new Gait("walker", WorldConstants.MaxStepUp, WorldConstants.WalkerRadius, WorldConstants.WalkerHeight,
            WorldConstants.WalkSpeed, WorldConstants.RunMultiplier, WorldConstants.TurnSpeed, Acceleration: 30f,
            Strafes: true, Jumps: true, Ladders: true, Mass: 75f, PushForce: 450f, PushPower: 300f);
    }
}
