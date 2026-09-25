// Optional argument: 2 for the road layout (Game2); otherwise where to start in the world (hills,
// plateau, causeway, basin (in the lake), pond, far, lockers, town, house, bedroom, attic, barn, street, pool).
if (args.Length > 0 && args[0] == "2")
{
    using var roads = new Basic.World.Game2();
    roads.Run();
}
else
{
    using var game = new Basic.World.Game1(args.Length > 0 ? args[0] : null);
    game.Run();
}
