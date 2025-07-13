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
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("Searching players with query '{Query}' for date {Date}, page {Page}, size {PageSize}",
            query, utcDate, page, pageSize);

        _logger.LogInformation("🔍 PLAYER QUERY: Querying players for date {QueryDate} (UTC: {UtcDate})", date, utcDate);

        var playersQuery = _context.Players
            .Where(p => p.ValidFrom <= utcDate && (p.ValidTo == null || p.ValidTo > utcDate));

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
            .OrderByDescending(p => p.Might)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation("🎯 PLAYER QUERY: Found {Count} players for date {QueryDate}. Sample ValidTo values: {SampleValidTo}",
            totalCount, utcDate,
            string.Join(", ", players.Take(3).Select(p => $"{p.Name}[ValidTo:{p.ValidTo}]")));

        // Load alliance information for players that have alliance IDs
        foreach (var player in players.Where(p => !string.IsNullOrEmpty(p.AllianceId)))
        {
            player.Alliance = await _context.Alliances
                .Where(a => a.AllianceId == player.AllianceId &&
                           a.ValidFrom <= utcDate &&
                           (a.ValidTo == null || a.ValidTo > utcDate))
                .OrderByDescending(a => a.ValidFrom)  // Get the most recent record that's valid for this date
                .FirstOrDefaultAsync();
        }

        var playerDtos = _mapper.Map<List<PlayerDto>>(players);
        
        // Set the data date for each player
        foreach (var dto in playerDtos)
        {
            dto.DataDate = utcDate;
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
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("Getting player {PlayerId} for date {Date}", playerId, utcDate);

        var player = await _context.Players
            .Where(p => p.PlayerId == playerId &&
                       p.ValidFrom <= utcDate &&
                       (p.ValidTo == null || p.ValidTo > utcDate))
            .OrderByDescending(p => p.ValidFrom)  // Get the most recent record that's valid for this date
            .FirstOrDefaultAsync();

        if (player == null)
        {
            _logger.LogWarning("Player {PlayerId} not found for date {Date}", playerId, utcDate);
            return null;
        }

        // Load tiles manually since navigation properties are ignored in EF configuration
        var tiles = await _context.Tiles
            .Where(t => t.PlayerId == player.PlayerId &&
                       t.ValidFrom <= utcDate &&
                       (t.ValidTo == null || t.ValidTo > utcDate))
            .ToListAsync();
        player.Tiles = tiles;

        // Load the alliance if the player has an alliance ID
        if (!string.IsNullOrEmpty(player.AllianceId))
        {
            player.Alliance = await _context.Alliances
                .Where(a => a.AllianceId == player.AllianceId &&
                           a.ValidFrom <= utcDate &&
                           (a.ValidTo == null || a.ValidTo > utcDate))
                .OrderByDescending(a => a.ValidFrom)  // Get the most recent record that's valid for this date
                .FirstOrDefaultAsync();
        }

        var playerDetail = _mapper.Map<PlayerDetailDto>(player);
        playerDetail.DataDate = utcDate;

        _logger.LogInformation("Found player {PlayerName} with {TileCount} tiles", player.Name, player.Tiles.Count);

        return playerDetail;
    }

    public async Task<List<TileDto>> GetPlayerTilesAsync(string playerId, DateTime date)
    {
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("Getting tiles for player {PlayerId} for date {Date}", playerId, utcDate);

        var tiles = await _context.Tiles
            .Where(t => t.PlayerId == playerId &&
                       t.ValidFrom <= utcDate &&
                       (t.ValidTo == null || t.ValidTo > utcDate))
            .OrderBy(t => t.Type)
            .ThenBy(t => t.X)
            .ThenBy(t => t.Y)
            .ToListAsync();

        var tileDtos = _mapper.Map<List<TileDto>>(tiles);
        
        // Set the data date for each tile
        foreach (var dto in tileDtos)
        {
            dto.DataDate = utcDate;
        }

        _logger.LogInformation("Found {Count} tiles for player {PlayerId}", tiles.Count, playerId);

        return tileDtos;
    }

    public async Task<List<HistoryEntryDto<PlayerDto>>> GetPlayerHistoryAsync(string playerId)
    {
        _logger.LogInformation("Getting history for player {PlayerId}", playerId);

        var playerHistory = await _context.Players
            .Where(p => p.PlayerId == playerId)
            .OrderByDescending(p => p.ValidFrom)
            .ToListAsync();

        var historyEntries = new List<HistoryEntryDto<PlayerDto>>();

        for (int i = 0; i < playerHistory.Count; i++)
        {
            var player = playerHistory[i];
            var playerDto = _mapper.Map<PlayerDto>(player);

            string changeType = "Added";
            var changeDetails = new List<string>();

            if (i < playerHistory.Count - 1)
            {
                var previousPlayer = playerHistory[i + 1];

                // Check basic player changes
                var hasBasicChanges = false;
                if (player.Name != previousPlayer.Name)
                {
                    changeDetails.Add($"Name changed from '{previousPlayer.Name}' to '{player.Name}'");
                    hasBasicChanges = true;
                }
                if (player.Might != previousPlayer.Might)
                {
                    changeDetails.Add($"Might changed from {previousPlayer.Might:N0} to {player.Might:N0}");
                    hasBasicChanges = true;
                }
                if (player.CityName != previousPlayer.CityName)
                {
                    changeDetails.Add($"City name changed from '{previousPlayer.CityName}' to '{player.CityName}'");
                    hasBasicChanges = true;
                }
                if (player.AllianceId != previousPlayer.AllianceId)
                {
                    var oldAllianceName = previousPlayer.Alliance?.Name ?? "None";
                    var newAllianceName = player.Alliance?.Name ?? "None";
                    changeDetails.Add($"Alliance changed from '{oldAllianceName}' to '{newAllianceName}'");
                    hasBasicChanges = true;
                }

                // Check for city coordinate and wilderness changes by analyzing tile data
                await AnalyzeTileChangesForPlayerAsync(playerId, player.ValidFrom, previousPlayer.ValidFrom, changeDetails);

                if (hasBasicChanges || changeDetails.Count > 0)
                {
                    changeType = "Modified";
                }
            }

            if (!player.IsActive)
            {
                changeType = "Removed";
            }

            // Create a custom change type that includes details
            var displayChangeType = changeType;
            if (changeDetails.Any())
            {
                displayChangeType = $"{changeType}: {string.Join("; ", changeDetails)}";
            }

            historyEntries.Add(new HistoryEntryDto<PlayerDto>
            {
                Data = playerDto,
                ValidFrom = player.ValidFrom,
                ValidTo = player.ValidTo,
                ChangeType = displayChangeType
            });
        }

        _logger.LogInformation("Found {Count} history entries for player {PlayerId}", historyEntries.Count, playerId);

        return historyEntries;
    }

    private async Task AnalyzeTileChangesForPlayerAsync(string playerId, DateTime currentDate, DateTime previousDate, List<string> changeDetails)
    {
        // Get tiles for both dates
        var currentTiles = await _context.Tiles
            .Where(t => t.PlayerId == playerId &&
                       t.ValidFrom <= currentDate &&
                       (t.ValidTo == null || t.ValidTo > currentDate))
            .ToListAsync();

        var previousTiles = await _context.Tiles
            .Where(t => t.PlayerId == playerId &&
                       t.ValidFrom <= previousDate &&
                       (t.ValidTo == null || t.ValidTo > previousDate))
            .ToListAsync();

        // Check for city coordinate changes
        var currentCityTile = currentTiles.FirstOrDefault(t => t.Type == "City");
        var previousCityTile = previousTiles.FirstOrDefault(t => t.Type == "City");

        if (currentCityTile != null && previousCityTile != null)
        {
            if (currentCityTile.X != previousCityTile.X || currentCityTile.Y != previousCityTile.Y)
            {
                changeDetails.Add($"City moved from ({previousCityTile.X},{previousCityTile.Y}) to ({currentCityTile.X},{currentCityTile.Y})");
            }
        }

        // Check for wilderness changes
        var currentWilderness = currentTiles.Where(t => t.Type != "City").ToList();
        var previousWilderness = previousTiles.Where(t => t.Type != "City").ToList();

        var currentWildernessKeys = currentWilderness.Select(t => $"{t.X},{t.Y}").ToHashSet();
        var previousWildernessKeys = previousWilderness.Select(t => $"{t.X},{t.Y}").ToHashSet();

        var gainedWilderness = currentWilderness.Where(t => !previousWildernessKeys.Contains($"{t.X},{t.Y}")).ToList();
        var lostWilderness = previousWilderness.Where(t => !currentWildernessKeys.Contains($"{t.X},{t.Y}")).ToList();

        if (gainedWilderness.Any())
        {
            var gainedDetails = gainedWilderness.Select(t => $"{t.Type} at ({t.X},{t.Y})").ToList();
            changeDetails.Add($"Gained {gainedWilderness.Count} wilderness: {string.Join(", ", gainedDetails)}");
        }

        if (lostWilderness.Any())
        {
            var lostDetails = lostWilderness.Select(t => $"{t.Type} at ({t.X},{t.Y})").ToList();
            changeDetails.Add($"Lost {lostWilderness.Count} wilderness: {string.Join(", ", lostDetails)}");
        }
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
