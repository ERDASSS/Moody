using System.Diagnostics;
using ApiMethods;
using Database;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TGBot.States;

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

    private InputHandlingState currentState = InitState.Instance;
    private readonly Dictionary<long, TgUser> users = new();
    private readonly TelegramBotClient bot;
    private readonly DbAccessor dbAccessor;
    private readonly ReactionToIncorrectInput onIncorrectInput;

    private readonly CancellationTokenSource cts = new(); // так и не понял что это и зачем

    public StateMachine(
        string token,
        DbAccessor dbAccessor,
        ReactionToIncorrectInput onIncorrectInput = ReactionToIncorrectInput.Ignore)
    {
        bot = new TelegramBotClient(token, cancellationToken: cts.Token);
        this.dbAccessor = dbAccessor;
        this.onIncorrectInput = onIncorrectInput;
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

    private async Task OnUpdate(Update update)
    {
        var (chatId, username) = update switch
        {
            // "[field] : { }" - это проверка на не-null
            // ({ } символизирует не-null-овый объект)
            { Message: { } msg } => (msg.Chat.Id, msg.Chat.Username),
            { CallbackQuery: { } cbQuery } => (cbQuery.From.Id, cbQuery.From.Username),
            _ => throw new InvalidOperationException($"пришел Update неожиданного типа: {update}")
        };

        if (!users.ContainsKey(chatId))
            users[chatId] = new TgUser(chatId, username);
        var currentUser = users[chatId];

        try
        {
            var nextState = update switch
            {
                { Message: { } message } =>
                    await currentState.OnMessage(bot, dbAccessor, currentUser, message),
                { CallbackQuery: { } callback } =>
                    await currentState.OnCallback(bot, dbAccessor, currentUser, callback),
                _ => throw new InvalidOperationException($"пришел Update неожиданного типа: {update}")
            };
            if (nextState is null) return;
            // выполняем все лямбда переходы, пока не упремся в состояние, требующее ввода
            while (nextState is LambdaState lambdaState)
                nextState = await lambdaState.Execute(bot, dbAccessor, currentUser);
            if (nextState is not InputHandlingState nextInputHandlingState)
                throw new InvalidOperationException(
                    $"ожидалось, что состояние может быть либо Lambda либо InputHandling, а оно: {nextState}");

            currentState = nextInputHandlingState;
            await nextInputHandlingState.BeforeAnswer(bot, dbAccessor, currentUser);
        }
        catch (InputException e)
        {
            if (onIncorrectInput == ReactionToIncorrectInput.Ignore)
                Console.WriteLine(e.Message); // при игнорировании не меняется ничего - все равно, что ничего не вводить
            else throw new UnreachableException();
        }
    }
}

public enum ReactionToIncorrectInput
{
    Ignore,
}

public abstract class State
{
}

public abstract class InputHandlingState : State
{
    public abstract Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user);

    // по умолчанию никакие входящие сигналы не обрабатываются
    // если вернулся null - значит состояние не нужно менять
    public virtual Task<State?> OnMessage(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user, Message message) =>
        throw new UnexpectedMessageException(message);

    public virtual Task<State?> OnCallback(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user,
        CallbackQuery callback) =>
        throw new UnexpectedCallbackException(callback);
}

public abstract class LambdaState : State
{
    // сразу возвращает State, на который нужно перейти, не ожидая ввода (лямбда переход)
    public abstract Task<State> Execute(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user);
}