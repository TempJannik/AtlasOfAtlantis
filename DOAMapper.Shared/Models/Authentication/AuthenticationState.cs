namespace DOAMapper.Shared.Models.Authentication;

public class AuthenticationState
{
    public bool IsAuthenticated { get; set; }
    public UserRole Role { get; set; } = UserRole.None;
    public DateTime? LoginTime { get; set; }

    public bool IsAdmin => IsAuthenticated && Role == UserRole.Admin;
    public bool IsUser => IsAuthenticated && (Role == UserRole.User || Role == UserRole.Admin);

    public static AuthenticationState Unauthenticated => new()
    {
        IsAuthenticated = false,
        Role = UserRole.None,
        LoginTime = null
    };

    public static AuthenticationState CreateUser() => new()
    {
        IsAuthenticated = true,
        Role = UserRole.User,
        LoginTime = DateTime.UtcNow
    };

    public static AuthenticationState CreateAdmin() => new()
    {
        IsAuthenticated = true,
        Role = UserRole.Admin,
        LoginTime = DateTime.UtcNow
    };
}
