using System.Diagnostics;
using ApiMethods;
using Database;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TGBot;

public class StateMachine
{
    // Обработка состояний идет следующим образом:
    //   - объявляется стартовое состояние S
    //         (код входа в S никогда не выполняется, так как первым пишет пользователь, а не бот)
    //   - далее состояния сменяются в зависимости от ввода:
    //     Смена состояния с A на следующее происходит следующим образом:
    //       - считывается ввод пользователя
    //       - выполняется код выхода из состояния A и определяется следующее состояние B
    //       - состояние меняется с A на B
    //       - выполняется код входа в состояние B

    public ReactionToIncorrectInput OnIncorrectInput { get; set; }
    public State CurrentState { get; set; } = InitState.Instance;

    private readonly TelegramBotClient bot;
    private readonly DbAccessor dbAccessor;
    private readonly Dictionary<long, TgUser> users = new();

    private readonly CancellationTokenSource cts = new(); // так и не понял что это и зачем


    public StateMachine(
        string token,
        DbAccessor dbAccessor,
        ReactionToIncorrectInput onIncorrectInput = ReactionToIncorrectInput.Ignore)
    {
        bot = new TelegramBotClient(token, cancellationToken: cts.Token);
        this.dbAccessor = dbAccessor;
        OnIncorrectInput = onIncorrectInput;
        bot.OnError += OnError;
        bot.OnUpdate += OnUpdate;

        var me = bot.GetMe().Result;
        Console.WriteLine($"{me.FirstName} запущен на @Moody_24_bot!");
    }

    private async Task OnError(Exception exception, HandleErrorSource source)
    {
        Console.WriteLine("Бот отловил ошибку и не упал:");
        Console.WriteLine(exception);
        await Task.Delay(2000, cts.Token);
    }

    // public void Start()
    // {
    //     try
    //     {
    //         CurrentState.BeforeAnswer(bot);
    //     }
    //     catch (IncorrectInputException e)
    //     {
    //         if (OnIncorrectInput == ReactionToIncorrectInput.Ignore)
    //             Console.WriteLine(e.Message);
    //         else
    //             throw new UnreachableException();
    //     }
    // }

    private async Task OnUpdate(Update update)
    {
        var (chatId, username) = update switch
        {
            { Message: { } msg } => (msg.Chat.Id, msg.Chat.Username),
            { CallbackQuery: { } cbQuery } => (cbQuery.From.Id, cbQuery.From.Username),
            _ => throw new InvalidOperationException($"пришел Update неожиданного типа: {update}")
        };
        // "[field] : { }" - это проверка на не-null
        // ({ } символизирует не-null-овый объект)

        if (!users.ContainsKey(chatId))
            users[chatId] = new TgUser(chatId, username);
        var currentUser = users[chatId];

        try
        {
            var nextState = update switch
            {
                { Message: { } message } => CurrentState.OnMessage(bot, currentUser, message),
                { CallbackQuery: { } callback } => CurrentState.OnCallback(bot, currentUser, callback),
                _ => throw new InvalidOperationException($"пришел Update неожиданного типа: {update}")
            };
            CurrentState = nextState;
            await CurrentState.BeforeAnswer(bot, currentUser);
        }
        catch (IncorrectInputException e)
        {
            if (OnIncorrectInput == ReactionToIncorrectInput.Ignore)
                Console.WriteLine(e.Message);
            else
                throw new UnreachableException();
        }
    }
}

public enum ReactionToIncorrectInput
{
    Ignore,
}

// public interface ITgAnswer
// {
// }
//
// public class TgMessage(Message msg, UpdateType type) : ITgAnswer
// {
//     public Message Message { get; } = msg;
//     public UpdateType Type { get; } = type;
// }
//
// public class TgUpdate(Update update) : ITgAnswer
// {
//     public Update Update { get; } = update;
// }

// public enum StateName
// {
// }

// public interface IState<out TConcreteState> where TConcreteState : IState<TConcreteState>
public abstract class State
{
    public abstract Task BeforeAnswer(TelegramBotClient bot, TgUser user);

    // public IState AfterAnswer(TelegramBotClient bot, TgUser user, ITgAnswer answer);
    // public IState AfterAnswer(TelegramBotClient bot, TgUser user, Message msg, UpdateType type);

    public virtual State OnMessage(TelegramBotClient bot, TgUser user, Message message) =>
        throw new UnexpectedMessageException(message);

    public virtual State OnCallback(TelegramBotClient bot, TgUser user, CallbackQuery callback) =>
        throw new UnexpectedCallbackException(callback);

    // public State AfterAnswer(TelegramBotClient bot, TgUser user, Update update);
}

