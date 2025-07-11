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

    public async Task<ChangeSet<Player>> DetectPlayerChangesAsync(List<Player> incoming, List<Player> current, List<Tile> incomingTiles, List<Tile> currentTiles)
    {
        _logger.LogInformation("Detecting player changes with tile data. Incoming: {IncomingCount}, Current: {CurrentCount}, IncomingTiles: {IncomingTileCount}, CurrentTiles: {CurrentTileCount}",
            incoming.Count, current.Count, incomingTiles.Count, currentTiles.Count);

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

        // Find modified players (including city coordinate and wilderness changes)
        foreach (var kvp in incomingDict)
        {
            if (currentDict.TryGetValue(kvp.Key, out var currentPlayer))
            {
                var incomingPlayer = kvp.Value;
                var hasBasicChanges = !PlayersEqual(currentPlayer, incomingPlayer);
                var hasCityCoordChanges = HasCityCoordinateChanged(currentPlayer.PlayerId, currentTiles, incomingTiles);
                var wildernessChanges = GetWildernessOwnershipChanges(currentPlayer.PlayerId, currentTiles, incomingTiles);
                var hasWildernessChanges = wildernessChanges.Gained.Any() || wildernessChanges.Lost.Any();

                if (hasBasicChanges || hasCityCoordChanges || hasWildernessChanges)
                {
                    changeSet.Modified.Add(incomingPlayer);
                    changeSet.Changes.Add((currentPlayer, incomingPlayer));

                    // Log detailed change information
                    if (hasCityCoordChanges)
                    {
                        var currentCoords = GetPlayerCityCoordinates(currentPlayer.PlayerId, currentTiles);
                        var incomingCoords = GetPlayerCityCoordinates(incomingPlayer.PlayerId, incomingTiles);
                        _logger.LogInformation("Player {PlayerName} (ID: {PlayerId}) city moved from ({OldX},{OldY}) to ({NewX},{NewY})",
                            currentPlayer.Name, currentPlayer.PlayerId, currentCoords.X, currentCoords.Y, incomingCoords.X, incomingCoords.Y);
                    }

                    if (hasWildernessChanges)
                    {
                        _logger.LogInformation("Player {PlayerName} (ID: {PlayerId}) wilderness changes: +{Gained} gained, -{Lost} lost",
                            currentPlayer.Name, currentPlayer.PlayerId, wildernessChanges.Gained.Count, wildernessChanges.Lost.Count);
                    }
                }
            }
        }

        _logger.LogInformation("Player changes detected with tile analysis. Added: {Added}, Modified: {Modified}, Removed: {Removed}",
            changeSet.Added.Count, changeSet.Modified.Count, changeSet.Removed.Count);

        if (changeSet.Removed.Any())
        {
            _logger.LogInformation("Players to be removed: {RemovedPlayers}",
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
               current.Might == incoming.Might &&
               current.AllianceId == incoming.AllianceId;
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

    // Helper methods for city coordinate extraction
    private static (int? X, int? Y) GetPlayerCityCoordinates(string playerId, List<Tile> tiles)
    {
        var cityTile = tiles.FirstOrDefault(t => t.PlayerId == playerId && t.Type == "City");
        return cityTile != null ? (cityTile.X, cityTile.Y) : (null, null);
    }

    private static bool HasCityCoordinateChanged(string playerId, List<Tile> currentTiles, List<Tile> incomingTiles)
    {
        var currentCoords = GetPlayerCityCoordinates(playerId, currentTiles);
        var incomingCoords = GetPlayerCityCoordinates(playerId, incomingTiles);

        return currentCoords.X != incomingCoords.X || currentCoords.Y != incomingCoords.Y;
    }

    // Helper methods for wilderness tile identification and ownership changes
    private static bool IsWildernessTile(Tile tile)
    {
        // Wilderness tiles are non-City tiles that can be owned by players
        return tile.Type != "City" && !string.IsNullOrEmpty(tile.PlayerId) && tile.PlayerId != "0";
    }

    private static List<Tile> GetPlayerWildernessTiles(string playerId, List<Tile> tiles)
    {
        return tiles.Where(t => t.PlayerId == playerId && IsWildernessTile(t)).ToList();
    }

    private static (List<Tile> Gained, List<Tile> Lost) GetWildernessOwnershipChanges(string playerId, List<Tile> currentTiles, List<Tile> incomingTiles)
    {
        var currentWilderness = GetPlayerWildernessTiles(playerId, currentTiles);
        var incomingWilderness = GetPlayerWildernessTiles(playerId, incomingTiles);

        var currentKeys = currentWilderness.Select(GetTileKey).ToHashSet();
        var incomingKeys = incomingWilderness.Select(GetTileKey).ToHashSet();

        var gained = incomingWilderness.Where(t => !currentKeys.Contains(GetTileKey(t))).ToList();
        var lost = currentWilderness.Where(t => !incomingKeys.Contains(GetTileKey(t))).ToList();

        return (gained, lost);
    }
}
