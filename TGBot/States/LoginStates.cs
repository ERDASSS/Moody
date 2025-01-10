using System.Text.RegularExpressions;
using ApiMethods;
using Database;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using VkNet.AudioBypassService.Exceptions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace TGBot.States;

class EnterLoginState : InputHandlingState
{
    public static EnterLoginState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        var commands = new ReplyKeyboardMarkup(true).AddButton("/back");
        await bot.SendMessage(user.ChatId, "Введите логин (номер телефона или почта).\n" +
            "/back  -  назад", 
            replyMarkup: commands);
    }

    public override async Task<State?> OnMessage(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user,
        Message message)
    {
        if (message.Text == "/back")
            return LoginMenuState.Instance;

        var login = message.Text.Replace(" ", "");
        if (!login.Contains("@"))
            login = login.Replace("-", "").Replace("(", "").Replace(")", ""); ;

        if (!(Regex.IsMatch(login, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") ||
              Regex.IsMatch(login, @"^\+?[1-9]\d{8,14}$")))
        {
            await bot.SendMessage(message.Chat.Id,
                "Неверный формат логина. Пожалуйста, введите корректный логин (почта или номер телефона)");
            throw new IncorrectMessageException("логин в неверном формате");
        }

        user.Authorization.SetLogin(message.Text);
        return EnterPasswordState.Instance;
    }
}

class EnterPasswordState : InputHandlingState
{
    public static EnterPasswordState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        var commands = new ReplyKeyboardMarkup(true).AddButton("/back").AddButton("/exit");
        await bot.SendMessage(user.ChatId, "Введите пароль.\n" +
            "/back  -   назад\n" +
            "/exit  -   в стартовое меню",
            replyMarkup: commands);
    }

    public override async Task<State?> OnMessage(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user,
        Message message)
    {
        if (message.Text == "/back")
            return EnterLoginState.Instance;

        if (message.Text == "/exit")
            return LoginMenuState.Instance;

        if (message.Text is null)
        {
            await bot.SendMessage(message.Chat.Id,
                "В вашем сообщении отсутствует текст (вы что - тестировщик?)\n" +
                "Пожалуйста, введите пароль");
            throw new IncorrectMessageException("пароль отсутствует");
        }

        user.Authorization.SetPassword(message.Text);
        await bot.SendMessage(user.ChatId, "Попытка авторизации...");
        var authResult = VkApiWrapper.TryAuthorize(
            out var apiWrapper, user.Authorization.Login!, user.Authorization.Password!);
        switch (authResult)
        {
            case AuthorizationResult.Success:
                user.ApiWrapper = apiWrapper;
                return MainMenuState.Instance;

            case AuthorizationResult.WrongLoginOrPassword:
                await bot.SendMessage(message.Chat.Id, "Неправильный логин или пароль. Попробуйте еще раз");
                return EnterLoginState.Instance;

            case AuthorizationResult.TooManyLoginAttempts:
                await bot.SendMessage(message.Chat.Id,
                    "Произведено слишком много попыток входа в этот аккаунт по паролю\n" +
                    "Воспользуйтесь другим способом входа или попробуйте через несколько часов");
                return LoginMenuState.Instance;

            case AuthorizationResult.UnknownException:
                await bot.SendMessage(message.Chat.Id, "Неизвестная ошибка, позовите админа посмотреть логи");
                return LoginMenuState.Instance;

            case AuthorizationResult.Need2FA:
                return Enter2FACodeState.Instance;

            case AuthorizationResult.WrongCode2FA:

            default:
                throw new InvalidOperationException($"неожиданный результат аутентификации: {authResult}");
        }
    }
}

class Enter2FACodeState : InputHandlingState
{
    public static Enter2FACodeState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        await bot.SendMessage(user.ChatId, "Введите код двухфакторной авторизации:");
    }

    public override async Task<State?> OnMessage(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user,
        Message message)
    {
        if (message.Text is null)
        {
            await bot.SendMessage(message.Chat.Id,
                "В вашем сообщении отсутствует текст\n" +
                "Пожалуйста, введите код двухфакторной авторизации");
            throw new IncorrectMessageException("код двухфакторной авторизации отсутствует");
        }

        user.Authorization.SetCode(message.Text);
        await bot.SendMessage(user.ChatId, "Попытка авторизации...");
        var authResult = VkApiWrapper.TryAuthorize(
            out var apiWrapper, user.Authorization.Login!, user.Authorization.Password!, user.Authorization.Code!);
        switch (authResult)
        {
            case AuthorizationResult.Success:
                user.ApiWrapper = apiWrapper;
                return MainMenuState.Instance;

            case AuthorizationResult.WrongCode2FA:
                await bot.SendMessage(message.Chat.Id, "Введен неверный код. Попробуйте еще раз");
                throw new IncorrectMessageException("Неверный код");

            case AuthorizationResult.TooManyLoginAttempts:
                await bot.SendMessage(message.Chat.Id,
                    "Произведено слишком много попыток входа в этот аккаунт по паролю\n" +
                    "Воспользуйтесь другим способом входа или попробуйте через несколько часов");
                return LoginMenuState.Instance;

            case AuthorizationResult.UnknownException:
                await bot.SendMessage(message.Chat.Id, "Неизвестная ошибка, позовите админа посмотреть логи");
                return LoginMenuState.Instance;

            case AuthorizationResult.Need2FA:
            case AuthorizationResult.WrongLoginOrPassword:
            default:
                throw new InvalidOperationException($"Неожиданный результат аутентификации: {authResult}");
        }
    }
}