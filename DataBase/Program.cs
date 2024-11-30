using Microsoft.Data.Sqlite;
using System;
using System.Data.SQLite;
using System.IO;

namespace Database;

public static class Program
{
    public static void Main()
    {
        var accessor = new DbAccessor("moody.db");
        
        
        // // Путь к базе данных (рядом с исполняемым файлом)
        // var databasePath = Path.Combine(@"D:\code\sync_code\c_sh\oop\_project\Database\moody.db");
        //
        // using var connection = new SqliteConnection($"Data Source={databasePath}");
        // connection.Open();
        //
        // using var command = connection.CreateCommand();
        //
        // // Вставка данных (если необходимо)
        // command.CommandText = "INSERT INTO users (name) VALUES ('admin_01');";
        // command.ExecuteNonQuery();
        //
        // // Запрос на выборку данных
        // command.CommandText = "SELECT * FROM users";
        // using var reader = command.ExecuteReader();
        // while (reader.Read())
        // {
        //     var name = reader.GetString(1); // Чтение столбца "name"
        //     Console.WriteLine(name);
    }
}
