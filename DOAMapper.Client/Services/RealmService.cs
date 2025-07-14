using System.Net.Http.Json;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Shared.Services;

namespace DOAMapper.Client.Services;

public class RealmService : IRealmService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationService _authService;

    public RealmService(HttpClient httpClient, IAuthenticationService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<List<RealmDto>> GetAvailableRealmsAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<List<RealmDto>>("api/realm");
        return response ?? new List<RealmDto>();
    }

    public async Task<RealmDto?> GetRealmAsync(string realmId)
    {
        return await _httpClient.GetFromJsonAsync<RealmDto>($"api/realm/{Uri.EscapeDataString(realmId)}");
    }

    public async Task<RealmDto> CreateRealmAsync(string realmId, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/realm");
        await AddAuthHeadersAsync(request);

        var payload = new { RealmId = realmId, Name = name };
        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<RealmDto>();
        return result ?? throw new InvalidOperationException("Failed to create realm");
    }

    public async Task<RealmDto?> UpdateRealmAsync(string realmId, string name, bool isActive)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"api/realm/{Uri.EscapeDataString(realmId)}");
        await AddAuthHeadersAsync(request);

        var payload = new { Name = name, IsActive = isActive };
        request.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RealmDto>();
    }

    public async Task<bool> DeleteRealmAsync(string realmId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"api/realm/{Uri.EscapeDataString(realmId)}");
        await AddAuthHeadersAsync(request);

        var response = await _httpClient.SendAsync(request);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        response.EnsureSuccessStatusCode();
        return true;
    }



    public async Task<bool> RealmExistsAsync(string realmId)
    {
        try
        {
            var realm = await GetRealmAsync(realmId);
            return realm != null;
        }
        catch
        {
            return false;
        }
    }

    private async Task AddAuthHeadersAsync(HttpRequestMessage request)
    {
        var isAdmin = await _authService.IsAdminAsync();
        if (isAdmin)
        {
            var adminPassword = _authService.GetAdminPassword();
            request.Headers.Add("X-Admin-Password", adminPassword);
        }
    }
}
