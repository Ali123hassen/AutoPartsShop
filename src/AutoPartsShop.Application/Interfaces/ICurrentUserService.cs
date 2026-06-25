namespace AutoPartsShop.Application.Interfaces;

public interface ICurrentUserService
{
    int? UserId { get; }
    string? Username { get; }
    void SetUser(int? userId, string? username);
    void Clear();
}

public class CurrentUserService : ICurrentUserService
{
    public int? UserId { get; private set; }
    public string? Username { get; private set; }

    public void SetUser(int? userId, string? username)
    {
        UserId = userId;
        Username = username;
    }

    public void Clear()
    {
        UserId = null;
        Username = null;
    }
}
