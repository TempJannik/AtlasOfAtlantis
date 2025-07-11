using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;

namespace DOAMapper.Services;

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
        
        // Handle duplicate player IDs by keeping the first occurrence
        var currentDict = current.GroupBy(p => p.PlayerId).ToDictionary(g => g.Key, g => g.First());
        var incomingDict = incoming.GroupBy(p => p.PlayerId).ToDictionary(g => g.Key, g => g.First());

        // Log if duplicates were found
        var currentDuplicates = current.GroupBy(p => p.PlayerId).Where(g => g.Count() > 1).Count();
        var incomingDuplicates = incoming.GroupBy(p => p.PlayerId).Where(g => g.Count() > 1).Count();

        if (currentDuplicates > 0)
            _logger.LogWarning("Found {Count} duplicate player IDs in current data", currentDuplicates);
        if (incomingDuplicates > 0)
            _logger.LogWarning("Found {Count} duplicate player IDs in incoming data", incomingDuplicates);
        
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
        
        _logger.LogInformation("?? Player changes detected. Added: {Added}, Modified: {Modified}, Removed: {Removed}",
            changeSet.Added.Count, changeSet.Modified.Count, changeSet.Removed.Count);

        if (changeSet.Removed.Any())
        {
            _logger.LogInformation("??? Players to be removed: {RemovedPlayers}",
                string.Join(", ", changeSet.Removed.Select(p => $"{p.Name} (ID: {p.PlayerId})")));
        }
            
        return changeSet;
    }

    public async Task<ChangeSet<Alliance>> DetectAllianceChangesAsync(List<Alliance> incoming, List<Alliance> current)
    {
        _logger.LogInformation("Detecting alliance changes. Incoming: {IncomingCount}, Current: {CurrentCount}", 
            incoming.Count, current.Count);
            
        var changeSet = new ChangeSet<Alliance>();
        
        // Handle duplicate alliance IDs by keeping the first occurrence
        var currentDict = current.GroupBy(a => a.AllianceId).ToDictionary(g => g.Key, g => g.First());
        var incomingDict = incoming.GroupBy(a => a.AllianceId).ToDictionary(g => g.Key, g => g.First());

        // Log if duplicates were found
        var currentDuplicates = current.GroupBy(a => a.AllianceId).Where(g => g.Count() > 1).Count();
        var incomingDuplicates = incoming.GroupBy(a => a.AllianceId).Where(g => g.Count() > 1).Count();

        if (currentDuplicates > 0)
            _logger.LogWarning("Found {Count} duplicate alliance IDs in current data", currentDuplicates);
        if (incomingDuplicates > 0)
            _logger.LogWarning("Found {Count} duplicate alliance IDs in incoming data", incomingDuplicates);
        
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
        
        _logger.LogInformation("?? Alliance changes detected. Added: {Added}, Modified: {Modified}, Removed: {Removed}",
            changeSet.Added.Count, changeSet.Modified.Count, changeSet.Removed.Count);

        if (changeSet.Removed.Any())
        {
            _logger.LogInformation("??? Alliances to be removed: {RemovedAlliances}",
                string.Join(", ", changeSet.Removed.Select(a => $"{a.Name} (ID: {a.AllianceId})")));
        }
            
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
