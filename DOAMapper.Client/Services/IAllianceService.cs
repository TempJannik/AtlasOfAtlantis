using DOAMapper.Shared.Models.DTOs;

namespace DOAMapper.Client.Services;

public interface IAllianceService
{
    Task<PagedResult<AllianceDto>> GetAlliancesAsync(DateTime date, int page, int pageSize);
    Task<PagedResult<AllianceDto>> SearchAlliancesAsync(string query, DateTime date, int page, int pageSize);
    Task<AllianceDto?> GetAllianceAsync(string allianceId, DateTime date);
    Task<PagedResult<PlayerDto>> GetAllianceMembersAsync(string allianceId, DateTime date, int page, int pageSize);
    Task<List<TileDto>> GetAllianceTilesAsync(string allianceId, DateTime date);
    Task<List<HistoryEntryDto<AllianceDto>>> GetAllianceHistoryAsync(string allianceId);
    Task<List<DateTime>> GetAvailableDatesAsync();
}
