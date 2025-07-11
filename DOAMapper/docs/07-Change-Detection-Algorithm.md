# Change Detection Algorithm

## Overview

The change detection algorithm is a critical component that ensures efficient storage by only saving data that has actually changed between imports. This prevents database bloat and maintains optimal performance even with frequent data imports.

## Algorithm Design

### Core Principles

1. **Entity Comparison**: Compare incoming data with current active records
2. **Field-Level Detection**: Identify specific fields that have changed
3. **Temporal Versioning**: Maintain historical versions with validity periods
4. **Efficient Processing**: Use dictionary-based lookups for O(1) comparisons

### Change Types

- **Added**: New entities that don't exist in current data
- **Modified**: Existing entities with changed field values
- **Removed**: Entities that exist in current data but not in incoming data
- **Unchanged**: Entities that exist in both datasets with identical values

## Implementation

### Change Detection Service Interface

```csharp
// Services/Interfaces/IChangeDetectionService.cs
public interface IChangeDetectionService
{
    Task<ChangeSet<Tile>> DetectTileChangesAsync(List<Tile> incoming, List<Tile> current);
    Task<ChangeSet<Player>> DetectPlayerChangesAsync(List<Player> incoming, List<Player> current);
    Task<ChangeSet<Alliance>> DetectAllianceChangesAsync(List<Alliance> incoming, List<Alliance> current);
}

// Models/ChangeSet.cs
public class ChangeSet<T>
{
    public List<T> Added { get; set; } = new();
    public List<T> Modified { get; set; } = new();
    public List<T> Removed { get; set; } = new();
    public List<(T Old, T New)> Changes { get; set; } = new();
    
    public int TotalChanges => Added.Count + Modified.Count + Removed.Count;
    public bool HasChanges => TotalChanges > 0;
}
```

### Change Detection Service Implementation

```csharp
// Services/ChangeDetectionService.cs
public class ChangeDetectionService : IChangeDetectionService
{
    private readonly ILogger<ChangeDetectionService> _logger;

    public ChangeDetectionService(ILogger<ChangeDetectionService> logger)
    {
        _logger = logger;
    }

    public async Task<ChangeSet<Tile>> DetectTileChangesAsync(List<Tile> incoming, List<Tile> current)
    {
        _logger.LogInformation("Detecting tile changes. Incoming: {IncomingCount}, Current: {CurrentCount}", 
            incoming.Count, current.Count);
            
        var changeSet = new ChangeSet<Tile>();
        
        // Create dictionaries for efficient lookup
        var currentDict = current.ToDictionary(t => GetTileKey(t));
        var incomingDict = incoming.ToDictionary(t => GetTileKey(t));
        
        // Find added tiles
        changeSet.Added = incoming
            .Where(t => !currentDict.ContainsKey(GetTileKey(t)))
            .ToList();
            
        // Find removed tiles
        changeSet.Removed = current
            .Where(t => !incomingDict.ContainsKey(GetTileKey(t)))
            .ToList();
            
        // Find modified tiles
        foreach (var kvp in incomingDict)
        {
            if (currentDict.TryGetValue(kvp.Key, out var currentTile))
            {
                var incomingTile = kvp.Value;
                if (!TilesEqual(currentTile, incomingTile))
                {
                    changeSet.Modified.Add(incomingTile);
                    changeSet.Changes.Add((currentTile, incomingTile));
                }
            }
        }
        
        _logger.LogInformation("Tile changes detected. Added: {Added}, Modified: {Modified}, Removed: {Removed}",
            changeSet.Added.Count, changeSet.Modified.Count, changeSet.Removed.Count);
            
        return changeSet;
    }

    public async Task<ChangeSet<Player>> DetectPlayerChangesAsync(List<Player> incoming, List<Player> current)
    {
        _logger.LogInformation("Detecting player changes. Incoming: {IncomingCount}, Current: {CurrentCount}", 
            incoming.Count, current.Count);
            
        var changeSet = new ChangeSet<Player>();
        
        var currentDict = current.ToDictionary(p => p.PlayerId);
        var incomingDict = incoming.ToDictionary(p => p.PlayerId);
        
        // Find added players
        changeSet.Added = incoming
            .Where(p => !currentDict.ContainsKey(p.PlayerId))
            .ToList();
            
        // Find removed players
        changeSet.Removed = current
            .Where(p => !incomingDict.ContainsKey(p.PlayerId))
            .ToList();
            
        // Find modified players
        foreach (var kvp in incomingDict)
        {
            if (currentDict.TryGetValue(kvp.Key, out var currentPlayer))
            {
                var incomingPlayer = kvp.Value;
                if (!PlayersEqual(currentPlayer, incomingPlayer))
                {
                    changeSet.Modified.Add(incomingPlayer);
                    changeSet.Changes.Add((currentPlayer, incomingPlayer));
                }
            }
        }
        
        _logger.LogInformation("Player changes detected. Added: {Added}, Modified: {Modified}, Removed: {Removed}",
            changeSet.Added.Count, changeSet.Modified.Count, changeSet.Removed.Count);
            
        return changeSet;
    }

    public async Task<ChangeSet<Alliance>> DetectAllianceChangesAsync(List<Alliance> incoming, List<Alliance> current)
    {
        _logger.LogInformation("Detecting alliance changes. Incoming: {IncomingCount}, Current: {CurrentCount}", 
            incoming.Count, current.Count);
            
        var changeSet = new ChangeSet<Alliance>();
        
        var currentDict = current.ToDictionary(a => a.AllianceId);
        var incomingDict = incoming.ToDictionary(a => a.AllianceId);
        
        // Find added alliances
        changeSet.Added = incoming
            .Where(a => !currentDict.ContainsKey(a.AllianceId))
            .ToList();
            
        // Find removed alliances
        changeSet.Removed = current
            .Where(a => !incomingDict.ContainsKey(a.AllianceId))
            .ToList();
            
        // Find modified alliances
        foreach (var kvp in incomingDict)
        {
            if (currentDict.TryGetValue(kvp.Key, out var currentAlliance))
            {
                var incomingAlliance = kvp.Value;
                if (!AlliancesEqual(currentAlliance, incomingAlliance))
                {
                    changeSet.Modified.Add(incomingAlliance);
                    changeSet.Changes.Add((currentAlliance, incomingAlliance));
                }
            }
        }
        
        _logger.LogInformation("Alliance changes detected. Added: {Added}, Modified: {Modified}, Removed: {Removed}",
            changeSet.Added.Count, changeSet.Modified.Count, changeSet.Removed.Count);
            
        return changeSet;
    }

    // Helper methods for entity comparison
    private static string GetTileKey(Tile tile) => $"{tile.X},{tile.Y}";

    private static bool TilesEqual(Tile current, Tile incoming)
    {
        return current.Type == incoming.Type &&
               current.Level == incoming.Level &&
               current.PlayerId == incoming.PlayerId &&
               current.AllianceId == incoming.AllianceId;
    }

    private static bool PlayersEqual(Player current, Player incoming)
    {
        return current.Name == incoming.Name &&
               current.CityName == incoming.CityName &&
               current.Might == incoming.Might;
    }

    private static bool AlliancesEqual(Alliance current, Alliance incoming)
    {
        return current.Name == incoming.Name &&
               current.OverlordName == incoming.OverlordName &&
               current.Power == incoming.Power &&
               current.FortressLevel == incoming.FortressLevel &&
               current.FortressX == incoming.FortressX &&
               current.FortressY == incoming.FortressY;
    }
}
```

