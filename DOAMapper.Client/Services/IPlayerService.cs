using DOAMapper.Shared.Models.DTOs;

namespace DOAMapper.Client.Services;

public interface IPlayerService
{
    Task<PagedResult<PlayerDto>> SearchPlayersAsync(string query, string realmId, DateTime date, int page, int pageSize);
    Task<PlayerDetailDto?> GetPlayerAsync(string playerId, string realmId, DateTime date);
    Task<List<TileDto>> GetPlayerTilesAsync(string playerId, string realmId, DateTime date);
    Task<List<HistoryEntryDto<PlayerDto>>> GetPlayerHistoryAsync(string playerId, string realmId);
    Task<List<DateTime>> GetAvailableDatesAsync(string realmId);
}
