using ApiMethods;
using Database;
using Database.db_models;
using VkNet.Model.Attachments;

namespace TGBot;

public class TgUser(long chatId, string? tgUsername)
{
    public long ChatId { get; } = chatId;
    public string? TgUsername { get; set; } = tgUsername;

    // вк и авторизация
    public Authorization Authorization { get; } = new Authorization();
    public IVkApiWrapper? ApiWrapper { get; set; } = null;

    // настройки плейлиста
    public Dictionary<string, DbMood> SuggestedMoods { get; set; } = new(); // DbMood по DbMood.Name
    public Dictionary<string, DbGenre> SuggestedGenres { get; set; } = new(); // DbGenre по DbGenre.Name
    public HashSet<DbMood> SelectedMoods { get; } = new();
    public HashSet<DbGenre> SelectedGenres { get; } = new();

    // это чтобы иметь возможность менять тип контейнера с настроениями, не меняя интерфейс
    public void SelectMood(DbMood mood) => SelectedMoods.Add(mood);
    public void SelectGenre(DbGenre genre) => SelectedGenres.Add(genre);

    // бд
    public DbUser? DbUser { get; set; } = null;

    // разметка
    public bool IsMarkingUnmarked { get; set; } = false;
    public Audio? CurrentTrack { get; set; }
    public DbAudio? CurrentDbTrack { get; set; }
    public bool HasGenresVotes { get; set; }
    public List<Audio> UnmarkedTracks { get; set; }
    public List<Audio> ChosenTracks { get; set; }
    public int CurrentSkip { get; set; } = 1;

    public void ResetMoodsAndGenres()
    {
        SelectedMoods.Clear();
        SelectedGenres.Clear();
    }

    public Filter MakeFilter() => new Filter(SelectedMoods, SelectedGenres);
}