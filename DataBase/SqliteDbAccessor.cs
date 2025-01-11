using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using Database.db_models;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace Database;

// todo: добавить голосам временную метку
public class SqliteDbAccessor : IDbAccessor
{
    public string DbPath { get; }
    public string ConnectionString => $"Data Source={DbPath};Version=3;";
    public SQLiteConnection Connection { get; set; }

    public SqliteDbAccessor(string dbPath = "moody.db")
    {
        DbPath = dbPath;
        Connection = new SQLiteConnection(ConnectionString);
        Connection.Open();
    }

    public IEnumerable<Audio> FilterAndSaveNewInDb(IEnumerable<Audio> usersFavouriteAudios, Filter filter)
    {
        // по треку получаем его параметры
        // TODO: 600-700 запросов работают крайне медленно
        // TODO: нет, КРАЙНЕ медленно
        foreach (var vkAudio in usersFavouriteAudios)
        {
            var dbAudio = TryGetAudioFromBd(vkAudio);
            if (dbAudio == null)
            {
                // если трека нет в бд, сохраняем его туда с пустыми параметрами
                // TODO: **Делать 1 запрос, а не по запросу на каждый трек**
                SaveAudioInDb(vkAudio);
                continue;
            }

            var success = filter.Check(dbAudio);
            if (success)
                yield return vkAudio;
        }
    }

    public List<DbMood> GetMoods() => GetParameterValues<DbMood>();
    public List<DbGenre> GetGenres() => GetParameterValues<DbGenre>();

    /// <summary>
    /// Получает все существующие значения данного параметра
    /// (например по параметру "настроение" возвращает все существующие настроения)
    /// </summary>
    private List<TParameterValue> GetParameterValues<TParameterValue>()
        where TParameterValue : DbAudioParameterValue
    {
        var parameter = DbAudioParameterValue.GetParameter<TParameterValue>();

        const string query = @"
        SELECT pv.value_id, pv.name, pv.description
        FROM parameter_values pv
        WHERE pv.param_id = @ParamId";

        using var command = new SQLiteCommand(query, Connection);
        command.Parameters.AddWithValue("@ParamId", parameter.Id);

        using var reader = command.ExecuteReader();

        var parameterValues = new List<TParameterValue>();
        while (reader.Read())
        {
            var id = reader.GetInt32(reader.GetOrdinal("value_id"));
            var name = reader.GetString(reader.GetOrdinal("name"));
            var description = reader.IsDBNull(reader.GetOrdinal("description"))
                ? null
                : reader.GetString(reader.GetOrdinal("description"));

            parameterValues.Add(DbAudioParameterValue.Create<TParameterValue>(id, parameter.Id, name, description));
            // yield return DbAudioParameterValue.Create(id, parameter.Id, name, description);
        }

        return parameterValues;
    }

