using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using Database.db_models;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace Database;

public class DbAccessor
{
    public string DbPath { get; }
    public string ConnectionString => $"Data Source={DbPath};Version=3;";

    public DbAccessor(string dbPath = "moody.db")
    {
        DbPath = dbPath;
    }

    public IEnumerable<Audio> FilterAndSaveNewInDb(VkCollection<Audio> usersFavouriteAudios, Filter filter)
    {
        // соединение создается на 1 фильтрацию
        using var connection = new SQLiteConnection(ConnectionString);
        connection.Open();

        // по треку получаем его параметры
        foreach (var vkAudio in usersFavouriteAudios)
        {
            var dbAudio = TryGetAudioFromBd(vkAudio, connection);
            if (dbAudio == null)
            {
                // если трека нет в бд, сохраняем его туда с пустыми параметрами
                SaveAudioInDb(vkAudio, connection);
                continue;
            }

            var success = filter.Check(dbAudio);
            if (success)
                yield return vkAudio;
        }
    }


    public List<Mood> GetMoods()
    {
        return GetParameterValues(MoodParameter.Instance)
            .Select(pv => new Mood(pv.Id, pv.Name, pv.Description))
            .ToList();
        // TODO: исправить этот ужас
    }

    public List<Genre> GetGenres()
    {
        return GetParameterValues(GenreParameter.Instance)
            .Select(pv => new Genre(pv.Id, pv.Name, pv.Description))
            .ToList();
        // TODO: исправить этот ужас
    }

    private List<DbAudioParameterValue> GetParameterValues(DbAudioParameter parameter)
    {
        // получает все существующие значения данного параметра
        // (например по параметру "настроение" возвращает все существующие настроения)

        // todo: создать соединение один раз
        using var connection = new SQLiteConnection(ConnectionString);
        connection.Open();

        const string query = @"
        SELECT pv.value_id, pv.name, pv.description
        FROM parameter_values pv
        WHERE pv.param_id = @ParamId";

        using var command = new SQLiteCommand(query, connection);
        command.Parameters.AddWithValue("@ParamId", parameter.Id);

        using var reader = command.ExecuteReader();
        var parameterValues = new List<DbAudioParameterValue>();

        while (reader.Read())
        {
            var id = reader.GetInt32(reader.GetOrdinal("value_id"));
            var name = reader.GetString(reader.GetOrdinal("name"));
            var description = reader.IsDBNull(reader.GetOrdinal("description"))
                ? null
                : reader.GetString(reader.GetOrdinal("description"));

            parameterValues.Add(new DbAudioParameterValue(id, parameter.Id, name, description));
        }

        return parameterValues;
    }


