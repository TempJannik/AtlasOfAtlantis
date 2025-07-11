using System.Net.Http.Json;
using DOAMapper.Shared.Models.DTOs;

namespace DOAMapper.Client.Services;

public class AllianceService : IAllianceService
{
    private readonly HttpClient _httpClient;

    public AllianceService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<AllianceDto>> GetAlliancesAsync(DateTime date, int page, int pageSize)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedResult<AllianceDto>>(
            $"api/alliances?date={date:yyyy-MM-dd}&page={page}&size={pageSize}");
        return response ?? new PagedResult<AllianceDto>();
    }

    public async Task<PagedResult<AllianceDto>> SearchAlliancesAsync(string query, DateTime date, int page, int pageSize)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedResult<AllianceDto>>(
            $"api/alliances/search?query={Uri.EscapeDataString(query)}&date={date:yyyy-MM-dd}&page={page}&size={pageSize}");
        return response ?? new PagedResult<AllianceDto>();
    }

    public async Task<AllianceDto?> GetAllianceAsync(string allianceId, DateTime date)
    {
        return await _httpClient.GetFromJsonAsync<AllianceDto>(
            $"api/alliances/{Uri.EscapeDataString(allianceId)}?date={date:yyyy-MM-dd}");
    }

    public async Task<PagedResult<PlayerDto>> GetAllianceMembersAsync(string allianceId, DateTime date, int page, int pageSize)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedResult<PlayerDto>>(
            $"api/alliances/{Uri.EscapeDataString(allianceId)}/members?date={date:yyyy-MM-dd}&page={page}&size={pageSize}");
        return response ?? new PagedResult<PlayerDto>();
    }

    public async Task<List<TileDto>> GetAllianceTilesAsync(string allianceId, DateTime date)
    {
        var response = await _httpClient.GetFromJsonAsync<List<TileDto>>(
            $"api/alliances/{Uri.EscapeDataString(allianceId)}/tiles?date={date:yyyy-MM-dd}");
        return response ?? new List<TileDto>();
    }

    public async Task<List<HistoryEntryDto<AllianceDto>>> GetAllianceHistoryAsync(string allianceId)
    {
        var response = await _httpClient.GetFromJsonAsync<List<HistoryEntryDto<AllianceDto>>>(
            $"api/alliances/{Uri.EscapeDataString(allianceId)}/history");
        return response ?? new List<HistoryEntryDto<AllianceDto>>();
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<List<DateTime>>("api/alliances/dates");
        return response ?? new List<DateTime>();
    }
}
