using System.Collections.Generic;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace ApiMethods;

public interface IVkApiWrapper
{
    public void AuthorizeWithout2FA(string login, string password);
    
    public void AuthorizeWith2FA(string login, string password, string code);

    public IEnumerable<Audio> GetFavouriteTracks();

    public AudioPlaylist CreatePlaylist(
        string playListName,
        IEnumerable<Audio> songList,
        string? description = null
    );
}