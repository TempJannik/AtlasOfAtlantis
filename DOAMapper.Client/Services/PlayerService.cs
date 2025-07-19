using System.Net.Http.Json;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Shared.Services;

namespace DOAMapper.Client.Services;

public class PlayerService : IPlayerService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationService _authService;

    public PlayerService(HttpClient httpClient, IAuthenticationService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<PagedResult<PlayerDto>> SearchPlayersAsync(string query, string realmId, DateTime date, int page, int pageSize)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedResult<PlayerDto>>(
            $"api/players/search?query={Uri.EscapeDataString(query)}&realmId={Uri.EscapeDataString(realmId)}&date={date:yyyy-MM-dd}&page={page}&size={pageSize}");
        return response ?? new PagedResult<PlayerDto>();
    }

    public async Task<PlayerDetailDto?> GetPlayerAsync(string playerId, string realmId, DateTime date)
    {
        return await _httpClient.GetFromJsonAsync<PlayerDetailDto>(
            $"api/players/{Uri.EscapeDataString(realmId)}/{Uri.EscapeDataString(playerId)}?date={date:yyyy-MM-dd}");
    }

    public async Task<List<TileDto>> GetPlayerTilesAsync(string playerId, string realmId, DateTime date)
    {
        var response = await _httpClient.GetFromJsonAsync<List<TileDto>>(
            $"api/players/{Uri.EscapeDataString(realmId)}/{Uri.EscapeDataString(playerId)}/tiles?date={date:yyyy-MM-dd}");
        return response ?? new List<TileDto>();
    }

    public async Task<List<HistoryEntryDto<PlayerDto>>> GetPlayerHistoryAsync(string playerId, string realmId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/players/{Uri.EscapeDataString(realmId)}/{Uri.EscapeDataString(playerId)}/history");
        await AddAuthHeadersAsync(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<HistoryEntryDto<PlayerDto>>>();
        return result ?? new List<HistoryEntryDto<PlayerDto>>();
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync(string realmId)
    {
        var response = await _httpClient.GetFromJsonAsync<List<DateTime>>($"api/players/dates?realmId={Uri.EscapeDataString(realmId)}");
        return response ?? new List<DateTime>();
    }

    private async Task AddAuthHeadersAsync(HttpRequestMessage request)
    {
        var isAuthenticated = await _authService.IsAuthenticatedAsync();
        if (isAuthenticated)
        {
            var isAdmin = await _authService.IsAdminAsync();
            if (isAdmin)
            {
                var adminPassword = _authService.GetAdminPassword();
                if (!string.IsNullOrEmpty(adminPassword))
                {
                    request.Headers.Add("X-Admin-Password", adminPassword);
                }
            }
        }
    }
}
