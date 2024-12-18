using Database;
using Telegram.Bot;

namespace TGBot.States;

public class MarkState : InputHandlingState
{
    public static MarkState Instance { get; } = new();

    public override Task BeforeAnswer(TelegramBotClient bot, DbAccessor dbAccessor, TgUser user)
    {
        dbAccessor.AddOrUpdateUser(user.ChatId, user.TgUsername);
        user.DbUser = dbAccessor.GetUserByChatId(user.ChatId);
        if (!user.IsMarkingUnmarked!.Value)
            user.UnmarkedTracks = user.ApiWrapper!.GetFavouriteTracks().ToList();
        user.CurrentTrack = user.UnmarkedTracks.FirstOrDefault();
        user.CurrentSkip = 1;

        await StartMarking(chatId);
    }
}