    private void SaveAudioInDb(Audio vkAudio, SQLiteConnection connection)
    {
        if (connection.State != System.Data.ConnectionState.Open)
            throw new InvalidOperationException("Подключение к базе данных должно быть открыто.");

        // SQL-запросы
        const string insertAuthorQuery = @"
        INSERT OR IGNORE INTO authors (name)
        VALUES (@AuthorName)";

        const string getAuthorIdQuery = @"
        SELECT author_id FROM authors WHERE name = @AuthorName";

        const string insertTrackQuery = @"
        INSERT INTO tracks (title, author_id)
        VALUES (@Title, @AuthorId)";

        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. Добавляем автора, если его еще нет
            using (var authorCommand = new SQLiteCommand(insertAuthorQuery, connection, transaction))
            {
                authorCommand.Parameters.AddWithValue("@AuthorName", vkAudio.Artist);
                authorCommand.ExecuteNonQuery();
            }

            // 2. Получаем идентификатор автора
            int authorId;
            using (var getAuthorIdCommand = new SQLiteCommand(getAuthorIdQuery, connection, transaction))
            {
                getAuthorIdCommand.Parameters.AddWithValue("@AuthorName", vkAudio.Artist);
                var authorIdOrNull = getAuthorIdCommand.ExecuteScalar();
                if (authorIdOrNull == null)
                    throw new InvalidOperationException($"Мы только что добавили {vkAudio.Artist}, куда он делся?");
                authorId = Convert.ToInt32(authorIdOrNull);
            }

            // 3. Добавляем трек
            using (var trackCommand = new SQLiteCommand(insertTrackQuery, connection, transaction))
            {
                trackCommand.Parameters.AddWithValue("@Title", vkAudio.Title);
                trackCommand.Parameters.AddWithValue("@AuthorId", authorId);
                trackCommand.ExecuteNonQuery();
            }

            // Завершаем транзакцию
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private DbAudio? TryGetAudioFromBd(Audio vkAudio, SQLiteConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            throw new InvalidOperationException("Подключение к базе данных должно быть открыто.");

        const string query = @"
            SELECT 
                t.track_id, 
                t.title, 
                a.author_id, 
                a.name AS author_name,
                v.vote_id,
                v.user_id,
                v.vote_value,
                pv.name AS value_name,
                pv.description,
                p.param_id,
                p.name AS param_name
            FROM tracks t
            JOIN authors a ON t.author_id = a.author_id
            LEFT JOIN votes v ON t.track_id = v.track_id
            LEFT JOIN parameter_values pv ON v.param_value_id = pv.value_id
            LEFT JOIN parameters p ON pv.param_id = p.param_id
            WHERE t.title = @Title AND a.name = @AuthorName";


        using var command = new SQLiteCommand(query, connection);
        command.Parameters.AddWithValue("@Title", vkAudio.Title);
        command.Parameters.AddWithValue("@AuthorName", vkAudio.Artist);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null; // Аудио не найдено
        var trackId = reader.GetInt32(reader.GetOrdinal("track_id"));
        var title = reader.GetString(reader.GetOrdinal("title"));
        var author = new DbAuthor(
            reader.GetInt32(reader.GetOrdinal("author_id")),
            reader.GetString(reader.GetOrdinal("name"))
        );

        var parameters = new AudioParameters();
        //Собираем параметры
        do
        {
            if (reader.IsDBNull(reader.GetOrdinal("param_name"))) continue;

            // вытаскиваем параметр
            var paramId = reader.GetInt32(reader.GetOrdinal("param_id"));
            var paramName = reader.GetString(reader.GetOrdinal("param_name"));
            var param = new DbAudioParameter(paramId, paramName);
            if (!parameters.Parameters.TryGetValue(param, out var values))
            {
                values = new AudioParameterValues();
                parameters.Parameters[param] = values;
            }

            // его значение (и сохраняем параметер/значение)
            var paramValueId = reader.GetInt32(reader.GetOrdinal("value_id"));
            var paramValueName = reader.GetString(reader.GetOrdinal("value_name"));
            var paramValueDescription = reader.GetString(reader.GetOrdinal("description"));
            var paramValue = new DbAudioParameterValue(paramValueId, paramId, paramValueName, paramValueDescription);
            if (!values.Values.TryGetValue(paramValue, out var votes))
            {
                votes = new UsersVotes();
                values.Values[paramValue] = votes;
            }

            // пользователя, проголосовавшего за это значение
            var userId = reader.GetInt32(reader.GetOrdinal("user_id"));
            var user = new User(userId);

            // голос за это значение (и сохраняем параметер/значение/тип голоса)
            var voteId = reader.GetInt32(reader.GetOrdinal("vote_id"));
            var intVoteValue = reader.GetInt32(reader.GetOrdinal("vote_value"));
            if (!Enum.IsDefined(typeof(VoteValue), 3))
                throw new Exception($"у голоса с id:{voteId} неизвестное значение: {intVoteValue}");
            var voteValue = (VoteValue)intVoteValue;
            if (!votes.Votes.TryGetValue(voteValue, out var users))
            {
                users = new List<User>();
                votes.Votes[voteValue] = users;
            }

            // и наконец сохраняем параметер/значение/тип голоса/пользователи

            users.Add(user);
        } while (reader.Read());

        return new DbAudio(trackId, title, author, parameters);
    }
}

public class Filter
{
    public HashSet<DbAudioParameterValue>? targetMoods { get; }
    public HashSet<DbAudioParameterValue>? targetGenres { get; }

    public Filter(
        HashSet<DbAudioParameterValue>? targetMoods = null,
        HashSet<DbAudioParameterValue>? targetGenres = null
    )
    {
        this.targetMoods = targetMoods;
        this.targetGenres = targetGenres;
    }

    public bool Check(DbAudio dbAudio)
    {
        if (targetMoods != null)
            foreach (var targetMood in targetMoods)
                if (!CheckMood(dbAudio, targetMood))
                    return false;

        if (targetGenres != null)
            foreach (var targetGenre in targetGenres)
                if (!CheckGenre(dbAudio, targetGenre))
                    return false;

        return true;
    }

    private bool CheckMood(DbAudio dbAudio, DbAudioParameterValue targetMood)
    {
        // если суммарный балл голосов > 0, то считаем, что аудио относится к данному настроению
        var score = 0.0;
        var votes = dbAudio.Parameters.Parameters[MoodParameter.Instance].Values[targetMood].Votes;
        foreach (var voteValue in votes.Keys)
            score += voteValue.Cost() * votes[voteValue].Count;
        return score > 0;
    }

    private bool CheckGenre(DbAudio dbAudio, DbAudioParameterValue targetGenre, bool considerSubgenres = true)
    {
        var score = 0.0;
        var votes = dbAudio.Parameters.Parameters[MoodParameter.Instance].Values[targetGenre].Votes;
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