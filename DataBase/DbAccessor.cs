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

        // // если файла нет - создаем
        // if (!File.Exists(dbPath))
        //     SQLiteConnection.CreateFile(dbPath);
        //
        // // если таблиц нет - создаем
        // var sql = File.ReadAllText(DbPath);
        // using (var connection = new SQLiteConnection(ConnectionString))
        // {
        //     connection.Open();
        //     using (var command = new SQLiteCommand(sql, connection))
        //         command.ExecuteNonQuery();
        // }
    }


    public IEnumerable<Audio> FilterAndSaveNewInDb(VkCollection<Audio> usersFavouriteAudios, Filter filter)
    {
        // соединение создается на 1 фильтрацию
        using var connection = new SQLiteConnection(ConnectionString);
        connection.Open();

        // по треку получаем его параметры
        foreach (var vkAudio in usersFavouriteAudios)
        {
            // var dbAuthor = TryGetAuthorByName(vkAudio.Artist, connection);
            // if (dbAuthor == null)
            // {
            //     SaveAudioInDb(vkAudio, connection);
            //     yield break;
            // }

            var dbAudio = TryGetAudioFromBd(vkAudio, connection);
            if (dbAudio == null)
            {
                SaveAudioInDb(vkAudio, connection);
                yield break;
            }

            var success = filter.Check(dbAudio);
            if (success)
                yield return vkAudio;
        }
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


    public DbAuthor? TryGetAuthorByName(string name, SQLiteConnection connection)
    {
        const string query = @"
            SELECT author_id, name 
            FROM authors
            WHERE name = @Name
        ";

        using var command = new SQLiteCommand(query, connection);
        command.Parameters.AddWithValue("@Name", name);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new DbAuthor(
                reader.GetInt32(0),
                reader.GetString(1)
            );
        }

        return null;
    }

    public DbAudio? TryGetAudioFromBd(Audio vkAudio, SQLiteConnection connection)
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
            var paramValue = new DbAudioParameterValue(paramValueId, paramValueName);
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

    // public DbAudio? TryGetAudioFromBd(Audio vkAudio, SQLiteConnection connection)
    // {
    //     if (connection.State != System.Data.ConnectionState.Open)
    //         throw new InvalidOperationException("Подключение к базе данных должно быть открыто.");
    //
    //     const string query = @"
    //     SELECT t.track_id, t.title, a.author_id, a.name 
    //     FROM tracks t
    //     JOIN authors a ON t.author_id = a.author_id
    //     WHERE t.title = @Title AND a.name = @AuthorName";
    //
    //     using var command = new SQLiteCommand(query, connection);
    //     command.Parameters.AddWithValue("@Title", vkAudio.Title);
    //     command.Parameters.AddWithValue("@AuthorName", vkAudio.Artist);
    //
    //     using var reader = command.ExecuteReader();
    //     if (reader.Read())
    //     {
    //         return new DbAudio(
    //             reader.GetInt32(reader.GetOrdinal("track_id")),
    //             reader.GetString(reader.GetOrdinal("title")),
    //             new DbAuthor(
    //                 reader.GetInt32(reader.GetOrdinal("author_id")),
    //                 reader.GetString(reader.GetOrdinal("name"))
    //             ),
    //             
    //         );
    //     }
    //
    //     return null; // Аудио не найдено
    // }
}

public class Filter
{
    public string Name;
    public string Author;
}