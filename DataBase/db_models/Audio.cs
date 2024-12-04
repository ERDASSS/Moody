namespace Database.db_models;

public class DbAudio
{
    public int DbAudioId { get; }
    public string Title { get; }
    public DbAuthor Author { get; }
    public AudioParameters Parameters { get; }

    public DbAudio(int dbAudioId, string title, DbAuthor author, AudioParameters parameters)
    {
        DbAudioId = dbAudioId;
        Title = title;
        Author = author;
        Parameters = parameters;
    }
}

public class DbAuthor(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
}

public class AudioParameters
{
    public Dictionary<DbAudioParameter, AudioParameterValues> Parameters { get; } = new();
}

public class DbAudioParameter // например настроение или жанр
    (int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
}

public class MoodParameter() : DbAudioParameter(1, "mood")
{
    public static MoodParameter Instance { get; } = new MoodParameter();
}

public class GenreParameter() : DbAudioParameter(2, "genre")
{
    public static GenreParameter Instance { get; } = new GenreParameter();
}

public class DbAudioParameterValue(int id, int parameterId, string name, string? description)
    // например "веселая" или "спокойная" для настроения
    // или "рок" для жанра
{
    public int Id { get; } = id;
    public int ParameterId { get; } = parameterId;
    public string Name { get; } = name;
    public string? Description { get; } = description;

    public virtual DbAudioParameter GetParameter()
    {
        throw new NotImplementedException();
    }
}

public class Mood(int id, string name, string? description)
    : DbAudioParameterValue(id, MoodParameter.Instance.Id, name, description)
{
    public override DbAudioParameter GetParameter() => MoodParameter.Instance;
}

public class Genre(int id, string name, string? description)
    : DbAudioParameterValue(id, GenreParameter.Instance.Id, name, description)
{
    public override DbAudioParameter GetParameter() => GenreParameter.Instance;

}

public class AudioParameterValues
{
    public Dictionary<DbAudioParameterValue, UsersVotes> Values { get; } = new();
}

public class UsersVotes()
{
    public Dictionary<VoteValue, List<User>> Votes { get; } = new();
}

// public class Vote(int id, User user, VoteValue voteValue)
// {
//     public int Id { get; } = id;
//     public User User { get; } = user;
//     public VoteValue VoteValue { get; } = voteValue;
// }

public class User(int id)
{
    public int Id = id;
}

public enum VoteValue
{
    Confirmation, // добавляется, если пользователь нажал кнопку, что он согласен с разметкой (+1)
    Against, //      добавляется, если пользователь проголосовал против (-1)
    // Markup, //       добавляется при первоначальной разметке (+1.1) (может не стоит различать это и подтверждение)
    // Usage, //        добавляется, если пользователь использовал эту разметку и не голосовал против (+0.01)
}

public static class VoteValueExtensions
{
    public static float Cost(this VoteValue value)
    {
        return value switch
        {
            VoteValue.Confirmation => 1,
            VoteValue.Against => -1,
            // VoteValue.Markup => 1.1f,
            // VoteValue.Usage => 0.01f,
            _ => throw new NotImplementedException($"нужно прописать цену голоса типа {value}")
        };
    }
}