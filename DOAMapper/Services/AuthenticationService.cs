using DOAMapper.Shared.Models.Authentication;
using DOAMapper.Shared.Services;

namespace DOAMapper.Services;

public class AuthenticationService : IAuthenticationService
{
    private const string AdminPassword = "accutane";
    
    private AuthenticationState _currentState = AuthenticationState.Unauthenticated;
    
    public event Action<AuthenticationState>? AuthenticationStateChanged;

    public Task<LoginResponse> LoginAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return Task.FromResult(new LoginResponse
            {
                Success = false,
                Message = "Password is required"
            });
        }

        if (password == AdminPassword)
        {
            _currentState = AuthenticationState.CreateAdmin();
            AuthenticationStateChanged?.Invoke(_currentState);

            return Task.FromResult(new LoginResponse
            {
                Success = true,
                Role = UserRole.Admin,
                Message = "Admin login successful"
            });
        }

        return Task.FromResult(new LoginResponse
        {
            Success = false,
            Message = "Invalid password"
        });
    }

    public Task LogoutAsync()
    {
        _currentState = AuthenticationState.Unauthenticated;
        AuthenticationStateChanged?.Invoke(_currentState);
        return Task.CompletedTask;
    }

    public Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(_currentState);
    }

    public Task<bool> IsAuthenticatedAsync()
    {
        return Task.FromResult(_currentState.IsAuthenticated);
    }

    public Task<bool> IsAdminAsync()
    {
        return Task.FromResult(_currentState.IsAdmin);
    }

    public Task<UserRole> GetUserRoleAsync()
    {
        return Task.FromResult(_currentState.Role);
    }

    public bool ValidateAdminPassword(string password)
    {
        return password == AdminPassword;
    }

    public Task EnsureInitializedAsync()
    {
        // Server-side service doesn't need initialization
        return Task.CompletedTask;
    }

    public string GetAdminPassword()
    {
        return AdminPassword;
    }


}
