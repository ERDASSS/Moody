using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using VkNet.AudioBypassService.Exceptions;
using ApiMethods;
using System.Net;
using System;
using System.Text.RegularExpressions;
using Database;
using VkNet.Model.Attachments;
using VkNet.Utils;
using Database.db_models;
using VkNet.Model;
using System.Data.Entity;
using System.Linq;

namespace TGBot;

public class TGBot
{
    public TGBot(string token, DbAccessor dbAccessor)
    {
        bot = new TelegramBotClient(token, cancellationToken: cts.Token);
        me = bot.GetMe().Result;
        // await bot.DeleteWebhook();
        // await bot.DropPendingUpdates();
        bot.OnError += OnError;
        bot.OnMessage += OnMessage;
        bot.OnUpdate += OnUpdate;
        this.dbAccessor = dbAccessor;
        Console.WriteLine($"{me.FirstName} запущен на @Moody_24_bot!");
    }

    private readonly TelegramBotClient bot;
    private readonly Telegram.Bot.Types.User me;
    private readonly CancellationTokenSource cts = new();
    private readonly Dictionary<long, Authorization> authorizations = new();
    private readonly Dictionary<long, VkUser> users = new();
    private readonly DbAccessor dbAccessor;

    private readonly ReplyKeyboardMarkup replyKeyboardStart = new(
        new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton("/start")
            }
        })
    {
        ResizeKeyboard = false
    };

    private readonly ReplyKeyboardMarkup replyKeyboardLogin = new(
        new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton("/login")
            }
        })
    {
        ResizeKeyboard = false
    };

    private readonly ReplyKeyboardMarkup replyKeyboardPlaylistAndMark = new(new List<KeyboardButton[]>
    {
        new[]
        {
            new KeyboardButton("/playlist"),
            new KeyboardButton("/mark")
        }
    })
    {
        ResizeKeyboard = false
    };

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

    private async Task OnError(Exception exception, HandleErrorSource source)
    {
        Console.WriteLine(exception);
        await Task.Delay(2000, cts.Token);
    }

    private async Task OnUpdate(Update update)
    {
        switch (update)
        {
            case { CallbackQuery: { } query }:
            {
                if (query.Data is null || query.Message is null) break;
                var chatId = query.Message.Chat.Id;
                if (!users.ContainsKey(chatId))
                    break;

                // todo: чет много дублирования на настроение и жанр
                if (!users[chatId].AreMoodsSelected && query.Data.EndsWith("Mood"))
                {
                    var mood = query.Data.Replace("Mood", "");
                    await bot.AnswerCallbackQuery(query.Id, $"Вы выбрали {mood}");
                    users[chatId].AddMood(users[chatId].SuggestedMoods[mood]);
                }
                else if (!users[chatId].AreGenresSelected && query.Data.EndsWith("Genre"))
                {
                    var genre = query.Data.Replace("Genre", "");
                    await bot.AnswerCallbackQuery(query.Id, $"Вы выбрали {genre}");
                    users[chatId].AddGenre(users[chatId].SuggestedGenres[genre]);
                }
                else if (query.Data.StartsWith("accept"))
                {
                    if (query.Data.EndsWith("Moods"))
                    {
                        await bot.AnswerCallbackQuery(query.Id, "Принято", showAlert: true);
                        users[chatId].AreMoodsSelected = true;
                    }
                    else if (query.Data.EndsWith("Genres"))
                    {
                        await bot.AnswerCallbackQuery(query.Id, "Принято", showAlert: true);
                        users[chatId].AreGenresSelected = true;
                    }

                    if (users[chatId].CurrentCommand == "/playlist")
                        await GetPlayList(chatId);

                    if (users[chatId].CurrentCommand == "/mark" || users[chatId].CurrentCommand == "/mark_unmarked")
                        await StartMarking(chatId);
                }

                break;
            }

            default:
                Console.WriteLine($"Не обрабатывается тип {update.Type}");
                break;
        }
    }

    private async Task OnMessage(Telegram.Bot.Types.Message msg, UpdateType type)
    {
        if (msg.Text is not { } text)
            Console.WriteLine($"Отправлено сообщение типа {msg.Type}");
        else if (text.StartsWith('/'))
        {
            var space = text.IndexOf(' ');
            if (space < 0) space = text.Length;
            var command = text[..space].ToLower();
            if (command.LastIndexOf('@') is > 0 and var at)
                if (command[(at + 1)..].Equals(me.Username, StringComparison.OrdinalIgnoreCase))
                    command = command[..at];
                else
                    return;
            await OnCommand(command, text[space..].TrimStart(), msg);
        }
        else
            await OnTextMessage(msg);
    }

    private async Task OnCommand(string command, string args, Telegram.Bot.Types.Message msg)
    {
        Console.WriteLine($"Обработка команды {command} {args}");

        if (users.ContainsKey(msg.Chat.Id))
        {
            users[msg.Chat.Id].CurrentCommand = command;
            users[msg.Chat.Id].SetUsername(msg.From.Username);
        }

        switch (command)
        {
            case "/start":
            {
                await SendStartMessage(msg.Chat.Id);
                break;
            }
            case "/login":
            {
                await StartAuthorization(msg.Chat.Id);
                break;
            }
            case "/playlist":
            {
                await GetPlayList(msg.Chat.Id);
                break;
            }
            case "/mark":
            {
                await MarkTracks(msg.Chat.Id);
                break;
            }
            case "/continue":
            {
                await ContinueCreatingPlaylist(msg.Chat.Id);
                break;
            }
            case "/mark_unmarked":
            {
                users[msg.Chat.Id].IsMarkingUnmarked = true;
                await MarkTracks(msg.Chat.Id);
                break;
            }
        }
    }

    private async Task OnTextMessage(Telegram.Bot.Types.Message msg)
    {
        if (authorizations.ContainsKey(msg.Chat.Id))
            await ProcessAuthorizations(msg);
    }

    private async Task SendStartMessage(long chatId)
    {
        await bot.SendMessage(chatId,
            "Добро пожаловать! Пожалуйста, залогиньтесь во ВКонтакте. Для этого введите команду /login");
        await bot.SendMessage(chatId,
            "Внимание! Бот не собирает ваши личные данные. Производится только только авторизация",
            replyMarkup: replyKeyboardLogin);
    }

    // TODO: выделить авторизацию и выбор треков в 2 разных класса
    // TODO: потом все другие "сценарии" работы (например разметка треков) тоже в отдельные классы

    private async Task AuthorizeWithToken(long chatId)
    {
        authorizations[chatId] = new Authorization();
        await bot.SendMessage(chatId, "Тестовый режим входа по токену");
        try
        {
            users[chatId].VkApi.AuthorizeWithToken();
        }
        catch (VkAuthException exception)
        {
            Console.WriteLine(exception);
            return;
        }

        await ConfirmAuthorization(chatId, true);
    }

    private async Task StartAuthorization(long chatId)
    {
        authorizations[chatId] = new Authorization();
        authorizations[chatId].SetCorrectData(true);
        await bot.SendMessage(chatId, "Введите логин (номер телефона или почта)", replyMarkup: replyKeyboardLogin);
    }

    private async Task ProcessAuthorizations(Telegram.Bot.Types.Message message)
    {
        if (!authorizations.TryGetValue(message.Chat.Id, out var authorization) || message.Text is null)
            return;

        if (authorization.Login is null)
        {
            if (!(Regex.IsMatch(message.Text, @"^[^@\s]+@[^@\s]+\.[^@\s]+$") ||
                  Regex.IsMatch(message.Text, @"^\+?[1-9]\d{8,14}$")))
            {
                await bot.SendMessage(message.Chat.Id,
                    "Неверный формат логина. Пожалуйста, введите корректный логин (почта или номер телефона).");
                return;
            }

            authorization.AddLogin(message.Text);
            await bot.SendMessage(message.Chat.Id, "Введите пароль");
            return;
        }

        if (authorization.Password is null)
        {
            authorization.AddPassword(message.Text);
            await bot.SendMessage(message.Chat.Id, "Попытка авторизации");
            var wasAuthorizationSuccessful = await TryAuthorizeWithout2FA(message.Chat.Id);
            if (!wasAuthorizationSuccessful && authorization.IsCorrectData == true)
                await bot.SendMessage(message.Chat.Id, "Введите код двухфакторной авторизации:");
            return;
        }

        authorization.AddCode(message.Text);
        await TryAuthorizeWith2FA(message.Chat.Id);
    }

    private async Task ConfirmAuthorization(long chatId, bool wasAuthorizationSuccessful)
    {
        if (!authorizations.TryGetValue(chatId, out _))
        {
            Console.WriteLine("Нет такого chatId");
            return;
        }

        if (wasAuthorizationSuccessful)
        {
            authorizations.Remove(chatId);
            await bot.SendMessage(chatId, "Авторизация прошла успешно", replyMarkup: replyKeyboardPlaylistAndMark);
        }
        else
        {
            await bot.SendMessage(chatId, "Неправильный логин или пароль, попробуйте еще раз",
                replyMarkup: replyKeyboardLogin);
            await StartAuthorization(chatId);
        }
    }

    private async Task<bool> TryAuthorizeWithout2FA(long chatId)
    {
        if (!authorizations.TryGetValue(chatId, out var authorization)
            || authorization.Login is null
            || authorization.Password is null)
            return false;

        var vkApi = new VkApiWrapper();
        try
        {
            vkApi.AuthorizeWithout2FA(authorization.Login, authorization.Password);
        }
        catch (VkAuthException exception)
        {
            Console.WriteLine(exception);
            switch (exception.Message)
            {
                case "Неправильный логин или пароль":
                    authorization.SetCorrectData(false);
                    await ConfirmAuthorization(chatId, false);
                    break;

                case
                    "Произведено слишком много попыток входа в этот аккаунт по паролю. Воспользуйтесь другим способом входа или попробуйте через несколько часов."
                    :
                    await bot.SendMessage(chatId,
                        "Произведено слишком много попыток входа в этот аккаунт по паролю. Попробуйте через несколько часов.");
                    authorization.SetCorrectData(false);
                    break;
            }

            return false;
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine(exception);
            return false;
        }

        users[chatId] = new(vkApi);
        await ConfirmAuthorization(chatId, true);
        return true;
    }

    private async Task<bool> TryAuthorizeWith2FA(long chatId)
    {
        if (!authorizations.TryGetValue(chatId, out var authorization)
            || authorization.Login is null
            || authorization.Password is null
            || authorization.Code is null)
            return false;

        var vkApi = new VkApiWrapper();
        try
        {
            vkApi.AuthorizeWith2FA(authorization.Login, authorization.Password, authorization.Code);
        }
        catch (VkAuthException exception)
        {
            Console.WriteLine(exception);
            switch (exception.Message)
            {
                case "Неправильный логин или пароль":
                    authorization.SetCorrectData(false);
                    await ConfirmAuthorization(chatId, false);
                    break;

                case
                    "Произведено слишком много попыток входа в этот аккаунт по паролю. Воспользуйтесь другим способом входа или попробуйте через несколько часов."
                    :
                    await bot.SendMessage(chatId, exception.Message);
                    authorization.SetCorrectData(false);
                    break;

                case "Вы ввели неверный код":
                    await bot.SendMessage(chatId, "Введен неверный код двухфакторной авторизации. Попробуйте еще раз");
                    await bot.SendMessage(chatId, "Введите код двухфакторной авторизации:");
                    break;
            }

            return false;
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine(exception);
            return false;
        }

        users[chatId] = new(vkApi);
        await ConfirmAuthorization(chatId, true);
        return true;
    }

    private async Task GetPlayList(long chatId)
    {
        if (!users.TryGetValue(chatId, out var user))
        {
            await StartAuthorization(chatId);
            return;
        }

        if (!user.AreMoodsSelected)
        {
            users[chatId].SuggestedMoods = dbAccessor.GetMoods().ToDictionary(m => m.Name, m => m);
            await bot.SendMessage(chatId, "Выберите настроение",
                replyMarkup: users[chatId].SuggestedMoods.ToInlineKeyboardMarkup());
            return;
        }

        if (!user.AreGenresSelected)
        {
            users[chatId].SuggestedGenres = dbAccessor.GetGenres().ToDictionary(g => g.Name, g => g);
            await bot.SendMessage(chatId, "Выберите жанры",
                replyMarkup: users[chatId].SuggestedGenres.ToInlineKeyboardMarkup());
            return;
        }

        // await bot.SendMessage(chatId, "Пока только ваши треки");
        //var favouriteTracks = users[chatId].VkApi.GetFavouriteTracks();
        //var filter = users[chatId].GetFilter();
        //var filteredTracks = dbAccessor.FilterAndSaveNewInDb(favouriteTracks, filter);
        // var tracks = string.Join('\n', users[chatId].VkApi.GetFavoriteTracks().Select(x => x.Title));
        CreatePlaylist(chatId);
        //Console.WriteLine(tracks);
        // var tracksList
        user.ResetMoodsAndGenres();
    }

    private async void CreatePlaylist(long chatId)
    {
        try // чтобы бот не падал, если что-то не так с бд
        {
            var favouriteTracks = users[chatId].VkApi.GetFavouriteTracks();
            var chosenTracks = dbAccessor.FilterAndSaveNewInDb(favouriteTracks, users[chatId].GetFilter()).ToList();

            if (chosenTracks.Count == 0)
            {
                await bot.SendMessage(chatId, "У вас не нашлось подходящих размеченных треков(. Вы можете их разметить с помощью команды /mark");
                return;
            }

            var nonSelectedTracks = favouriteTracks.Except(chosenTracks).ToList();
            var unmarkedTracks = await GetUnmarkedTracks(nonSelectedTracks);

            // todo: выравнивать список по колонкам
            var message = "В плейлист вошли:\n" + string.Join('\n', chosenTracks.Select(x => $"> {x.Title} - {x.Artist}"));
            await bot.SendMessage(chatId, message);

            users[chatId].UnmarkedTracks = unmarkedTracks;
            users[chatId].ChosenTracks = chosenTracks;

            if (unmarkedTracks.Count > 0)
            {
                message = $"У вас обнаружено {unmarkedTracks.Count} не размеченных треков." +
                    $" Вы можете их разметить: /mark_unmarked, или продолжить создание плейлиста: /continue";
                await bot.SendMessage(chatId, message, replyMarkup: replyKeyboardUnmarkedContinue);
                return;
            }

            await ContinueCreatingPlaylist(chatId);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            await bot.SendMessage(chatId,
                "Сформировать плейлист не удалось, из за внутренней ошибки, попробуйте позже");
        }
    }

    private async Task<List<VkNet.Model.Attachments.Audio>> GetUnmarkedTracks(List<VkNet.Model.Attachments.Audio> nonSelectedTracks)
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

    private async Task ContinueCreatingPlaylist(long chatId)
    {
        users[chatId].VkApi.CreatePlaylist("Избранные треки created by Moody", "", users[chatId].ChosenTracks);
        await bot.SendMessage(chatId, "Плейлист готов!", replyMarkup: replyKeyboardPlaylistAndMark);
    }
    

    private async Task StartMarking(long chatId)
    {
        var dbAudio = dbAccessor.TryGetAudioFromBd(users[chatId].CurrentTrack);

        if (dbAudio == null)
        {
            dbAccessor.SaveAudioInDb(users[chatId].CurrentTrack);
            dbAudio = dbAccessor.TryGetAudioFromBd(users[chatId].CurrentTrack);
        }

        if (!users[chatId].AreMoodsSelected)
        {
            await ShowTrackInfo(chatId, dbAudio);

            users[chatId].SuggestedMoods = dbAccessor.GetMoods().ToDictionary(m => m.Name, m => m);
            await bot.SendMessage(chatId, "Выберите настроение для трека",
                replyMarkup: users[chatId].SuggestedMoods.ToInlineKeyboardMarkup());
            return;
        }

        if (!users[chatId].AreGenresSelected)
        {
            users[chatId].SuggestedGenres = dbAccessor.GetGenres().ToDictionary(g => g.Name, g => g);
            await bot.SendMessage(chatId, "Выберите жанр для трека",
                replyMarkup: users[chatId].SuggestedGenres.ToInlineKeyboardMarkup());
            return;
        }

        foreach (var mood in users[chatId].SelectedMoods)
            dbAccessor.AddVote(dbAudio.DbAudioId, mood.Id, VoteValue.Confirmation, users[chatId].DbUser.Id);

        foreach (var genre in users[chatId].SelectedGenres)
            dbAccessor.AddVote(dbAudio.DbAudioId, genre.Id, VoteValue.Confirmation, users[chatId].DbUser.Id);


        users[chatId].ResetMoodsAndGenres();
        users[chatId].CurrentTrack = users[chatId].UnmarkedTracks.Skip(users[chatId].CurrentSkip).FirstOrDefault();
        users[chatId].CurrentSkip++;

        if (users[chatId].CurrentTrack == null || users[chatId].CurrentTrack == default)
        {
            await bot.SendMessage(chatId, "Разметка окончена. Вы можете создать плейлист с помощью команды /playlist", replyMarkup: replyKeyboardPlaylistAndMark);
            users[chatId].IsMarkingUnmarked = false;
            return;
        }

        users[chatId].UnmarkedTracks.Skip(1);
        await StartMarking(chatId);
    }

    private async Task ShowTrackInfo(long chatId, DbAudio dbAudio)
    {
        await bot.SendMessage(chatId, $"Укажите жанр и настроение для: " +
                                          $"{users[chatId].CurrentTrack.Artist} - {users[chatId].CurrentTrack.Title}");
        if (dbAudio.Votes != null && !users[chatId].IsMarkingUnmarked)
        {
            var votes = dbAudio.GetVotesStatistics();
            await bot.SendMessage(chatId, "Текущая разметка трека:");
            await bot.SendMessage(chatId, "Настроения:");
            await ShowTrackStat(chatId, 1, votes);
            await bot.SendMessage(chatId, "Жанры:");
            await ShowTrackStat(chatId, 2, votes);
        }
    }

    private async Task ShowTrackStat(long chatId, int parameterId, Dictionary<DbAudioParameterValue, Dictionary<VoteValue, int>> votes)
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

    private async Task MarkTracks(long chatId)
    {
        if (!users.ContainsKey(chatId))
        {
            await StartAuthorization(chatId);
            return;
        }

        dbAccessor.AddOrUpdateUser(chatId, users[chatId].Username);
        users[chatId].DbUser = dbAccessor.GetUserByChatId(chatId);
        if (!users[chatId].IsMarkingUnmarked)
            users[chatId].UnmarkedTracks = users[chatId].VkApi.GetFavouriteTracks().ToList();
        users[chatId].CurrentTrack = users[chatId].UnmarkedTracks.FirstOrDefault();
        users[chatId].CurrentSkip = 1;

        await StartMarking(chatId);
    }
}