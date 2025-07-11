using DOAMapper.Shared.Models.DTOs;

namespace DOAMapper.Services.Interfaces;

public interface IMapService
{
    Task<List<TileDto>> GetRegionTilesAsync(int x1, int y1, int x2, int y2, DateTime date);
    Task<TileDto?> GetTileAsync(int x, int y, DateTime date);
    Task<List<HistoryEntryDto<TileDto>>> GetTileHistoryAsync(int x, int y);
    Task<Dictionary<string, int>> GetTileStatisticsAsync(DateTime date);
    Task<List<DateTime>> GetAvailableDatesAsync();
}
