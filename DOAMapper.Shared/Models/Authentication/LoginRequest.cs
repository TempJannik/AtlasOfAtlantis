namespace DOAMapper.Shared.Models.Authentication;

public class LoginRequest
{
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public bool Success { get; set; }
    public UserRole Role { get; set; } = UserRole.None;
    public string Message { get; set; } = string.Empty;
}
