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

public class DbAudioParameterValue(int id, string name) // например веселая или спокойная для настроения
{
    public int Id { get; } = id;
    public string Name { get; } = name;
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
    Markup, //       добавляется при первоначальной разметке (+1.1) (может не стоит различать это и подтверждение)
    Confirmation, // добавляется, если пользователь нажал кнопку, что он согласен с разметкой (+1)
    Usage, //        добавляется, если пользователь использовал эту разметку и не голосовал против (+0.01)
    Against, //      добавляется, если пользователь проголосовал против (-1)
}