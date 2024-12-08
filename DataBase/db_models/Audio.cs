namespace Database.db_models;

public class DbAudio(int dbAudioId, string title, DbAuthor author, DbUsersByPvVv votes)
{
    public int DbAudioId { get; } = dbAudioId;
    public string Title { get; } = title;
    public DbAuthor Author { get; } = author;

    public DbUsersByPvVv Votes { get; } = votes;
}

public class DbAuthor(int id, string name)
{
    public int Id { get; } = id;
    public string Name { get; } = name;
}

public class DbUsersByPvVv :
    // в ...[значение_параметра][тип_голоса] лежит список проголосовавших пользователей
    // т.е. тут хранятся пользователи по ключам Pv (ParameterValue), Vv (VoteValue)
    // прошу прощения за такой балдежный нейминг c четырьмя согласными подряд,
    // но DbUsersByParameterValueAndVoteValue было слишком длинно)
    // Dictionary<DbAudioParameter,
    DefaultDictionary<DbAudioParameterValue,
        DefaultDictionary<VoteValue,
            List<User>>>;
// >

public class DbAudioParameter(int id, string name)
    // например "настроение" или "жанр"
{
    public int Id { get; } = id;
    public string Name { get; } = name;

    // todo: может получать id из бд, а не хардкодить
    public static DbAudioParameter MoodParameter { get; } = new DbAudioParameter(1, "mood");
    public static DbAudioParameter GenreParameter { get; } = new DbAudioParameter(2, "genre");

    public static DbAudioParameter GetById(int id) =>
        id switch
        {
            1 => MoodParameter,
            2 => GenreParameter,
            _ => throw new NotImplementedException($"не реализовано получение параметра по id={id}")
        };
}

public abstract class DbAudioParameterValue(int id, int parameterId, string name, string? description)
    // например "веселая" или "спокойная" для настроения
    // или "рок" для жанра
{
    public int Id { get; } = id;
    public int ParameterId { get; } = parameterId;
    public string Name { get; } = name;
    public string? Description { get; } = description;

    public DbAudioParameter GetParameter() => DbAudioParameter.GetById(Id);


    public static DbAudioParameter GetParameter<TParameterValue>()
        where TParameterValue : DbAudioParameterValue
    {
        var type = typeof(TParameterValue);
        if (type == typeof(DbMood))
            return DbAudioParameter.MoodParameter;
        if (type == typeof(DbGenre))
            return DbAudioParameter.GenreParameter;
        throw new ArgumentException($"не удалось обработать тип: {type}");
    }

    public static DbAudioParameterValue Create(int id, int parameterId, string name, string? description)
    {
        if (parameterId == DbAudioParameter.MoodParameter.Id)
            return new DbMood(id, name, description);
        if (parameterId == DbAudioParameter.GenreParameter.Id)
            return new DbGenre(id, name, description);
        throw new ArgumentException($"ожидался id параметра, как у " +
                                    $"mood: {DbAudioParameter.MoodParameter.Id} или" +
                                    $"genre: {DbAudioParameter.GenreParameter.Id}" +
                                    $"но получен: {id}");
    }

    public static TParameterValue Create<TParameterValue>(int id, int parameterId, string name, string? description)
        where TParameterValue : DbAudioParameterValue
    {
        var result = Create(id, parameterId, name, description);
        if (result is TParameterValue typedResult)
            return typedResult;
        throw new InvalidCastException($"Не удалось привести объект типа {result.GetType()} " +
                                       $"к {typeof(TParameterValue)}.");
    }

    public override int GetHashCode() => HashCode.Combine(Id, ParameterId, Name, Description);

    public override bool Equals(object? obj)
    {
        if (obj is not DbAudioParameterValue other) return false;
        return Id == other.Id &&
               ParameterId == other.ParameterId &&
               Name == other.Name &&
               Description == other.Description;
    }
}

public class DbMood(int id, string name, string? description)
    : DbAudioParameterValue(id, DbAudioParameter.MoodParameter.Id, name, description);

public class DbGenre(int id, string name, string? description)
    : DbAudioParameterValue(id, DbAudioParameter.GenreParameter.Id, name, description);

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