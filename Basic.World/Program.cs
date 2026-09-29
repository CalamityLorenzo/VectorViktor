using System.Linq;

// Optional arguments: 2 for the road layout (Game2); otherwise where to start in the world (hills,
// plateau, causeway, basin (in the lake), pond, far, lockers, town, house, bedroom, attic, barn, street, pool), and
// bird to watch it from the camera chasing the bird roaming round there - in either order: "town bird", "bird".
if (args.Length > 0 && args[0] == "2")
{
    using var roads = new Basic.World.Game2();
    roads.Run();
}
else
{
    var followBird = args.Contains("bird");
    using var game = new Basic.World.Game1(args.FirstOrDefault(a => a != "bird"), followBird);
    game.Run();
}
