using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TGBot;

public enum Genre
{
    None,
    Pop,
    Rock,
    HipHop,
    Jazz,
    Classical,
    Blues,
    Country,
    Electronic,
    Folk,
    Punk
}

public static class GenreExtensions
{
    private static readonly Dictionary<string, Genre> Genres = new()
    {
        { "поп", Genre.Pop },
        { "рок", Genre.Rock },
        { "хип-хоп", Genre.HipHop },
        { "джаз", Genre.Jazz },
        { "классическая", Genre.Classical },
        { "блюз", Genre.Blues },
        { "кантри", Genre.Country },
        { "электронная", Genre.Electronic },
        { "фолк", Genre.Folk },
        { "панк", Genre.Punk },
    };
    
    public static Genre GenreParse(this string genreString)
    {
        if (Genres.TryGetValue(genreString, out var genre))
            return genre;
        throw new ArgumentException();
    }
    
    public static InlineKeyboardMarkup CreateInlineKeyboardMarkup()
    {
        var rows = Genres.Keys
            .Select(moodStr => new[] { InlineKeyboardButton.WithCallbackData(moodStr, $"{moodStr}Genre") })
            .Append(new []{InlineKeyboardButton.WithCallbackData("подтвердить", "acceptGenres")});

        return new InlineKeyboardMarkup(rows);
    }

    public static IEnumerable<InputPollOption> CreateInputPollOptions()
    {
        return Genres.Keys.Select(key => new InputPollOption(key));
    }
}