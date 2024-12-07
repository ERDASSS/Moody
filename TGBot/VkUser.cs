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
    public HashSet<DbMood> SelectedMoods { get; } = new();
    public HashSet<DbGenre> SelectedGenres { get; } = new();
    public Filter Filter => MakeFilter();
    public bool AreMoodsSelected { get; set; }
    public bool AreGenresSelected { get; set; }

    public void ResetMoodsAndGenres()
    {
        SelectedMoods.Clear();
        SelectedGenres.Clear();
        AreMoodsSelected = false;
        AreGenresSelected = false;
    }

    public void AddMood(DbMood mood) => SelectedMoods.Add(mood);
    public void AddGenre(DbGenre genre) => SelectedGenres.Add(genre);

    private Filter MakeFilter()
    {
        var targetMoods = SelectedMoods.Select(m => (DbAudioParameterValue)m).ToHashSet();
        var targetGenres = SelectedGenres.Select(g => (DbAudioParameterValue)g).ToHashSet();

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

        return type switch
        {
            "Mood" => new DbMood(id, name, description),
            "Genre" => new DbGenre(id, name, description),
            _ => throw new ArgumentException($"Неизвестный тип параметра {type}")
        };
    }
}