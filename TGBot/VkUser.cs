using ApiMethods;
using Database;
using Database.db_models;

namespace TGBot;

public class VkUser
{
    public VkUser(VkApiWrapper vkApi)
    {
        VkApi = vkApi;
    }
    
    public VkApiWrapper VkApi { get; private set; }
    public HashSet<Mood> Moods { get; } = new();
    public HashSet<Genre> Genres { get; } = new();
    public Filter Filter { get => MakeFilter(); }
    public bool AreMoodsSelected { get; set; }
    public bool AreGenresSelected { get; set; }

    public void ResetMoodsAndGenres()
    {
        Moods.Clear();
        Genres.Clear();
        AreMoodsSelected = false;
        AreGenresSelected = false;
    }

    public void AddMood(Mood mood)
    {
        Moods.Add(mood);
    }

    public void AddGenre(Genre genre)
    {
        Genres.Add(genre);
    }

    private Filter MakeFilter()
    {
        var targetMoods = Moods.Select(m => (DbAudioParameterValue)m).ToHashSet();
        var targetGenres = Genres.Select(g => (DbAudioParameterValue)g).ToHashSet();

        return new Filter(targetMoods, targetGenres);
    }

    public DbAudioParameterValue ParseParameter(string input)
    {
        var parts = input.Split(':');
        if (parts.Length < 3)
            throw new ArgumentException("Неверный формат строки");
        

        var type = parts[0];
        var id = int.Parse(parts[1]);
        var name = parts[2];
        var description = parts.Length > 3 ? parts[3] : null;

        switch (type)
        {
            case "Mood":
                return new Mood(id, name, description);
            case "Genre":
                return new Genre(id, name, description);
            default:
                throw new ArgumentException("Неизвестный тип параметра");
        }
    }
}