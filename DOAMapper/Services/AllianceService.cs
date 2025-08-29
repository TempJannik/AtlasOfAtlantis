using AutoMapper;
using DOAMapper.Data;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using DOAMapper.Shared.Models.Enums;


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

    public async Task<PagedResult<AllianceDto>> GetAlliancesAsync(string realmId, DateTime date, int page, int pageSize)
    {
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("Getting alliances for realm {RealmId}, date {Date}, page {Page}, size {PageSize}", realmId, utcDate, page, pageSize);

        // First get all valid alliances for the realm and date
        var validAlliances = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, as_ => as_.Session.RealmId, r => r.Id, (as_, r) => new { as_.Alliance, as_.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId &&
                         asr.Alliance.ValidFrom <= utcDate &&
                         (asr.Alliance.ValidTo == null || asr.Alliance.ValidTo > utcDate))
            .Select(asr => asr.Alliance)
            .ToListAsync();

        // Deduplicate in memory by taking the most recent record for each AllianceId
        var deduplicatedAlliances = validAlliances
            .GroupBy(a => a.AllianceId)
            .Select(g => g.OrderByDescending(a => a.ValidFrom).First())
            .ToList();

        var alliancesQuery = deduplicatedAlliances.AsQueryable();

        var totalCount = alliancesQuery.Count();

        var alliances = alliancesQuery
            .OrderByDescending(a => a.Power)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Load member information for each alliance to get accurate member counts
        foreach (var alliance in alliances)
        {
            var members = await _context.Players
                .Join(_context.ImportSessions, p => p.ImportSessionId, s => s.Id, (p, s) => new { Player = p, Session = s })
                .Join(_context.Realms, ps => ps.Session.RealmId, r => r.Id, (ps, r) => new { ps.Player, ps.Session, Realm = r })
                .Where(psr => psr.Realm.RealmId == realmId &&
                             psr.Player.AllianceId == alliance.AllianceId &&
                             psr.Player.ValidFrom <= utcDate &&
                             (psr.Player.ValidTo == null || psr.Player.ValidTo > utcDate))
                .Select(psr => psr.Player)
                .ToListAsync();

            // Ensure Members collection is not null
            alliance.Members = members ?? new List<Player>();
        }

        var allianceDtos = _mapper.Map<List<AllianceDto>>(alliances);

        // Build alliance rank lookup (by Power desc) for this realm/date
        var allAlliancesForRank = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, @as => @as.Session.RealmId, r => r.Id, (@as, r) => new { @as.Alliance, @as.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId &&
                         asr.Alliance.ValidFrom <= utcDate &&
                         (asr.Alliance.ValidTo == null || asr.Alliance.ValidTo > utcDate))
            .Select(asr => asr.Alliance)
            .ToListAsync();

        var dedupAlliancesForRank = allAlliancesForRank
            .GroupBy(a => a.AllianceId)
            .Select(g => g.OrderByDescending(a => a.ValidFrom).First())
            .OrderByDescending(a => a.Power)
            .ToList();

        var allianceRankLookup = dedupAlliancesForRank
            .Select((a, i) => new { a.AllianceId, Rank = i + 1 })
            .ToDictionary(x => x.AllianceId, x => x.Rank, StringComparer.Ordinal);

        // Set the data date and rank for each alliance
        foreach (var dto in allianceDtos)
        {
            dto.DataDate = utcDate;
            dto.Rank = allianceRankLookup.TryGetValue(dto.AllianceId, out var r) ? r : 0;
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

    public async Task<PagedResult<AllianceDto>> SearchAlliancesAsync(string query, string realmId, DateTime date, int page, int pageSize)
    {
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("Searching alliances with query '{Query}' for realm {RealmId}, date {Date}, page {Page}, size {PageSize}",
            query, realmId, utcDate, page, pageSize);

        // First get all valid alliances for the realm and date
        var validAlliances = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, as_ => as_.Session.RealmId, r => r.Id, (as_, r) => new { as_.Alliance, as_.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId &&
                         asr.Alliance.ValidFrom <= utcDate &&
                         (asr.Alliance.ValidTo == null || asr.Alliance.ValidTo > utcDate))
            .Select(asr => asr.Alliance)
            .ToListAsync();

        // Deduplicate in memory by taking the most recent record for each AllianceId
        var deduplicatedAlliances = validAlliances
            .GroupBy(a => a.AllianceId)
            .Select(g => g.OrderByDescending(a => a.ValidFrom).First())
            .ToList();

        var alliancesQuery = deduplicatedAlliances.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var lowerQuery = query.ToLower();
            alliancesQuery = alliancesQuery.Where(a =>
                a.Name.ToLower().Contains(lowerQuery) ||
                a.AllianceId.ToLower().Contains(lowerQuery) ||
                a.OverlordName.ToLower().Contains(lowerQuery));
        }

        var totalCount = alliancesQuery.Count();

        var alliances = alliancesQuery
            .OrderByDescending(a => a.Power)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Load member information for each alliance to get accurate member counts
        foreach (var alliance in alliances)
        {
            var members = await _context.Players
                .Join(_context.ImportSessions, p => p.ImportSessionId, s => s.Id, (p, s) => new { Player = p, Session = s })
                .Join(_context.Realms, ps => ps.Session.RealmId, r => r.Id, (ps, r) => new { ps.Player, ps.Session, Realm = r })
                .Where(psr => psr.Realm.RealmId == realmId &&
                             psr.Player.AllianceId == alliance.AllianceId &&
                             psr.Player.ValidFrom <= utcDate &&
                             (psr.Player.ValidTo == null || psr.Player.ValidTo > utcDate))
                .Select(psr => psr.Player)
                .ToListAsync();

            // Ensure Members collection is not null
            alliance.Members = members ?? new List<Player>();
        }

        var allianceDtos = _mapper.Map<List<AllianceDto>>(alliances);

        // Build alliance rank lookup (by Power desc) for this realm/date
        var allAlliancesForRank = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, @as => @as.Session.RealmId, r => r.Id, (@as, r) => new { @as.Alliance, @as.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId &&
                         asr.Alliance.ValidFrom <= utcDate &&
                         (asr.Alliance.ValidTo == null || asr.Alliance.ValidTo > utcDate))
            .Select(asr => asr.Alliance)
            .ToListAsync();

        var dedupAlliancesForRank = allAlliancesForRank
            .GroupBy(a => a.AllianceId)
            .Select(g => g.OrderByDescending(a => a.ValidFrom).First())
            .OrderByDescending(a => a.Power)
            .ToList();

        var allianceRankLookup = dedupAlliancesForRank
            .Select((a, i) => new { a.AllianceId, Rank = i + 1 })
            .ToDictionary(x => x.AllianceId, x => x.Rank, StringComparer.Ordinal);

        // Set the data date and rank for each alliance
        foreach (var dto in allianceDtos)
        {
            dto.DataDate = utcDate;
            dto.Rank = allianceRankLookup.TryGetValue(dto.AllianceId, out var r) ? r : 0;
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

    public async Task<AllianceDto?> GetAllianceAsync(string allianceId, string realmId, DateTime date)
    {
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("🔍 ALLIANCE SERVICE: Getting alliance {AllianceId} for realm {RealmId}, date {Date}", allianceId, realmId, utcDate);

        // First, let's see ALL alliance records for this ID in this realm
        var allRecords = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, as_ => as_.Session.RealmId, r => r.Id, (as_, r) => new { as_.Alliance, as_.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId && asr.Alliance.AllianceId == allianceId)
            .OrderBy(asr => asr.Alliance.ValidFrom)
            .Select(asr => asr.Alliance)
            .ToListAsync();

        _logger.LogInformation("🔍 ALLIANCE SERVICE: Found {Count} total records for alliance {AllianceId} in realm {RealmId}", allRecords.Count, allianceId, realmId);
        foreach (var record in allRecords)
        {
            _logger.LogInformation("🔍 ALLIANCE SERVICE: Record - ValidFrom: {ValidFrom}, ValidTo: {ValidTo}, Power: {Power}, IsActive: {IsActive}",
                record.ValidFrom, record.ValidTo, record.Power, record.IsActive);
        }

        var alliance = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, as_ => as_.Session.RealmId, r => r.Id, (as_, r) => new { as_.Alliance, as_.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId &&
                         asr.Alliance.AllianceId == allianceId &&
                         asr.Alliance.ValidFrom <= utcDate &&
                         (asr.Alliance.ValidTo == null || asr.Alliance.ValidTo > utcDate))
            .Select(asr => asr.Alliance)
            .OrderByDescending(a => a.ValidFrom)  // Get the most recent record that's valid for this date
            .FirstOrDefaultAsync();

        if (alliance != null)
        {
            _logger.LogInformation("🔍 ALLIANCE SERVICE: Found alliance {Name} with power {Power} for date {Date}",
                alliance.Name, alliance.Power, utcDate);
        }

        if (alliance == null)
        {
            _logger.LogWarning("Alliance {AllianceId} not found for date {Date}", allianceId, utcDate);
            return null;
        }

        // Load members manually since navigation properties are ignored in EF configuration
        var members = await _context.Players
            .Join(_context.ImportSessions, p => p.ImportSessionId, s => s.Id, (p, s) => new { Player = p, Session = s })
            .Join(_context.Realms, ps => ps.Session.RealmId, r => r.Id, (ps, r) => new { ps.Player, ps.Session, Realm = r })
            .Where(psr => psr.Realm.RealmId == realmId &&
                         psr.Player.AllianceId == alliance.AllianceId &&
                         psr.Player.ValidFrom <= utcDate &&
                         (psr.Player.ValidTo == null || psr.Player.ValidTo > utcDate))
            .Select(psr => psr.Player)
            .ToListAsync();
        alliance.Members = members;

        var allianceDto = _mapper.Map<AllianceDto>(alliance);
        allianceDto.DataDate = utcDate;

        // Compute and set alliance rank by power within realm/date
        var allAlliancesForRank = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, @as => @as.Session.RealmId, r => r.Id, (@as, r) => new { @as.Alliance, @as.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId &&
                         asr.Alliance.ValidFrom <= utcDate &&
                         (asr.Alliance.ValidTo == null || asr.Alliance.ValidTo > utcDate))
            .Select(asr => asr.Alliance)
            .ToListAsync();

        var dedupAlliancesForRank = allAlliancesForRank
            .GroupBy(a => a.AllianceId)
            .Select(g => g.OrderByDescending(a => a.ValidFrom).First())
            .OrderByDescending(a => a.Power)
            .ToList();

        allianceDto.Rank = dedupAlliancesForRank.FindIndex(a => a.AllianceId == alliance.AllianceId) + 1;

        _logger.LogInformation("Found alliance {AllianceName} with {MemberCount} members",
            alliance.Name, alliance.Members.Count);

        return allianceDto;
    }

    public async Task<PagedResult<PlayerDto>> GetAllianceMembersAsync(string allianceId, string realmId, DateTime date, int page, int pageSize)
    {
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("Getting members for alliance {AllianceId} for realm {RealmId}, date {Date}, page {Page}, size {PageSize}",
            allianceId, realmId, utcDate, page, pageSize);

        var membersQuery = _context.Players
            .Join(_context.ImportSessions, p => p.ImportSessionId, s => s.Id, (p, s) => new { Player = p, Session = s })
            .Join(_context.Realms, ps => ps.Session.RealmId, r => r.Id, (ps, r) => new { ps.Player, ps.Session, Realm = r })
            .Where(psr => psr.Realm.RealmId == realmId &&
                         psr.Player.AllianceId == allianceId &&
                         psr.Player.ValidFrom <= utcDate &&
                         (psr.Player.ValidTo == null || psr.Player.ValidTo > utcDate))
            .Select(psr => psr.Player);

        var totalCount = await membersQuery.CountAsync();

        var members = await membersQuery
            .OrderByDescending(p => p.Might)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var memberDtos = _mapper.Map<List<PlayerDto>>(members);

        // Load city tile coordinates for these members at the given date/realm
        try
        {
            var playerIds = members.Select(p => p.PlayerId).Distinct().ToList();
            if (playerIds.Count > 0)
            {
                var cityTiles = await _context.Tiles
                    .Join(_context.ImportSessions, t => t.ImportSessionId, s => s.Id, (t, s) => new { Tile = t, Session = s })
                    .Join(_context.Realms, ts => ts.Session.RealmId, r => r.Id, (ts, r) => new { ts.Tile, ts.Session, Realm = r })
                    .Where(tsr => tsr.Realm.RealmId == realmId &&
                                  tsr.Tile.PlayerId != null && playerIds.Contains(tsr.Tile.PlayerId) &&
                                  tsr.Tile.ValidFrom <= utcDate &&
                                  (tsr.Tile.ValidTo == null || tsr.Tile.ValidTo > utcDate) &&
                                  tsr.Tile.Type.Trim().ToLower() == "city")
                    .Select(tsr => tsr.Tile)
                    .ToListAsync();

                var cityLookup = cityTiles
                    .GroupBy(t => t.PlayerId!)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.ValidFrom).First());

                foreach (var dto in memberDtos)
                {
                    if (dto.PlayerId != null && cityLookup.TryGetValue(dto.PlayerId, out var tile))
                    {
                        dto.CityX = tile.X;
                        dto.CityY = tile.Y;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load city coordinates for alliance members page {Page}", page);
        }

        // Set the data date and rank for each member
        // Build rank lookup across all players in realm+date for consistent ranking
        var allPlayersForRank = await _context.Players
            .Join(_context.ImportSessions, p => p.ImportSessionId, s => s.Id, (p, s) => new { Player = p, Session = s })
            .Join(_context.Realms, ps => ps.Session.RealmId, r => r.Id, (ps, r) => new { ps.Player, ps.Session, Realm = r })
            .Where(psr => psr.Realm.RealmId == realmId &&
                         psr.Player.ValidFrom <= utcDate &&
                         (psr.Player.ValidTo == null || psr.Player.ValidTo > utcDate))
            .Select(psr => psr.Player)
            .ToListAsync();

        var dedupForRank = allPlayersForRank
            .GroupBy(p => p.PlayerId)
            .Select(g => g.OrderByDescending(p => p.ValidFrom).First())
            .OrderByDescending(p => p.Might)
            .ToList();

        var rankLookup = dedupForRank
            .Select((p, i) => new { PlayerId = p.PlayerId.Trim(), Rank = i + 1 })
            .ToDictionary(x => x.PlayerId, x => x.Rank, StringComparer.Ordinal);

        foreach (var dto in memberDtos)
        {
            dto.DataDate = utcDate;
            var key = (dto.PlayerId ?? string.Empty).Trim();
            dto.Rank = rankLookup.TryGetValue(key, out var r) ? r : 0;
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

    public async Task<List<TileDto>> GetAllianceTilesAsync(string allianceId, string realmId, DateTime date)
    {
        // Ensure date is UTC for PostgreSQL compatibility
        var utcDate = date.Kind == DateTimeKind.Utc ? date : DateTime.SpecifyKind(date, DateTimeKind.Utc);

        _logger.LogInformation("Getting tiles for alliance {AllianceId} for realm {RealmId}, date {Date}", allianceId, realmId, utcDate);

        var tiles = await _context.Tiles
            .Join(_context.ImportSessions, t => t.ImportSessionId, s => s.Id, (t, s) => new { Tile = t, Session = s })
            .Join(_context.Realms, ts => ts.Session.RealmId, r => r.Id, (ts, r) => new { ts.Tile, ts.Session, Realm = r })
            .Where(tsr => tsr.Realm.RealmId == realmId &&
                         tsr.Tile.AllianceId == allianceId &&
                         tsr.Tile.ValidFrom <= utcDate &&
                         (tsr.Tile.ValidTo == null || tsr.Tile.ValidTo > utcDate))
            .Select(tsr => tsr.Tile)
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

        _logger.LogInformation("Found {Count} tiles for alliance {AllianceId}", tiles.Count, allianceId);

        return tileDtos;
    }

    public async Task<List<HistoryEntryDto<AllianceDto>>> GetAllianceHistoryAsync(string allianceId, string realmId)
    {
        _logger.LogInformation("Getting history for alliance {AllianceId} in realm {RealmId}", allianceId, realmId);

        var allianceHistory = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, as_ => as_.Session.RealmId, r => r.Id, (as_, r) => new { as_.Alliance, as_.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId && asr.Alliance.AllianceId == allianceId)
            .OrderByDescending(asr => asr.Alliance.ValidFrom)
            .Select(asr => asr.Alliance)
            .ToListAsync();

        var historyEntries = new List<HistoryEntryDto<AllianceDto>>();

        for (int i = 0; i < allianceHistory.Count; i++)
        {
            var alliance = allianceHistory[i];

            // Get member count for this alliance at this point in time
            var memberCount = await _context.Players
                .Join(_context.ImportSessions, p => p.ImportSessionId, s => s.Id, (p, s) => new { Player = p, Session = s })
                .Join(_context.Realms, ps => ps.Session.RealmId, r => r.Id, (ps, r) => new { ps.Player, ps.Session, Realm = r })
                .Where(psr => psr.Realm.RealmId == realmId &&
                             psr.Player.AllianceId == allianceId &&
                             psr.Player.ValidFrom <= alliance.ValidFrom &&
                             (psr.Player.ValidTo == null || psr.Player.ValidTo > alliance.ValidFrom))
                .GroupBy(psr => psr.Player.PlayerId)  // Group by player ID to avoid counting duplicates
                .CountAsync();

            var allianceDto = _mapper.Map<AllianceDto>(alliance);
            allianceDto.MemberCount = memberCount; // Override the member count with the correct historical value

            // Compute alliance rank for this snapshot (Power desc within realm at this date)
            try
            {
                var allAlliancesForRank = await _context.Alliances
                    .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
                    .Join(_context.Realms, @as => @as.Session.RealmId, r => r.Id, (@as, r) => new { @as.Alliance, @as.Session, Realm = r })
                    .Where(asr => asr.Realm.RealmId == realmId &&
                                 asr.Alliance.ValidFrom <= alliance.ValidFrom &&
                                 (asr.Alliance.ValidTo == null || asr.Alliance.ValidTo > alliance.ValidFrom))
                    .Select(asr => asr.Alliance)
                    .ToListAsync();

                var dedupAlliancesForRank = allAlliancesForRank
                    .GroupBy(a => a.AllianceId)
                    .Select(g => g.OrderByDescending(a => a.ValidFrom).First())
                    .OrderByDescending(a => a.Power)
                    .ToList();

                var normalizedId = (alliance.AllianceId ?? string.Empty).Trim();
                allianceDto.Rank = dedupAlliancesForRank.FindIndex(a => string.Equals(a.AllianceId.Trim(), normalizedId, StringComparison.Ordinal)) + 1;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute alliance rank for history entry at {Date}", alliance.ValidFrom);
                allianceDto.Rank = 0;
            }

            string changeType = "Added";
            var changeDetails = new List<string>();
            if (i < allianceHistory.Count - 1)
            {
                var previousAlliance = allianceHistory[i + 1];

                if (alliance.Power != previousAlliance.Power)
                {
                    var direction = alliance.Power > previousAlliance.Power ? "increase" : "decrease";
                    changeDetails.Add($"Power {direction}: {previousAlliance.Power:N0} → {alliance.Power:N0}");
                }

                // Member count change (use realm/date-scoped counts, not navigation property)
                var prevMemberCount = await _context.Players
                    .Join(_context.ImportSessions, p => p.ImportSessionId, s => s.Id, (p, s) => new { Player = p, Session = s })
                    .Join(_context.Realms, ps => ps.Session.RealmId, r => r.Id, (ps, r) => new { ps.Player, ps.Session, Realm = r })
                    .Where(psr => psr.Realm.RealmId == realmId &&
                                 psr.Player.AllianceId == allianceId &&
                                 psr.Player.ValidFrom <= previousAlliance.ValidFrom &&
                                 (psr.Player.ValidTo == null || psr.Player.ValidTo > previousAlliance.ValidFrom))
                    .GroupBy(psr => psr.Player.PlayerId)
                    .CountAsync();

                if (memberCount != prevMemberCount)
                {
                    var diff = memberCount - prevMemberCount;
                    changeDetails.Add(diff > 0 ? $"Members increased by {diff}" : $"Members decreased by {-diff}");
                }

                // Rank change: compute rank for both snapshots and compare
                try
                {
                    var prevRank = await ComputeAllianceRankAsync(realmId, previousAlliance.ValidFrom, previousAlliance.AllianceId);
                    var curRank = await ComputeAllianceRankAsync(realmId, alliance.ValidFrom, alliance.AllianceId);
                    if (prevRank > 0 && curRank > 0 && prevRank != curRank)
                    {
                        var dir = curRank < prevRank ? "increase" : "decrease"; // smaller number is better
                        changeDetails.Add($"Rank {dir}: {prevRank} → {curRank}");
                    }
                }
                catch { /* ignore rank diff failures */ }

                if (alliance.FortressLevel > previousAlliance.FortressLevel)
                {
                    changeDetails.Add($"Fortress level up: {previousAlliance.FortressLevel} → {alliance.FortressLevel}");
                }

                if (changeDetails.Count > 0)
                {
                    changeType = "Modified";
                }
            }

            if (!alliance.IsActive)
            {
                changeType = "Removed";
            }

            var state = changeType == "Added" ? HistoryState.Added
                       : changeType == "Modified" ? HistoryState.Changed
                       : HistoryState.Removed;

            historyEntries.Add(new HistoryEntryDto<AllianceDto>
            {
                Data = allianceDto,
                ValidFrom = alliance.ValidFrom,
                ValidTo = alliance.ValidTo,
                State = state,
                Changes = changeDetails
            });
        }

        _logger.LogInformation("Found {Count} history entries for alliance {AllianceId}", historyEntries.Count, allianceId);

        return historyEntries;
    }

    public async Task<List<DateTime>> GetAvailableDatesAsync(string realmId)
    {
        var dates = await _context.ImportSessions
            .Join(_context.Realms, s => s.RealmId, r => r.Id, (s, r) => new { Session = s, Realm = r })
            .Where(sr => sr.Realm.RealmId == realmId && sr.Session.Status == DOAMapper.Shared.Models.Enums.ImportStatus.Completed)
            .Select(sr => sr.Session.ImportDate.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();

        // Ensure all dates are UTC for PostgreSQL compatibility
        return dates.Select(d => DateTime.SpecifyKind(d, DateTimeKind.Utc)).ToList();
    }

    private async Task<int> ComputeAllianceRankAsync(string realmId, DateTime asOfDate, string allianceId)
    {
        var utcDate = asOfDate.Kind == DateTimeKind.Utc ? asOfDate : DateTime.SpecifyKind(asOfDate, DateTimeKind.Utc);
        var validAlliances = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Join(_context.Realms, @as => @as.Session.RealmId, r => r.Id, (@as, r) => new { @as.Alliance, @as.Session, Realm = r })
            .Where(asr => asr.Realm.RealmId == realmId &&
                         asr.Alliance.ValidFrom <= utcDate &&
                         (asr.Alliance.ValidTo == null || asr.Alliance.ValidTo > utcDate))
            .Select(asr => asr.Alliance)
            .ToListAsync();

        var ordered = validAlliances
            .GroupBy(a => a.AllianceId)
            .Select(g => g.OrderByDescending(a => a.ValidFrom).First())
            .OrderByDescending(a => a.Power)
            .ToList();

        var norm = (allianceId ?? string.Empty).Trim();
        var rank = ordered.FindIndex(a => string.Equals(a.AllianceId.Trim(), norm, StringComparison.Ordinal)) + 1;
        return rank > 0 ? rank : 0;
    }
}
