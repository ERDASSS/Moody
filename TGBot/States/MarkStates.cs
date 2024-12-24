using Database;
using Database.db_models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using VkNet.Model;

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

        if (user.CurrentSkip == 1)
        {
            if (!user.IsMarkingUnmarked)
                user.UnmarkedTracks = user.ApiWrapper!.GetFavouriteTracks().ToList();
            user.CurrentTrack = user.UnmarkedTracks.FirstOrDefault();
        }

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

        if (user.IsMarkingUnmarked || !user.CurrentDbTrack.Votes.Where(v => v.Key.ParameterId == 2).Any())
            return MarkGenreState.Instance;

        return MarkAgreementStateGenres.Instance;
    }

    private async Task ShowTrackInfo(TelegramBotClient bot, TgUser user)
    {
        await bot.SendMessage(user.ChatId, $"Укажите жанр и настроение для: " +
                                           $"{user.CurrentTrack.Artist} - {user.CurrentTrack.Title}");
        if (user.CurrentDbTrack.Votes.Count != 0 && !user.IsMarkingUnmarked)
        {
            var votes = user.CurrentDbTrack.GetVotesStatistics();
            await bot.SendMessage(user.ChatId, "Текущая разметка трека:");
            await bot.SendMessage(user.ChatId, "Настроения:");
            await ShowTrackStat(bot, user.ChatId, 1, votes, user);
            await bot.SendMessage(user.ChatId, "Жанры:");
            await ShowTrackStat(bot, user.ChatId, 2, votes, user);
        }
        else if (user.CurrentDbTrack.Votes.Count == 0)
        {
            await bot.SendMessage(user.ChatId, "У данного трека отстутствует разметка. Вы будете первым!");
        }
    }

    private async Task ShowTrackStat(TelegramBotClient bot, long chatId, int parameterId,
        Dictionary<DbAudioParameterValue, Dictionary<VoteValue, int>> votes, TgUser user)
    {
        if (!votes.Where(v => v.Key.ParameterId == parameterId).Any())
        {
            var genreOrMood = parameterId == 1 ? "жанры" : "настроения";
            await bot.SendMessage(user.ChatId, $"У данного трека отстутствуют {genreOrMood}. Вы будете первым!");
            return;
        }
        foreach (var vote in votes.Where(v => v.Key.ParameterId == parameterId))
        {
            var confirm = 0;
            var against = 0;

            if (vote.Value.ContainsKey(VoteValue.Confirmation))
                confirm = vote.Value[VoteValue.Confirmation];

            if (vote.Value.ContainsKey(VoteValue.Against))
                against = vote.Value[VoteValue.Against];

            var delta = confirm - against;
            var deltaText = delta.ToString("+#;-#;0");

            await bot.SendMessage(chatId, $"{vote.Key.Name}: {deltaText}");
        }
    }
}

public class MarkAgreementStateGenres : InputHandlingState
{
    public static MarkAgreementStateGenres Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        var commands = new ReplyKeyboardMarkup(true).AddButton("/yes").AddButton("/no").AddButton("/exit");

        await bot.SendMessage(user.ChatId,
            "Согласны с текущими жанрами?\n" +
            "/yes  -  да\n" +
            "/no   -  нет\n" +
            "/exit -  выйти в главное меню",
            replyMarkup: commands);
    }

    public override async Task<State?> OnMessage(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user, Telegram.Bot.Types.Message message)
    {
        switch (message.Text)
        {
            case "/yes":

                // Add confirmation votes for genres
                foreach (var genre in user.CurrentDbTrack.Votes.Keys.Where(vote => vote.ParameterId == 2))
                    dbAccessor.AddVote(user.CurrentDbTrack.DbAudioId, genre.Id, VoteValue.Confirmation, user.DbUser.Id);
                
                await bot.SendMessage(user.ChatId, "Вы подтвердили жанры.");
                return MarkAgreementStateMoods.Instance; // Proceed to mood agreement

            case "/no":

                // Add against votes for genres
                foreach (var genre in user.CurrentDbTrack.Votes.Keys.Where(vote => vote.ParameterId == 2))
                    dbAccessor.AddVote(user.CurrentDbTrack.DbAudioId, genre.Id, VoteValue.Against, user.DbUser.Id);
                
                await bot.SendMessage(user.ChatId, "Вы не согласны с жанрами.");
                return MarkGenreState.Instance; // Allow user to select new genres

            case "/exit":

                return MainMenuState.Instance;

        }
        return null;
    }
}

public class MarkAgreementStateMoods : InputHandlingState
{
    public static MarkAgreementStateMoods Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        var commands = new ReplyKeyboardMarkup(true).AddButton("/yes").AddButton("/no").AddButton("/exit");

        await bot.SendMessage(user.ChatId,
            "Согласны с текущими настроениями?\n" +
            "/yes  -  да\n" +
            "/no   -  нет\n" +
            "/exit -  выйти в главное меню",
            replyMarkup: commands);
    }

    public override async Task<State?> OnMessage(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user, Telegram.Bot.Types.Message message)
    {
        switch (message.Text)
        {
            case "/yes":

                // Add confirmation votes for moods
                foreach (var mood in user.CurrentDbTrack.Votes.Keys.Where(vote => vote.ParameterId == 1))
                    dbAccessor.AddVote(user.CurrentDbTrack.DbAudioId, mood.Id, VoteValue.Confirmation, user.DbUser.Id);

                await bot.SendMessage(user.ChatId, "Вы подтвердили настроения.");
                return AddVoteState.Instance; // Proceed to add votes

            case "/no":

                // Add against votes for moods
                foreach (var mood in user.CurrentDbTrack.Votes.Keys.Where(vote => vote.ParameterId == 1))
                    dbAccessor.AddVote(user.CurrentDbTrack.DbAudioId, mood.Id, VoteValue.Against, user.DbUser.Id);

                await bot.SendMessage(user.ChatId, "Вы не согласны с настроениями.");
                return MarkMoodState.Instance; // Allow user to select new moods

            case "/exit":

                return MainMenuState.Instance;

        }
        return null;
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
                if (user.IsMarkingUnmarked || !user.CurrentDbTrack.Votes.Where(v => v.Key.ParameterId == 1).Any())
                    return MarkMoodState.Instance;

                return MarkAgreementStateMoods.Instance;
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
            user.CurrentSkip = 1;
            return MainMenuState.Instance;
        }

        return BeginMarkState.Instance;
    }
}