## Change Application Process

### Temporal Data Management

```csharp
// Services/TemporalDataService.cs
public class TemporalDataService
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
        var importDate = DateTime.UtcNow;
        
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
            var modifiedKeys = changes.Modified.Select(GetTileKey).ToHashSet();
            var tilesToUpdate = await _context.Tiles
                .Where(t => t.IsActive && modifiedKeys.Contains($"{t.X},{t.Y}"))
                .ToListAsync();
                
            foreach (var tile in tilesToUpdate)
            {
                tile.IsActive = false;
                tile.ValidTo = importDate;
            }
            
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

        // Handle added tiles
        if (changes.Added.Any())
        {
            foreach (var addedTile in changes.Added)
            {
                addedTile.ImportSessionId = importSessionId;
                addedTile.IsActive = true;
                addedTile.ValidFrom = importDate;
                _context.Tiles.Add(addedTile);
            }
            
            _logger.LogInformation("Added {Count} new tiles", changes.Added.Count);
        }

        await _context.SaveChangesAsync();
    }

    public async Task ApplyPlayerChangesAsync(ChangeSet<Player> changes, Guid importSessionId)
    {
        var importDate = DateTime.UtcNow;
        
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
            
            // Add new versions
            foreach (var modifiedPlayer in changes.Modified)
            {
                modifiedPlayer.ImportSessionId = importSessionId;
                modifiedPlayer.IsActive = true;
                modifiedPlayer.ValidFrom = importDate;
                _context.Players.Add(modifiedPlayer);
            }
        }

        // Handle added players
        if (changes.Added.Any())
        {
            foreach (var addedPlayer in changes.Added)
            {
                addedPlayer.ImportSessionId = importSessionId;
                addedPlayer.IsActive = true;
                addedPlayer.ValidFrom = importDate;
                _context.Players.Add(addedPlayer);
            }
        }

        await _context.SaveChangesAsync();
    }

    public async Task ApplyAllianceChangesAsync(ChangeSet<Alliance> changes, Guid importSessionId)
    {
        var importDate = DateTime.UtcNow;
        
        // Similar implementation for alliances
        // Handle removed, modified, and added alliances
        // Following the same pattern as tiles and players
        
        await _context.SaveChangesAsync();
    }
}
```

