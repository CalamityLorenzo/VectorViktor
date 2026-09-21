
if(args.Length > 0)
{
    int.TryParse(args[0], out int gameNumber);
    if(gameNumber == 3)
    {
        using var game = new BasicTests.Game3();
        game.Run();
    }
    if (gameNumber == 2)
    {
        using var game = new BasicTests.Game2();
        game.Run();
    }
    if (gameNumber == 1)
    {
        using var game = new BasicTests.Game1();
        game.Run();
    }
}
else
{
    using var game = new BasicTests.Game2();
    game.Run();
}
