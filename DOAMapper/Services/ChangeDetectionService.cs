using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;
using System.Collections.Concurrent;

namespace DOAMapper.Services;

public class ChangeDetectionService : IChangeDetectionService
{
    private readonly ILogger<ChangeDetectionService> _logger;

    public ChangeDetectionService(ILogger<ChangeDetectionService> logger)
    {
        _logger = logger;
    }

    // Optimized tile key structure to avoid string allocations
    private readonly struct TileKey : IEquatable<TileKey>
    {
        public readonly int X;
        public readonly int Y;

        public TileKey(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(TileKey other) => X == other.X && Y == other.Y;
        public override bool Equals(object? obj) => obj is TileKey other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
    }

    public async Task<ChangeSet<Tile>> DetectTileChangesAsync(List<Tile> incoming, List<Tile> current)
    {
        _logger.LogInformation("Detecting tile changes. Incoming: {IncomingCount}, Current: {CurrentCount}",
            incoming.Count, current.Count);

        var changeSet = new ChangeSet<Tile>();

        // Pre-compute tile keys to avoid repeated string operations
        var currentTileMap = new Dictionary<TileKey, Tile>(current.Count);
        var incomingTileMap = new Dictionary<TileKey, Tile>(incoming.Count);

        // Build current tiles map
        foreach (var tile in current)
        {
            var key = new TileKey(tile.X, tile.Y);
            currentTileMap[key] = tile;
        }

        // Build incoming tiles map and find added/modified tiles in one pass
        var addedTiles = new List<Tile>();
        var modifiedTiles = new List<Tile>();
        var changes = new List<(Tile Old, Tile New)>();

        foreach (var incomingTile in incoming)
        {
            var key = new TileKey(incomingTile.X, incomingTile.Y);
            incomingTileMap[key] = incomingTile;

            if (currentTileMap.TryGetValue(key, out var currentTile))
            {
                // Check if modified
                if (!TilesEqual(currentTile, incomingTile))
                {
                    modifiedTiles.Add(incomingTile);
                    changes.Add((currentTile, incomingTile));
                }
            }
            else
            {
                // New tile
                addedTiles.Add(incomingTile);
            }
        }

        // Find removed tiles
        var removedTiles = new List<Tile>();
        foreach (var kvp in currentTileMap)
        {
            if (!incomingTileMap.ContainsKey(kvp.Key))
            {
                removedTiles.Add(kvp.Value);
            }
        }

        changeSet.Added = addedTiles;
        changeSet.Modified = modifiedTiles;
        changeSet.Removed = removedTiles;
        changeSet.Changes = changes;

        _logger.LogInformation("Tile changes detected. Added: {Added}, Modified: {Modified}, Removed: {Removed}",
            changeSet.Added.Count, changeSet.Modified.Count, changeSet.Removed.Count);

        return changeSet;
    }

    public async Task<ChangeSet<Player>> DetectPlayerChangesAsync(List<Player> incoming, List<Player> current)
    {
        _logger.LogInformation("Detecting player changes. Incoming: {IncomingCount}, Current: {CurrentCount}", 
            incoming.Count, current.Count);
            
        var changeSet = new ChangeSet<Player>();
        
        // Use HashSet for faster lookups and pre-size collections
        var currentPlayerMap = new Dictionary<string, Player>(current.Count);
        var incomingPlayerIds = new HashSet<string>(incoming.Count);

        // Build current players map and check for duplicates
        var currentDuplicates = 0;
        foreach (var player in current)
        {
            if (!currentPlayerMap.TryAdd(player.PlayerId, player))
                currentDuplicates++;
        }

        // Process incoming players in one pass
        var addedPlayers = new List<Player>();
        var modifiedPlayers = new List<Player>();
        var changes = new List<(Player Old, Player New)>();
        var incomingDuplicates = 0;

        foreach (var incomingPlayer in incoming)
        {
            if (!incomingPlayerIds.Add(incomingPlayer.PlayerId))
            {
                incomingDuplicates++;
                continue; // Skip duplicates
            }

            if (currentPlayerMap.TryGetValue(incomingPlayer.PlayerId, out var currentPlayer))
            {
                // Check if modified
                if (!PlayersEqual(currentPlayer, incomingPlayer))
                {
                    modifiedPlayers.Add(incomingPlayer);
                    changes.Add((currentPlayer, incomingPlayer));
                }
            }
            else
            {
                // New player
                addedPlayers.Add(incomingPlayer);
            }
        }

        // Find removed players
        var removedPlayers = new List<Player>();
        foreach (var kvp in currentPlayerMap)
        {
            if (!incomingPlayerIds.Contains(kvp.Key))
            {
                removedPlayers.Add(kvp.Value);
            }
        }

        changeSet.Added = addedPlayers;
        changeSet.Modified = modifiedPlayers;
        changeSet.Removed = removedPlayers;
        changeSet.Changes = changes;

        // Log warnings for duplicates
        if (currentDuplicates > 0)
            _logger.LogWarning("Found {Count} duplicate player IDs in current data", currentDuplicates);
        if (incomingDuplicates > 0)
            _logger.LogWarning("Found {Count} duplicate player IDs in incoming data", incomingDuplicates);

        _logger.LogInformation("Player changes detected. Added: {Added}, Modified: {Modified}, Removed: {Removed}",
            changeSet.Added.Count, changeSet.Modified.Count, changeSet.Removed.Count);

        if (changeSet.Removed.Any())
        {
            _logger.LogInformation("Players to be removed: {RemovedPlayers}",
                string.Join(", ", changeSet.Removed.Select(p => $"{p.Name} (ID: {p.PlayerId})")));
        }
            
        return changeSet;
    }

    public async Task<ChangeSet<Player>> DetectPlayerChangesAsync(List<Player> incoming, List<Player> current, List<Tile> incomingTiles, List<Tile> currentTiles)
    {
        _logger.LogInformation("Detecting player changes with tile data. Incoming: {IncomingCount}, Current: {CurrentCount}, IncomingTiles: {IncomingTileCount}, CurrentTiles: {CurrentTileCount}",
            incoming.Count, current.Count, incomingTiles.Count, currentTiles.Count);

        var changeSet = new ChangeSet<Player>();

        // Build optimized tile indexes for fast lookups
        var currentTilesByPlayer = BuildPlayerTileIndex(currentTiles);
        var incomingTilesByPlayer = BuildPlayerTileIndex(incomingTiles);

        // Use HashSet for faster lookups and pre-size collections
        var currentPlayerMap = new Dictionary<string, Player>(current.Count);
        var incomingPlayerIds = new HashSet<string>(incoming.Count);

        // Build current players map and check for duplicates
        var currentDuplicates = 0;
        foreach (var player in current)
        {
            if (!currentPlayerMap.TryAdd(player.PlayerId, player))
                currentDuplicates++;
        }

        // Process incoming players in one pass
        var addedPlayers = new List<Player>();
        var modifiedPlayers = new List<Player>();
        var changes = new List<(Player Old, Player New)>();
        var incomingDuplicates = 0;

        foreach (var incomingPlayer in incoming)
        {
            if (!incomingPlayerIds.Add(incomingPlayer.PlayerId))
            {
                incomingDuplicates++;
                continue; // Skip duplicates
            }

            if (currentPlayerMap.TryGetValue(incomingPlayer.PlayerId, out var currentPlayer))
            {
                // Check basic changes
                var hasBasicChanges = !PlayersEqual(currentPlayer, incomingPlayer);

                // Check city coordinate changes using indexed lookups
                var hasCityCoordChanges = HasCityCoordinateChangedOptimized(currentPlayer.PlayerId, currentTilesByPlayer, incomingTilesByPlayer);

                // Check wilderness changes using indexed lookups
                var wildernessChangeResult = GetWildernessOwnershipChangesOptimized(currentPlayer.PlayerId, currentTilesByPlayer, incomingTilesByPlayer);
                var hasWildernessChanges = wildernessChangeResult.Gained > 0 || wildernessChangeResult.Lost > 0;

                if (hasBasicChanges || hasCityCoordChanges || hasWildernessChanges)
                {
                    modifiedPlayers.Add(incomingPlayer);
                    changes.Add((currentPlayer, incomingPlayer));

                    // Log detailed change information (original logging style)
                    if (hasCityCoordChanges)
                    {
                        var currentCoords = GetPlayerCityCoordinatesOptimized(currentPlayer.PlayerId, currentTilesByPlayer);
                        var incomingCoords = GetPlayerCityCoordinatesOptimized(incomingPlayer.PlayerId, incomingTilesByPlayer);
                        _logger.LogInformation("Player {PlayerName} (ID: {PlayerId}) city moved from ({OldX},{OldY}) to ({NewX},{NewY})",
                            currentPlayer.Name, currentPlayer.PlayerId, currentCoords.X, currentCoords.Y, incomingCoords.X, incomingCoords.Y);
                    }

                    if (hasWildernessChanges)
                    {
                        _logger.LogInformation("Player {PlayerName} (ID: {PlayerId}) wilderness changes: +{Gained} gained, -{Lost} lost",
                            currentPlayer.Name, currentPlayer.PlayerId, wildernessChangeResult.Gained, wildernessChangeResult.Lost);
                    }
                }
            }
            else
            {
                // New player
                addedPlayers.Add(incomingPlayer);
            }
        }

        // Find removed players
        var removedPlayers = new List<Player>();
        foreach (var kvp in currentPlayerMap)
        {
            if (!incomingPlayerIds.Contains(kvp.Key))
            {
                removedPlayers.Add(kvp.Value);
            }
        }

        changeSet.Added = addedPlayers;
        changeSet.Modified = modifiedPlayers;
        changeSet.Removed = removedPlayers;
        changeSet.Changes = changes;

        // Log warnings for duplicates (original logging style)
        if (currentDuplicates > 0)
            _logger.LogWarning("Found {Count} duplicate player IDs in current data", currentDuplicates);
        if (incomingDuplicates > 0)
            _logger.LogWarning("Found {Count} duplicate player IDs in incoming data", incomingDuplicates);



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
    private static string GetTileKey(Tile tile) => $"{tile.X},{tile.Y}"; // Legacy method for compatibility

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

    // Optimized helper methods for better performance with large datasets
    private static Dictionary<string, List<Tile>> BuildPlayerTileIndex(List<Tile> tiles)
    {
        var index = new Dictionary<string, List<Tile>>();

        foreach (var tile in tiles)
        {
            if (!string.IsNullOrEmpty(tile.PlayerId) && tile.PlayerId != "0")
            {
                if (!index.TryGetValue(tile.PlayerId, out var playerTiles))
                {
                    playerTiles = new List<Tile>();
                    index[tile.PlayerId] = playerTiles;
                }
                playerTiles.Add(tile);
            }
        }

        return index;
    }

    private static (int? X, int? Y) GetPlayerCityCoordinatesOptimized(string playerId, Dictionary<string, List<Tile>> tileIndex)
    {
        if (tileIndex.TryGetValue(playerId, out var playerTiles))
        {
            var cityTile = playerTiles.FirstOrDefault(t => t.Type == "City");
            return cityTile != null ? (cityTile.X, cityTile.Y) : (null, null);
        }
        return (null, null);
    }

    private static bool HasCityCoordinateChangedOptimized(string playerId, Dictionary<string, List<Tile>> currentTileIndex, Dictionary<string, List<Tile>> incomingTileIndex)
    {
        var currentCoords = GetPlayerCityCoordinatesOptimized(playerId, currentTileIndex);
        var incomingCoords = GetPlayerCityCoordinatesOptimized(playerId, incomingTileIndex);

        return currentCoords.X != incomingCoords.X || currentCoords.Y != incomingCoords.Y;
    }

    private static (int Gained, int Lost) GetWildernessOwnershipChangesOptimized(string playerId, Dictionary<string, List<Tile>> currentTileIndex, Dictionary<string, List<Tile>> incomingTileIndex)
    {
        var currentWildernessKeys = new HashSet<TileKey>();
        var incomingWildernessKeys = new HashSet<TileKey>();

        // Count current wilderness tiles
        if (currentTileIndex.TryGetValue(playerId, out var currentPlayerTiles))
        {
            foreach (var tile in currentPlayerTiles)
            {
                if (IsWildernessTile(tile))
                {
                    currentWildernessKeys.Add(new TileKey(tile.X, tile.Y));
                }
            }
        }

        // Count incoming wilderness tiles
        if (incomingTileIndex.TryGetValue(playerId, out var incomingPlayerTiles))
        {
            foreach (var tile in incomingPlayerTiles)
            {
                if (IsWildernessTile(tile))
                {
                    incomingWildernessKeys.Add(new TileKey(tile.X, tile.Y));
                }
            }
        }

        // Calculate gained and lost counts
        var gained = incomingWildernessKeys.Count(key => !currentWildernessKeys.Contains(key));
        var lost = currentWildernessKeys.Count(key => !incomingWildernessKeys.Contains(key));

        return (gained, lost);
    }
}
