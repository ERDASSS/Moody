using Database.db_models;

namespace Database;

public class Filter(
    HashSet<DbMood>? targetMoods = null,
    HashSet<DbGenre>? targetGenres = null
)
{
    public HashSet<DbMood>? targetMoods { get; } = targetMoods;
    public HashSet<DbGenre>? targetGenres { get; } = targetGenres;

    public bool Check(DbAudio dbAudio)
    {
        if (targetMoods != null)
            if (targetMoods.Any(targetMood => CheckMood(dbAudio, targetMood)))
                return true;
        if (targetGenres != null)
            if (targetGenres.Any(targetGenre => CheckGenre(dbAudio, targetGenre)))
                return true;
        return false;
    }

    private bool CheckMood(DbAudio dbAudio, DbAudioParameterValue targetMood)
    {
        // если суммарный балл голосов > 0, то считаем, что аудио относится к данному настроению
        var score = 0.0;
        var votes = dbAudio.Votes[targetMood];
        foreach (var voteValue in votes.Keys)
            score += voteValue.Cost() * votes[voteValue].Count;
        return score > 0;
    }

    private bool CheckGenre(DbAudio dbAudio, DbAudioParameterValue targetGenre, bool considerSubgenres = true)
    {
        var score = 0.0;
        var votes = dbAudio.Votes[targetGenre];
        foreach (var voteValue in votes.Keys)
            score += voteValue.Cost() * votes[voteValue].Count;
        return score > 0;

        // if (considerSubgenres)
        //     // если есть поджанр искомого жанра, которым ,
        //     // то считаем, что аудио относится к данному жанру
        //     foreach (var subgenre in dbAudio.Parameters.Parameters[Genre.Instance].Values.Keys)
        //     {
        //         if (!IsItASubgenre(subgenre.Name, targetGenre.Name)) continue;
        //         foreach (var voteValue in dbAudio.Parameters.Parameters[Genre.Instance].Values[subgenre].Votes.Keys)
        //             score += voteValue.Cost();
        //     }
        // else
        // {
        //     var votes = dbAudio.Parameters.Parameters[Mood.Instance].Values[targetGenre].Votes;
        //     foreach (var voteValue in votes.Keys)
        //         score += voteValue.Cost() * votes[voteValue].Count;
        // }

        // хотя вряд ли учет поджанров будет работать, потому что какой-нибудь глубинный
        // aatmospheric ambient white black metal настолько отличается от верхнеуровнегого metal, 
        // что вряд ли вообще можно сказать, что первый является вторым
    }


    private bool IsItASubgenre(string subgenre, string genre)
    {
        // G1 - поджанр G2 <=> все слова из G2 содержатся в G1
        // # black folk metal - поджанр black metal <=> все слова из "black metal" содержатся в "black folk metal"
        foreach (var word in genre)
            if (!subgenre.Contains(word))
                return false;
        return true;
    }
}