// Optional argument: which scene, 1, 2 or 3 (see Game1, Game2, Game3); 2 if none, or if it's anything else.
var gameNumber = args.Length > 0 && int.TryParse(args[0], out var n) ? n : 2;
using Microsoft.Xna.Framework.Game game = gameNumber switch
{
    1 => new Basic.Models.Game1(),
    3 => new Basic.Models.Game3(),
    _ => new Basic.Models.Game2(),
};
game.Run();
