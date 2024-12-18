namespace TGBot;

public class OldAuthorization
{
    public string? Login { get; private set; }
    public string? Password { get; private set; }
    public string? Code { get; private set; }
    public bool IsCorrectData { get => isCorrectData; }
    private bool isCorrectData = true;

    public void SetCorrectData(bool isCorrect) => isCorrectData = isCorrect;

    public void AddLogin(string login) => Login = login;
    
    public void AddPassword(string password) => Password = password;
    
    public void AddCode(string code) => Code = code;
    
    public void Reset() => Login = Password = Code = null;
    
    public override string ToString()
        => $"Login: {Login}\nPassword: {Password}\nCode 2FA: {Code}";
    
}