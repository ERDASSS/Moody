using Database.db_models;
using VkNet.Model.Attachments;

namespace Database;

public class FullInfoAboutTrack(Audio vkAudio, DbAudio dbAudio)
{
    public Audio VkAudio = vkAudio;
    public DbAudio DbAudio = dbAudio;
}