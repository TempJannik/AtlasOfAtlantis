using DOAMapper.Data;
using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DOAMapper.Services;

public class TemporalDataService : ITemporalDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TemporalDataService> _logger;

    public TemporalDataService(ApplicationDbContext context, ILogger<TemporalDataService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ApplyTileChangesAsync(ChangeSet<Tile> changes, Guid importSessionId)
    {
        var importDate = DateTime.UtcNow.Date;
        
        // Handle removed tiles - set ValidTo date
        if (changes.Removed.Any())
        {
            var removedKeys = changes.Removed.Select(GetTileKey).ToHashSet();
            var tilesToDeactivate = await _context.Tiles
                .Where(t => t.IsActive && removedKeys.Contains($"{t.X},{t.Y}"))
                .ToListAsync();
                
            foreach (var tile in tilesToDeactivate)
            {
                tile.IsActive = false;
                tile.ValidTo = importDate;
            }
            
            _logger.LogInformation("Deactivated {Count} removed tiles", tilesToDeactivate.Count);
        }

        // Handle modified tiles - deactivate old, add new
        if (changes.Modified.Any())
        {
            // Get all active tiles first, then filter in memory
            var allActiveTiles = await _context.Tiles
                .Where(t => t.IsActive)
                .ToListAsync();

            var modifiedCoordinates = changes.Modified.Select(t => new { t.X, t.Y }).ToHashSet();
            var tilesToUpdate = allActiveTiles
                .Where(t => modifiedCoordinates.Contains(new { t.X, t.Y }))
                .ToList();
                
            foreach (var tile in tilesToUpdate)
            {
                tile.IsActive = false;
                tile.ValidTo = importDate;
            }

            // Save changes to commit the deactivation before adding new versions
            await _context.SaveChangesAsync();

            // Clear change tracker to avoid conflicts
            _context.ChangeTracker.Clear();

            // Add new versions
            foreach (var modifiedTile in changes.Modified)
            {
                modifiedTile.ImportSessionId = importSessionId;
                modifiedTile.IsActive = true;
                modifiedTile.ValidFrom = importDate;
                _context.Tiles.Add(modifiedTile);
            }
            
            _logger.LogInformation("Updated {Count} modified tiles", changes.Modified.Count);
        }

        // Handle added tiles - ensure no conflicts with modified tiles
        if (changes.Added.Any())
        {
            // Get the tile coordinates that were already processed in the modified section
            var modifiedCoordinates = changes.Modified.Select(t => new { t.X, t.Y }).ToHashSet();

            // Filter out added tiles that have the same coordinates as modified ones
            var filteredAddedTiles = changes.Added
                .Where(t => !modifiedCoordinates.Contains(new { t.X, t.Y }))
                .ToList();

            // Batch prepare all tiles for insertion
            foreach (var addedTile in filteredAddedTiles)
            {
                addedTile.ImportSessionId = importSessionId;
                addedTile.IsActive = true;
                addedTile.ValidFrom = importDate;
            }

            // Batch insert all tiles at once
            if (filteredAddedTiles.Any())
            {
                _logger.LogInformation("Batch inserting {Count} tiles", filteredAddedTiles.Count);
                _context.Tiles.AddRange(filteredAddedTiles);
            }

            var totalFiltered = changes.Added.Count - filteredAddedTiles.Count;
            if (totalFiltered > 0)
            {
                _logger.LogWarning("Excluded {Count} added tiles that were already in modified list", totalFiltered);
            }

            _logger.LogInformation("Added {Count} new tiles (filtered from {Original} original)",
                filteredAddedTiles.Count, changes.Added.Count);
        }

        // Final safety check: verify no duplicate tile coordinates in the change tracker
        var trackedTiles = _context.ChangeTracker.Entries<Tile>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        var tileCoordinateGroups = trackedTiles.GroupBy(t => new { t.X, t.Y }).ToList();
        var duplicateGroups = tileCoordinateGroups.Where(g => g.Count() > 1).ToList();

        if (duplicateGroups.Any())
        {
            _logger.LogError("Found {Count} duplicate tile coordinates in change tracker before SaveChanges: {DuplicateKeys}",
                duplicateGroups.Count,
                string.Join(", ", duplicateGroups.Select(g => $"{g.Key.X},{g.Key.Y}")));

            // Remove duplicates, keeping only the first occurrence
            foreach (var duplicateGroup in duplicateGroups)
            {
                var duplicates = duplicateGroup.Skip(1).ToList();
                foreach (var duplicate in duplicates)
                {
                    _context.Entry(duplicate).State = EntityState.Detached;
                    _logger.LogWarning("Removed duplicate tile at coordinates ({X},{Y}) from change tracker", duplicate.X, duplicate.Y);
                }
            }
        }

        // Check for conflicts with existing tiles (both active and inactive)
        var tileCoordinates = trackedTiles.Select(t => new { t.X, t.Y }).ToHashSet();

        // Get all existing tiles first, then filter in memory
        var allExistingTiles = await _context.Tiles
            .Select(t => new { t.X, t.Y })
            .ToListAsync();

        var existingTileCoordinates = allExistingTiles
            .Where(t => tileCoordinates.Contains(new { t.X, t.Y }))
            .ToList();

        if (existingTileCoordinates.Any())
        {
            _logger.LogError("Found {Count} tile coordinates that already exist in database: {ExistingKeys}",
                existingTileCoordinates.Count,
                string.Join(", ", existingTileCoordinates.Select(t => $"{t.X},{t.Y}")));

            // Remove tiles that already exist in the database
            var conflictingTiles = trackedTiles
                .Where(t => existingTileCoordinates.Any(existing => existing.X == t.X && existing.Y == t.Y))
                .ToList();

            foreach (var conflictingTile in conflictingTiles)
            {
                _context.Entry(conflictingTile).State = EntityState.Detached;
                _logger.LogWarning("Removed tile with existing coordinates ({X},{Y}) from change tracker", conflictingTile.X, conflictingTile.Y);
            }
        }

        // Check for foreign key constraints - validate PlayerId and AllianceId exist
        var remainingTiles = _context.ChangeTracker.Entries<Tile>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        // Get valid PlayerIds and AllianceIds from database
        var playerIds = remainingTiles.Where(t => !string.IsNullOrEmpty(t.PlayerId)).Select(t => t.PlayerId).Distinct().ToList();
        var allianceIds = remainingTiles.Where(t => !string.IsNullOrEmpty(t.AllianceId)).Select(t => t.AllianceId).Distinct().ToList();

        var validPlayerIds = await _context.Players
            .Where(p => playerIds.Contains(p.PlayerId))
            .Select(p => p.PlayerId)
            .ToListAsync();

        var validAllianceIds = await _context.Alliances
            .Where(a => allianceIds.Contains(a.AllianceId))
            .Select(a => a.AllianceId)
            .ToListAsync();

        // Remove tiles with invalid foreign keys
        var tilesToRemove = remainingTiles
            .Where(t => (!string.IsNullOrEmpty(t.PlayerId) && !validPlayerIds.Contains(t.PlayerId)) ||
                       (!string.IsNullOrEmpty(t.AllianceId) && !validAllianceIds.Contains(t.AllianceId)))
            .ToList();

        if (tilesToRemove.Any())
        {
            _logger.LogWarning("Removing {Count} tiles with invalid foreign key references", tilesToRemove.Count);
            foreach (var tile in tilesToRemove)
            {
                _context.Entry(tile).State = EntityState.Detached;
                if (!string.IsNullOrEmpty(tile.PlayerId) && !validPlayerIds.Contains(tile.PlayerId))
                {
                    _logger.LogWarning("Removed tile at ({X},{Y}) - invalid PlayerId: {PlayerId}", tile.X, tile.Y, tile.PlayerId);
                }
                if (!string.IsNullOrEmpty(tile.AllianceId) && !validAllianceIds.Contains(tile.AllianceId))
                {
                    _logger.LogWarning("Removed tile at ({X},{Y}) - invalid AllianceId: {AllianceId}", tile.X, tile.Y, tile.AllianceId);
                }
            }
        }

        var finalRemainingTiles = _context.ChangeTracker.Entries<Tile>()
            .Where(e => e.State == EntityState.Added)
            .Count();
        _logger.LogInformation("About to save {Count} tiles to database", finalRemainingTiles);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully saved {Count} tiles to database", finalRemainingTiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tiles to database. Error: {ErrorMessage}", ex.Message);
            throw; // Re-throw to trigger rollback
        }
    }

    public async Task ApplyPlayerChangesAsync(ChangeSet<Player> changes, Guid importSessionId)
    {
        var importDate = DateTime.UtcNow.Date;
        
        // Handle removed players
        if (changes.Removed.Any())
        {
            var removedIds = changes.Removed.Select(p => p.PlayerId).ToHashSet();
            var playersToDeactivate = await _context.Players
                .Where(p => p.IsActive && removedIds.Contains(p.PlayerId))
                .ToListAsync();
                
            foreach (var player in playersToDeactivate)
            {
                player.IsActive = false;
                player.ValidTo = importDate;
            }
            
            _logger.LogInformation("??? Deactivated {Count} removed players: {PlayerNames}",
                playersToDeactivate.Count,
                string.Join(", ", playersToDeactivate.Select(p => $"{p.Name} (ID: {p.PlayerId})")));
        }

        // Handle modified players
        if (changes.Modified.Any())
        {
            var modifiedIds = changes.Modified.Select(p => p.PlayerId).ToHashSet();
            var playersToUpdate = await _context.Players
                .Where(p => p.IsActive && modifiedIds.Contains(p.PlayerId))
                .ToListAsync();
                
            foreach (var player in playersToUpdate)
            {
                player.IsActive = false;
                player.ValidTo = importDate;
            }

            // Save changes to commit the deactivation before adding new versions
            await _context.SaveChangesAsync();

            // Clear change tracker to avoid conflicts
            _context.ChangeTracker.Clear();

            // Add new versions
            foreach (var modifiedPlayer in changes.Modified)
            {
                modifiedPlayer.ImportSessionId = importSessionId;
                modifiedPlayer.IsActive = true;
                modifiedPlayer.ValidFrom = importDate;
                _context.Players.Add(modifiedPlayer);
            }
            
            _logger.LogInformation("Updated {Count} modified players", changes.Modified.Count);
        }

        // Handle added players - ensure no duplicates and no conflicts with modified players
        if (changes.Added.Any())
        {
            // Get the PlayerIds that were already processed in the modified section
            var modifiedPlayerIds = changes.Modified.Select(p => p.PlayerId).ToHashSet();

            // Filter out added players that have the same PlayerId as modified ones
            var filteredAddedPlayers = changes.Added
                .Where(p => !modifiedPlayerIds.Contains(p.PlayerId))
                .ToList();

            // Group by PlayerId and take only the first occurrence to avoid EF tracking conflicts
            var uniqueAddedPlayers = filteredAddedPlayers
                .GroupBy(p => p.PlayerId)
                .Select(g => g.First())
                .ToList();

            // Batch prepare all players for insertion
            foreach (var addedPlayer in uniqueAddedPlayers)
            {
                addedPlayer.ImportSessionId = importSessionId;
                addedPlayer.IsActive = true;
                addedPlayer.ValidFrom = importDate;
            }

            // Batch insert all players at once
            if (uniqueAddedPlayers.Any())
            {
                _logger.LogInformation("Batch inserting {Count} players", uniqueAddedPlayers.Count);
                _context.Players.AddRange(uniqueAddedPlayers);
            }

            var totalFiltered = changes.Added.Count - filteredAddedPlayers.Count;
            var totalDuplicates = filteredAddedPlayers.Count - uniqueAddedPlayers.Count;

            if (totalFiltered > 0)
            {
                _logger.LogWarning("Excluded {Count} added players that were already in modified list", totalFiltered);
            }

            if (totalDuplicates > 0)
            {
                _logger.LogWarning("Removed {Count} duplicate players during add operation", totalDuplicates);
            }

            _logger.LogInformation("Added {Count} new players (filtered from {Original} original)",
                uniqueAddedPlayers.Count, changes.Added.Count);
        }

        // Final safety check: verify no duplicate PlayerIds in the change tracker
        var trackedPlayers = _context.ChangeTracker.Entries<Player>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        var playerIdGroups = trackedPlayers.GroupBy(p => p.PlayerId).ToList();
        var duplicateGroups = playerIdGroups.Where(g => g.Count() > 1).ToList();

        if (duplicateGroups.Any())
        {
            _logger.LogError("Found {Count} duplicate PlayerIds in change tracker before SaveChanges: {DuplicateIds}",
                duplicateGroups.Count,
                string.Join(", ", duplicateGroups.Select(g => g.Key)));

            // Remove duplicates, keeping only the first occurrence
            foreach (var duplicateGroup in duplicateGroups)
            {
                var duplicates = duplicateGroup.Skip(1).ToList();
                foreach (var duplicate in duplicates)
                {
                    _context.Entry(duplicate).State = EntityState.Detached;
                    _logger.LogWarning("Removed duplicate player with ID {PlayerId} from change tracker", duplicate.PlayerId);
                }
            }
        }

        // Check for conflicts with existing players (both active and inactive)
        var playerIdsToAdd = trackedPlayers.Select(p => p.PlayerId).ToList();
        var existingPlayerIds = await _context.Players
            .Where(p => playerIdsToAdd.Contains(p.PlayerId))
            .Select(p => p.PlayerId)
            .ToListAsync();

        if (existingPlayerIds.Any())
        {
            _logger.LogError("Found {Count} PlayerIds that already exist in database: {ExistingIds}",
                existingPlayerIds.Count,
                string.Join(", ", existingPlayerIds));

            // Remove players that already exist in the database
            var conflictingPlayers = trackedPlayers
                .Where(p => existingPlayerIds.Contains(p.PlayerId))
                .ToList();

            foreach (var conflictingPlayer in conflictingPlayers)
            {
                _context.Entry(conflictingPlayer).State = EntityState.Detached;
                _logger.LogWarning("Removed player with existing ID {PlayerId} from change tracker", conflictingPlayer.PlayerId);
            }
        }

        var finalPlayerCount = trackedPlayers.Count - duplicateGroups.Sum(g => g.Count() - 1) - existingPlayerIds.Count;
        _logger.LogInformation("About to save {Count} players to database", finalPlayerCount);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully saved {Count} players to database", finalPlayerCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save players to database. Error: {ErrorMessage}", ex.Message);
            throw; // Re-throw to trigger rollback
        }
    }

    public async Task ApplyAllianceChangesAsync(ChangeSet<Alliance> changes, Guid importSessionId)
    {
        var importDate = DateTime.UtcNow.Date;
        
        // Handle removed alliances
        if (changes.Removed.Any())
        {
            var removedIds = changes.Removed.Select(a => a.AllianceId).ToHashSet();
            var alliancesToDeactivate = await _context.Alliances
                .Where(a => a.IsActive && removedIds.Contains(a.AllianceId))
                .ToListAsync();
                
            foreach (var alliance in alliancesToDeactivate)
            {
                alliance.IsActive = false;
                alliance.ValidTo = importDate;
            }
            
            _logger.LogInformation("??? Deactivated {Count} removed alliances: {AllianceNames}",
                alliancesToDeactivate.Count,
                string.Join(", ", alliancesToDeactivate.Select(a => $"{a.Name} (ID: {a.AllianceId})")));
        }

        // Handle modified alliances
        if (changes.Modified.Any())
        {
            var modifiedIds = changes.Modified.Select(a => a.AllianceId).ToHashSet();
            var alliancesToUpdate = await _context.Alliances
                .Where(a => a.IsActive && modifiedIds.Contains(a.AllianceId))
                .ToListAsync();
                
            foreach (var alliance in alliancesToUpdate)
            {
                alliance.IsActive = false;
                alliance.ValidTo = importDate;
            }

            // Save changes to commit the deactivation before adding new versions
            await _context.SaveChangesAsync();

            // Clear change tracker to avoid conflicts
            _context.ChangeTracker.Clear();

            // Add new versions
            foreach (var modifiedAlliance in changes.Modified)
            {
                modifiedAlliance.ImportSessionId = importSessionId;
                modifiedAlliance.IsActive = true;
                modifiedAlliance.ValidFrom = importDate;
                _context.Alliances.Add(modifiedAlliance);
            }
            
            _logger.LogInformation("Updated {Count} modified alliances", changes.Modified.Count);
        }

        // Handle added alliances - ensure no duplicates and no conflicts with modified alliances
        if (changes.Added.Any())
        {
            // Get the AllianceIds that were already processed in the modified section
            var modifiedAllianceIds = changes.Modified.Select(a => a.AllianceId).ToHashSet();

            // Filter out added alliances that have the same AllianceId as modified ones
            var filteredAddedAlliances = changes.Added
                .Where(a => !modifiedAllianceIds.Contains(a.AllianceId))
                .ToList();

            // Group by AllianceId and take only the first occurrence to avoid EF tracking conflicts
            var uniqueAddedAlliances = filteredAddedAlliances
                .GroupBy(a => a.AllianceId)
                .Select(g => g.First())
                .ToList();

            // Batch prepare all alliances for insertion
            foreach (var addedAlliance in uniqueAddedAlliances)
            {
                addedAlliance.ImportSessionId = importSessionId;
                addedAlliance.IsActive = true;
                addedAlliance.ValidFrom = importDate;
            }

            // Batch insert all alliances at once
            if (uniqueAddedAlliances.Any())
            {
                _logger.LogInformation("Batch inserting {Count} alliances", uniqueAddedAlliances.Count);
                _context.Alliances.AddRange(uniqueAddedAlliances);
            }

            var totalFiltered = changes.Added.Count - filteredAddedAlliances.Count;
            var totalDuplicates = filteredAddedAlliances.Count - uniqueAddedAlliances.Count;

            if (totalFiltered > 0)
            {
                _logger.LogWarning("Excluded {Count} added alliances that were already in modified list", totalFiltered);
            }

            if (totalDuplicates > 0)
            {
                _logger.LogWarning("Removed {Count} duplicate alliances during add operation", totalDuplicates);
            }

            _logger.LogInformation("Added {Count} new alliances (filtered from {Original} original)",
                uniqueAddedAlliances.Count, changes.Added.Count);
        }

        // Final safety check: verify no duplicate AllianceIds in the change tracker
        var trackedAlliances = _context.ChangeTracker.Entries<Alliance>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .ToList();

        var allianceIdGroups = trackedAlliances.GroupBy(a => a.AllianceId).ToList();
        var duplicateGroups = allianceIdGroups.Where(g => g.Count() > 1).ToList();

        if (duplicateGroups.Any())
        {
            _logger.LogError("Found {Count} duplicate AllianceIds in change tracker before SaveChanges: {DuplicateIds}",
                duplicateGroups.Count,
                string.Join(", ", duplicateGroups.Select(g => g.Key)));

            // Remove duplicates, keeping only the first occurrence
            foreach (var duplicateGroup in duplicateGroups)
            {
                var duplicates = duplicateGroup.Skip(1).ToList();
                foreach (var duplicate in duplicates)
                {
                    _context.Entry(duplicate).State = EntityState.Detached;
                    _logger.LogWarning("Removed duplicate alliance with ID {AllianceId} from change tracker", duplicate.AllianceId);
                }
            }
        }

        // Check for conflicts with existing alliances (both active and inactive)
        var allianceIdsToAdd = trackedAlliances.Select(a => a.AllianceId).ToList();
        var existingAllianceIds = await _context.Alliances
            .Where(a => allianceIdsToAdd.Contains(a.AllianceId))
            .Select(a => a.AllianceId)
            .ToListAsync();

        if (existingAllianceIds.Any())
        {
            _logger.LogError("Found {Count} AllianceIds that already exist in database: {ExistingIds}",
                existingAllianceIds.Count,
                string.Join(", ", existingAllianceIds));

            // Remove alliances that already exist in the database
            var conflictingAlliances = trackedAlliances
                .Where(a => existingAllianceIds.Contains(a.AllianceId))
                .ToList();

            foreach (var conflictingAlliance in conflictingAlliances)
            {
                _context.Entry(conflictingAlliance).State = EntityState.Detached;
                _logger.LogWarning("Removed alliance with existing ID {AllianceId} from change tracker", conflictingAlliance.AllianceId);
            }
        }

        var finalAllianceCount = trackedAlliances.Count - duplicateGroups.Sum(g => g.Count() - 1) - existingAllianceIds.Count;
        _logger.LogInformation("About to save {Count} alliances to database", finalAllianceCount);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Successfully saved {Count} alliances to database", finalAllianceCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save alliances to database. Error: {ErrorMessage}", ex.Message);
            throw; // Re-throw to trigger rollback
        }
    }

    private static string GetTileKey(Tile tile) => $"{tile.X},{tile.Y}";
}
