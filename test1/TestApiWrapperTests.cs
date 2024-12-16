using ApiMethods;

namespace serialisationTest;

public class TestApiWrapperTests
{
    public static void ShowAllTracks()
    {
        foreach (var track in new TestApiWrapper().GetFavouriteTracks())
        {
            Console.WriteLine($"{track.Title,-60} {track.Artist}");
        }  
    }
}