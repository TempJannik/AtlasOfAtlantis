using AutoMapper;
using DOAMapper.Data;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;
using DOAMapper.Shared.Services;
using DOAMapper.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace DOAMapper.Services;

public class RealmService : IRealmService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<RealmService> _logger;

    public RealmService(ApplicationDbContext context, IMapper mapper, ILogger<RealmService> logger)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<List<RealmDto>> GetAvailableRealmsAsync()
    {
        _logger.LogInformation("Getting available realms");

        var realmsWithCounts = await _context.Realms
            .Where(r => r.IsActive)
            .GroupJoin(
                _context.ImportSessions,
                realm => realm.Id,
                session => session.RealmId,
                (realm, sessions) => new { Realm = realm, ImportSessionCount = sessions.Count() })
            .OrderBy(r => r.Realm.Name)
            .ToListAsync();

        var realmDtos = realmsWithCounts.Select(r =>
        {
            var dto = _mapper.Map<RealmDto>(r.Realm);
            dto.ImportSessionCount = r.ImportSessionCount;
            return dto;
        }).ToList();

        _logger.LogInformation("Found {Count} active realms", realmDtos.Count);
        return realmDtos;
    }

    public async Task<RealmDto?> GetRealmAsync(string realmId)
    {
        _logger.LogInformation("Getting realm {RealmId}", realmId);

        var realm = await _context.Realms
            .FirstOrDefaultAsync(r => r.RealmId == realmId && r.IsActive);

        if (realm == null)
        {
            _logger.LogWarning("Realm {RealmId} not found", realmId);
            return null;
        }

        var importSessionCount = await _context.ImportSessions
            .Where(s => s.RealmId == realm.Id)
            .CountAsync();

        var dto = _mapper.Map<RealmDto>(realm);
        dto.ImportSessionCount = importSessionCount;

        return dto;
    }

    public async Task<RealmDto> CreateRealmAsync(string realmId, string name)
    {
        _logger.LogInformation("Creating realm {RealmId} with name '{Name}'", realmId, name);

        // Validate input
        ValidateRealmInput(realmId, name);

        // Check if realm already exists
        var existingRealm = await _context.Realms
            .FirstOrDefaultAsync(r => r.RealmId == realmId);

        if (existingRealm != null)
        {
            throw new InvalidOperationException($"Realm with ID '{realmId}' already exists");
        }

        var realm = new Realm
        {
            Id = Guid.NewGuid(),
            RealmId = realmId,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Realms.Add(realm);
        await _context.SaveChangesAsync();

        var dto = _mapper.Map<RealmDto>(realm);
        dto.ImportSessionCount = 0;

        _logger.LogInformation("Created realm {RealmId} successfully", realmId);
        return dto;
    }

    public async Task<RealmDto?> UpdateRealmAsync(string realmId, string name, bool isActive)
    {
        _logger.LogInformation("Updating realm {RealmId} with name '{Name}', active: {IsActive}", realmId, name, isActive);

        var realm = await _context.Realms
            .FirstOrDefaultAsync(r => r.RealmId == realmId);

        if (realm == null)
        {
            _logger.LogWarning("Realm {RealmId} not found for update", realmId);
            return null;
        }

        realm.Name = name;
        realm.IsActive = isActive;

        await _context.SaveChangesAsync();

        var importSessionCount = await _context.ImportSessions
            .Where(s => s.RealmId == realm.Id)
            .CountAsync();

        var dto = _mapper.Map<RealmDto>(realm);
        dto.ImportSessionCount = importSessionCount;

        _logger.LogInformation("Updated realm {RealmId} successfully", realmId);
        return dto;
    }

    public async Task<bool> DeleteRealmAsync(string realmId)
    {
        _logger.LogInformation("Deleting realm {RealmId}", realmId);

        var realm = await _context.Realms
            .FirstOrDefaultAsync(r => r.RealmId == realmId);

        if (realm == null)
        {
            _logger.LogWarning("Realm {RealmId} not found for deletion", realmId);
            return false;
        }

        // Check if realm has any import sessions
        var hasImportSessions = await _context.ImportSessions
            .AnyAsync(s => s.RealmId == realm.Id);

        if (hasImportSessions)
        {
            _logger.LogWarning("Cannot delete realm {RealmId} - it has associated import sessions", realmId);
            throw new InvalidOperationException($"Cannot delete realm '{realmId}' because it has associated import sessions");
        }

        _context.Realms.Remove(realm);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Deleted realm {RealmId} successfully", realmId);
        return true;
    }



    public async Task<bool> RealmExistsAsync(string realmId)
    {
        return await _context.Realms
            .AnyAsync(r => r.RealmId == realmId && r.IsActive);
    }

    private static void ValidateRealmInput(string realmId, string name)
    {
        if (string.IsNullOrWhiteSpace(realmId))
            throw new ArgumentException("Realm ID cannot be null or empty", nameof(realmId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Realm name cannot be null or empty", nameof(name));

        if (realmId.Length > RealmConstants.MaxRealmIdLength)
            throw new ArgumentException($"Realm ID cannot exceed {RealmConstants.MaxRealmIdLength} characters", nameof(realmId));

        if (name.Length > RealmConstants.MaxRealmNameLength)
            throw new ArgumentException($"Realm name cannot exceed {RealmConstants.MaxRealmNameLength} characters", nameof(name));

        if (!Regex.IsMatch(realmId, RealmConstants.RealmIdPattern))
            throw new ArgumentException("Realm ID can only contain letters, numbers, hyphens, and underscores", nameof(realmId));

        if (RealmConstants.ReservedRealmIds.Contains(realmId))
            throw new ArgumentException($"Realm ID '{realmId}' is reserved and cannot be used", nameof(realmId));
    }
}
