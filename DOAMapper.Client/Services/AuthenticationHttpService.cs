using DOAMapper.Shared.Models.Authentication;
using System.Net.Http.Json;

namespace DOAMapper.Client.Services;

public interface IAuthenticationHttpService
{
    Task<LoginResponse> LoginAsync(string password);
}

public class AuthenticationHttpService : IAuthenticationHttpService
{
    private readonly HttpClient _httpClient;

    public AuthenticationHttpService(HttpClient httpClient)
    {
        _httpClient = httpClient;
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
            var loginRequest = new LoginRequest { Password = password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginRequest);
            
            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                return loginResponse ?? new LoginResponse
                {
                    Success = false,
                    Message = "Invalid response from server"
                };
            }
            
            return new LoginResponse
            {
                Success = false,
                Message = "Invalid password"
            };
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
}
