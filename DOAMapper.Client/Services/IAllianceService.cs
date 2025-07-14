using DOAMapper.Shared.Models.DTOs;

namespace DOAMapper.Client.Services;

public interface IAllianceService
{
    Task<PagedResult<AllianceDto>> GetAlliancesAsync(string realmId, DateTime date, int page, int pageSize);
    Task<PagedResult<AllianceDto>> SearchAlliancesAsync(string query, string realmId, DateTime date, int page, int pageSize);
    Task<AllianceDto?> GetAllianceAsync(string allianceId, string realmId, DateTime date);
    Task<PagedResult<PlayerDto>> GetAllianceMembersAsync(string allianceId, string realmId, DateTime date, int page, int pageSize);
    Task<List<TileDto>> GetAllianceTilesAsync(string allianceId, string realmId, DateTime date);
    Task<List<HistoryEntryDto<AllianceDto>>> GetAllianceHistoryAsync(string allianceId, string realmId);
    Task<List<DateTime>> GetAvailableDatesAsync(string realmId);
}