public class InitState : State
{
    public static State Instance { get; } = new InitState();

    public override async Task BeforeAnswer(TelegramBotClient bot, TgUser user)
    {
        // никогда не вызовется
    }

    // на любое сообщение пользователя переходит к приветственному
    public override State OnMessage(TelegramBotClient bot, TgUser user, Message message) => StartState.Instance;
    public override State OnCallback(TelegramBotClient bot, TgUser user, CallbackQuery callback) => StartState.Instance;
}

public class StartState : State
{
    public static State Instance { get; } = new StartState();

    private readonly ReplyKeyboardMarkup commands = new(
        new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton("/login"),
                new KeyboardButton("/demo")
            }
        })
    {
        ResizeKeyboard = false
    };

    public override async Task BeforeAnswer(TelegramBotClient bot, TgUser user)
    {
        await bot.SendMessage(user.ChatId,
            "Добро пожаловать!\n" +
            "Это бот для фильтрации вашей музыки в вк\n" +
            "Пожалуйста, перейдите к авторизации, введя команду /login\n" +
            "Или опробуйте функционал на заранее заданных треках, введя /demo");
        await bot.SendMessage(user.ChatId,
            "Внимание! Бот не собирает ваши личные данные. Производится только только авторизация",
            replyMarkup: commands);
    }

    // todo: Выделить бота и юзера в отдельный класс?
    public override State OnMessage(TelegramBotClient bot, TgUser user, Message msg)
    {
        switch (msg.Text)
        {
            case "/login":
                return EnterLoginState.Instance;
            case "/demo":
                user.ApiWrapper = new TestApiWrapper();
                return MenuState.Instance;
            default:
                throw new IncorrectMessageException(msg.Text ?? "[null]", "/login, /demo");
        }
    }
}

class EnterLoginState : State
{
    public static State Instance { get; } = new EnterLoginState();

    public override Task BeforeAnswer(TelegramBotClient bot, TgUser user)
    {
        throw new NotImplementedException();
    }
}

class EnterPasswordState : State
{
    public override Task BeforeAnswer(TelegramBotClient bot, TgUser user)
    {
        throw new NotImplementedException();
    }
}

class Enter2FaCodeState : State
{
    public override Task BeforeAnswer(TelegramBotClient bot, TgUser user)
    {
        throw new NotImplementedException();
    }
}

class MenuState : State
{
    public static State Instance { get; } = new MenuState();

    // TODO: проверить, работает ли
    private readonly ReplyKeyboardMarkup commands =
        new ReplyKeyboardMarkup(true).AddButton("/playlist").AddButton("/mark");
    // private readonly ReplyKeyboardMarkup commands = new(new List<KeyboardButton[]>
    // {
    //     new[]
    //     {
    //         new KeyboardButton("/playlist"),
    //         new KeyboardButton("/mark")
    //     }
    // })
    // {
    //     ResizeKeyboard = false
    // };

    public override async Task BeforeAnswer(TelegramBotClient bot, TgUser user)
    {
        await bot.SendMessage(user.ChatId,
            "Можете выбрать команду из меню:\n" +
            "/playlist - создать плейлист\n" +
            "/mark     - разметить треки\n",
            replyMarkup: commands);
    }

    public override State OnMessage(TelegramBotClient bot, TgUser user, Message msg)
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

public class IncorrectInputException : Exception
{
    protected IncorrectInputException()
    {
    }

    protected IncorrectInputException(string message) : base(message)
    {
    }
}

public class IncorrectMessageException : IncorrectInputException
{
    public IncorrectMessageException()
    {
    }

    public IncorrectMessageException(string receivedMessage)
        : base($"Пришло некорректное сообщение: {receivedMessage}")
    {
    }

    public IncorrectMessageException(string receivedMessage, string acceptableMessages)
        : base($"Ожидались сообщения: {acceptableMessages}, а получено {receivedMessage}")
    {
    }
}

public class UnexpectedMessageException : IncorrectInputException
{
    public UnexpectedMessageException()
    {
    }

    public UnexpectedMessageException(Message message)
        : base($"Пришло неожиданное сообщение: {message}")
    {
    }
}

public class UnexpectedCallbackException : IncorrectInputException
{
    public UnexpectedCallbackException()
    {
    }

    public UnexpectedCallbackException(CallbackQuery callback)
        : base($"Пришел неожиданный калбек: {callback}")
    {
    }

    // public IncorrectUpdateException(string receivedMessage, string acceptableMessages)
    //     : base($"Ожидалось сообщение {acceptableMessages}, а получено {receivedMessage}")
    // {
    // }
}