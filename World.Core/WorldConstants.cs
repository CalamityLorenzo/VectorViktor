namespace World.Core
{
    // The numbers the world's parts have to agree on, kept in one place: the physics and the walkers fall
    // at the same rate, and the rooms' ramps, the character controller and Basic.Levels' own walking all
    // step as high and stand as tall.
    public static class WorldConstants
    {
        public const float Gravity = 9.81f;         // metres per second per second

        // A walker: an upright cylinder, feet to the top of the head, and where its eyes are
        public const float WalkerHeight = 1.8f;
        public const float WalkerRadius = 0.3f;
        public const float EyeHeight = 1.6f;

        // The highest step a walker takes in its stride
        public const float MaxStepUp = 0.3f;

        public const float WalkSpeed = 2.5f;        // metres per second
        public const float RunMultiplier = 2.5f;
        public const float TurnSpeed = 2.0f;        // radians per second
    }
}
