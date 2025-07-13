using AutoMapper;
using DOAMapper.Data;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DOAMapper.Services;

public class MapService : IMapService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<MapService> _logger;

    public MapService(ApplicationDbContext context, IMapper mapper, ILogger<MapService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<TileDto>> GetRegionTilesAsync(int x1, int y1, int x2, int y2, DateTime date)
    {
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("Getting tiles for region ({X1},{Y1}) to ({X2},{Y2}) for date {Date}",
            x1, y1, x2, y2, utcDate);

        // Ensure coordinates are in correct order
        var minX = Math.Min(x1, x2);
        var maxX = Math.Max(x1, x2);
        var minY = Math.Min(y1, y2);
        var maxY = Math.Max(y1, y2);

        // Limit region size to prevent excessive data retrieval
        const int maxRegionSize = 100; // 100x100 tiles max
        if ((maxX - minX) > maxRegionSize || (maxY - minY) > maxRegionSize)
        {
            _logger.LogWarning("Region size too large: {Width}x{Height}, limiting to {MaxSize}x{MaxSize}", 
                maxX - minX, maxY - minY, maxRegionSize, maxRegionSize);
            
            maxX = Math.Min(maxX, minX + maxRegionSize);
            maxY = Math.Min(maxY, minY + maxRegionSize);
        }

        var tiles = await _context.Tiles
            .Where(t => t.X >= minX && t.X <= maxX &&
                       t.Y >= minY && t.Y <= maxY &&
                       t.ValidFrom <= utcDate &&
                       (t.ValidTo == null || t.ValidTo > utcDate))
            .OrderBy(t => t.X)
            .ThenBy(t => t.Y)
            .ToListAsync();

        var tileDtos = _mapper.Map<List<TileDto>>(tiles);
        
        // Set the data date for each tile
        foreach (var dto in tileDtos)
        {
            dto.DataDate = utcDate;
        }

        _logger.LogInformation("Found {Count} tiles in region ({MinX},{MinY}) to ({MaxX},{MaxY})", 
            tiles.Count, minX, minY, maxX, maxY);

        return tileDtos;
    }

    public async Task<TileDto?> GetTileAsync(int x, int y, DateTime date)
    {
        _logger.LogInformation("Getting tile at ({X},{Y}) for date {Date}", x, y, date);

        var tile = await _context.Tiles
            .FirstOrDefaultAsync(t => t.X == x && t.Y == y &&
                                   t.ValidFrom <= date &&
                                   (t.ValidTo == null || t.ValidTo > date));

        if (tile == null)
        {
            _logger.LogWarning("Tile at ({X},{Y}) not found for date {Date}", x, y, date);
            return null;
        }

        var tileDto = _mapper.Map<TileDto>(tile);
        tileDto.DataDate = date;

        _logger.LogInformation("Found tile at ({X},{Y}): {Type} level {Level}", x, y, tile.Type, tile.Level);

        return tileDto;
    }

    public async Task<List<HistoryEntryDto<TileDto>>> GetTileHistoryAsync(int x, int y)
    {
        _logger.LogInformation("Getting history for tile at ({X},{Y})", x, y);

        var tileHistory = await _context.Tiles
            .Where(t => t.X == x && t.Y == y)
            .OrderByDescending(t => t.ValidFrom)
            .ToListAsync();

        var historyEntries = new List<HistoryEntryDto<TileDto>>();

        for (int i = 0; i < tileHistory.Count; i++)
        {
            var tile = tileHistory[i];
            var tileDto = _mapper.Map<TileDto>(tile);
            
            string changeType = "Added";
            if (i < tileHistory.Count - 1)
            {
                var previousTile = tileHistory[i + 1];
                if (tile.Type != previousTile.Type || 
                    tile.Level != previousTile.Level || 
                    tile.PlayerId != previousTile.PlayerId ||
                    tile.AllianceId != previousTile.AllianceId)
                {
                    changeType = "Modified";
                }
            }

            if (!tile.IsActive)
            {
                changeType = "Removed";
            }

            historyEntries.Add(new HistoryEntryDto<TileDto>
            {
                Data = tileDto,
                ValidFrom = tile.ValidFrom,
                ValidTo = tile.ValidTo,
                ChangeType = changeType
            });
        }

        _logger.LogInformation("Found {Count} history entries for tile at ({X},{Y})", historyEntries.Count, x, y);

        return historyEntries;
    }

    public async Task<Dictionary<string, int>> GetTileStatisticsAsync(DateTime date)
    {
        _logger.LogInformation("Getting tile statistics for date {Date}", date);

        var statistics = await _context.Tiles
            .Where(t => t.ValidFrom <= date &&
                       (t.ValidTo == null || t.ValidTo > date))
            .GroupBy(t => t.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count);

        _logger.LogInformation("Generated tile statistics for {TypeCount} tile types", statistics.Count);

        return statistics;
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync()
    {
        var dates = await _context.ImportSessions
            .Where(s => s.Status == DOAMapper.Shared.Models.Enums.ImportStatus.Completed)
            .Select(s => s.ImportDate.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        // Ensure all dates are UTC for PostgreSQL compatibility
        return dates.Select(d => DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToList();
    }
}
