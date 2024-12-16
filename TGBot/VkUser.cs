using ApiMethods;
using Database;
using Database.db_models;
using Telegram.Bot.Types;
using VkNet.Utils;

namespace TGBot;

public class VkUser
{
    public VkUser(IVkApiWrapper vkApi)
    {
        VkApi = vkApi;
    }

    public IVkApiWrapper VkApi { get; private set; }
    public Dictionary<string, DbMood> SuggestedMoods { get; set; } = new(); // DbMood по DbMood.Name
    public Dictionary<string, DbGenre> SuggestedGenres { get; set; } = new(); // DbGenre по DbGenre.Name
    public HashSet<DbMood> SelectedMoods { get; } = new();
    public HashSet<DbGenre> SelectedGenres { get; } = new();
    public bool AreMoodsSelected { get; set; }
    public bool AreGenresSelected { get; set; }
    public string CurrentCommand { get; set; }
    public string Username { get; private set; }
    public DbUser DbUser { get; set; }
    public bool IsMarkingUnmarked { get; set; }
    public VkNet.Model.Attachments.Audio CurrentTrack { get; set; }
    public List<VkNet.Model.Attachments.Audio> UnmarkedTracks { get; set; }
    public List<VkNet.Model.Attachments.Audio> ChosenTracks { get; set; }
    public int CurrentSkip { get; set; }

    public void ResetMoodsAndGenres()
    {
        SelectedMoods.Clear();
        SelectedGenres.Clear();
        AreMoodsSelected = false;
        AreGenresSelected = false;
    }

    public void SetUsername(string username) => Username = username;

    public void AddMood(DbMood mood) => SelectedMoods.Add(mood);
    public void AddGenre(DbGenre genre) => SelectedGenres.Add(genre);

    public Filter GetFilter() => new Filter(SelectedMoods, SelectedGenres);
}