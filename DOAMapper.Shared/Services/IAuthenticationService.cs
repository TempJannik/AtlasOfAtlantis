using DOAMapper.Shared.Models.Authentication;

namespace DOAMapper.Shared.Services;

public interface IAuthenticationService
{
    Task<LoginResponse> LoginAsync(string password);
    Task LogoutAsync();
    Task<AuthenticationState> GetAuthenticationStateAsync();
    Task<bool> IsAuthenticatedAsync();
    Task<bool> IsAdminAsync();
    Task<UserRole> GetUserRoleAsync();
    Task EnsureInitializedAsync();
    string GetAdminPassword();
    string GetUserPassword();
    event Action<AuthenticationState>? AuthenticationStateChanged;
}
