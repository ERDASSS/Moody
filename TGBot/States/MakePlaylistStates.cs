using Database;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TGBot.States;

// TODO: много дублирования

class BeginMakingPlaylist : LambdaState // просто алиас для удобного вызова снаружи
{
    public static BeginMakingPlaylist Instance { get; } = new();

    public override Task<State> Execute(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        return Task.FromResult<State>(SelectMoodsState.Instance);
    }
}

class SelectMoodsState : InputHandlingState
{
    public static SelectMoodsState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        user.SuggestedMoods = dbAccessor.GetMoods().ToDictionary(m => m.Name, m => m);
        await bot.SendMessage(user.ChatId, "Выберите настроения",
            replyMarkup: user.SuggestedMoods
                .Where(p => !p.Key.StartsWith('['))
                .ToDictionary()
                .ToInlineKeyboardMarkup());
    }

    public override async Task<State?> OnCallback(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user,
        CallbackQuery callback)
    {
        if (callback.Data is null)
            throw new IncorrectCallbackException("[null]");

        if (callback.Data.StartsWith("accept") && callback.Data.EndsWith("Moods"))
        {
            await bot.AnswerCallbackQuery(callback.Id, "Принято");
            return SelectGenreState.Instance;
        }

        var mood = callback.Data.Replace("Mood", "");
        await bot.AnswerCallbackQuery(callback.Id, $"Вы выбрали {mood}");
        user.SelectMood(user.SuggestedMoods[mood]);
        return null;
 
        //throw new IncorrectCallbackException(callback.Data, ".*Mood | acceptMoods");
    }
}

class SelectGenreState : InputHandlingState
{
    public static SelectGenreState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        user.SuggestedGenres = dbAccessor.GetGenres().ToDictionary(g => g.Name, g => g);
        await bot.SendMessage(user.ChatId, "Выберите жанры (если вам не важен жанр, сразу нажмите [подтвердить])",
            replyMarkup: user.SuggestedGenres
                .Where(p => !p.Key.StartsWith('['))
                .ToDictionary()
                .ToInlineKeyboardMarkup());
    }

    public override async Task<State?> OnCallback(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user,
        CallbackQuery callback)
    {
        if (callback.Data is null)
            throw new IncorrectCallbackException("[null]");

        if (callback.Data.StartsWith("accept") && callback.Data.EndsWith("Genres"))
        {
            await bot.AnswerCallbackQuery(callback.Id, "Принято");
            return CreatePlaylistState.Instance;
        }

        var genre = callback.Data.Replace("Genre", "");
        await bot.AnswerCallbackQuery(callback.Id, $"Вы выбрали {genre}");
        user.SelectGenre(user.SuggestedGenres[genre]);
        return null;

        //throw new IncorrectCallbackException(callback.Data, ".*Genre | acceptGenres");
    }
}

public class CreatePlaylistState : LambdaState
{
    public static CreatePlaylistState Instance { get; } = new();

    public override async Task<State> Execute(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        try // чтобы бот не падал, если что-то не так с бд
        {
            var favouriteTracks = user.ApiWrapper!.GetFavouriteTracks();
            var chosenTracks = dbAccessor.FilterAndSaveNewInDb(favouriteTracks, user.MakeFilter()).ToList();

            if (chosenTracks.Count == 0)
            {
                await bot.SendMessage(user.ChatId,
                    "У вас не нашлось подходящих размеченных треков(\n" +
                    "Вы можете их разметить с помощью команды /mark");
                return MainMenuState.Instance;
            }

            var nonSelectedTracks = favouriteTracks.Except(chosenTracks).ToList();
            var unmarkedTracks = await GetUnmarkedTracks(nonSelectedTracks, dbAccessor);
            
            var message = "В плейлист вошли:\n\n" +
                          string.Join('\n', chosenTracks.Select(x => $"*> {x.Title}* - _{x.Artist}_"));
            await bot.SendMessage(user.ChatId, message, ParseMode.Markdown);

            user.UnmarkedTracks = dbAccessor.FetchAndAddIfNecessary(unmarkedTracks).ToList();
            user.ChosenTracks = chosenTracks;

            if (unmarkedTracks.Count > 0)
                return MarkOrContinueState.Instance;
            
            return FinishCreatingPlaylist.Instance;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await bot.SendMessage(user.ChatId,
                "Сформировать плейлист не удалось, из за внутренней ошибки, попробуйте позже");
            return MainMenuState.Instance;
        }
    }


    private async Task<List<VkNet.Model.Attachments.Audio>> GetUnmarkedTracks(
        List<VkNet.Model.Attachments.Audio> nonSelectedTracks,
        IDbAccessor dbAccessor
    )
    {
        var unmarkedTracks = new List<VkNet.Model.Attachments.Audio>();

        foreach (var track in nonSelectedTracks)
        {
            var dbTrack = dbAccessor.TryGetAudioFromBd(track);
            var votes = dbTrack.Votes;

            if (votes.Count == 0)
                unmarkedTracks.Add(track);
        }

        return unmarkedTracks;
    }
}

public class MarkOrContinueState : InputHandlingState
{
    public static MarkOrContinueState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        var commands = new ReplyKeyboardMarkup(true).AddButton("/mark_unmarked").AddButton("/continue");

        var message = $"У вас обнаружено {user.UnmarkedTracks.Count} не размеченных треков." +
                      $" Вы можете их разметить: /mark_unmarked, или продолжить создание плейлиста: /continue";

        await bot.SendMessage(user.ChatId, message, replyMarkup: commands);
    }

    public override async Task<State?> OnMessage(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user,
        Message message)
    {
        switch (message.Text)
        {
            case "/continue":
                return FinishCreatingPlaylist.Instance;
            case "/mark_unmarked":
                user.IsMarkingUnmarked = true;
                return BeginMarkState.Instance;
            default:
                throw new IncorrectMessageException(message.Text ?? "[null]", "/continue, /mark_unmarked");
        }
    }
}

public class FinishCreatingPlaylist : LambdaState
{
    public static FinishCreatingPlaylist Instance { get; } = new();

    public override async Task<State> Execute(TelegramBotClient bot, IDbAccessor dbAccessor, TgUser user)
    {
        var description = "Плейлист создан по следующим жанрам и настроениям:\nЖанры:";
    
        foreach (var genre in user.SelectedGenres)
        {
            var genreName = genre.Name.ToString();
            var temp = genre.Name;
            description += genreName + "; ";
        }

        description = description.Remove(description.Length - 2);
        description += "\nНастроения:";

        foreach (var mood in user.SelectedMoods)
        {
            var moodName = mood.Name.ToString();
            var temp = mood.Name;
            description += moodName + "; ";
        }
        description = description.Remove(description.Length - 2);
        //$"Жанры:{user.SelectedGenres.Select(genre => $"{genre.Name.ToString()}; ")}\n" +
        //$"Настроения:{user.SelectedMoods.Select(mood => $"{mood.Name.ToString()}; ")}";

        user.ApiWrapper!.CreatePlaylist("Избранные треки created by Moody", user.ChosenTracks, description);
        await bot.SendMessage(user.ChatId, "Плейлист готов!");
        user.ResetMoodsAndGenres();

        return new MainMenuState();
    }
}