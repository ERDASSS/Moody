using Database;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TGBot.States;

// TODO: много дублирования
class SelectMoodsState : InputHandlingState
{
    public SelectMoodsState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        user.SuggestedMoods = dbAccessor.GetMoods().ToDictionary(m => m.Name, m => m);
        await bot.SendMessage(user.ChatId, "Выберите настроения",
            replyMarkup: user.SuggestedMoods.ToInlineKeyboardMarkup());
    }

    public override async Task<State?> OnCallback(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user,
        CallbackQuery callback)
    {
        if (callback.Data is null)
            throw new IncorrectCallbackException("[null]");
        if (callback.Data.EndsWith("Mood"))
        {
            var mood = callback.Data.Replace("Mood", "");
            await bot.AnswerCallbackQuery(callback.Id, $"Вы выбрали {mood}");
            user.SelectMood(user.SuggestedMoods[mood]);
            return null;
        }

        if (callback.Data.StartsWith("accept") && callback.Data.EndsWith("Moods"))
        {
            await bot.AnswerCallbackQuery(callback.Id, "Принято", showAlert: true);
            return SelectGenreState.Instance;
        }

        throw new IncorrectCallbackException(callback.Data, ".*Mood | acceptMoods");
    }
}

class SelectGenreState : InputHandlingState
{
    public static SelectGenreState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        user.SuggestedGenres = dbAccessor.GetGenres().ToDictionary(g => g.Name, g => g);
        await bot.SendMessage(user.ChatId, "Выберите жанры",
            replyMarkup: user.SuggestedGenres.ToInlineKeyboardMarkup());
    }

    public override async Task<State?> OnCallback(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user,
        CallbackQuery callback)
    {
        if (callback.Data is null)
            throw new IncorrectCallbackException("[null]");
        if (callback.Data.EndsWith("Genre"))
        {
            var genre = callback.Data.Replace("Genre", "");
            await bot.AnswerCallbackQuery(callback.Id, $"Вы выбрали {genre}");
            user.SelectGenre(user.SuggestedGenres[genre]);
            return null;
        }

        if (callback.Data.StartsWith("accept") && callback.Data.EndsWith("Genres"))
        {
            await bot.AnswerCallbackQuery(callback.Id, "Принято", showAlert: true);
            return CreatePlaylistState.Instance;
        }

        throw new IncorrectCallbackException(callback.Data, ".*Genre | acceptGenres");
    }
}

public class CreatePlaylistState : LambdaState
{
    public static CreatePlaylistState Instance { get; } = new();

    public override async Task<State> Execute(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
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

            // todo: выравнивать список по колонкам
            var message = "В плейлист вошли:\n" +
                          string.Join('\n', chosenTracks.Select(x => $"> {x.Title} - {x.Artist}"));
            await bot.SendMessage(user.ChatId, message);

            user.UnmarkedTracks = unmarkedTracks;
            user.ChosenTracks = chosenTracks;

            if (unmarkedTracks.Count > 0)
            {
                return MarkOrContinueState.Instance;
            }

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
        DbAccessor dbAccessor
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
    private readonly ReplyKeyboardMarkup replyKeyboardUnmarkedContinue = new(new List<KeyboardButton[]>
    {
        new[]
        {
            new KeyboardButton("/continue"),
            new KeyboardButton("/mark_unmarked")
        }
    })
    {
        ResizeKeyboard = false
    };

    public static MarkOrContinueState Instance { get; } = new();

    public override async Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        var message = $"У вас обнаружено {user.UnmarkedTracks.Count} не размеченных треков." +
                      $" Вы можете их разметить: /mark_unmarked, или продолжить создание плейлиста: /continue";
        await bot.SendMessage(user.ChatId, message, replyMarkup: replyKeyboardUnmarkedContinue);
    }

    public override async Task<State?> OnMessage(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user,
        Message message)
    {
        switch (message.Text)
        {
            case "/continue":
                return FinishCreatingPlaylist.Instance;
                break;
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

    public override async Task<State> Execute(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        user.ApiWrapper!.CreatePlaylist("Избранные треки created by Moody", user.ChosenTracks, "");
        await bot.SendMessage(user.ChatId, "Плейлист готов!");
        user.ResetMoodsAndGenres();
        return new MainMenuState();
    }
}