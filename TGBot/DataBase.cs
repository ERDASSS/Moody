using System.Data.Common;
using Database.db_models;
using Telegram.Bot.Types.ReplyMarkups;
using VkNet.Model.Attachments;

namespace TGBot;

static class ParametersExtension
{
    // что-то мне все меньше нравится этот класс
    public static InlineKeyboardMarkup ToInlineKeyboardMarkup<TParameterValue>(
        this Dictionary<string, TParameterValue> parameterValues)
        where TParameterValue : DbAudioParameterValue
    {
        var parameter = parameterValues.Values.First().GetParameter();
        string parameterSuffix;
        if (parameter == DbAudioParameter.MoodParameter)
            parameterSuffix = "Mood";
        else if (parameter == DbAudioParameter.GenreParameter)
            parameterSuffix = "Genre";
        else
            throw new ArgumentException($"на месте {parameter} ожидалась коллекция Moods или Genres");

        var rows = parameterValues.Keys
            .Select(pvName => new[] { InlineKeyboardButton.WithCallbackData(pvName, $"{pvName}{parameterSuffix}") })
            .Append([InlineKeyboardButton.WithCallbackData("[подтвердить]", $"accept{parameterSuffix}s")]);

        return new InlineKeyboardMarkup(rows);
    }


    public static InlineKeyboardMarkup ToInlineKeyboardMarkup(this IEnumerable<DbAudioParameterValue> parameterValues)
    {
        var parameterName = parameterValues switch
        {
            IEnumerable<DbMood> => "Mood",
            IEnumerable<DbGenre> => "Genre",
            _ => throw new ArgumentException($"на месте {parameterValues} ожидалась коллекция Moods или Genres")
        };

        var rows = parameterValues
            .Select(pv => pv.Name)
            .Select(pvName => new[] { InlineKeyboardButton.WithCallbackData(pvName, $"{pvName}{parameterName}") })
            .Append([InlineKeyboardButton.WithCallbackData("[подтвердить]", $"accept{parameterName}s")]);

        return new InlineKeyboardMarkup(rows);
    }


    // public static InlineKeyboardMarkup ToInlineKeyboardMarkup(this IEnumerable<DbMood> moods)
    // {
    //     var rows = moods
    //         .Select(mood => mood.Name)
    //         .Select(moodStr => new[] { InlineKeyboardButton.WithCallbackData(moodStr, $"{moodStr}Mood") })
    //         .Append([InlineKeyboardButton.WithCallbackData("[подтвердить]", "acceptMoods")]);
    //
    //     return new InlineKeyboardMarkup(rows);
    // }
    //
    // public static InlineKeyboardMarkup ToInlineKeyboardMarkup(this IEnumerable<DbGenre> genres)
    // {
    //     var rows = genres
    //         .Select(mood => mood.Name)
    //         .Select(genreStr => new[] { InlineKeyboardButton.WithCallbackData(genreStr, $"{genreStr}Genre") })
    //         .Append([InlineKeyboardButton.WithCallbackData("[подтвердить]", "acceptGenres")]);
    //
    //     return new InlineKeyboardMarkup(rows);
    // }
}

// class DataBase
// {
//     public static void AddTrackToDataBase(Audio track, DbGenre dbGenre = default, Mood mood = default)
//     {
//         var trackName = track.Title;
//         var trackAuthor = track.Artist;
//         //тут если genre == Genre.None то добавляем бюез жанра, аналогично с настроением
//
//         //добавляем треки в бд
//         throw new NotImplementedException();
//     }
//
//     public static bool HasTrackInDataBase(Audio track)
//     {
//         //чекаем есть ли трек в бд
//         throw new NotImplementedException();
//     }
//
//     public static bool IsRightTrack(Audio track, DbGenre dbGenre = default, Mood mood = default)
//     {
//         //проверяем подходит ли трек по настроению и жанру
//         throw new NotImplementedException();
//     }
// }