using Database;
using Telegram.Bot;

namespace TGBot.States;

public class BeginMarkState : LambdaState
{
    public static BeginMarkState Instance { get; } = new();

    public override async Task<State> Execute(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        throw new NotImplementedException();
        // dbAccessor.AddOrUpdateUser(user.user.ChatId, user.TgUsername);
        // user.DbUser = dbAccessor.GetUserByuser.ChatId(user.user.ChatId);
        // if (!user.IsMarkingUnmarked!.Value)
        //     user.UnmarkedTracks = user.ApiWrapper!.GetFavouriteTracks().ToList();
        // user.CurrentTrack = user.UnmarkedTracks.FirstOrDefault();
        // user.CurrentSkip = 1;
        //
        // return MarkState.Instance;
    }
}

public class MarkState : InputHandlingState
{
    public static MarkState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        throw new NotImplementedException();
        // var dbAudio = dbAccessor.TryGetAudioFromBd(user.CurrentTrack);
        //
        // if (dbAudio == null)
        // {
        //     dbAccessor.SaveAudioInDb(user.CurrentTrack);
        //     dbAudio = dbAccessor.TryGetAudioFromBd(user.CurrentTrack);
        // }
        //
        // await ShowTrackInfo(user.ChatId, dbAudio);
        //
        // user.SuggestedMoods = dbAccessor.GetMoods().ToDictionary(m => m.Name, m => m);
        // await bot.SendMessage(user.ChatId, "Выберите настроение для трека",
        //     replyMarkup: user.SuggestedMoods.ToInlineKeyboardMarkup());
        // return;
        //
        // if (!user.AreGenresSelected)
        // {
        //     user.SuggestedGenres = dbAccessor.GetGenres().ToDictionary(g => g.Name, g => g);
        //     await bot.SendMessage(user.ChatId, "Выберите жанр для трека",
        //         replyMarkup: user.SuggestedGenres.ToInlineKeyboardMarkup());
        //     return;
        // }
        //
        // foreach (var mood in user.SelectedMoods)
        //     dbAccessor.AddVote(dbAudio.DbAudioId, mood.Id, VoteValue.Confirmation, user.DbUser.Id);
        //
        // foreach (var genre in user.SelectedGenres)
        //     dbAccessor.AddVote(dbAudio.DbAudioId, genre.Id, VoteValue.Confirmation, user.DbUser.Id);
        //
        //
        // user.ResetMoodsAndGenres();
        // user.CurrentTrack = user.UnmarkedTracks.Skip(user.CurrentSkip).FirstOrDefault();
        // user.CurrentSkip++;
        //
        // if (user.CurrentTrack == null || user.CurrentTrack == default)
        // {
        //     await bot.SendMessage(user.ChatId, "Разметка окончена. Вы можете создать плейлист с помощью команды /playlist",
        //         replyMarkup: replyKeyboardPlaylistAndMark);
        //     user.IsMarkingUnmarked = false;
        //     return;
        // }
        //
        // user.UnmarkedTracks.Skip(1);
        // await StartMarking(user.ChatId);
    }
}

public class MarkMoodState : InputHandlingState
{
    public static MarkMoodState Instance { get; } = new();

    public override Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        throw new NotImplementedException();
    }
}