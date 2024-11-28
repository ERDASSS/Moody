using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TGBot;

public enum Mood
{
    None,
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
        if (Moods.TryGetValue(moodString, out var mood))
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