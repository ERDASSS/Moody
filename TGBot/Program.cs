class Program
{
    public static async Task Main()
    {
        var bot = new TGBot("7727939273:AAFqtb1fa1rNsHxDDUjLO8JLZztddX1LvMo");
        await bot.Start();
        Console.ReadKey();
    }
}