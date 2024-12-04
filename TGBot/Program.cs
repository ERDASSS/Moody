using Database;

namespace TGBot;

class Program
{
    public static async Task Main()
    {
        var dbAccessor = new DbAccessor();
        var bot = new TGBot("7727939273:AAFqtb1fa1rNsHxDDUjLO8JLZztddX1LvMo", dbAccessor);
        Console.ReadKey();
    }
}