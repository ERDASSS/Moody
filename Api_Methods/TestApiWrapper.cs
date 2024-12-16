using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace ApiMethods;

/// <summary>
/// Тестовая обертка, чтобы не нужно было каждый раз логиниться в боте, подвергая свой акк риску блокировки
/// </summary>
public class TestApiWrapper : IVkApiWrapper
{
    private const string TracksPath = "test_tracks.json";

    public void AuthorizeWithout2FA(string login, string password)
    {
        Console.WriteLine("аутентификация \"прошла\"!");
    }
    
    public void AuthorizeWith2FA(string login, string password, string code)
    {
    }

    public VkCollection<Audio> GetFavouriteTracks()
    {
        var favTracksList = IterateFavouriteTracks().ToList();
        return new VkCollection<Audio>((ulong)favTracksList.Count, favTracksList);
    }

    private IEnumerable<Audio> IterateFavouriteTracks()
    {
        var json = File.ReadAllText(TracksPath);
        var jsonArray = JArray.Parse(json);

        foreach (var item in jsonArray)
        {
            var title = item["title"]!.ToString();
            var author = item["author"]!.ToString();
            var duration = item["duration"]!.ToString();
            var seconds = (int)TimeSpan.Parse(duration).TotalSeconds;
            yield return new Audio { Title = title, Artist = author, Duration = seconds };
        }
    }

    public AudioPlaylist CreatePlaylist(string playListName, IEnumerable<Audio> songList,
        string? description = null)
    {
        Console.WriteLine("плейлист \"создан\"!");
        return new AudioPlaylist { Title = playListName, Description = description };
    }

    // public AudioPlaylist CreateEmptyPlaylist(string playListName)
    // {
    //     Console.WriteLine("пустой плейлист \"создан\"!");
    //     return new AudioPlaylist { Title = playListName };
    // }
    //
    // public void AddTrackToPlaylist(Audio track, AudioPlaylist playlist)
    // {
    //     Console.WriteLine($"трек {track.Title} - {track.Artist} в плейлист {playlist.Title} \"добавлен\"!");
    // }
}

