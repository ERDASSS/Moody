using Database;
using Ninject;
using Telegram.Bot;

namespace TGBot;

public static class Program
{
    public static async Task Main()
    {
        var stateMachine = DiConstructor.GetContainer().Get<StateMachine>();
        Console.ReadKey();
    }
}