    public void SaveAudioInDb(Audio vkAudio)
    {
        // SQL-запросы
        const string insertAuthorQuery = @"
        INSERT INTO authors (name) VALUES (@AuthorName)
        ON CONFLICT (name) DO NOTHING";

        const string getAuthorIdQuery = @"
        SELECT author_id FROM authors WHERE name = @AuthorName";

        const string insertTrackQuery = @"
        INSERT INTO tracks (title, author_id)
        VALUES (@Title, @AuthorId)";

        using var transaction = Connection.BeginTransaction();
        try
        {
            // 1. Добавляем автора, если его еще нет
            using (var authorCommand = new SQLiteCommand(insertAuthorQuery, Connection, transaction))
            {
                authorCommand.Parameters.AddWithValue("@AuthorName", vkAudio.Artist);
                authorCommand.ExecuteNonQuery();
            }

            // 2. Получаем идентификатор автора
            int authorId;
            using (var getAuthorIdCommand = new SQLiteCommand(getAuthorIdQuery, Connection, transaction))
            {
                getAuthorIdCommand.Parameters.AddWithValue("@AuthorName", vkAudio.Artist);
                var authorIdOrNull = getAuthorIdCommand.ExecuteScalar();
                if (authorIdOrNull == null)
                    throw new InvalidOperationException($"Мы только что добавили {vkAudio.Artist}, куда он делся?");
                authorId = Convert.ToInt32(authorIdOrNull);
            }

            // 3. Добавляем трек
            using (var trackCommand = new SQLiteCommand(insertTrackQuery, Connection, transaction))
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

    public DbAudio? TryGetAudioFromBd(Audio vkAudio)
    {
        const string query = @"
            SELECT 
                t.track_id, 
                t.title, 
                a.author_id, 
                a.name AS author_name,
                v.vote_id,
                v.user_id,
                v.vote_value,
                u.user_id,
                u.chat_id,
                u.username,
                pv.value_id,
                pv.name AS value_name,
                pv.description,
                p.param_id,
                p.name AS param_name
            FROM tracks t
            JOIN authors a ON t.author_id = a.author_id
            LEFT JOIN votes v ON t.track_id = v.track_id -- считываем голоса, если они есть (чтобы отличать отсутствующие треки от треков без голосов)
            LEFT JOIN users u ON v.user_id = u.user_id
            LEFT JOIN parameter_values pv ON v.param_value_id = pv.value_id
            LEFT JOIN parameters p ON pv.param_id = p.param_id
            WHERE t.title = @Title AND a.name = @AuthorName";
        using var command = new SQLiteCommand(query, Connection);
        command.Parameters.AddWithValue("@Title", vkAudio.Title);
        command.Parameters.AddWithValue("@AuthorName", vkAudio.Artist);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null; // Аудио не найдено

        var trackId = reader.GetInt32(reader.GetOrdinal("track_id"));
        var title = reader.GetString(reader.GetOrdinal("title"));
        var author = new DbAuthor(
            reader.GetInt32(reader.GetOrdinal("author_id")),
            reader.GetString(reader.GetOrdinal("author_name"))
        );
        var trackIds = new HashSet<int>(); // ожидается, что тут будет id ровно 1-го трека

        // Собираем голоса за параметры
        var votes = new DbUsersByPvVv();
        do
        {
            trackIds.Add(trackId);

            // вытаскиваем голос за это значение
            if (reader.IsDBNull(reader.GetOrdinal("param_id")))
            {
                // Никаких голосов за этот трек может и не быть,
                // тогда все остальные поля (параметры этого голоса) тоже будут NULL 
                // так что дальше можно не считывать
                continue;
            }

            var voteId = reader.GetInt32(reader.GetOrdinal("vote_id"));
            var intVoteValue = reader.GetInt32(reader.GetOrdinal("vote_value"));
            if (!Enum.IsDefined(typeof(VoteValue), intVoteValue))
                throw new Exception($"у голоса с id: {voteId} неизвестное значение: {intVoteValue}");
            var voteValue = (VoteValue)intVoteValue;

            // вытаскиваем параметр
            var paramId = reader.GetInt32(reader.GetOrdinal("param_id"));
            var paramName = reader.GetString(reader.GetOrdinal("param_name"));
            var param = new DbAudioParameter(paramId, paramName);

            // вытаскиваем его значение
            var paramValueId = reader.GetInt32(reader.GetOrdinal("value_id"));
            var paramValueName = reader.GetString(reader.GetOrdinal("value_name"));
            // todo: выпилить описание (или по крайней мере перенести его в отдельную таблицу)
            string? paramValueDescription = null;
            if (!reader.IsDBNull(reader.GetOrdinal("description")))
                paramValueDescription = reader.GetString(reader.GetOrdinal("description"));
            var paramValue = DbAudioParameterValue.Create(paramValueId, paramId, paramValueName, paramValueDescription);

            // вытаскиваем пользователя, проголосовавшего за это значение
            var userId = reader.GetInt32(reader.GetOrdinal("user_id"));
            var chatId = (long)reader.GetInt32(reader.GetOrdinal("chat_id"));
            string? username = null;
            if (!reader.IsDBNull(reader.GetOrdinal("username")))
                username = reader.GetString(reader.GetOrdinal("username"));
            var user = new DbUser(userId, chatId, username);

            // и наконец сохраняем значение/тип голоса/пользователи
            votes[paramValue][voteValue].Add(user);
        } while (reader.Read());

        if (trackIds.Count != 1)
            Console.WriteLine($"Предупреждение: в бд найдено более одного трека с названием '{vkAudio.Title}' " +
                              $"и автором '{vkAudio.Artist}' " +
                              $"(использованы параметры обоих)");

        return new DbAudio(trackId, title, author, votes);
    }


    public void AddVote(
        int audioId,
        int parameterValueId,
        VoteValue voteValue,
        int userId)
    {
        const string query = @"
        INSERT INTO votes (user_id, track_id, param_value_id, vote_value) 
        VALUES (@UserId, @TrackId, @ParamValueId, @VoteValue)";

        using var command = new SQLiteCommand(query, Connection);

        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@TrackId", audioId);
        command.Parameters.AddWithValue("@ParamValueId", parameterValueId);
        command.Parameters.AddWithValue("@VoteValue", (int)voteValue);

        command.ExecuteNonQuery();
    }

    public void AddOrUpdateUser(long chatId, string? username)
    {
        const string insertOrUpdateQuery = @"
        INSERT INTO users (chat_id, username) VALUES (@ChatId, @Username)
        ON CONFLICT(chat_id) DO UPDATE SET username = @Username";

        using var authorCommand = new SQLiteCommand(insertOrUpdateQuery, Connection);
        authorCommand.Parameters.AddWithValue("@ChatId", chatId);
        authorCommand.Parameters.AddWithValue("@Username", username);
        authorCommand.ExecuteNonQuery();
    }

    // todo: сделать меньше дублирования (добавить метод для создания команды и сразу добавления в нее параметров)

    public DbUser? GetUserByChatId(long chatId)
    {
        const string query = "SELECT user_id, chat_id, username FROM users WHERE chat_id = @ChatId";
        using var command = new SQLiteCommand(query, Connection);
        command.Parameters.AddWithValue("@ChatId", chatId);
        return GetUserByUsername(command);
    }

    public DbUser? GetUserByUsername(string username)
    {
        using var command = new SQLiteCommand(
            "SELECT user_id, chat_id, username FROM users WHERE chat_id = @Username",
            Connection);
        command.Parameters.AddWithValue("@ChatId", username);
        return GetUserByUsername(command);
    }

    private DbUser? GetUserByUsername(SQLiteCommand command)
    {
        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null; // User not found
        return new DbUser(
            reader.GetInt32(reader.GetOrdinal("user_id")),
            reader.GetInt32(reader.GetOrdinal("chat_id")),
            reader.GetString(reader.GetOrdinal("username"))
        );
    }
}