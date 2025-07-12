using DOAMapper.Shared.Models.Authentication;
using DOAMapper.Shared.Services;
using Microsoft.JSInterop;
using System.Text.Json;

namespace DOAMapper.Client.Services;

public class AuthenticationService : IAuthenticationService
{
    private const string UserPassword = "mm25";
    private const string AdminPassword = "accutane";
    private const string StorageKey = "doamapper_auth_state";

    private readonly IJSRuntime _jsRuntime;
    private AuthenticationState _currentState = AuthenticationState.Unauthenticated;
    private bool _initialized = false;

    public event Action<AuthenticationState>? AuthenticationStateChanged;

    public AuthenticationService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var storedState = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(storedState))
            {
                var authState = JsonSerializer.Deserialize<AuthenticationState>(storedState);
                if (authState != null)
                {
                    _currentState = authState;
                }
            }
        }
        catch (Exception)
        {
            // If there's an error reading from storage, use default unauthenticated state
            _currentState = AuthenticationState.Unauthenticated;
        }

        _initialized = true;
    }

    private async Task SaveStateAsync()
    {
        try
        {
            var stateJson = JsonSerializer.Serialize(_currentState);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, stateJson);
        }
        catch (Exception)
        {
            // If there's an error saving to storage, continue without persistence
        }
    }

    public async Task<LoginResponse> LoginAsync(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Password is required"
            };
        }

        if (password == AdminPassword)
        {
            _currentState = AuthenticationState.CreateAdmin();
            await SaveStateAsync();
            AuthenticationStateChanged?.Invoke(_currentState);

            return new LoginResponse
            {
                Success = true,
                Role = UserRole.Admin,
                Message = "Admin login successful"
            };
        }

        if (password == UserPassword)
        {
            _currentState = AuthenticationState.CreateUser();
            await SaveStateAsync();
            AuthenticationStateChanged?.Invoke(_currentState);

            return new LoginResponse
            {
                Success = true,
                Role = UserRole.User,
                Message = "User login successful"
            };
        }

        return new LoginResponse
        {
            Success = false,
            Message = "Invalid password"
        };
    }

    public async Task LogoutAsync()
    {
        _currentState = AuthenticationState.Unauthenticated;
        await SaveStateAsync();
        AuthenticationStateChanged?.Invoke(_currentState);
    }

    public async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        await InitializeAsync();
        return _currentState;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        await InitializeAsync();
        return _currentState.IsAuthenticated;
    }

    public async Task<bool> IsAdminAsync()
    {
        await InitializeAsync();
        return _currentState.IsAdmin;
    }

    public async Task<UserRole> GetUserRoleAsync()
    {
        await InitializeAsync();
        return _currentState.Role;
    }

    public string GetAdminPassword()
    {
        return AdminPassword;
    }

    public string GetUserPassword()
    {
        return UserPassword;
    }
}
