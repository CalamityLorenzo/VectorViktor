// Optional argument: the id of a room to start in (corridor, lounge, tvroom, octagon, octagonupper, ...).
using var game = new Basic.Levels.Game1(args.Length > 0 ? args[0] : null);
game.Run();
