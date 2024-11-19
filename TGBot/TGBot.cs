using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VkNet.AudioBypassService.Exceptions;
using ApiMethods;

namespace TGBot;

public class TGBot
{
    public TGBot(string token)
    {
        bot = new TelegramBotClient(token, cancellationToken: cts.Token);
        me = bot.GetMe().Result;
        // await bot.DeleteWebhook();
        // await bot.DropPendingUpdates();
        bot.OnError += OnError;
        bot.OnMessage += OnMessage;
        bot.OnUpdate += OnUpdate;
        Console.WriteLine($"{me.FirstName} запущен!");
    }


    private readonly TelegramBotClient bot;
    private readonly User me;
    private readonly CancellationTokenSource cts = new();
    private readonly Dictionary<long, Authorization> authorizations = new();
    //private readonly Dictionary<long, (bool, HashSet<Mood>?, HashSet<Genre>?)> users = new();
    private readonly Dictionary<long, VkUser> users = new();
    //private readonly Dictionary<long, (string?, string?)> polls = new();

    private readonly ReplyKeyboardMarkup replyKeyboardWithFunc = new(
        new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton("/start"),
                new KeyboardButton("/playlist")
            }
        })
    {
        ResizeKeyboard = false
    };

    // private readonly List<InputPollOption> pollMoods = MoodExtensions.CreateInputPollOptions().ToList();
    // private readonly List<InputPollOption> pollGenres = GenreExtensions.CreateInputPollOptions().ToList();
    private readonly InlineKeyboardMarkup inlineMoods = MoodExtensions.CreateInlineKeyboardMarkup();
    private readonly InlineKeyboardMarkup inlineGenres = GenreExtensions.CreateInlineKeyboardMarkup();

    // private async Task Authorize(long chatId)
    // {
    //     await StartAuthorization(chatId);
    // }

    private async Task OnError(Exception exception, HandleErrorSource source)
    {
        Console.WriteLine(exception);
        await Task.Delay(2000, cts.Token);
    }

    private async Task OnUpdate(Update update)
    {
        switch (update)
        {
            // case { PollAnswer: { } pollAnswer }:
            //     if (pollAnswer.User is null)
            //         break;
            //     
            //     if (!polls.ContainsKey(pollAnswer.User.Id))
            //         break;
            //     if (pollAnswer.PollId == polls[pollAnswer.User.Id].Item1)
            //         await OnPollAnswer<Mood>(pollAnswer);
            //     else if (pollAnswer.PollId == polls[pollAnswer.User.Id].Item2)
            //         await OnPollAnswer<Genre>(pollAnswer);
            //     break;
            case { CallbackQuery: { } query }:
            {
                if (query.Data is null || query.Message is null) break;
                var chatId = query.Message.Chat.Id;
                //Console.WriteLine(chatId);
                if (query.Data.EndsWith("Mood"))
                {
                    var mood = query.Data.Replace("Mood", "");
                    await bot.AnswerCallbackQuery(query.Id, $"Вы выбрали {mood}");
                    users[chatId].AddMood(mood.MoodParse());
                }
                
                else if (query.Data.EndsWith("Genre"))
                {
                    var genre = query.Data.Replace("Genre", "");
                    await bot.AnswerCallbackQuery(query.Id, $"Вы выбрали {genre}");
                    users[chatId].AddGenre(genre.GenreParse());
                }
                
                else if (query.Data.StartsWith("accept"))
                {
                    if (query.Data.EndsWith("Moods"))
                    {
                        await bot.AnswerCallbackQuery(query.Id, "Принято", showAlert: true);
                        users[chatId].AreMoodsSelected = true;
                    }
                    else if (query.Data.EndsWith("Genres"))
                    {
                        await bot.AnswerCallbackQuery(query.Id, "Принято", showAlert: true);
                        users[chatId].AreGenresSelected = true;
                    }
                    else break;

                    await GetPlayList(chatId);
                }

                break;
            }

            default:
                Console.WriteLine($"Не обрабатывается тип {update.Type}");
                break;
        }
    }

    private async Task OnMessage(Message msg, UpdateType type)
    {
        if (msg.Text is not { } text)
            Console.WriteLine($"Отправлено сообщение типа {msg.Type}");
        else if (text.StartsWith('/'))
        {
            var space = text.IndexOf(' ');
            if (space < 0) space = text.Length;
            var command = text[..space].ToLower();
            if (command.LastIndexOf('@') is > 0 and var at)
                if (command[(at + 1)..].Equals(me.Username, StringComparison.OrdinalIgnoreCase))
                    command = command[..at];
                else
                    return;
            await OnCommand(command, text[space..].TrimStart(), msg);
        }
        else
            await OnTextMessage(msg);
    }

    private async Task OnCommand(string command, string args, Message msg)
    {
        Console.WriteLine($"Обработка команды {command} {args}");
        switch (command)
        {
            case "/start":
            case "/login":
            {
                await StartAuthorization(msg.Chat.Id);
                break;
            }
            case "/playlist":
            {
                Console.WriteLine(msg.Chat.Id);
                await GetPlayList(msg.Chat.Id);
                break;
            }
        }
    }

    private async Task OnTextMessage(Message msg)
    {
        if (authorizations.ContainsKey(msg.Chat.Id))
            await ProcessAuthorizations(msg);
    }

    private async Task StartAuthorization(long chatId)
    {
        authorizations[chatId] = new Authorization();
        await bot.SendMessage(chatId, "Введите логин", replyMarkup: replyKeyboardWithFunc);
    }

    private async Task ProcessAuthorizations(Message message)
    {
        if (!authorizations.TryGetValue(message.Chat.Id, out var authorization) || message.Text is null) return;
        if (authorization.Login is null)
        {
            authorization.AddLogin(message.Text);
            await bot.SendMessage(message.Chat.Id, "Введите пароль");
            return;
        }

        if (authorization.Password is null)
        {
            authorization.AddPassword(message.Text);
            var wasAuthorizationSuccessful = await TryAuthorizeWithout2FA(message.Chat.Id);
            if (!wasAuthorizationSuccessful)
                await bot.SendMessage(message.Chat.Id, "Введите код 2FA:");
            return;
        }

        authorization.AddCode(message.Text);
        await TryAuthorizeWith2FA(message.Chat.Id);
    }

    private async Task ConfirmAuthorization(long chatId, bool wasAuthorizationSuccessful)
    {
        if (!authorizations.TryGetValue(chatId, out _))
        {
            Console.WriteLine("Нет такого chatId");
            return;
        }

        if (wasAuthorizationSuccessful)
        {
            authorizations.Remove(chatId);
            await bot.SendMessage(chatId, "Авторизация успешна");
            await GetPlayList(chatId);
        }

        else
        {
            await bot.SendMessage(chatId, "Неправильный логин или пароль");
            await StartAuthorization(chatId);
        }
    }

    private async Task<bool> TryAuthorizeWithout2FA(long chatId)
    {
        if (!authorizations.TryGetValue(chatId, out var authorization)
            || authorization.Login is null
            || authorization.Password is null)
            return false;

        var vkApi = new VkApiWrapper();
        try
        {
            vkApi.AuthorizeWithout2FA(authorization.Login, authorization.Password);
        }
        catch (VkAuthException exception)
        {
            Console.WriteLine(exception);
            if (exception.Message == "Неправильный логин или пароль")
                await ConfirmAuthorization(chatId, false);
            else if (exception.Message == "Произведено слишком много попыток входа")
                await bot.SendMessage(chatId, "Произведено слишком много попыток входа");
            return false;
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine(exception);
            return false;
        }

        users[chatId] = new(vkApi);
        await ConfirmAuthorization(chatId, true);
        return true;
    }

    private async Task<bool> TryAuthorizeWith2FA(long chatId)
    {
        if (!authorizations.TryGetValue(chatId, out var authorization)
            || authorization.Login is null
            || authorization.Password is null
            || authorization.Code is null)
            return false;

        var vkApi = new VkApiWrapper();
        try
        {
            vkApi.AuthorizeWith2FA(authorization.Login, authorization.Password, authorization.Code);
        }
        catch (VkAuthException exception)
        {
            Console.WriteLine(exception);
            if (exception.Message == "Неправильный логин или пароль")
                await ConfirmAuthorization(chatId, false);
            else if (exception.Message == "Произведено слишком много попыток входа")
                await bot.SendMessage(chatId, "Произведено слишком много попыток входа");
            return false;
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine(exception);
            return false;
        }

        users[chatId] = new(vkApi);
        await ConfirmAuthorization(chatId, true);
        return true;
    }


    // private async Task ChooseMoodsPoll(long chatId)
    // {
    //     var msg = await bot.SendPoll(chatId, "Выберите настроение", pollMoods, isAnonymous: false,
    //         allowsMultipleAnswers: true);
    //     if (msg.Poll is null)
    //     {
    //         Console.WriteLine("нет опроса");
    //         return;
    //     }
    //
    //     polls[chatId] = polls.TryGetValue(chatId, out var poll)
    //         ? (msg.Poll.Id, poll.Item2)
    //         : (msg.Poll.Id, null);
    // }
    //
    // private async Task ChooseGenresPoll(long chatId)
    // {
    //     var msg = await bot.SendPoll(chatId, "Выберите жанр", pollGenres, isAnonymous: false,
    //         allowsMultipleAnswers: true);
    //     if (msg.Poll is null)
    //     {
    //         Console.WriteLine("нет опроса");
    //         return;
    //     }
    //
    //     polls[chatId] = polls.TryGetValue(chatId, out var poll)
    //         ? (poll.Item1, msg.Poll.Id)
    //         : (null, msg.Poll.Id);
    // }
    //
    // private async Task OnPollAnswer<T>(PollAnswer pollAnswer) where T : Enum
    // {
    //     if (pollAnswer.User == null) return;
    //     var currentSelections = pollAnswer.OptionIds.Distinct().Select(x => (T)(object)x).ToHashSet();
    //     Console.WriteLine($"{pollAnswer.User.FirstName} выбрал [{string.Join(',', currentSelections)}]");
    //
    //     var user = users[pollAnswer.User.Id];
    //     if (typeof(T) == typeof(Mood))
    //     {
    //         users[pollAnswer.User.Id] = (user.Item1, currentSelections as HashSet<Mood>, user.Item3);
    //     }
    //     else if (typeof(T) == typeof(Genre))
    //     {
    //         users[pollAnswer.User.Id] = (user.Item1, user.Item2, currentSelections as HashSet<Genre>);
    //     }
    //
    //     await GetPlayList(pollAnswer.User.Id);
    // }

    private async Task GetPlayList(long chatId)
    {
        if (!users.TryGetValue(chatId, out var user))
        {
            //users[chatId] = new VkUser(new VkApiWrapper());
            await StartAuthorization(chatId);
            return;
        }

        // if (user.Item2 is null)
        // {
        //     await ChooseMoods(chatId);
        //     return;
        // }
        //
        // if (user.Item3 is null)
        // {
        //     await ChooseGenres(chatId);
        //     return;
        // }
        
        if (!user.AreMoodsSelected)
        {
            await bot.SendMessage(chatId, "Выберите настроение", replyMarkup: inlineMoods);
            return;
        }

        if (!user.AreGenresSelected)
        {
            await bot.SendMessage(chatId, "Выберите жанры", replyMarkup: inlineGenres);
            return;
        }

        await bot.SendMessage(chatId, "Пока только ваши треки");
        await bot.SendMessage(chatId, string.Join('\n', users[chatId].VkApi.GetFavoriteTracks().Select(x => x.Title)));
        Console.WriteLine(users[chatId].VkApi.GetFavoriteTracks().Select(x => x.Title));
    }
}