## Performance Optimizations

### Batch Processing

```csharp
public async Task ProcessLargeDatasetAsync(List<Tile> incomingTiles, List<Tile> currentTiles)
{
    const int batchSize = 1000;
    var totalBatches = (int)Math.Ceiling((double)incomingTiles.Count / batchSize);
    
    for (int i = 0; i < totalBatches; i++)
    {
        var batch = incomingTiles.Skip(i * batchSize).Take(batchSize).ToList();
        var relevantCurrent = currentTiles
            .Where(t => batch.Any(b => GetTileKey(b) == GetTileKey(t)))
            .ToList();
            
        var changes = await DetectTileChangesAsync(batch, relevantCurrent);
        await ApplyTileChangesAsync(changes, importSessionId);
        
        // Clear change tracker to prevent memory buildup
        _context.ChangeTracker.Clear();
    }
}
```

### Memory Management

```csharp
public async Task<ChangeSet<T>> DetectChangesWithMemoryOptimization<T>(
    IEnumerable<T> incoming, 
    IEnumerable<T> current,
    Func<T, string> keySelector,
    Func<T, T, bool> equalityComparer)
{
    var changeSet = new ChangeSet<T>();
    
    // Process in chunks to avoid loading everything into memory
    const int chunkSize = 5000;
    
    var currentDict = current
        .ToLookup(keySelector)
        .ToDictionary(g => g.Key, g => g.First());
    
    foreach (var chunk in incoming.Chunk(chunkSize))
    {
        var chunkDict = chunk.ToDictionary(keySelector);
        
        // Process this chunk
        foreach (var kvp in chunkDict)
        {
            if (!currentDict.TryGetValue(kvp.Key, out var currentItem))
            {
                changeSet.Added.Add(kvp.Value);
            }
            else if (!equalityComparer(currentItem, kvp.Value))
            {
                changeSet.Modified.Add(kvp.Value);
                changeSet.Changes.Add((currentItem, kvp.Value));
            }
        }
    }
    
    // Find removed items
    var incomingKeys = incoming.Select(keySelector).ToHashSet();
    changeSet.Removed = current
        .Where(c => !incomingKeys.Contains(keySelector(c)))
        .ToList();
    
    return changeSet;
}
```

## Testing the Change Detection Algorithm

### Unit Tests

```csharp
[Test]
public async Task DetectTileChanges_ShouldIdentifyAddedTiles()
{
    // Arrange
    var current = new List<Tile>
    {
        new Tile { X = 1, Y = 1, Type = "Mountain", Level = 5 }
    };
    
    var incoming = new List<Tile>
    {
        new Tile { X = 1, Y = 1, Type = "Mountain", Level = 5 },
        new Tile { X = 2, Y = 2, Type = "Forest", Level = 3 }
    };
    
    // Act
    var changes = await _changeDetectionService.DetectTileChangesAsync(incoming, current);
    
    // Assert
    Assert.AreEqual(1, changes.Added.Count);
    Assert.AreEqual(0, changes.Modified.Count);
    Assert.AreEqual(0, changes.Removed.Count);
    Assert.AreEqual(2, changes.Added[0].X);
}

[Test]
public async Task DetectPlayerChanges_ShouldIdentifyModifiedPlayers()
{
    // Arrange
    var current = new List<Player>
    {
        new Player { PlayerId = "123", Name = "Player1", Might = 1000 }
    };
    
    var incoming = new List<Player>
    {
        new Player { PlayerId = "123", Name = "Player1", Might = 1500 }
    };
    
    // Act
    var changes = await _changeDetectionService.DetectPlayerChangesAsync(incoming, current);
    
    // Assert
    Assert.AreEqual(0, changes.Added.Count);
    Assert.AreEqual(1, changes.Modified.Count);
    Assert.AreEqual(0, changes.Removed.Count);
    Assert.AreEqual(1500, changes.Modified[0].Might);
}
```

## Monitoring and Metrics

### Change Detection Metrics

```csharp
public class ChangeDetectionMetrics
{
    public int TotalIncomingRecords { get; set; }
    public int TotalCurrentRecords { get; set; }
    public int AddedRecords { get; set; }
    public int ModifiedRecords { get; set; }
    public int RemovedRecords { get; set; }
    public int UnchangedRecords { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public double ChangePercentage => TotalIncomingRecords > 0 
        ? (double)(AddedRecords + ModifiedRecords + RemovedRecords) / TotalIncomingRecords * 100 
        : 0;
}
```

This change detection algorithm ensures that only actual changes are stored in the database, significantly reducing storage requirements and maintaining optimal performance even with frequent large data imports.
