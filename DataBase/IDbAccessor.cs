using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using Database.db_models;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace Database;

// TODO: оказывается юзернейм есть не у всех пользователей тг
// todo: добавить голосам временную метку
public interface IDbAccessor
{
    public string DbPath { get; }
    public string ConnectionString => $"Data Source={DbPath};Version=3;";
    public SQLiteConnection Connection { get; set; }
    
    /// <summary>
    /// Достает из бд все искомые треки, а если их там нет, то сначала добавляет, а потом достает
    /// </summary>
    public IEnumerable<FullInfoAboutTrack> FetchAndAddIfNecessary(IEnumerable<Audio> targetTracks);
    public IEnumerable<Audio> FilterAndSaveNewInDb(IEnumerable<Audio> usersFavouriteAudios, Filter filter);
    public List<DbMood> GetMoods();
    public List<DbGenre> GetGenres();
    public void SaveAudioInDb(Audio vkAudio);
    public DbAudio? TryGetAudioFromBd(Audio vkAudio);
    public void AddVote(int audioId, int parameterValueId, VoteValue voteValue, int userId);
    public void AddOrUpdateUser(long chatId, string? username);
    public DbUser? GetUserByChatId(long chatId);

    public DbUser? GetUserByUsername(string username);
}