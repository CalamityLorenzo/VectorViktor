using Microsoft.Xna.Framework;

namespace World.Core.Movement
{
    // What the player asked for this tick. Move is (sideways, forward), each -1..1, relative to where the
    // walker is facing; Turn is -1..1, positive turning right. Jump is true only on the tick it's pressed.
    public readonly record struct MoveInput(Vector2 Move, float Turn = 0f, bool Run = false, bool Jump = false)
    {
        public static readonly MoveInput None = new MoveInput(Vector2.Zero);
    }
}
