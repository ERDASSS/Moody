using System.Data.SQLite;
using VkNet.Model.Attachments;
using Xunit;

namespace Database.Tests;

public class SqliteDbAccessorTests : IDisposable
{
    private readonly SqliteDbAccessor _dbAccessor;
    private readonly SQLiteConnection _connection;

    public SqliteDbAccessorTests()
    {
        _connection = new SQLiteConnection("Data Source=:memory:;Version=3;");
        _connection.Open();
        _dbAccessor = new SqliteDbAccessor(":memory:") { Connection = _connection };
        SetupDatabase();
    }

    private void SetupDatabase()
    {
        // Настройка базы данных для тестов
        var createTablesQuery = @"
        CREATE TABLE IF NOT EXISTS authors (
            author_id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT UNIQUE
        );
        CREATE TABLE IF NOT EXISTS tracks (
            track_id INTEGER PRIMARY KEY AUTOINCREMENT,
            title TEXT,
            author_id INTEGER,
            FOREIGN KEY (author_id) REFERENCES authors (author_id)
        );
        CREATE TABLE IF NOT EXISTS users (
            user_id INTEGER PRIMARY KEY AUTOINCREMENT,
            chat_id INTEGER UNIQUE,
            username TEXT
        );
        CREATE TABLE IF NOT EXISTS votes (
            vote_id INTEGER PRIMARY KEY AUTOINCREMENT,
            user_id INTEGER,
            track_id INTEGER,
            param_value_id INTEGER,
            vote_value INTEGER,
            FOREIGN KEY (user_id) REFERENCES users (user_id),
            FOREIGN KEY (track_id) REFERENCES tracks (track_id)
        );
        CREATE TABLE IF NOT EXISTS parameter_values (
            value_id INTEGER PRIMARY KEY AUTOINCREMENT,
            param_id INTEGER,
            name TEXT,
            description TEXT,
            FOREIGN KEY (param_id) REFERENCES parameters (param_id)
        );
        CREATE TABLE IF NOT EXISTS parameters (
            param_id INTEGER PRIMARY KEY AUTOINCREMENT,
            name TEXT
        );";

        using var command = new SQLiteCommand(createTablesQuery, _connection);
        command.ExecuteNonQuery();
    }

    [Fact]
    public void SaveAudioInDb_ShouldInsertNewAudio()
    {
        var vkAudio = new Audio { Artist = "Test Artist", Title = "Test Title" };

        _dbAccessor.SaveAudioInDb(vkAudio);

        var dbAudio = _dbAccessor.TryGetAudioFromBd(vkAudio);
        Assert.NotNull(dbAudio);
        Assert.Equal(vkAudio.Title, dbAudio.Title);
        Assert.Equal(vkAudio.Artist, dbAudio.Author.Name);
    }

    [Fact]
    public void TryGetAudioFromBd_ShouldReturnNullIfAudioDoesNotExist()
    {
        var vkAudio = new Audio { Artist = "NonExistent Artist", Title = "NonExistent Title" };

        var dbAudio = _dbAccessor.TryGetAudioFromBd(vkAudio);

        Assert.Null(dbAudio);
    }


    [Fact]
    public void AddOrUpdateUser_ShouldInsertNewUser()
    {
        var chatId = 123456789L;
        var username = "testuser";

        _dbAccessor.AddOrUpdateUser(chatId, username);

        var dbUser = _dbAccessor.GetUserByChatId(chatId);
        Assert.NotNull(dbUser);
        Assert.Equal(chatId, dbUser.ChatId);
        Assert.Equal(username, dbUser.Username);
    }

    [Fact]
    public void AddOrUpdateUser_ShouldUpdateExistingUser()
    {
        var chatId = 123456789L;
        var oldUsername = "olduser";
        var newUsername = "newuser";

        _dbAccessor.AddOrUpdateUser(chatId, oldUsername);

        _dbAccessor.AddOrUpdateUser(chatId, newUsername);

        var dbUser = _dbAccessor.GetUserByChatId(chatId);
        Assert.NotNull(dbUser);
        Assert.Equal(chatId, dbUser.ChatId);
        Assert.Equal(newUsername, dbUser.Username);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}