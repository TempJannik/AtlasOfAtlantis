using System.Net.Http.Json;
using DOAMapper.Shared.Models.DTOs;

namespace DOAMapper.Client.Services;

public class PlayerService : IPlayerService
{
    private readonly HttpClient _httpClient;

    public PlayerService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PagedResult<PlayerDto>> SearchPlayersAsync(string query, DateTime date, int page, int pageSize)
    {
        var response = await _httpClient.GetFromJsonAsync<PagedResult<PlayerDto>>(
            $"api/players/search?query={Uri.EscapeDataString(query)}&date={date:yyyy-MM-dd}&page={page}&size={pageSize}");
        return response ?? new PagedResult<PlayerDto>();
    }

    public async Task<PlayerDetailDto?> GetPlayerAsync(string playerId, DateTime date)
    {
        return await _httpClient.GetFromJsonAsync<PlayerDetailDto>(
            $"api/players/{Uri.EscapeDataString(playerId)}?date={date:yyyy-MM-dd}");
    }

    public async Task<List<TileDto>> GetPlayerTilesAsync(string playerId, DateTime date)
    {
        var response = await _httpClient.GetFromJsonAsync<List<TileDto>>(
            $"api/players/{Uri.EscapeDataString(playerId)}/tiles?date={date:yyyy-MM-dd}");
        return response ?? new List<TileDto>();
    }

    public async Task<List<HistoryEntryDto<PlayerDto>>> GetPlayerHistoryAsync(string playerId)
    {
        var response = await _httpClient.GetFromJsonAsync<List<HistoryEntryDto<PlayerDto>>>(
            $"api/players/{Uri.EscapeDataString(playerId)}/history");
        return response ?? new List<HistoryEntryDto<PlayerDto>>();
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<List<DateTime>>("api/players/dates");
        return response ?? new List<DateTime>();
    }
}
