using ApiMethods;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TGBot.States;

public class InitState : InputHandlingState
{
    public static InitState Instance { get; } = new();

    public override Task BeforeAnswer(TelegramBotClient bot, TgUser user)
        => throw new InvalidOperationException("Никогда должен был быть вызван");

    // на любое действие пользователя переходит к приветственному состоянию
    public override Task<State> OnMessage(TelegramBotClient bot, TgUser user, Message message)
        => Task.FromResult<State>(GreetingState.Instance);

    public override Task<State> OnCallback(TelegramBotClient bot, TgUser user, CallbackQuery callback)
        => Task.FromResult<State>(GreetingState.Instance);
}

public class GreetingState : LambdaState
{
    public static GreetingState Instance { get; } = new();

    public override async Task<State> Execute(TelegramBotClient bot, TgUser user)
    {
        await bot.SendMessage(user.ChatId,
            "Добро пожаловать!\n" +
            "Это бот для фильтрации вашей музыки в вк\n" +
            "Внимание! Бот не хранит ваши личные данные. Производится только только авторизация");
        return LoginMenuState.Instance;
    }
}

class LoginMenuState : InputHandlingState
{
    public static GreetingState Instance { get; } = new();

    private readonly ReplyKeyboardMarkup commands =
        new ReplyKeyboardMarkup(true).AddButton("/login").AddButton("/demo");

    public override async Task BeforeAnswer(TelegramBotClient bot, TgUser user)
    {
        await bot.SendMessage(user.ChatId,
            "Выберите одну из следующих команд\n" +
            "/login  -  перейти к авторизации\n" +
            "/demo   -  опробовать функционал на заранее заданных треках",
            replyMarkup: commands);
    }

    // todo: Выделить бота и юзера в отдельный класс?
    public override Task<State> OnMessage(TelegramBotClient bot, TgUser user, Message msg)
    {
        switch (msg.Text)
        {
            case "/login":
                return Task.FromResult<State>(EnterLoginState.Instance);
            case "/demo":
                user.ApiWrapper = new TestApiWrapper();
                return Task.FromResult<State>(MainMenuState.Instance);
            default:
                throw new IncorrectMessageException(msg.Text ?? "[null]", "/login, /demo");
        }
    }
}

class MainMenuState : InputHandlingState
{
    public static MainMenuState Instance { get; } = new();

    private readonly ReplyKeyboardMarkup commands =
        new ReplyKeyboardMarkup(true).AddButton("/playlist").AddButton("/mark");

    public override async Task BeforeAnswer(TelegramBotClient bot, TgUser user)
    {
        await bot.SendMessage(user.ChatId,
            "Можете выбрать команду из меню:\n" +
            "/playlist  -  создать плейлист\n" +
            "/mark      -  разметить треки\n",
            replyMarkup: commands);
    }

    public override Task<State> OnMessage(TelegramBotClient bot, TgUser user, Message msg)
    {
        switch (msg.Text)
        {
            case "/playlist":
                throw new NotImplementedException();
            case "/mark":
                throw new NotImplementedException();
            default:
                throw new IncorrectMessageException(msg.Text ?? "[null]", "/playlist, /mark");
            // todo: формировать ожидаемый список автоматически, а не вручную
        }
    }
}