using ApiMethods;
using Database;
using Database.db_models;

namespace TGBot;

public class TgUser(long chatId, string? tgUsername)
{
    public long ChatId { get; } = chatId;
    public Dictionary<string, DbMood> SuggestedMoods { get; } = new(); // DbMood по DbMood.Name
    public Dictionary<string, DbGenre> SuggestedGenres { get; } = new(); // DbGenre по DbGenre.Name
    public HashSet<DbMood> SelectedMoods { get; } = new();
    public HashSet<DbGenre> SelectedGenres { get; } = new();
    public string? TgUsername { get; set; } = tgUsername;
    public DbUser? DbUser { get; set; } = null;
    public IVkApiWrapper? ApiWrapper { get; set; } = null;

    // public VkNet.Model.Attachments.Audio CurrentTrack { get; set; }
    // public List<VkNet.Model.Attachments.Audio> UnmarkedTracks { get; set; }
    // public List<VkNet.Model.Attachments.Audio> ChosenTracks { get; set; }
    // public int CurrentSkip { get; set; }

    public void ResetMoodsAndGenres()
    {
        SelectedMoods.Clear();
        SelectedGenres.Clear();
    }

    // public void AddMood(DbMood mood) => SelectedMoods.Add(mood);
    // public void AddGenre(DbGenre genre) => SelectedGenres.Add(genre);

    public Filter MakeFilter() => new Filter(SelectedMoods, SelectedGenres);
}