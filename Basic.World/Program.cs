// Optional argument: where to start (hills, plateau, causeway, basin).
using var game = new Basic.World.Game1(args.Length > 0 ? args[0] : null);
game.Run();
