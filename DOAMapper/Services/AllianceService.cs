using AutoMapper;
using DOAMapper.Data;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DOAMapper.Services;

public class AllianceService : IAllianceService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<AllianceService> _logger;

    public AllianceService(ApplicationDbContext context, IMapper mapper, ILogger<AllianceService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<PagedResult<AllianceDto>> GetAlliancesAsync(DateTime date, int page, int pageSize)
    {
        _logger.LogInformation("Getting alliances for date {Date}, page {Page}, size {PageSize}", date, page, pageSize);

        var alliancesQuery = _context.Alliances
            .Where(a => a.IsActive && a.ValidFrom <= date && (a.ValidTo == null || a.ValidTo > date));

        var totalCount = await alliancesQuery.CountAsync();

        var alliances = await alliancesQuery
            .Include(a => a.Members.Where(m => m.IsActive && m.ValidFrom <= date && (m.ValidTo == null || m.ValidTo > date)))
            .OrderByDescending(a => a.Power)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var allianceDtos = _mapper.Map<List<AllianceDto>>(alliances);
        
        // Set the data date for each alliance
        foreach (var dto in allianceDtos)
        {
            dto.DataDate = date;
        }

        _logger.LogInformation("Found {Count} alliances for date {Date}", totalCount, date);

        return new PagedResult<AllianceDto>
        {
            Items = allianceDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PagedResult<AllianceDto>> SearchAlliancesAsync(string query, DateTime date, int page, int pageSize)
    {
        _logger.LogInformation("Searching alliances with query '{Query}' for date {Date}, page {Page}, size {PageSize}", 
            query, date, page, pageSize);

        var alliancesQuery = _context.Alliances
            .Where(a => a.IsActive && a.ValidFrom <= date && (a.ValidTo == null || a.ValidTo > date));

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLower();
            alliancesQuery = alliancesQuery.Where(a =>
                a.Name.ToLower().Contains(lowerQuery) ||
                a.AllianceId.ToLower().Contains(lowerQuery) ||
                a.OverlordName.ToLower().Contains(lowerQuery));
        }

        var totalCount = await alliancesQuery.CountAsync();

        var alliances = await alliancesQuery
            .Include(a => a.Members.Where(m => m.IsActive && m.ValidFrom <= date && (m.ValidTo == null || m.ValidTo > date)))
            .OrderByDescending(a => a.Power)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var allianceDtos = _mapper.Map<List<AllianceDto>>(alliances);
        
        // Set the data date for each alliance
        foreach (var dto in allianceDtos)
        {
            dto.DataDate = date;
        }

        _logger.LogInformation("Found {Count} alliances matching query '{Query}'", totalCount, query);

        return new PagedResult<AllianceDto>
        {
            Items = allianceDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AllianceDto?> GetAllianceAsync(string allianceId, DateTime date)
    {
        _logger.LogInformation("Getting alliance {AllianceId} for date {Date}", allianceId, date);

        var alliance = await _context.Alliances
            .Include(a => a.Members.Where(m => m.IsActive && m.ValidFrom <= date && (m.ValidTo == null || m.ValidTo > date)))
            .FirstOrDefaultAsync(a => a.AllianceId == allianceId && 
                                   a.IsActive && 
                                   a.ValidFrom <= date && 
                                   (a.ValidTo == null || a.ValidTo > date));

        if (alliance == null)
        {
            _logger.LogWarning("Alliance {AllianceId} not found for date {Date}", allianceId, date);
            return null;
        }

        var allianceDto = _mapper.Map<AllianceDto>(alliance);
        allianceDto.DataDate = date;

        _logger.LogInformation("Found alliance {AllianceName} with {MemberCount} members", 
            alliance.Name, alliance.Members.Count);

        return allianceDto;
    }

    public async Task<PagedResult<PlayerDto>> GetAllianceMembersAsync(string allianceId, DateTime date, int page, int pageSize)
    {
        _logger.LogInformation("Getting members for alliance {AllianceId} for date {Date}, page {Page}, size {PageSize}",
            allianceId, date, page, pageSize);

        var membersQuery = _context.Players
            .Include(p => p.Alliance)
            .Where(p => p.AllianceId == allianceId &&
                       p.IsActive &&
                       p.ValidFrom <= date &&
                       (p.ValidTo == null || p.ValidTo > date));

        var totalCount = await membersQuery.CountAsync();

        var members = await membersQuery
            .OrderByDescending(p => p.Might)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var memberDtos = _mapper.Map<List<PlayerDto>>(members);

        // Set the data date for each member
        foreach (var dto in memberDtos)
        {
            dto.DataDate = date;
        }

        _logger.LogInformation("Found {Count} total members for alliance {AllianceId}, returning page {Page}",
            totalCount, allianceId, page);

        return new PagedResult<PlayerDto>
        {
            Items = memberDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<List<TileDto>> GetAllianceTilesAsync(string allianceId, DateTime date)
    {
        _logger.LogInformation("Getting tiles for alliance {AllianceId} for date {Date}", allianceId, date);

        var tiles = await _context.Tiles
            .Include(t => t.Player)
            .Include(t => t.Alliance)
            .Where(t => t.AllianceId == allianceId && 
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

        _logger.LogInformation("Found {Count} tiles for alliance {AllianceId}", tiles.Count, allianceId);

        return tileDtos;
    }

    public async Task<List<HistoryEntryDto<AllianceDto>>> GetAllianceHistoryAsync(string allianceId)
    {
        _logger.LogInformation("Getting history for alliance {AllianceId}", allianceId);

        var allianceHistory = await _context.Alliances
            .Where(a => a.AllianceId == allianceId)
            .OrderByDescending(a => a.ValidFrom)
            .ToListAsync();

        var historyEntries = new List<HistoryEntryDto<AllianceDto>>();

        for (int i = 0; i < allianceHistory.Count; i++)
        {
            var alliance = allianceHistory[i];

            // Get member count for this alliance at this point in time
            var memberCount = await _context.Players
                .Where(p => p.AllianceId == allianceId &&
                           p.IsActive &&
                           p.ValidFrom <= alliance.ValidFrom &&
                           (p.ValidTo == null || p.ValidTo > alliance.ValidFrom))
                .CountAsync();

            var allianceDto = _mapper.Map<AllianceDto>(alliance);
            allianceDto.MemberCount = memberCount; // Override the member count with the correct historical value

            string changeType = "Added";
            if (i < allianceHistory.Count - 1)
            {
                var previousAlliance = allianceHistory[i + 1];
                if (alliance.Name != previousAlliance.Name ||
                    alliance.Power != previousAlliance.Power ||
                    alliance.OverlordName != previousAlliance.OverlordName ||
                    alliance.FortressLevel != previousAlliance.FortressLevel)
                {
                    changeType = "Modified";
                }
            }

            if (!alliance.IsActive)
            {
                changeType = "Removed";
            }

            historyEntries.Add(new HistoryEntryDto<AllianceDto>
            {
                Data = allianceDto,
                ValidFrom = alliance.ValidFrom,
                ValidTo = alliance.ValidTo,
                ChangeType = changeType
            });
        }

        _logger.LogInformation("Found {Count} history entries for alliance {AllianceId}", historyEntries.Count, allianceId);

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
