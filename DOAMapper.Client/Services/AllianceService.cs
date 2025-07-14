using System.Net.Http.Json;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Shared.Services;

namespace DOAMapper.Client.Services;

public class AllianceService : IAllianceService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationService _authService;

    public AllianceService(HttpClient httpClient, IAuthenticationService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<PagedResult<AllianceDto>> GetAlliancesAsync(string realmId, DateTime date, int page, int pageSize)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedResult<AllianceDto>>(
            $"api/alliances?realmId={Uri.EscapeDataString(realmId)}&date={date:yyyy-MM-dd}&page={page}&size={pageSize}");
        return response ?? new PagedResult<AllianceDto>();
    }

    public async Task<PagedResult<AllianceDto>> SearchAlliancesAsync(string query, string realmId, DateTime date, int page, int pageSize)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedResult<AllianceDto>>(
            $"api/alliances/search?query={Uri.EscapeDataString(query)}&realmId={Uri.EscapeDataString(realmId)}&date={date:yyyy-MM-dd}&page={page}&size={pageSize}");
        return response ?? new PagedResult<AllianceDto>();
    }

    public async Task<AllianceDto?> GetAllianceAsync(string allianceId, string realmId, DateTime date)
    {
        return await _httpClient.GetFromJsonAsync<AllianceDto>(
            $"api/alliances/{Uri.EscapeDataString(realmId)}/{Uri.EscapeDataString(allianceId)}?date={date:yyyy-MM-dd}");
    }

    public async Task<PagedResult<PlayerDto>> GetAllianceMembersAsync(string allianceId, string realmId, DateTime date, int page, int pageSize)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedResult<PlayerDto>>(
            $"api/alliances/{Uri.EscapeDataString(realmId)}/{Uri.EscapeDataString(allianceId)}/members?date={date:yyyy-MM-dd}&page={page}&size={pageSize}");
        return response ?? new PagedResult<PlayerDto>();
    }

    public async Task<List<TileDto>> GetAllianceTilesAsync(string allianceId, string realmId, DateTime date)
    {
        var response = await _httpClient.GetFromJsonAsync<List<TileDto>>(
            $"api/alliances/{Uri.EscapeDataString(realmId)}/{Uri.EscapeDataString(allianceId)}/tiles?date={date:yyyy-MM-dd}");
        return response ?? new List<TileDto>();
    }

    public async Task<List<HistoryEntryDto<AllianceDto>>> GetAllianceHistoryAsync(string allianceId, string realmId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/alliances/{Uri.EscapeDataString(realmId)}/{Uri.EscapeDataString(allianceId)}/history");
        await AddAuthHeadersAsync(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<HistoryEntryDto<AllianceDto>>>();
        return result ?? new List<HistoryEntryDto<AllianceDto>>();
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync(string realmId)
    {
        var response = await _httpClient.GetFromJsonAsync<List<DateTime>>($"api/alliances/dates?realmId={Uri.EscapeDataString(realmId)}");
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
            else
            {
                var userPassword = _authService.GetUserPassword();
                if (!string.IsNullOrEmpty(userPassword))
                {
                    request.Headers.Add("X-User-Password", userPassword);
                }
            }
        }
    }
}
