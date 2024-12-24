using Telegram.Bot.Types;

namespace TGBot;

public class InputException : Exception
{
    protected InputException()
    {
    }

    protected InputException(string message) : base(message)
    {
    }
}

public class UnexpectedInputException(string message) : InputException(message)
{
    // на случай, если тип сообщения не верный
    // (напр. пришел callback вместо message)
}

public class IncorrectInputException(string message) : InputException(message)
{
    // на случай, если тип сообщения верный, но сообщение - нет
    // (напр. пришло h23352_.-'$╘шLчb██------9fq34 вместо /login)
}

public class IncorrectMessageException : IncorrectInputException
{
    public IncorrectMessageException(string receivedMessage)
        : base($"Пришло некорректное сообщение: {receivedMessage}")
    {
    }

    public IncorrectMessageException(string receivedMessage, string acceptableMessages)
        : base($"Ожидались сообщения: {acceptableMessages}, а получено {receivedMessage}")
    {
    }
}

public class UnexpectedMessageException(Message message)
    : UnexpectedInputException($"Пришло неожиданное сообщение: {message}");

public class IncorrectCallbackException : IncorrectInputException
{
    public IncorrectCallbackException(string receivedCallback)
        : base($"Пришел некорректный калбек: {receivedCallback}")
    {
    }

    public IncorrectCallbackException(string receivedCallback, string acceptableCallbacks)
        : base($"Ожидались калбеки: {acceptableCallbacks}, а получено {receivedCallback}")
    {
    }
}

public class UnexpectedCallbackException(CallbackQuery callback)
    : UnexpectedInputException($"Пришел неожиданный калбек: {callback}");