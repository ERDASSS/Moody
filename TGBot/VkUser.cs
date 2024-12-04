using ApiMethods;
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
}