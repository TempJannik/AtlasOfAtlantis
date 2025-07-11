using AutoMapper;
using DOAMapper.Data;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DOAMapper.Services;

public class PlayerService : IPlayerService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(ApplicationDbContext context, IMapper mapper, ILogger<PlayerService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResult<PlayerDto>> SearchPlayersAsync(string query, DateTime date, int page, int pageSize)
    {
        _logger.LogInformation("Searching players with query '{Query}' for date {Date}, page {Page}, size {PageSize}", 
            query, date, page, pageSize);

        var playersQuery = _context.Players
            .Where(p => p.IsActive && p.ValidFrom <= date && (p.ValidTo == null || p.ValidTo > date));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLower();
            playersQuery = playersQuery.Where(p =>
                p.Name.ToLower().Contains(lowerQuery) ||
                p.PlayerId.ToLower().Contains(lowerQuery) ||
                p.CityName.ToLower().Contains(lowerQuery));
        }

        var totalCount = await playersQuery.CountAsync();

        var players = await playersQuery
            .Include(p => p.Alliance)
            .OrderByDescending(p => p.Might)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var playerDtos = _mapper.Map<List<PlayerDto>>(players);
        
        // Set the data date for each player
        foreach (var dto in playerDtos)
        {
            dto.DataDate = date;
        }

        _logger.LogInformation("Found {Count} players matching query '{Query}'", totalCount, query);

        return new PagedResult<PlayerDto>
        {
            Items = playerDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PlayerDetailDto?> GetPlayerAsync(string playerId, DateTime date)
    {
        _logger.LogInformation("Getting player {PlayerId} for date {Date}", playerId, date);

        var player = await _context.Players
            .Include(p => p.Alliance)
            .Include(p => p.Tiles.Where(t => t.IsActive && t.ValidFrom <= date && (t.ValidTo == null || t.ValidTo > date)))
            .FirstOrDefaultAsync(p => p.PlayerId == playerId && 
                                   p.IsActive && 
                                   p.ValidFrom <= date && 
                                   (p.ValidTo == null || p.ValidTo > date));

        if (player == null)
        {
            _logger.LogWarning("Player {PlayerId} not found for date {Date}", playerId, date);
            return null;
        }

        var playerDetail = _mapper.Map<PlayerDetailDto>(player);
        playerDetail.DataDate = date;

        _logger.LogInformation("Found player {PlayerName} with {TileCount} tiles", player.Name, player.Tiles.Count);

        return playerDetail;
    }

    public async Task<List<TileDto>> GetPlayerTilesAsync(string playerId, DateTime date)
    {
        _logger.LogInformation("Getting tiles for player {PlayerId} for date {Date}", playerId, date);

        var tiles = await _context.Tiles
            .Include(t => t.Player)
            .Include(t => t.Alliance)
            .Where(t => t.PlayerId == playerId && 
                       t.IsActive && 
                       t.ValidFrom <= date && 
                       (t.ValidTo == null || t.ValidTo > date))
            .OrderBy(t => t.Type)
            .ThenBy(t => t.X)
            .ThenBy(t => t.Y)
            .ToListAsync();

        var tileDtos = _mapper.Map<List<TileDto>>(tiles);
        
        // Set the data date for each tile
        foreach (var dto in tileDtos)
        {
            dto.DataDate = date;
        }

        _logger.LogInformation("Found {Count} tiles for player {PlayerId}", tiles.Count, playerId);

        return tileDtos;
    }

    public async Task<List<HistoryEntryDto<PlayerDto>>> GetPlayerHistoryAsync(string playerId)
    {
        _logger.LogInformation("Getting history for player {PlayerId}", playerId);

        var playerHistory = await _context.Players
            .Include(p => p.Alliance)
            .Where(p => p.PlayerId == playerId)
            .OrderByDescending(p => p.ValidFrom)
            .ToListAsync();

        var historyEntries = new List<HistoryEntryDto<PlayerDto>>();

        for (int i = 0; i < playerHistory.Count; i++)
        {
            var player = playerHistory[i];
            var playerDto = _mapper.Map<PlayerDto>(player);
            
            string changeType = "Added";
            if (i < playerHistory.Count - 1)
            {
                var previousPlayer = playerHistory[i + 1];
                if (player.Name != previousPlayer.Name || 
                    player.CityName != previousPlayer.CityName ||
                    player.AllianceId != previousPlayer.AllianceId)
                {
                    changeType = "Modified";
                }
            }

            if (!player.IsActive)
            {
                changeType = "Removed";
            }

            historyEntries.Add(new HistoryEntryDto<PlayerDto>
            {
                Data = playerDto,
                ValidFrom = player.ValidFrom,
                ValidTo = player.ValidTo,
                ChangeType = changeType
            });
        }

        _logger.LogInformation("Found {Count} history entries for player {PlayerId}", historyEntries.Count, playerId);

        return historyEntries;
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync()
    {
        return await _context.ImportSessions
            .Where(s => s.Status == DOAMapper.Shared.Models.Enums.ImportStatus.Completed)
            .Select(s => s.ImportDate.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();
    }
}
