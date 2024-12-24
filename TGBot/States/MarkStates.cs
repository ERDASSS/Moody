using Database;
using Database.db_models;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TGBot.States;

// todo: понатыкать кнопок отмены и назад да побольше
// TODO: Особенно в mark состояния, а то сейчас оттуда нельзя выйти, не разметив вообще всё)
// TODO: подумать, что сделать с кучей nullable полей

public class BeginMarkState : LambdaState
{
    public static BeginMarkState Instance { get; } = new();

    public override Task<State> Execute(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        dbAccessor.AddOrUpdateUser(user.ChatId, user.TgUsername);
        user.DbUser = dbAccessor.GetUserByChatId(user.ChatId);
        if (!user.IsMarkingUnmarked)
            user.UnmarkedTracks = user.ApiWrapper!.GetFavouriteTracks().ToList();
        user.CurrentTrack = user.UnmarkedTracks.FirstOrDefault();
        user.CurrentSkip = 1;


        var dbAudio = dbAccessor.TryGetAudioFromBd(user.CurrentTrack);
        if (dbAudio == null)
        {
            dbAccessor.SaveAudioInDb(user.CurrentTrack);
            dbAudio = dbAccessor.TryGetAudioFromBd(user.CurrentTrack);
        }

        user.CurrentDbTrack = dbAudio;


        return Task.FromResult<State>(ShowMarkupInfoState.Instance);
    }
}

public class ShowMarkupInfoState : LambdaState
{
    public static ShowMarkupInfoState Instance { get; } = new();

    public override async Task<State> Execute(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        await ShowTrackInfo(bot, user);
        return MarkGenreState.Instance;
    }

    private async Task ShowTrackInfo(TelegramBotClient bot, TgUser user)
    {
        await bot.SendMessage(user.ChatId, $"Укажите жанр и настроение для: " +
                                           $"{user.CurrentTrack.Artist} - {user.CurrentTrack.Title}");
        if (user.CurrentDbTrack.Votes != null && !user.IsMarkingUnmarked)
        {
            var votes = user.CurrentDbTrack.GetVotesStatistics();
            await bot.SendMessage(user.ChatId, "Текущая разметка трека:");
            await bot.SendMessage(user.ChatId, "Настроения:");
            await ShowTrackStat(bot, user.ChatId, 1, votes);
            await bot.SendMessage(user.ChatId, "Жанры:");
            await ShowTrackStat(bot, user.ChatId, 2, votes);
        }
    }

    private async Task ShowTrackStat(TelegramBotClient bot, long chatId, int parameterId,
        Dictionary<DbAudioParameterValue, Dictionary<VoteValue, int>> votes)
    {
        foreach (var vote in votes.Where(v => v.Key.ParameterId == parameterId))
        {
            var confirm = 0;
            var against = 0;

            if (vote.Value.ContainsKey(VoteValue.Confirmation))
                confirm = vote.Value[VoteValue.Confirmation];

            if (vote.Value.ContainsKey(VoteValue.Against))
                against = vote.Value[VoteValue.Against];

            await bot.SendMessage(chatId, $"{vote.Key.Name}: {confirm} - за, {against} - против");
        }
    }
}

public class MarkGenreState : InputHandlingState
{
    public static MarkGenreState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        user.SuggestedGenres = dbAccessor.GetGenres().ToDictionary(g => g.Name, g => g);
        await bot.SendMessage(user.ChatId, "Выберите жанр для трека",
            replyMarkup: user.SuggestedGenres.ToInlineKeyboardMarkup());
    }

    public override async Task<State?> OnCallback(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user,
        CallbackQuery callback)
    {
        // todo: чет много дублирования на настроение и жанр
        if (callback.Data.EndsWith("Genre"))
        {
            var genre = callback.Data.Replace("Genre", "");
            await bot.AnswerCallbackQuery(callback.Id, $"Вы выбрали {genre}");
            user.SelectGenre(user.SuggestedGenres[genre]);
            // todo: в качестве хранилища для жанров при разметке используется тот же контейнер,
            // todo: что и для выбора жанра при формировании плейлиста
            // todo: хз плохо ли это, но по хорошему это состояние не должно иметь доступ к тому полю
            // todo: но я хз как это реализовать
            return null;
        }

        if (callback.Data.StartsWith("accept"))
        {
            if (callback.Data.EndsWith("Genres"))
            {
                await bot.AnswerCallbackQuery(callback.Id, "Принято", showAlert: true);
                return MarkMoodState.Instance;
            }
        }

        return null;
    }
}

public class MarkMoodState : InputHandlingState
{
    public static MarkMoodState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        user.SuggestedMoods = dbAccessor.GetMoods().ToDictionary(m => m.Name, m => m);
        await bot.SendMessage(user.ChatId, "Выберите настроение для трека",
            replyMarkup: user.SuggestedMoods.ToInlineKeyboardMarkup());
    }

    public override async Task<State?> OnCallback(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user,
        CallbackQuery callback)
    {
        // todo: чет много дублирования на настроение и жанр
        if (callback.Data.EndsWith("Mood"))
        {
            var mood = callback.Data.Replace("Mood", "");
            await bot.AnswerCallbackQuery(callback.Id, $"Вы выбрали {mood}");
            user.SelectMood(user.SuggestedMoods[mood]);
            return null;
        }

        if (callback.Data.StartsWith("accept"))
        {
            if (callback.Data.EndsWith("Moods"))
            {
                await bot.AnswerCallbackQuery(callback.Id, "Принято", showAlert: true);
                return AddVoteState.Instance;
            }
        }

        return null;
    }
}

public class AddVoteState : LambdaState
{
    public static AddVoteState Instance { get; } = new();

    public override async Task<State> Execute(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        foreach (var mood in user.SelectedMoods)
            dbAccessor.AddVote(user.CurrentDbTrack.DbAudioId, mood.Id, VoteValue.Confirmation, user.DbUser.Id);

        foreach (var genre in user.SelectedGenres)
            dbAccessor.AddVote(user.CurrentDbTrack.DbAudioId, genre.Id, VoteValue.Confirmation, user.DbUser.Id);

        user.ResetMoodsAndGenres();
        user.CurrentTrack = user.UnmarkedTracks.Skip(user.CurrentSkip).FirstOrDefault();
        user.CurrentSkip++;

        if (user.CurrentTrack == null || user.CurrentTrack == default)
        {
            await bot.SendMessage(user.ChatId, "Разметка окончена!");
            return MainMenuState.Instance;
        }

        // todo: выглядит так, как будто это выражение ничего не делает - просто возвращает значение и ничего с ним не делает
        user.UnmarkedTracks.Skip(1);
        return BeginMarkState.Instance;
    }
}