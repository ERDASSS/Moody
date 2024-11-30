namespace Database.db_models;

public class DbAudio
{
    public int DbAudioId { get; }
    public DbAudioTitleVariants TitleVariants { get; set; }
    public DbAudioAuthorVariants AuthorVariants { get; set; }


    public DbAudio(int dbAudioId, DbAudioTitleVariants titleVariants, DbAudioAuthorVariants authorVariants)
    {
        DbAudioId = dbAudioId;
        TitleVariants = titleVariants;
        AuthorVariants = authorVariants;
    }
}

public class DbAudioTitleVariants(HashSet<string> variants)
{
    public HashSet<string> Variants = variants;
}

public class DbAudioAuthorVariants(HashSet<string> variants)
{
    public HashSet<string> Variants = variants;
}

public class AudioParameters
{
    public Dictionary<DbAudioParameter, AudioParameterValues> Parameters;
}

public class DbAudioParameter // например настроение или жанр
{
    public int Id;
    public string Name;
}

public class DbAudioMood : DbAudioParameter
{
}

public class DbAudioGenre : DbAudioParameter
{
}

public class DbAudioParameterValue // например веселая или спокойная для настроения
{
    public int Id;
    public string Name;
}

public class AudioParameterValues
{
    public Dictionary<AudioParameterValues, UsersVotes> Values;
}

public class UsersVotes
{
    public Dictionary<VoteValue, List<User>> Votes;
}

public enum VoteValue
{
    Markup, //       добавляется при первоначальной разметке (+1.1) (может не стоит различать это и подтверждение)
    Confirmation, // добавляется, если пользователь нажал кнопку, что он согласен с разметкой (+1)
    Usage, //        добавляется, если пользователь использовал эту разметку и не голосовал против (+0.01)
    Against, //      добавляется, если пользователь проголосовал против (-1)
}