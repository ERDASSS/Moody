using VkNet.Model.Attachments;

namespace TGBot
{
    class DataBase
    {
        public static void AddTrackToDataBase(Audio track, Genre genre = default, Mood mood = default)
        {
            var trackName = track.Title;
            var trackAuthor = track.Artist;
            //тут если genre == Genre.None то добавляем бюез жанра, аналогично с настроением
           
            //добавляем треки в бд
            throw new NotImplementedException();
        }

        public static bool HasTrackInDataBase(Audio track)
        {
            //чекаем есть ли трек в бд
            throw new NotImplementedException();
        }

        public static bool IsRightTrack(Audio track, Genre genre = default, Mood mood = default)
        {
            //проверяем подходит ли трек по настроению и жанру
            throw new NotImplementedException();
        }
    }
}
