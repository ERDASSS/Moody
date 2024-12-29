using System.Collections.Generic;
using Database;
using Database.db_models;
using Xunit;

public class FilterTests
{
    [Fact]
    public void Check_ShouldReturnTrueIfMoodMatches()
    {
        var targetMood = new DbMood(1, "Happy", "");
        var filter = new Filter(new HashSet<DbMood> { targetMood });

        var dbAudio = new DbAudio(1, "Test Title", new DbAuthor(1, "Test Artist"), new DbUsersByPvVv());
        dbAudio.Votes[targetMood][VoteValue.Confirmation].Add(new DbUser(1, 123456789, "testuser"));

        var result = filter.Check(dbAudio);

        Assert.True(result);
    }

    [Fact]
    public void Check_ShouldReturnFalseIfMoodDoesNotMatch()
    {
        var targetMood = new DbMood(1, "Happy", "");
        var filter = new Filter(new HashSet<DbMood> { targetMood });

        var dbAudio = new DbAudio(1, "Test Title", new DbAuthor(1, "Test Artist"), new DbUsersByPvVv());
        var differentMood = new DbMood(2, "Sad", "");
        dbAudio.Votes[differentMood][VoteValue.Confirmation].Add(new DbUser(1, 123456789, "testuser"));

        var result = filter.Check(dbAudio);

        Assert.False(result);
    }

    [Fact]
    public void Check_ShouldReturnTrueIfGenreMatches()
    {
        var targetGenre = new DbGenre(1, "Rock", "");
        var filter = new Filter(null, new HashSet<DbGenre> { targetGenre });

        var dbAudio = new DbAudio(1, "Test Title", new DbAuthor(1, "Test Artist"), new DbUsersByPvVv());
        dbAudio.Votes[targetGenre][VoteValue.Confirmation].Add(new DbUser(1, 123456789, "testuser"));

        var result = filter.Check(dbAudio);

        Assert.True(result);
    }

    [Fact]
    public void Check_ShouldReturnFalseIfGenreDoesNotMatch()
    {
        var targetGenre = new DbGenre(1, "Rock", "");
        var filter = new Filter(null, new HashSet<DbGenre> { targetGenre });

        var dbAudio = new DbAudio(1, "Test Title", new DbAuthor(1, "Test Artist"), new DbUsersByPvVv());
        var differentGenre = new DbGenre(2, "Jazz", "");
        dbAudio.Votes[differentGenre][VoteValue.Confirmation].Add(new DbUser(1, 123456789, "testuser"));

        var result = filter.Check(dbAudio);

        Assert.False(result);
    }
}
