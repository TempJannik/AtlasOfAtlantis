using DOAMapper.Shared.Models.Authentication;
using DOAMapper.Shared.Services;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;

namespace DOAMapper.Client.Services;

public class AuthenticationService : IAuthenticationService
{
    private const string StorageKey = "doamapper_auth_state";
    private const string PasswordStorageKey = "doamapper_auth_pwd";

    private readonly IJSRuntime _jsRuntime;
    private readonly IServiceProvider _serviceProvider;
    private AuthenticationState _currentState = AuthenticationState.Unauthenticated;
    private bool _initialized = false;
    private string? _storedPassword; // Store password for API calls
    private readonly SemaphoreSlim _initializationSemaphore = new(1, 1);

    public event Action<AuthenticationState>? AuthenticationStateChanged;

    public AuthenticationService(IJSRuntime jsRuntime, IServiceProvider serviceProvider)
    {
        _jsRuntime = jsRuntime;
        _serviceProvider = serviceProvider;
    }

    private async Task InitializeAsync()
    {
        if (_initialized) return;

        await _initializationSemaphore.WaitAsync();
        try
        {
            if (_initialized) return; // Double-check after acquiring lock

            // Restore authentication state
            var storedState = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(storedState))
            {
                var authState = JsonSerializer.Deserialize<AuthenticationState>(storedState);
                if (authState != null)
                {
                    _currentState = authState;
                }
            }

            // Restore password if authenticated
            if (_currentState.IsAuthenticated)
            {
                var storedPassword = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", PasswordStorageKey);
                if (!string.IsNullOrEmpty(storedPassword))
                {
                    // Simple base64 decode for basic obfuscation
                    try
                    {
                        var passwordBytes = Convert.FromBase64String(storedPassword);
                        _storedPassword = System.Text.Encoding.UTF8.GetString(passwordBytes);
                    }
                    catch
                    {
                        // If decoding fails, clear authentication state
                        _currentState = AuthenticationState.Unauthenticated;
                        _storedPassword = null;
                    }
                }
                else
                {
                    // If no password stored but state says authenticated, clear authentication
                    _currentState = AuthenticationState.Unauthenticated;
                }
            }
        }
        catch (Exception)
        {
            // If there's an error reading from storage, use default unauthenticated state
            _currentState = AuthenticationState.Unauthenticated;
            _storedPassword = null;
        }
        finally
        {
            _initialized = true;
            _initializationSemaphore.Release();
        }
    }

    private async Task SaveStateAsync()
    {
        try
        {
            var stateJson = JsonSerializer.Serialize(_currentState);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey, stateJson);

            // Save password if authenticated (with basic obfuscation)
            if (_currentState.IsAuthenticated && !string.IsNullOrEmpty(_storedPassword))
            {
                var passwordBytes = System.Text.Encoding.UTF8.GetBytes(_storedPassword);
                var encodedPassword = Convert.ToBase64String(passwordBytes);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", PasswordStorageKey, encodedPassword);
            }
            else
            {
                // Clear password if not authenticated
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", PasswordStorageKey);
            }
        }
        catch (Exception)
        {
            // If there's an error saving to storage, continue without persistence
        }
    }

    private async Task ClearStorageAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", StorageKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", PasswordStorageKey);
        }
        catch (Exception)
        {
            // If there's an error clearing storage, continue
        }
    }

    /// <summary>
    /// Ensures the authentication service is initialized and ready to use.
    /// This should be called during app startup to restore authentication state from localStorage.
    /// </summary>
    public async Task EnsureInitializedAsync()
    {
        await InitializeAsync();
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

        try
        {
            // Use scoped HTTP service for authentication
            using var scope = _serviceProvider.CreateScope();
            var authHttpService = scope.ServiceProvider.GetRequiredService<IAuthenticationHttpService>();
            var loginResponse = await authHttpService.LoginAsync(password);

            if (loginResponse.Success)
            {
                // Store password for API calls and update state
                _storedPassword = password;

                if (loginResponse.Role == UserRole.Admin)
                {
                    _currentState = AuthenticationState.CreateAdmin();
                }
                else
                {
                    _currentState = AuthenticationState.CreateUser();
                }

                // Save authentication state (but not password) to localStorage
                await SaveStateAsync();
                AuthenticationStateChanged?.Invoke(_currentState);
            }

            return loginResponse;
        }
        catch (Exception)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "Authentication service unavailable"
            };
        }
    }

    public async Task LogoutAsync()
    {
        _currentState = AuthenticationState.Unauthenticated;
        _storedPassword = null; // Clear stored password
        await ClearStorageAsync(); // Clear both state and password from storage
        AuthenticationStateChanged?.Invoke(_currentState);
    }

    public async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
        return _currentState;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
        return _currentState.IsAuthenticated;
    }

    public async Task<bool> IsAdminAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
        return _currentState.IsAdmin;
    }

    public async Task<UserRole> GetUserRoleAsync()
    {
        await InitializeAsync();
        return _currentState.Role;
    }

    public string GetAdminPassword()
    {
        return _storedPassword ?? string.Empty;
    }

    public string GetUserPassword()
    {
        return _storedPassword ?? string.Empty;
    }
}
