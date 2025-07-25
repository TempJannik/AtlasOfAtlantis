﻿using DOAMapper.Data;
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

    public async Task ApplyTileChangesAsync(ChangeSet<Tile> changes, Guid importSessionId, DateTime importDate)
    {
        
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
            _logger.LogInformation("🔄 TILE DEACTIVATION: Processing {Count} modified tiles for deactivation", changes.Modified.Count);

            // Log the coordinates being searched for
            var modifiedCoordinates = changes.Modified.Select(t => new { t.X, t.Y }).ToHashSet();
            _logger.LogInformation("🔍 TILE DEACTIVATION: Searching for tiles at coordinates: {Coordinates}",
                string.Join(", ", modifiedCoordinates.Take(10).Select(c => $"({c.X},{c.Y})")) +
                (modifiedCoordinates.Count > 10 ? $" and {modifiedCoordinates.Count - 10} more..." : ""));

            // Get all active tiles first, then filter in memory
            var allActiveTiles = await _context.Tiles
                .Where(t => t.IsActive)
                .ToListAsync();

            _logger.LogInformation("📊 TILE DEACTIVATION: Found {TotalActive} total active tiles in database", allActiveTiles.Count);

            var tilesToUpdate = allActiveTiles
                .Where(t => modifiedCoordinates.Contains(new { t.X, t.Y }))
                .ToList();

            _logger.LogInformation("🎯 TILE DEACTIVATION: Found {FoundCount} existing tiles to deactivate out of {SearchCount} modified tiles",
                tilesToUpdate.Count, changes.Modified.Count);

            if (tilesToUpdate.Any())
            {
                _logger.LogInformation("📝 TILE DEACTIVATION: Tiles to deactivate: {TileDetails}",
                    string.Join(", ", tilesToUpdate.Take(5).Select(t => $"({t.X},{t.Y})[ValidTo:{t.ValidTo}]")) +
                    (tilesToUpdate.Count > 5 ? $" and {tilesToUpdate.Count - 5} more..." : ""));
            }
            else
            {
                _logger.LogWarning("⚠️ TILE DEACTIVATION: No existing tiles found to deactivate! This may indicate a data matching issue.");
            }

            foreach (var tile in tilesToUpdate)
            {
                var oldValidTo = tile.ValidTo;
                tile.IsActive = false;
                tile.ValidTo = importDate;
                _logger.LogInformation("🔧 TILE DEACTIVATION: Updated tile ({X},{Y}) - IsActive: true→false, ValidTo: {OldValidTo}→{NewValidTo} (ImportDate: {ImportDate})",
                    tile.X, tile.Y, oldValidTo, importDate, importDate);
            }

            _logger.LogInformation("💾 TILE DEACTIVATION: Prepared {Count} deactivated tiles for transaction commit", tilesToUpdate.Count);

            // Note: Not calling SaveChanges() here as we're within a larger transaction
            // The ValidTo updates will be committed when the main import transaction commits
            _logger.LogInformation("✅ TILE DEACTIVATION: ValidTo updates prepared for {Count} tiles (will commit with main transaction)", tilesToUpdate.Count);

            // Note: NOT clearing change tracker here as it would wipe out the ValidTo updates
            // The ValidTo updates need to remain tracked until the main transaction commits

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

        // Check for conflicts with existing ACTIVE tiles only
        // Note: We exclude coordinates that we just deactivated in the modified tiles step
        var tileCoordinates = trackedTiles.Select(t => new { t.X, t.Y }).ToHashSet();
        var deactivatedTileCoordinates = changes.Modified.Select(t => new { t.X, t.Y }).ToHashSet();

        // Get existing active tiles only - WITHIN THE SAME REALM, excluding those we just deactivated
        // Get the realm for this import session
        var realmId = await _context.ImportSessions
            .AsNoTracking()
            .Where(s => s.Id == importSessionId)
            .Select(s => s.RealmId)
            .FirstOrDefaultAsync();

        var existingActiveTileCoordinates = await _context.Tiles
            .Join(_context.ImportSessions, t => t.ImportSessionId, s => s.Id, (t, s) => new { Tile = t, Session = s })
            .Where(ts => ts.Tile.IsActive && ts.Session.RealmId == realmId)
            .Select(ts => new { ts.Tile.X, ts.Tile.Y })
            .ToListAsync();

        var conflictingTileCoordinates = existingActiveTileCoordinates
            .Where(t => tileCoordinates.Contains(new { t.X, t.Y }) && !deactivatedTileCoordinates.Contains(new { t.X, t.Y }))
            .ToList();

        if (conflictingTileCoordinates.Any())
        {
            _logger.LogError("Found {Count} tile coordinates that already exist as ACTIVE in database: {ExistingKeys}",
                conflictingTileCoordinates.Count,
                string.Join(", ", conflictingTileCoordinates.Select(t => $"{t.X},{t.Y}")));

            // Remove tiles that already exist as active in the database
            var conflictingTiles = trackedTiles
                .Where(t => conflictingTileCoordinates.Any(existing => existing.X == t.X && existing.Y == t.Y))
                .ToList();

            foreach (var conflictingTile in conflictingTiles)
            {
                _context.Entry(conflictingTile).State = EntityState.Detached;
                _logger.LogWarning("Removed tile with existing ACTIVE coordinates ({X},{Y}) from change tracker", conflictingTile.X, conflictingTile.Y);
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

    public async Task ApplyPlayerChangesAsync(ChangeSet<Player> changes, Guid importSessionId, DateTime importDate)
    {
        
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
            _logger.LogInformation("🔄 PLAYER DEACTIVATION: Processing {Count} modified players for deactivation", changes.Modified.Count);

            var modifiedIds = changes.Modified.Select(p => p.PlayerId).ToHashSet();
            _logger.LogInformation("🔍 PLAYER DEACTIVATION: Searching for players with IDs: {PlayerIds}",
                string.Join(", ", modifiedIds.Take(10)) +
                (modifiedIds.Count > 10 ? $" and {modifiedIds.Count - 10} more..." : ""));

            var playersToUpdate = await _context.Players
                .Where(p => p.IsActive && modifiedIds.Contains(p.PlayerId))
                .ToListAsync();

            _logger.LogInformation("🎯 PLAYER DEACTIVATION: Found {FoundCount} existing players to deactivate out of {SearchCount} modified players",
                playersToUpdate.Count, changes.Modified.Count);

            if (playersToUpdate.Any())
            {
                _logger.LogInformation("📝 PLAYER DEACTIVATION: Players to deactivate: {PlayerDetails}",
                    string.Join(", ", playersToUpdate.Take(5).Select(p => $"{p.Name}(ID:{p.PlayerId})[ValidTo:{p.ValidTo}]")) +
                    (playersToUpdate.Count > 5 ? $" and {playersToUpdate.Count - 5} more..." : ""));
            }
            else
            {
                _logger.LogWarning("⚠️ PLAYER DEACTIVATION: No existing players found to deactivate! This may indicate a data matching issue.");
            }

            foreach (var player in playersToUpdate)
            {
                var oldValidTo = player.ValidTo;
                var entityEntry = _context.Entry(player);
                _logger.LogInformation("🔍 EF STATE BEFORE: Player {Name}(ID:{PlayerId}) - State: {EntityState}, IsActive: {IsActive}, ValidTo: {ValidTo}",
                    player.Name, player.PlayerId, entityEntry.State, player.IsActive, player.ValidTo);

                // FIX: If entity is detached, attach it to the context
                if (entityEntry.State == EntityState.Detached)
                {
                    _context.Players.Attach(player);
                    entityEntry = _context.Entry(player); // Refresh the entry
                    _logger.LogInformation("🔧 EF ATTACH: Attached detached player {Name}(ID:{PlayerId}) - New State: {EntityState}",
                        player.Name, player.PlayerId, entityEntry.State);
                }

                player.IsActive = false;
                player.ValidTo = importDate;

                // FIX: Explicitly mark the entity as modified to ensure EF tracks the changes
                entityEntry.State = EntityState.Modified;

                _logger.LogInformation("🔍 EF STATE AFTER: Player {Name}(ID:{PlayerId}) - State: {EntityState}, IsActive: {IsActive}, ValidTo: {ValidTo}",
                    player.Name, player.PlayerId, entityEntry.State, player.IsActive, player.ValidTo);

                // Check if EF detected the property changes
                var isActiveProperty = entityEntry.Property(nameof(player.IsActive));
                var validToProperty = entityEntry.Property(nameof(player.ValidTo));
                _logger.LogInformation("🔍 EF CHANGE DETECTION: Player {Name}(ID:{PlayerId}) - IsActive.IsModified: {IsActiveModified}, ValidTo.IsModified: {ValidToModified}",
                    player.Name, player.PlayerId, isActiveProperty.IsModified, validToProperty.IsModified);

                _logger.LogInformation("🔧 PLAYER DEACTIVATION: Updated player {Name}(ID:{PlayerId}) - IsActive: true→false, ValidTo: {OldValidTo}→{NewValidTo} (ImportDate: {ImportDate})",
                    player.Name, player.PlayerId, oldValidTo, importDate, importDate);
            }

            _logger.LogInformation("💾 PLAYER DEACTIVATION: Prepared {Count} deactivated players for transaction commit", playersToUpdate.Count);

            // Note: Not calling SaveChanges() here as we're within a larger transaction
            // The ValidTo updates will be committed when the main import transaction commits
            _logger.LogInformation("✅ PLAYER DEACTIVATION: ValidTo updates prepared for {Count} players (will commit with main transaction)", playersToUpdate.Count);

            // Note: NOT clearing change tracker here as it would wipe out the ValidTo updates
            // The ValidTo updates need to remain tracked until the main transaction commits

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

        // Check for conflicts with existing ACTIVE players only - WITHIN THE SAME REALM
        // Note: We exclude PlayerIds that we just deactivated in the modified players step
        var playerIdsToAdd = trackedPlayers.Select(p => p.PlayerId).ToList();
        var deactivatedPlayerIds = changes.Modified.Select(p => p.PlayerId).ToHashSet();

        // Get the realm for this import session
        var realmId = await _context.ImportSessions
            .AsNoTracking()
            .Where(s => s.Id == importSessionId)
            .Select(s => s.RealmId)
            .FirstOrDefaultAsync();

        var existingActivePlayerIds = await _context.Players
            .Join(_context.ImportSessions, p => p.ImportSessionId, s => s.Id, (p, s) => new { Player = p, Session = s })
            .Where(ps => ps.Player.IsActive &&
                        playerIdsToAdd.Contains(ps.Player.PlayerId) &&
                        !deactivatedPlayerIds.Contains(ps.Player.PlayerId) &&
                        ps.Session.RealmId == realmId)
            .Select(ps => ps.Player.PlayerId)
            .ToListAsync();

        if (existingActivePlayerIds.Any())
        {
            _logger.LogError("Found {Count} PlayerIds that already exist as ACTIVE in database: {ExistingIds}",
                existingActivePlayerIds.Count,
                string.Join(", ", existingActivePlayerIds));

            // Remove players that already exist as active in the database
            var conflictingPlayers = trackedPlayers
                .Where(p => existingActivePlayerIds.Contains(p.PlayerId))
                .ToList();

            foreach (var conflictingPlayer in conflictingPlayers)
            {
                _context.Entry(conflictingPlayer).State = EntityState.Detached;
                _logger.LogWarning("Removed player with existing ACTIVE ID {PlayerId} from change tracker", conflictingPlayer.PlayerId);
            }
        }

        var finalPlayerCount = trackedPlayers.Count - duplicateGroups.Sum(g => g.Count() - 1) - existingActivePlayerIds.Count;
        _logger.LogInformation("About to save {Count} players to database", finalPlayerCount);

        // Check what entities EF is tracking for changes before final SaveChanges
        var allTrackedEntities = _context.ChangeTracker.Entries()
            .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Modified || e.State == Microsoft.EntityFrameworkCore.EntityState.Added)
            .ToList();
        _logger.LogInformation("🔍 EF FINAL TRACKING: Found {ModifiedCount} modified + {AddedCount} added = {TotalCount} entities before final SaveChanges",
            allTrackedEntities.Count(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Modified),
            allTrackedEntities.Count(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added),
            allTrackedEntities.Count);

        var modifiedPlayers = allTrackedEntities
            .Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Modified && e.Entity is Player)
            .Take(3) // Log first 3 for brevity
            .ToList();

        foreach (var entry in modifiedPlayers)
        {
            if (entry.Entity is Player p)
            {
                _logger.LogInformation("🔍 EF TRACKED MODIFIED PLAYER: {Name}(ID:{PlayerId}) - IsActive: {IsActive}, ValidTo: {ValidTo}",
                    p.Name, p.PlayerId, p.IsActive, p.ValidTo);
            }
        }

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

    public async Task ApplyAllianceChangesAsync(ChangeSet<Alliance> changes, Guid importSessionId, DateTime importDate)
    {
        
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
            _logger.LogInformation("🔄 ALLIANCE DEACTIVATION: Processing {Count} modified alliances for deactivation", changes.Modified.Count);

            var modifiedIds = changes.Modified.Select(a => a.AllianceId).ToHashSet();
            _logger.LogInformation("🔍 ALLIANCE DEACTIVATION: Searching for alliances with IDs: {AllianceIds}",
                string.Join(", ", modifiedIds.Take(10)) +
                (modifiedIds.Count > 10 ? $" and {modifiedIds.Count - 10} more..." : ""));

            var alliancesToUpdate = await _context.Alliances
                .Where(a => a.IsActive && modifiedIds.Contains(a.AllianceId))
                .ToListAsync();

            _logger.LogInformation("🎯 ALLIANCE DEACTIVATION: Found {FoundCount} existing alliances to deactivate out of {SearchCount} modified alliances",
                alliancesToUpdate.Count, changes.Modified.Count);

            if (alliancesToUpdate.Any())
            {
                _logger.LogInformation("📝 ALLIANCE DEACTIVATION: Alliances to deactivate: {AllianceDetails}",
                    string.Join(", ", alliancesToUpdate.Take(5).Select(a => $"{a.Name}(ID:{a.AllianceId})[ValidTo:{a.ValidTo}]")) +
                    (alliancesToUpdate.Count > 5 ? $" and {alliancesToUpdate.Count - 5} more..." : ""));
            }
            else
            {
                _logger.LogWarning("⚠️ ALLIANCE DEACTIVATION: No existing alliances found to deactivate! This may indicate a data matching issue.");
            }

            foreach (var alliance in alliancesToUpdate)
            {
                var oldValidTo = alliance.ValidTo;
                alliance.IsActive = false;
                alliance.ValidTo = importDate;
                _logger.LogInformation("🔧 ALLIANCE DEACTIVATION: Updated alliance {Name}(ID:{AllianceId}) - IsActive: true→false, ValidTo: {OldValidTo}→{NewValidTo} (ImportDate: {ImportDate})",
                    alliance.Name, alliance.AllianceId, oldValidTo, importDate, importDate);
            }

            _logger.LogInformation("💾 ALLIANCE DEACTIVATION: Prepared {Count} deactivated alliances for transaction commit", alliancesToUpdate.Count);

            // Note: Not calling SaveChanges() here as we're within a larger transaction
            // The ValidTo updates will be committed when the main import transaction commits
            _logger.LogInformation("✅ ALLIANCE DEACTIVATION: ValidTo updates prepared for {Count} alliances (will commit with main transaction)", alliancesToUpdate.Count);

            // Note: NOT clearing change tracker here as it would wipe out the ValidTo updates
            // The ValidTo updates need to remain tracked until the main transaction commits

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

        // Check for conflicts with existing ACTIVE alliances only - WITHIN THE SAME REALM
        // Note: We exclude AllianceIds that we just deactivated in the modified alliances step
        var allianceIdsToAdd = trackedAlliances.Select(a => a.AllianceId).ToList();
        var deactivatedAllianceIds = changes.Modified.Select(a => a.AllianceId).ToHashSet();

        // Get the realm for this import session
        var realmId = await _context.ImportSessions
            .AsNoTracking()
            .Where(s => s.Id == importSessionId)
            .Select(s => s.RealmId)
            .FirstOrDefaultAsync();

        var existingActiveAllianceIds = await _context.Alliances
            .Join(_context.ImportSessions, a => a.ImportSessionId, s => s.Id, (a, s) => new { Alliance = a, Session = s })
            .Where(as_ => as_.Alliance.IsActive &&
                         allianceIdsToAdd.Contains(as_.Alliance.AllianceId) &&
                         !deactivatedAllianceIds.Contains(as_.Alliance.AllianceId) &&
                         as_.Session.RealmId == realmId)
            .Select(as_ => as_.Alliance.AllianceId)
            .ToListAsync();

        if (existingActiveAllianceIds.Any())
        {
            _logger.LogError("Found {Count} AllianceIds that already exist as ACTIVE in database: {ExistingIds}",
                existingActiveAllianceIds.Count,
                string.Join(", ", existingActiveAllianceIds));

            // Remove alliances that already exist as active in the database
            var conflictingAlliances = trackedAlliances
                .Where(a => existingActiveAllianceIds.Contains(a.AllianceId))
                .ToList();

            foreach (var conflictingAlliance in conflictingAlliances)
            {
                _context.Entry(conflictingAlliance).State = EntityState.Detached;
                _logger.LogWarning("Removed alliance with existing ACTIVE ID {AllianceId} from change tracker", conflictingAlliance.AllianceId);
            }
        }

        var finalAllianceCount = trackedAlliances.Count - duplicateGroups.Sum(g => g.Count() - 1) - existingActiveAllianceIds.Count;
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
