using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

public enum Mood
{
    Fun,
    Sad
}

public static class MoodExtensions
{
    private static readonly Dictionary<string, Mood> Moods = new()
    {
        { "весело", Mood.Fun },
        { "грустно", Mood.Sad }
    };
    
    public static Mood MoodParse(this string moodString)
    {
        if (Moods.TryGetValue(moodString.ToLower(), out var mood))
            return mood;
        throw new ArgumentException();
    }

    public static InlineKeyboardMarkup CreateInlineKeyboardMarkup()
    {
        var rows = Moods.Keys
            .Select(moodStr => new[] { InlineKeyboardButton.WithCallbackData(moodStr, $"{moodStr}Mood") })
            .Append(new []{InlineKeyboardButton.WithCallbackData("подтвердить", "acceptMoods")});

        return new InlineKeyboardMarkup(rows);
    }

    public static IEnumerable<InputPollOption> CreateInputPollOptions()
    {
        return Moods.Keys.Select(key => new InputPollOption(key));
    }
}