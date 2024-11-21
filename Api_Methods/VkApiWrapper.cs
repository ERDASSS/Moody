using Microsoft.Extensions.DependencyInjection;
using System.Text;
using VkNet;
using VkNet.AudioBypassService.Extensions;
using VkNet.Enums.Filters;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace ApiMethods
{
    public class VkApiWrapper
    {
        private VkApi vkApi;
        private const int applicationId = 52614150;

        public VkApiWrapper()
        {
            var services = new ServiceCollection();
            services.AddAudioBypass();
            vkApi = new VkApi(services);
        }

        public void AuthorizeWith2FA (string login, string password)
        {
            vkApi.Authorize(new ApiAuthParams
            {
                ApplicationId = applicationId,
                Login = login,
                Password = password,
                TwoFactorAuthorization = new Func<string>(() =>
                {
                    //Пока не знаю как будет работать бот
                    //TODO исправить это недразумение 
                    Console.WriteLine("Введите код 2FA:");
                    var code = Console.ReadLine();
                    return code;
                }),
                Settings = Settings.Audio
            });
        }
        
        public void AuthorizeWith2FA (string login, string password, string code)
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

        public long GetUserId() => (long)vkApi.UserId;

        public VkCollection<Audio> GetFavoriteTracks()
            => vkApi.Audio.Get(new VkNet.Model.RequestParams.AudioGetParams { OwnerId = GetUserId() });

        
        public AudioPlaylist CreatePlaylist(string playListName, VkCollection<Audio> songList = null)
        {
            var playlist = vkApi.Audio.CreatePlaylist(GetUserId(), playListName);
            var songListInVkFormat = CreateSongListVkFormat(songList);

            if (songListInVkFormat != null)
            {
                foreach (var song in songListInVkFormat)
                    vkApi.Audio.AddToPlaylist(GetUserId(), (long)playlist.Id, song.Split());
            }

            return playlist;
        }

        private string[] CreateSongListVkFormat(VkCollection<Audio> songCollection)
        {
            var songToVkFormat = new StringBuilder();

            foreach (var song in songCollection)
                songToVkFormat.Append($"{GetUserId()}_{song.Id},");

            songToVkFormat.Length--;

            return songToVkFormat.ToString().Split(',');
        }

    }
}
