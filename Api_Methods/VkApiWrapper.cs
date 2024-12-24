using Microsoft.Extensions.DependencyInjection;
using System.Text;
using VkNet;
using VkNet.AudioBypassService.Exceptions;
using VkNet.AudioBypassService.Extensions;
using VkNet.Enums.Filters;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Utils;

namespace ApiMethods;

public class VkApiWrapper : IApiWrapper
{
    private VkApi vkApi;
    private const int applicationId = 52614150;

    public VkApiWrapper()
    {
        var services = new ServiceCollection();
        services.AddAudioBypass();
        vkApi = new VkApi(services);
    }

    public static AuthorizationResult TryAuthorize(
        out VkApiWrapper apiWrapper,
        string login,
        string password,
        string? code = null)
    {
        apiWrapper = new VkApiWrapper();
        try
        {
            if (code is null) apiWrapper.AuthorizeWithout2FA(login, password);
            else apiWrapper.AuthorizeWith2FA(login, password, code);
        }
        catch (VkAuthException exception)
        {
            Console.WriteLine(exception);
            return exception.Message switch
            {
                "Неправильный логин или пароль" => AuthorizationResult.WrongLoginOrPassword,
                "Произведено слишком много попыток входа в этот аккаунт по паролю. " +
                    "Воспользуйтесь другим способом входа или попробуйте через несколько часов."
                    => AuthorizationResult.TooManyLoginAttempts,
                "Вы ввели неверный код" => AuthorizationResult.WrongCode2FA,
                // _ => AuthorizationResult.UnknownException
                _ => AuthorizationResult.Need2FA // TODO: возможно 2fa нужно возвращать не тут
            };
        }
        catch (InvalidOperationException exception)
        {
            Console.WriteLine(exception);
            return AuthorizationResult.Need2FA; // TODO: возможно 2fa нужно возвращать не тут
        }

        return AuthorizationResult.Success;
    }

    public void AuthorizeWith2FA(string login, string password, string code)
    {
        vkApi.Authorize(new ApiAuthParams
        {
            ApplicationId = applicationId,
            Login = login,
            Password = password,
            TwoFactorAuthorization = new Func<string>(() => code),
            Settings = Settings.Audio
        });
    }

    public void AuthorizeWithToken()
    {
        vkApi.Authorize(new ApiAuthParams
        {
            AccessToken = ""
        });
    }

    public void AuthorizeWithout2FA(string login, string password)
    {
        vkApi.Authorize(new ApiAuthParams
        {
            ApplicationId = applicationId,
            Login = login,
            Password = password,
            Settings = Settings.Audio
        });
    }

    private long GetUserId() => (long)vkApi.UserId;

    public IEnumerable<Audio> GetFavouriteTracks()
        => vkApi.Audio.Get(new VkNet.Model.RequestParams.AudioGetParams { OwnerId = GetUserId() });

    public void CreatePlaylist(string playListName,
        IEnumerable<Audio> songList,
        string? description = null)
    {
        var songListInVkFormat = CreateSongListVkFormat(songList);
        var playlist = vkApi.Audio.CreatePlaylist(GetUserId(), playListName, description, songListInVkFormat);
    }

    private IEnumerable<string> CreateSongListVkFormat(IEnumerable<Audio> songCollection)
    {
        foreach (var song in songCollection)
            yield return $"{GetUserId()}_{song.Id},";
    }
}

public enum AuthorizationResult
{
    Success,
    WrongLoginOrPassword,
    WrongCode2FA,
    TooManyLoginAttempts,
    UnknownException,
    Need2FA,
}