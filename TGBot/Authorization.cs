namespace TGBot;

public class Authorization
{
    public string? Login { get; private set; } = null;
    public string? Password { get; private set; } = null;
    public string? Code { get; private set; } = null;

    // заменяют встроенные публичные сеттеры, чтобы можно было устанавливать только не-null-овые значение
    public void SetLogin(string login) => Login = login;
    public void SetPassword(string password) => Password = password;
    public void SetCode(string code) => Code = code;
}