using Microsoft.Extensions.DependencyInjection;
using System.Text;
using VkNet;
using VkNet.AudioBypassService.Extensions;
using VkNet.Enums.Filters;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace ApiMethods;

public class VkApiWrapper : IVkApiWrapper
{
    private VkApi vkApi;
    private const int applicationId = 52614150;

    public VkApiWrapper()
    {
        var services = new ServiceCollection();
        services.AddAudioBypass();
        vkApi = new VkApi(services);
    }

    public void AuthorizeWith2FA(string login, string password, string code)
    {
        vkApi.Authorize(new ApiAuthParams
        {
            ApplicationId = applicationId,
            Login = login,
            Password = password,
            TwoFactorAuthorization = new Func<string>(() => code),
            Settings = Settings.Audio
        });
    }

    public void AuthorizeWithToken()
    {
        vkApi.Authorize(new ApiAuthParams
        {
            AccessToken = ""
        });
    }

    public void AuthorizeWithout2FA(string login, string password)
    {
        vkApi.Authorize(new ApiAuthParams
        {
            ApplicationId = applicationId,
            Login = login,
            Password = password,
            Settings = Settings.Audio
        });
    }

    private long GetUserId() => (long)vkApi.UserId;

    public VkCollection<Audio> GetFavouriteTracks()
        => vkApi.Audio.Get(new VkNet.Model.RequestParams.AudioGetParams { OwnerId = GetUserId() });

    public AudioPlaylist CreatePlaylist(string playListName,
        IEnumerable<Audio> songList,
        string? description = null)
    {
        var songListInVkFormat = CreateSongListVkFormat(songList);
        var playlist = vkApi.Audio.CreatePlaylist(GetUserId(), playListName, description, songListInVkFormat);

        //if (songListInVkFormat != null)
        //{
        //    foreach (var song in songListInVkFormat)
        //        vkApi.Audio.AddToPlaylist(GetUserId(), (long)playlist.Id, song.Split());
        //}

        return playlist;
    }

    public AudioPlaylist CreateEmptyPlaylist(string playListName)
    {
        var playlist = vkApi.Audio.CreatePlaylist(GetUserId(), playListName);
        return playlist;
    }

    public void AddTrackToPlaylist(Audio track, AudioPlaylist playlist)
    {
        var trackInVkFormat = $"{GetUserId()}_{track.Id}".Split();
        vkApi.Audio.AddToPlaylist(GetUserId(), (long)playlist.Id, trackInVkFormat);
    }

    private IEnumerable<string> CreateSongListVkFormat(IEnumerable<Audio> songCollection)
    {
        foreach (var song in songCollection)
            yield return $"{GetUserId()}_{song.Id},";
    }
}