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
    private readonly User me;
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

    private readonly ReplyKeyboardMarkup replyKeyboardPlaylist = new(
        new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton("/playlist")
            }
        })
    {
        ResizeKeyboard = false
    };

    // private readonly InlineKeyboardMarkup inlineMoods = MoodExtensions.CreateInlineKeyboardMarkup();
    // private readonly InlineKeyboardMarkup inlineGenres = GenreExtensions.CreateInlineKeyboardMarkup();

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
                
                
                if (!users[chatId].AreMoodsSelected && query.Data.EndsWith("Mood"))
                {
                    var mood = query.Data.Replace("Mood", "");
                    await bot.AnswerCallbackQuery(query.Id, $"Вы выбрали {mood}");
                    users[chatId].ParseParameter($"Mood:");
                    // TODO: добавить парсинг
                    // users[chatId].AddMood(mood.MoodParse());
                }
                else if (!users[chatId].AreGenresSelected && query.Data.EndsWith("Genre"))
                {
                    var genre = query.Data.Replace("Genre", "");
                    await bot.AnswerCallbackQuery(query.Id, $"Вы выбрали {genre}");
                    users[chatId].ParseParameter($"Genre:");
                    // TODO: добавить парсинг
                    // users[chatId].AddGenre(genre.GenreParse());
                }
                else if (query.Data.StartsWith("accept"))
                {
                    if (!users[chatId].AreMoodsSelected && query.Data.EndsWith("Moods"))
                    {
                        await bot.AnswerCallbackQuery(query.Id, "Принято", showAlert: true);
                        users[chatId].AreMoodsSelected = true;
                    }
                    else if (!users[chatId].AreGenresSelected && query.Data.EndsWith("Genres"))
                    {
                        await bot.AnswerCallbackQuery(query.Id, "Принято", showAlert: true);
                        users[chatId].AreGenresSelected = true;
                    }
                    else break;

                    await GetPlayList(chatId);
                }

                break;
            }

            default:
                Console.WriteLine($"Не обрабатывается тип {update.Type}");
                break;
        }
    }

    private async Task OnMessage(Message msg, UpdateType type)
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

    private async Task OnCommand(string command, string args, Message msg)
    {
        Console.WriteLine($"Обработка команды {command} {args}");
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
                Console.WriteLine(msg.Chat.Id);
                await GetPlayList(msg.Chat.Id);
                break;
            }
        }
    }

    private async Task OnTextMessage(Message msg)
    {
        if (authorizations.ContainsKey(msg.Chat.Id))
            await ProcessAuthorizations(msg);
    }

    private async Task SendStartMessage(long chatId)
    {
        await bot.SendMessage(chatId, "Добро пожаловать! Пожалуйста, залогиньтесь.", replyMarkup: replyKeyboardLogin);
    }

    private async Task StartAuthorization(long chatId)
    {
        authorizations[chatId] = new Authorization();
        authorizations[chatId].SetCorrectData(true);
        await bot.SendMessage(chatId, "Введите логин", replyMarkup: replyKeyboardLogin);
    }

    private async Task ProcessAuthorizations(Message message)
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
            await bot.SendMessage(message.Chat.Id, "Производится авторизация");
            var wasAuthorizationSuccessful = await TryAuthorizeWithout2FA(message.Chat.Id);
            if (!wasAuthorizationSuccessful && authorization.IsCorrectData == true)
                await bot.SendMessage(message.Chat.Id, "Введите код 2FA:");
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
            await bot.SendMessage(chatId, "Авторизация прошла успешно", replyMarkup: replyKeyboardPlaylist);
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
                    await bot.SendMessage(chatId, "Введите код 2FA:");
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
            await bot.SendMessage(chatId, "Выберите настроение",
                replyMarkup: dbAccessor.GetMoods().ToInlineKeyboardMarkup());
            return;
        }

        if (!user.AreGenresSelected)
        {
            await bot.SendMessage(chatId, "Выберите жанры",
                replyMarkup: dbAccessor.GetGenres().ToInlineKeyboardMarkup());
            return;
        }

        //await bot.SendMessage(chatId, "Пока только ваши треки");
        // TODO: обработка плейлиста
        //var tracks = string.Join('\n', users[chatId].VkApi.GetFavoriteTracks().Select(x => x.Title));
        //CreatePlaylist(chatId);

        //await bot.SendMessage(chatId, tracks);

        //Console.WriteLine(tracks);
        // var tracksList
        user.ResetMoodsAndGenres();
    }

    private async void CreatePlaylist(long chatId)
    {
        var favouriteTracks = users[chatId].VkApi.GetFavoriteTracks();
        var choosedTracks = dbAccessor.FilterAndSaveNewInDb(favouriteTracks, users[chatId].Filter);
        var playlist = users[chatId].VkApi.CreatePlaylist("Избранные треки created by Moody", "",  choosedTracks);
        

        //foreach (var track in tracksList)
        //{
        //    if (!DataBase.HasTrackInDataBase(track))
        //    {
        //        await bot.SendMessage(chatId, $"{track.Title} - {track.Artist} данный трек не найден в базе данных." +
        //                                      $" Для продолжения работы необходимо указать настроение и жанр данного трека");
        //        await bot.SendMessage(chatId, "Выберите настроение", replyMarkup: dbAccessor.GetMoods().ToInlineKeyboardMarkup());
        //        await bot.SendMessage(chatId, "Выберите жанр", replyMarkup: dbAccessor.GetGenres().ToInlineKeyboardMarkup());
        //        //DataBase.AddTrackToDataBase(track, genre, mood);
        //    }

        //    if (DataBase.IsRightTrack(track))
        //        users[chatId].VkApi.AddTrackToPlaylist(track, playlist);
        //}
    }
}