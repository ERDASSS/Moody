using System.Collections.Generic;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace ApiMethods;

public interface IVkApiWrapper
{
    public void AuthorizeWithout2FA(string login, string password);
    
    public void AuthorizeWith2FA(string login, string password, string code);

    public VkCollection<Audio> GetFavouriteTracks();

    public AudioPlaylist CreatePlaylist(
        string playListName,
        IEnumerable<Audio> songList,
        string? description = null
    );

    // public AudioPlaylist CreateEmptyPlaylist(string playListName);
    //
    // public void AddTrackToPlaylist(Audio track, AudioPlaylist playlist);
}