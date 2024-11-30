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


    public IEnumerable<Audio> FilterAndSaveInDb(VkCollection<Audio> usersFavouriteAudios, Filter)
    {
        // по треку получаем его параметры
        foreach (var audio in usersFavouriteAudios)
        {
            var dbAudio = 
        }
    }

    public DbAudio GetAudioFromBd(string name, string author)
    {
    }
}

public class Filter
{
    public string Name;
    public string Author;
}