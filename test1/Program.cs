using Microsoft.Extensions.DependencyInjection;
using System;
using System.Text;
using System.Text.RegularExpressions;
using VkNet;
using VkNet.AudioBypassService.Extensions;
using VkNet.Enums.Filters;
using VkNet.Model;
using Database;
using Database.db_models;
using System.Data.SQLite;
using NUnit.Framework;

[TestFixture]
class Program
{
    [Test] // переоформлено в тест, чтобы можно было иметь несколько точек входа
    public void Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddAudioBypass();
        var api = new VkApi(services);
        var dbAccessor = new DbAccessor();
        //api.Authorize(new ApiAuthParams
        //{
        //    ApplicationId = 52614150,
        //    AccessToken = "",
        //    Settings = Settings.Audio
        //});
        Console.WriteLine("Введите логин");
        var login = Console.ReadLine();
        Console.WriteLine("Введите пароль");
        var password = Console.ReadLine();
        Console.WriteLine("Есть ли двух факторка? [Y]|[N]");
        var response = Console.ReadLine();
        var flag = false;
        if (response == "Y")
            flag = true;

        if (flag)
            api.Authorize(new ApiAuthParams
            {
                ApplicationId = 52614150,
                Login = login,
                Password = password,
                TwoFactorAuthorization = new Func<string>(() =>
                {
                    Console.WriteLine("Введите код 2FA:");
                    var code = Console.ReadLine();
                    return code;
                }),
                Settings = Settings.All
            });
        else
            api.Authorize(new ApiAuthParams
            {
                ApplicationId = 52614150,
                Login = login,
                Password = password,
                Settings = Settings.All
            });

        var id = api.UserId;
        using var connection = new SQLiteConnection(dbAccessor.ConnectionString);
        connection.Open();
        var musicList = api.Audio.Get(new VkNet.Model.RequestParams.AudioGetParams { OwnerId = id });
        foreach (var music in musicList)
        {
            Console.WriteLine($"{music.Artist} - {music.Title}");
            Console.WriteLine("введите настроение трека (введи stop для остановки)");
            var moodStr = Console.ReadLine();
            if (moodStr == "stop")
                break;
            //TO DO метод получения id жанра, настроения
            //var moodId = GetMoodId()
            //var mood = new Mood(moodId, moodStr, "");
            Console.WriteLine("введите жанр");
            var genreStr = Console.ReadLine();
            //TO DO метод получения id жанра, настроения
            //var genreId = GetMoodId()
            //var genre = new Mood(genreId, genreStr, "");

            //dbAccessor.SaveAudioInDb(music, connection, mood, genre);
        }


        //Console.WriteLine(id);
        ////var song = "318348717_456239095".Split();
        ////api.Audio.CreatePlaylist(318348717, "СЮДАА", "", song);

        //var songToPlayList = new StringBuilder();
        //for (int i = 0; i < 3; i++)
        //{
        //    songToPlayList.Append($"{id}_{musicList[i].Id},");
        //}
        //songToPlayList.Length--;

        //var songsInPlayList = songToPlayList.ToString().Split(',');
        //var playlist = api.Audio.CreatePlaylist((long)id, "Тесты2");
        //foreach (var song in songsInPlayList)
        //{
        //    api.Audio.AddToPlaylist((long)id, (long)playlist.Id, song.Split());
        //}

        //var asd = api.Gifts.Get();
        //var x = api.Audio.Get(a);
        //api.Audio.AddToPlaylist((long)u.OwnerId, (long)u.Id, );
    }
}