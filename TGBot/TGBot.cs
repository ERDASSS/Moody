using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

public class TGBot
{
    public TGBot(string token)
    {
        this.token = token;
    }

    private readonly string token;
    private TelegramBotClient bot;
    private User me;
    private readonly CancellationTokenSource cts = new();
    private readonly Dictionary<long, Authorization> authorizations = new();
    private readonly Dictionary<long, (bool, HashSet<Mood>?, HashSet<Genre>?)> users = new();
    private readonly Dictionary<long, (string?, string?)> polls = new();

    private readonly ReplyKeyboardMarkup replyKeyboardWithFunc = new(
        new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton("/hello"),
                new KeyboardButton("/playlist")
            }
        })
    {
        ResizeKeyboard = false
    };

    private readonly List<InputPollOption> pollMoods = MoodExtensions.CreateInputPollOptions().ToList();
    private readonly List<InputPollOption> pollGenres = GenreExtensions.CreateInputPollOptions().ToList();

    public async Task Start()
    {
        bot = new TelegramBotClient(token, cancellationToken: cts.Token);
        me = await bot.GetMe();
        // await bot.DeleteWebhook();
        // await bot.DropPendingUpdates();
        bot.OnError += OnError;
        bot.OnMessage += OnMessage;
        bot.OnUpdate += OnUpdate;
        Console.WriteLine($"{me.FirstName} запущен!");
    }

    public async Task ConfirmAuthorization(long chatId, bool wasAuthorizationSuccessful)
    {
        if (!authorizations.TryGetValue(chatId, out _))
        {
            Console.WriteLine("Нет такого chatId");
            return;
        }

        if (wasAuthorizationSuccessful)
        {
            authorizations.Remove(chatId);
            if (users.TryGetValue(chatId, out var value))
                users[chatId] = (true, value.Item2, value.Item3);
            else
                users[chatId] = (true, null, null);
            await bot.SendMessage(chatId, "Авторизация успешна");
            await GetPlayList(chatId);
        }

        else
        {
            await bot.SendMessage(chatId, "Авторизация неуспешна");
            await StartAuthorization(chatId);
        }
    }

    private async Task OnError(Exception exception, HandleErrorSource source)
    {
        Console.WriteLine(exception);
        await Task.Delay(2000, cts.Token);
    }

    private async Task OnUpdate(Update update)
    {
        switch (update)
        {
            case { PollAnswer: { } pollAnswer }:
                if (pollAnswer.User is null)
                    break;

                if (!polls.ContainsKey(pollAnswer.User.Id))
                    break;
                if (pollAnswer.PollId == polls[pollAnswer.User.Id].Item1)
                    await OnPollAnswer<Mood>(pollAnswer);
                else if (pollAnswer.PollId == polls[pollAnswer.User.Id].Item2)
                    await OnPollAnswer<Genre>(pollAnswer);
                break;
            default:
                Console.WriteLine($"Received unhandled update {update.Type}");
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
            case "/moods":
            {
                if (!users.TryGetValue(msg.Chat.Id, out var user) || !user.Item1)
                {
                    await StartAuthorization(msg.Chat.Id);
                    break;
                }

                await ChooseMoods(msg.Chat.Id);
                break;
            }
            case "/genres":
            {
                if (!users.TryGetValue(msg.Chat.Id, out var user) || !user.Item1)
                {
                    await StartAuthorization(msg.Chat.Id);
                    break;
                }

                await ChooseGenres(msg.Chat.Id);
                break;
            }
            case "/playlist":
            {
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
            await bot.SendMessage(message.Chat.Id, "Введите код 2FA:");
            return;
        }

        authorization.AddCode(message.Text);
        // нужна функция, чтобы передать chatId и authorization в основную программу и оттуда вызвать следующий метод
        await ConfirmAuthorization(message.Chat.Id, true);
    }

    private async Task ChooseMoods(long chatId)
    {
        var msg = await bot.SendPoll(chatId, "Выберите настроение", pollMoods, isAnonymous: false,
            allowsMultipleAnswers: true);
        if (msg.Poll is null)
        {
            Console.WriteLine("нет опроса");
            return;
        }

        polls[chatId] = polls.TryGetValue(chatId, out var poll)
            ? (msg.Poll.Id, poll.Item2)
            : (msg.Poll.Id, null);
    }

    private async Task ChooseGenres(long chatId)
    {
        var msg = await bot.SendPoll(chatId, "Выберите жанр", pollGenres, isAnonymous: false,
            allowsMultipleAnswers: true);
        if (msg.Poll is null)
        {
            Console.WriteLine("нет опроса");
            return;
        }

        polls[chatId] = polls.TryGetValue(chatId, out var poll)
            ? (poll.Item1, msg.Poll.Id)
            : (null, msg.Poll.Id);
    }

    private async Task OnPollAnswer<T>(PollAnswer pollAnswer) where T : Enum
    {
        if (pollAnswer.User == null) return;
        var currentSelections = pollAnswer.OptionIds.Distinct().Select(x => (T)(object)x).ToHashSet();
        Console.WriteLine($"{pollAnswer.User.FirstName} выбрал [{string.Join(',', currentSelections)}]");

        var user = users[pollAnswer.User.Id];
        if (typeof(T) == typeof(Mood))
        {
            users[pollAnswer.User.Id] = (user.Item1, currentSelections as HashSet<Mood>, user.Item3);
        }
        else if (typeof(T) == typeof(Genre))
        {
            users[pollAnswer.User.Id] = (user.Item1, user.Item2, currentSelections as HashSet<Genre>);
        }

        await GetPlayList(pollAnswer.User.Id);
    }

    private async Task GetPlayList(long chatId)
    {
        if (!users.TryGetValue(chatId, out var user) || !user.Item1)
        {
            await StartAuthorization(chatId);
            return;
        }

        if (user.Item2 is null)
        {
            await ChooseMoods(chatId);
            return;
        }

        if (user.Item3 is null)
        {
            await ChooseGenres(chatId);
            return;
        }

        // тут нужна функция чтобы по chatId: int, moods: set и genre: set получить плейлист
        Console.WriteLine(user.ToString());
    }
}