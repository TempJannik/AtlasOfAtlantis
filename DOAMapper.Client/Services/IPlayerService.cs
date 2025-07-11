using DOAMapper.Shared.Models.DTOs;

namespace DOAMapper.Client.Services;

public interface IPlayerService
{
    Task<PagedResult<PlayerDto>> SearchPlayersAsync(string query, DateTime date, int page, int pageSize);
    Task<PlayerDetailDto?> GetPlayerAsync(string playerId, DateTime date);
    Task<List<TileDto>> GetPlayerTilesAsync(string playerId, DateTime date);
    Task<List<HistoryEntryDto<PlayerDto>>> GetPlayerHistoryAsync(string playerId);
    Task<List<DateTime>> GetAvailableDatesAsync();
}
