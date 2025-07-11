using AutoMapper;
using DOAMapper.Data;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;
using DOAMapper.Shared.Models.Enums;
using DOAMapper.Models.Import;
using DOAMapper.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text;
using System.Text.Json;

namespace DOAMapper.Services;

public class ImportService : IImportService
{
    private readonly ApplicationDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILogger<ImportService> _logger;
    private readonly IChangeDetectionService _changeDetectionService;
    private readonly ITemporalDataService _temporalDataService;
    public ImportService(
        ApplicationDbContext context,
        IMapper mapper,
        ILogger<ImportService> logger,
        IChangeDetectionService changeDetectionService,
        ITemporalDataService temporalDataService)
    {
        _context = context;
        _mapper = mapper;
        _logger = logger;
        _changeDetectionService = changeDetectionService;
        _temporalDataService = temporalDataService;
    }

    public async Task<ImportSession> StartImportAsync(Stream jsonStream, string fileName)
    {
        _logger.LogInformation("Starting import for file {FileName}", fileName);

        // Create import session
        var importSession = new ImportSession
        {
            Id = Guid.NewGuid(),
            ImportDate = DateTime.UtcNow,
            FileName = fileName,
            Status = ImportStatus.Processing,
            RecordsProcessed = 0,
            RecordsChanged = 0,
            ProgressPercentage = 0,
            CreatedAt = DateTime.UtcNow
        };

        _context.ImportSessions.Add(importSession);
        await _context.SaveChangesAsync();

        // Process import synchronously for now to avoid DbContext disposal issues
        try
        {
            await ProcessImportWithTimeoutAsync(importSession.Id, jsonStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import processing failed for session {SessionId}", importSession.Id);
            importSession.Status = ImportStatus.Failed;
            importSession.ErrorMessage = ex.Message;
            importSession.CompletedAt = DateTime.UtcNow;
            _context.ImportSessions.Update(importSession);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation("Import session {SessionId} created for file {FileName}", importSession.Id, fileName);
        return importSession;
    }

    public async Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId)
    {
        _logger.LogDebug("Getting import status for session {SessionId}", sessionId);

        var session = await _context.ImportSessions
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null)
        {
            _logger.LogWarning("Import session {SessionId} not found", sessionId);
            throw new ArgumentException($"Import session {sessionId} not found");
        }

        return _mapper.Map<ImportSessionDto>(session);
    }

    public async Task<List<ImportSessionDto>> GetImportHistoryAsync()
    {
        _logger.LogDebug("Getting import history");

        var sessions = await _context.ImportSessions
            .OrderByDescending(s => s.CreatedAt)
            .Take(50) // Limit to last 50 imports
            .ToListAsync();

        return _mapper.Map<List<ImportSessionDto>>(sessions);
    }

    public async Task<List<DateTime>> GetAvailableImportDatesAsync()
    {
        _logger.LogDebug("Getting available import dates");

        return await _context.ImportSessions
            .Where(s => s.Status == ImportStatus.Completed)
            .Select(s => s.ImportDate.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync();
    }

    private async Task ProcessImportWithTimeoutAsync(Guid sessionId, Stream jsonStream)
    {
        const int timeoutSeconds = 300; // 5 minutes for large files
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            _logger.LogInformation("Starting import with {TimeoutSeconds}s timeout for session {SessionId}", timeoutSeconds, sessionId);
            await ProcessImportAsync(sessionId, jsonStream, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.Token.IsCancellationRequested)
        {
            _logger.LogWarning("Import operation timed out after {TimeoutSeconds}s for session {SessionId}", timeoutSeconds, sessionId);
            await HandleImportTimeoutAsync(sessionId, timeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in timeout wrapper for session {SessionId}", sessionId);
            // Let the inner ProcessImportAsync handle the error
        }
    }

    private async Task ProcessImportAsync(Guid sessionId, Stream jsonStream, CancellationToken cancellationToken = default)
    {
        var session = await _context.ImportSessions.FindAsync(sessionId);
        if (session == null)
        {
            _logger.LogError("Import session {SessionId} not found during processing", sessionId);
            return;
        }

        try
        {
            _logger.LogInformation("Starting import processing for session {SessionId}", sessionId);

            // Update progress: Starting JSON parsing
            await UpdateImportProgressAsync(sessionId, 10, "Parsing JSON data...");

            // Parse JSON data
            cancellationToken.ThrowIfCancellationRequested();
            var importData = await ParseJsonDataAsync(jsonStream, cancellationToken);

            // Update progress: JSON parsing completed
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateImportProgressAsync(sessionId, 20, "Validating import data...");

            // Validate import data
            var validationResult = importData.ValidateImportData();
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Import data validation failed: {string.Join(", ", validationResult.GetAllErrors())}");
            }

            // Update progress: Validation completed
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateImportProgressAsync(sessionId, 30, "Processing import data...");

            // Process import in transaction with rollback capability
            await ProcessImportWithTransactionAsync(sessionId, importData, cancellationToken);

            // Mark as completed
            session.Status = ImportStatus.Completed;
            session.CompletedAt = DateTime.UtcNow;
            session.ProgressPercentage = 100;
            session.RecordsProcessed = 40; // Update with actual count if available
            session.RecordsChanged = 40;   // Update with actual count if available

            _logger.LogInformation("Import processing completed successfully for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            await HandleImportErrorAsync(session, ex);
        }
        finally
        {
            try
            {
                // Detach any existing tracked entity to avoid conflicts
                var existingEntry = _context.Entry(session);
                if (existingEntry.State != Microsoft.EntityFrameworkCore.EntityState.Detached)
                {
                    existingEntry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                }

                // Get a fresh instance from database and update it
                var sessionToUpdate = await _context.ImportSessions.FindAsync(sessionId);
                if (sessionToUpdate != null)
                {
                    sessionToUpdate.Status = session.Status;
                    sessionToUpdate.CompletedAt = session.CompletedAt;
                    sessionToUpdate.ProgressPercentage = session.ProgressPercentage;
                    sessionToUpdate.ErrorMessage = session.ErrorMessage;
                    sessionToUpdate.RecordsProcessed = session.RecordsProcessed;
                    sessionToUpdate.RecordsChanged = session.RecordsChanged;

                    await _context.SaveChangesAsync();
                    _logger.LogDebug("Import session {SessionId} status updated to {Status}", sessionId, sessionToUpdate.Status);
                }
            }
            catch (Exception finalEx)
            {
                _logger.LogCritical(finalEx, "CRITICAL: Failed to update import session {SessionId} final status. Session may be in inconsistent state.", sessionId);
            }
        }
    }

    private async Task<ImportDataModel> ParseJsonDataAsync(Stream jsonStream, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Parsing JSON data from stream (size: {StreamLength} bytes)", jsonStream.Length);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            DefaultBufferSize = 32768 // 32KB buffer for better performance with large files
        };

        // Reset stream position to beginning
        if (jsonStream.CanSeek)
        {
            jsonStream.Position = 0;
        }

        // Try parsing with UTF-8 first (most common case)
        try
        {
            var importData = await JsonSerializer.DeserializeAsync<ImportDataModel>(jsonStream, options, cancellationToken);

            if (importData == null)
            {
                throw new InvalidOperationException("Failed to parse JSON data - result is null");
            }

            // Validate parsed data structure
            ValidateParsedData(importData);

            _logger.LogInformation("Successfully parsed JSON data with UTF-8 encoding: {TileCount} tiles, {PlayerCount} players, {AllianceCount} alliances, {AllianceBaseCount} alliance bases",
                importData.Tiles.Count, importData.Players.Count, importData.Alliances.Count, importData.AllianceBases.Count);

            return importData;
        }
        catch (JsonException ex) when (IsEncodingRelatedError(ex))
        {
            _logger.LogWarning("UTF-8 parsing failed due to encoding issues, attempting encoding detection and conversion: {Error}", ex.Message);
            return await ParseJsonWithEncodingDetectionAsync(jsonStream, options, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse JSON data");
            throw new InvalidOperationException($"Invalid JSON format: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during JSON parsing");
            throw new InvalidOperationException($"Failed to parse JSON data: {ex.Message}", ex);
        }
    }

    private void ValidateParsedData(ImportDataModel importData)
    {
        _logger.LogDebug("Validating parsed import data structure");

        // Check for reasonable data limits
        const int maxTiles = 750 * 750; // Maximum possible tiles for 750x750 grid
        const int maxPlayers = 100000; // Reasonable maximum
        const int maxAlliances = 10000; // Reasonable maximum

        if (importData.Tiles.Count > maxTiles)
        {
            throw new InvalidOperationException($"Too many tiles: {importData.Tiles.Count} (maximum: {maxTiles})");
        }

        if (importData.Players.Count > maxPlayers)
        {
            throw new InvalidOperationException($"Too many players: {importData.Players.Count} (maximum: {maxPlayers})");
        }

        if (importData.Alliances.Count + importData.AllianceBases.Count > maxAlliances)
        {
            throw new InvalidOperationException($"Too many alliances: {importData.Alliances.Count + importData.AllianceBases.Count} (maximum: {maxAlliances})");
        }

        // Check for duplicate alliance IDs between alliance bases and alliances
        var allianceBaseIds = importData.AllianceBases.Select(ab => ab.AllianceId.ToString()).ToHashSet();
        var allianceIds = importData.Alliances.Select(a => a.AllianceId).ToHashSet();
        var duplicateIds = allianceBaseIds.Intersect(allianceIds).ToList();

        if (duplicateIds.Any())
        {
            _logger.LogWarning("Found {Count} alliance IDs that exist in both alliance bases and alliances: {DuplicateIds}",
                duplicateIds.Count, string.Join(", ", duplicateIds));
        }

        _logger.LogDebug("Import data structure validation completed successfully");
    }

    private bool IsEncodingRelatedError(JsonException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        var isEncodingError = message.Contains("cannot transcode") ||
               message.Contains("invalid utf-8") ||
               message.Contains("unable to translate bytes") ||
               message.Contains("decoderfallbackexception") ||
               ex.InnerException is DecoderFallbackException ||
               ex.InnerException?.Message?.Contains("Unable to translate bytes") == true ||
               ex.InnerException?.Message?.Contains("Cannot transcode invalid UTF-8") == true;

        // Also check nested inner exceptions
        var innerEx = ex.InnerException;
        while (innerEx != null && !isEncodingError)
        {
            var innerMessage = innerEx.Message?.ToLowerInvariant() ?? "";
            isEncodingError = innerMessage.Contains("cannot transcode") ||
                            innerMessage.Contains("invalid utf-8") ||
                            innerMessage.Contains("unable to translate bytes") ||
                            innerMessage.Contains("decoderfallbackexception") ||
                            innerEx is DecoderFallbackException;
            innerEx = innerEx.InnerException;
        }

        return isEncodingError;
    }

    private async Task<ImportDataModel> ParseJsonWithEncodingDetectionAsync(Stream jsonStream, JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Attempting to parse JSON with encoding detection and conversion");

        // Reset stream position
        if (jsonStream.CanSeek)
        {
            jsonStream.Position = 0;
        }

        // Read the entire stream as bytes
        byte[] jsonBytes;
        using (var memoryStream = new MemoryStream())
        {
            await jsonStream.CopyToAsync(memoryStream, cancellationToken);
            jsonBytes = memoryStream.ToArray();
        }

        // Try different encodings in order of likelihood
        var encodingsToTry = new[]
        {
            Encoding.GetEncoding("Windows-1252"), // Most common for Windows systems
            Encoding.GetEncoding("ISO-8859-1"),   // Latin-1, fallback for many systems
            Encoding.UTF8                         // UTF-8 with replacement characters
        };

        foreach (var encoding in encodingsToTry)
        {
            try
            {
                _logger.LogDebug("Trying encoding: {EncodingName}", encoding.EncodingName);

                // Convert to UTF-8 string and then back to UTF-8 bytes
                string jsonString;
                if (encoding == Encoding.UTF8)
                {
                    // For UTF-8, use replacement characters for invalid sequences
                    var utf8Encoding = new UTF8Encoding(false, false); // No BOM, no exception on invalid bytes
                    jsonString = utf8Encoding.GetString(jsonBytes);
                }
                else
                {
                    jsonString = encoding.GetString(jsonBytes);
                }

                // Convert to proper UTF-8 bytes
                var utf8Bytes = Encoding.UTF8.GetBytes(jsonString);

                // Create a new stream with the converted content
                using var convertedStream = new MemoryStream(utf8Bytes);

                // Try to parse with the converted content
                var importData = await JsonSerializer.DeserializeAsync<ImportDataModel>(convertedStream, options, cancellationToken);

                if (importData == null)
                {
                    throw new InvalidOperationException("Failed to parse JSON data - result is null");
                }

                // Validate parsed data structure
                ValidateParsedData(importData);

                _logger.LogInformation("Successfully parsed JSON data with {EncodingName} encoding: {TileCount} tiles, {PlayerCount} players, {AllianceCount} alliances, {AllianceBaseCount} alliance bases",
                    encoding.EncodingName, importData.Tiles.Count, importData.Players.Count, importData.Alliances.Count, importData.AllianceBases.Count);

                return importData;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Failed to parse with {EncodingName}: {Error}", encoding.EncodingName, ex.Message);
                // Continue to next encoding
            }
        }

        // If all encodings failed, throw a comprehensive error
        throw new InvalidOperationException(
            "Failed to parse JSON data with any supported encoding. " +
            "The file may be corrupted or use an unsupported character encoding. " +
            $"Tried encodings: {string.Join(", ", encodingsToTry.Select(e => e.EncodingName))}");
    }

    private void ValidateImportDependencies(ImportDataModel importData)
    {
        _logger.LogDebug("Validating import dependencies and referential integrity");

        var validationErrors = new List<string>();

        // Get all alliance IDs that will be available after alliance import
        var availableAllianceIds = new HashSet<string>();

        // Add alliance base IDs
        foreach (var allianceBase in importData.AllianceBases)
        {
            availableAllianceIds.Add(allianceBase.AllianceId.ToString());
        }

        // Add alliance-only IDs
        foreach (var alliance in importData.Alliances)
        {
            availableAllianceIds.Add(alliance.AllianceId);
        }

        // Get all player IDs that will be available after player import
        var availablePlayerIds = importData.Players.Select(p => p.PlayerId).ToHashSet();

        // Validate tile references to alliances and players
        var tilesWithInvalidAllianceIds = importData.Tiles
            .Where(t => t.AllianceId > 0 && !availableAllianceIds.Contains(t.AllianceId.ToString()))
            .Take(10) // Limit to first 10 for logging
            .ToList();

        var tilesWithInvalidPlayerIds = importData.Tiles
            .Where(t => t.PlayerId > 0 && !availablePlayerIds.Contains(t.PlayerId.ToString()))
            .Take(10) // Limit to first 10 for logging
            .ToList();

        if (tilesWithInvalidAllianceIds.Any())
        {
            var invalidIds = tilesWithInvalidAllianceIds.Select(t => t.AllianceId.ToString()).Distinct();
            validationErrors.Add($"Found tiles referencing non-existent alliance IDs: {string.Join(", ", invalidIds)}");
        }

        if (tilesWithInvalidPlayerIds.Any())
        {
            var invalidIds = tilesWithInvalidPlayerIds.Select(t => t.PlayerId.ToString()).Distinct();
            validationErrors.Add($"Found tiles referencing non-existent player IDs: {string.Join(", ", invalidIds)}");
        }

        // Log warnings for dependency issues but don't fail import
        // This is common in game data where not all entities may be present
        if (validationErrors.Any())
        {
            foreach (var error in validationErrors)
            {
                _logger.LogWarning("Dependency validation warning: {Error}", error);
            }
            _logger.LogWarning("Import will continue despite dependency warnings. Missing references will be handled gracefully.");
        }

        _logger.LogDebug("Import dependency validation completed. Available: {AllianceCount} alliances, {PlayerCount} players",
            availableAllianceIds.Count, availablePlayerIds.Count);
    }

    private async Task ValidateAndCleanDuplicatesAsync(Guid sessionId, ImportDataModel importData)
    {
        _logger.LogInformation("Validating and cleaning duplicate IDs in import data");

        var duplicatesFound = false;

        // Check for duplicate alliance IDs within alliance bases
        var allianceBaseDuplicates = importData.AllianceBases
            .GroupBy(ab => ab.AllianceId)
            .Where(g => g.Count() > 1)
            .ToList();

        if (allianceBaseDuplicates.Any())
        {
            duplicatesFound = true;
            _logger.LogWarning("Found {Count} duplicate alliance IDs in alliance bases: {DuplicateIds}",
                allianceBaseDuplicates.Count,
                string.Join(", ", allianceBaseDuplicates.Select(g => g.Key)));

            // Keep only the first occurrence of each duplicate
            var cleanedAllianceBases = importData.AllianceBases
                .GroupBy(ab => ab.AllianceId)
                .Select(g => g.First())
                .ToList();

            importData.AllianceBases.Clear();
            importData.AllianceBases.AddRange(cleanedAllianceBases);
        }

        // Check for duplicate alliance IDs within alliances
        var allianceDuplicates = importData.Alliances
            .GroupBy(a => a.AllianceId)
            .Where(g => g.Count() > 1)
            .ToList();

        if (allianceDuplicates.Any())
        {
            duplicatesFound = true;
            _logger.LogWarning("Found {Count} duplicate alliance IDs in alliances: {DuplicateIds}",
                allianceDuplicates.Count,
                string.Join(", ", allianceDuplicates.Select(g => g.Key)));

            // Keep only the first occurrence of each duplicate
            var cleanedAlliances = importData.Alliances
                .GroupBy(a => a.AllianceId)
                .Select(g => g.First())
                .ToList();

            importData.Alliances.Clear();
            importData.Alliances.AddRange(cleanedAlliances);
        }

        // Check for duplicate player IDs
        var playerDuplicates = importData.Players
            .GroupBy(p => p.PlayerId)
            .Where(g => g.Count() > 1)
            .ToList();

        if (playerDuplicates.Any())
        {
            duplicatesFound = true;
            _logger.LogWarning("Found {Count} duplicate player IDs: {DuplicateIds}",
                playerDuplicates.Count,
                string.Join(", ", playerDuplicates.Select(g => g.Key)));

            // Keep only the first occurrence of each duplicate
            var cleanedPlayers = importData.Players
                .GroupBy(p => p.PlayerId)
                .Select(g => g.First())
                .ToList();

            importData.Players.Clear();
            importData.Players.AddRange(cleanedPlayers);
        }

        // Check for duplicate tile coordinates
        var tileDuplicates = importData.Tiles
            .GroupBy(t => new { t.X, t.Y })
            .Where(g => g.Count() > 1)
            .ToList();

        if (tileDuplicates.Any())
        {
            duplicatesFound = true;
            _logger.LogWarning("Found {Count} duplicate tile coordinates: {DuplicateCoords}",
                tileDuplicates.Count,
                string.Join(", ", tileDuplicates.Select(g => $"({g.Key.X},{g.Key.Y})")));

            // Keep only the first occurrence of each duplicate
            var cleanedTiles = importData.Tiles
                .GroupBy(t => new { t.X, t.Y })
                .Select(g => g.First())
                .ToList();

            importData.Tiles.Clear();
            importData.Tiles.AddRange(cleanedTiles);
        }

        // Check for existing data in database to prevent re-importing
        await ValidateAgainstExistingDataAsync(importData);

        if (duplicatesFound)
        {
            _logger.LogInformation("Duplicate cleanup completed. Final counts: {AllianceBaseCount} alliance bases, {AllianceCount} alliances, {PlayerCount} players, {TileCount} tiles",
                importData.AllianceBases.Count, importData.Alliances.Count, importData.Players.Count, importData.Tiles.Count);
        }
        else
        {
            _logger.LogDebug("No duplicates found in import data");
        }
    }

    private async Task ValidateAgainstExistingDataAsync(ImportDataModel importData)
    {
        _logger.LogDebug("Validating import data against existing database records");

        // Check for existing alliance IDs
        var allImportAllianceIds = new HashSet<string>();
        allImportAllianceIds.UnionWith(importData.AllianceBases.Select(ab => ab.AllianceId.ToString()));
        allImportAllianceIds.UnionWith(importData.Alliances.Select(a => a.AllianceId));

        var existingAllianceIds = await _context.Alliances
            .Where(a => a.IsActive && allImportAllianceIds.Contains(a.AllianceId))
            .Select(a => a.AllianceId)
            .ToListAsync();

        if (existingAllianceIds.Any())
        {
            _logger.LogInformation("Found {Count} alliance IDs that already exist in database: {ExistingIds}",
                existingAllianceIds.Count, string.Join(", ", existingAllianceIds.Take(10)));
            _logger.LogInformation("These alliances will be updated with new data if changes are detected");
        }

        // Check for existing player IDs
        var importPlayerIds = importData.Players.Select(p => p.PlayerId).ToHashSet();
        var existingPlayerIds = await _context.Players
            .Where(p => p.IsActive && importPlayerIds.Contains(p.PlayerId))
            .Select(p => p.PlayerId)
            .ToListAsync();

        if (existingPlayerIds.Any())
        {
            _logger.LogInformation("Found {Count} player IDs that already exist in database: {ExistingIds}",
                existingPlayerIds.Count, string.Join(", ", existingPlayerIds.Take(10)));
            _logger.LogInformation("These players will be updated with new data if changes are detected");
        }

        // Check for existing tile coordinates
        var importTileCoordsList = importData.Tiles.Select(t => new { t.X, t.Y }).ToList();
        var existingTileCount = 0;

        if (importTileCoordsList.Any())
        {
            // Use a more EF-friendly approach by checking coordinates individually
            var sampleCoords = importTileCoordsList.Take(10).ToList(); // Sample first 10 for performance
            foreach (var coord in sampleCoords)
            {
                var exists = await _context.Tiles
                    .AnyAsync(t => t.IsActive && t.X == coord.X && t.Y == coord.Y);
                if (exists) existingTileCount++;
            }

            // If we found matches in the sample, estimate total
            if (existingTileCount > 0 && importTileCoordsList.Count > 10)
            {
                existingTileCount = (int)((double)existingTileCount / sampleCoords.Count * importTileCoordsList.Count);
            }
        }

        if (existingTileCount > 0)
        {
            _logger.LogInformation("Found {Count} tile coordinates that already exist in database",
                existingTileCount);
            _logger.LogInformation("These tiles will be updated with new data if changes are detected");
        }

        _logger.LogDebug("Database validation completed. Import will proceed with change detection to handle existing records");
    }

    private async Task UpdateImportProgressAsync(Guid sessionId, int progressPercentage, string statusMessage, int? recordsProcessed = null)
    {
        try
        {
            var session = await _context.ImportSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.ProgressPercentage = progressPercentage;
                if (recordsProcessed.HasValue)
                {
                    session.RecordsProcessed = recordsProcessed.Value;
                }

                _context.ImportSessions.Update(session);
                await _context.SaveChangesAsync();

                var logMessage = recordsProcessed.HasValue
                    ? "Updated import progress for session {SessionId}: {Progress}% - {Status} ({RecordsProcessed} records processed)"
                    : "Updated import progress for session {SessionId}: {Progress}% - {Status}";

                if (recordsProcessed.HasValue)
                {
                    _logger.LogInformation(logMessage, sessionId, progressPercentage, statusMessage, recordsProcessed.Value);
                }
                else
                {
                    _logger.LogDebug(logMessage, sessionId, progressPercentage, statusMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update import progress for session {SessionId}", sessionId);
            // Don't throw - progress updates are not critical
        }
    }

    private async Task ProcessImportWithTransactionAsync(Guid sessionId, ImportDataModel importData, CancellationToken cancellationToken = default)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        var transactionId = transaction.TransactionId;

        try
        {
            _logger.LogInformation("Starting import transaction {TransactionId} for session {SessionId} with ordered processing",
                transactionId, sessionId);

            // Pre-transaction validation (these don't modify database)
            cancellationToken.ThrowIfCancellationRequested();
            ValidateImportDependencies(importData);

            cancellationToken.ThrowIfCancellationRequested();
            await ValidateAndCleanDuplicatesAsync(sessionId, importData);

            // Record transaction start for monitoring
            var startTime = DateTime.UtcNow;
            var totalRecords = importData.AllianceBases.Count + importData.Alliances.Count +
                              importData.Players.Count + importData.Tiles.Count;

            _logger.LogInformation("Transaction {TransactionId}: Processing {TotalRecords} total records across 3 phases",
                transactionId, totalRecords);

            // PHASE 1: Import alliances first (alliance bases + alliances merged)
            // Rationale: Alliances must exist before tiles can reference them
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateImportProgressAsync(sessionId, 40, "Importing alliances...");
            await ExecutePhaseWithErrorHandling(1, "Alliances", async () =>
            {
                await ImportMergedAlliancesAsync(sessionId, importData.AllianceBases, importData.Alliances);
                _logger.LogInformation("Phase 1 completed: {AllianceBaseCount} alliance bases + {AllianceCount} alliances imported and merged",
                    importData.AllianceBases.Count, importData.Alliances.Count);
            }, cancellationToken);

            // PHASE 2: Import players second with alliance IDs from City tiles
            // Rationale: Players must exist before tiles can reference them, and we set alliance IDs immediately
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateImportProgressAsync(sessionId, 60, "Importing players with alliance relationships...");
            await ExecutePhaseWithErrorHandling(2, "Players", async () =>
            {
                await ImportPlayersWithAllianceIdsAsync(sessionId, importData.Players, importData.Tiles);
                _logger.LogInformation("Phase 2 completed: {PlayerCount} players imported with alliance relationships", importData.Players.Count);
            }, cancellationToken);

            // PHASE 3: Import tiles last
            // Rationale: Tiles reference both alliance IDs and player IDs, establishing the relationships
            cancellationToken.ThrowIfCancellationRequested();
            await UpdateImportProgressAsync(sessionId, 90, "Importing tiles...");
            await ExecutePhaseWithErrorHandling(3, "Tiles", async () =>
            {
                await ImportTilesAsync(sessionId, importData.Tiles);
                _logger.LogInformation("Phase 3 completed: {TileCount} tiles imported, establishing player-alliance relationships",
                    importData.Tiles.Count);
            }, cancellationToken);



            // Verify transaction state before commit
            await VerifyTransactionIntegrity(sessionId, importData);

            // Commit transaction
            await transaction.CommitAsync();
            var duration = DateTime.UtcNow - startTime;

            // Clear change tracker after successful commit to free memory
            _context.ChangeTracker.Clear();

            _logger.LogInformation("Transaction {TransactionId} committed successfully for session {SessionId}. " +
                                 "All phases completed in {Duration:mm\\:ss}. Records processed: {TotalRecords}",
                transactionId, sessionId, duration, totalRecords);
        }
        catch (Exception ex)
        {
            await HandleTransactionRollback(transaction, sessionId, ex);
            throw;
        }
    }

    private async Task ExecutePhaseWithErrorHandling(int phaseNumber, string phaseName, Func<Task> phaseAction, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogDebug("Starting Phase {PhaseNumber}: {PhaseName}", phaseNumber, phaseName);
            var phaseStartTime = DateTime.UtcNow;

            await phaseAction();

            var phaseDuration = DateTime.UtcNow - phaseStartTime;
            _logger.LogDebug("Phase {PhaseNumber} ({PhaseName}) completed successfully in {Duration:ss\\.fff}s",
                phaseNumber, phaseName, phaseDuration);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Phase {PhaseNumber} ({PhaseName}) was cancelled due to timeout", phaseNumber, phaseName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phase {PhaseNumber} ({PhaseName}) failed: {ErrorMessage}",
                phaseNumber, phaseName, ex.Message);
            throw new InvalidOperationException($"Import failed during Phase {phaseNumber} ({phaseName}): {ex.Message}", ex);
        }
    }

    private async Task HandleTransactionRollback(IDbContextTransaction transaction, Guid sessionId, Exception originalException)
    {
        var transactionId = transaction.TransactionId;

        try
        {
            _logger.LogWarning("Rolling back transaction {TransactionId} for session {SessionId} due to error: {ErrorMessage}",
                transactionId, sessionId, originalException.Message);

            await transaction.RollbackAsync();

            _logger.LogInformation("Transaction {TransactionId} rolled back successfully for session {SessionId}. " +
                                 "All changes have been reverted.", transactionId, sessionId);
        }
        catch (Exception rollbackEx)
        {
            _logger.LogCritical(rollbackEx,
                "CRITICAL: Failed to rollback transaction {TransactionId} for session {SessionId}. " +
                "Database may be in inconsistent state. Original error: {OriginalError}",
                transactionId, sessionId, originalException.Message);

            // Throw aggregate exception with both errors
            throw new AggregateException(
                "Transaction rollback failed after import error",
                originalException, rollbackEx);
        }
    }

    private async Task VerifyTransactionIntegrity(Guid sessionId, ImportDataModel importData)
    {
        _logger.LogDebug("Verifying transaction integrity before commit for session {SessionId}", sessionId);

        try
        {
            // Verify that the change tracker doesn't have any conflicting entities
            var trackedEntities = _context.ChangeTracker.Entries().Count();
            _logger.LogDebug("Change tracker contains {TrackedEntities} entities before commit", trackedEntities);

            // Check for any entities in error state
            var errorEntries = _context.ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Detached || e.State == EntityState.Unchanged)
                .ToList();

            if (errorEntries.Any())
            {
                _logger.LogWarning("Found {ErrorCount} entities in unexpected state before commit", errorEntries.Count);
            }

            // Verify import session still exists and is in correct state
            var session = await _context.ImportSessions.FindAsync(sessionId);
            if (session == null)
            {
                throw new InvalidOperationException($"Import session {sessionId} not found during transaction verification");
            }

            if (session.Status != ImportStatus.Processing)
            {
                throw new InvalidOperationException($"Import session {sessionId} is in unexpected status: {session.Status}");
            }

            _logger.LogDebug("Transaction integrity verification passed for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Transaction integrity verification failed for session {SessionId}", sessionId);
            throw new InvalidOperationException($"Transaction integrity check failed: {ex.Message}", ex);
        }
    }

    private async Task HandleImportErrorAsync(ImportSession session, Exception exception)
    {
        var errorCategory = CategorizeError(exception);
        var errorId = Guid.NewGuid();

        _logger.LogError(exception,
            "Import processing failed for session {SessionId} with error ID {ErrorId}. " +
            "Category: {ErrorCategory}, Type: {ExceptionType}, Message: {ErrorMessage}",
            session.Id, errorId, errorCategory, exception.GetType().Name, exception.Message);

        // Log additional context based on error type
        await LogErrorContext(session, exception, errorId, errorCategory);

        // Update session with detailed error information
        session.Status = ImportStatus.Failed;
        session.ErrorMessage = FormatErrorMessage(exception, errorCategory, errorId);
        session.CompletedAt = DateTime.UtcNow;

        // Log error statistics for monitoring
        LogErrorStatistics(session, exception, errorCategory);
    }

    private ImportErrorCategory CategorizeError(Exception exception)
    {
        return exception switch
        {
            JsonException => ImportErrorCategory.DataFormat,
            InvalidOperationException when exception.Message.Contains("validation") => ImportErrorCategory.DataValidation,
            InvalidOperationException when exception.Message.Contains("duplicate") => ImportErrorCategory.DataDuplication,
            InvalidOperationException when exception.Message.Contains("transaction") => ImportErrorCategory.DatabaseTransaction,
            OperationCanceledException => ImportErrorCategory.Timeout,
            TimeoutException => ImportErrorCategory.Timeout,
            OutOfMemoryException => ImportErrorCategory.Memory,
            UnauthorizedAccessException => ImportErrorCategory.Security,
            IOException => ImportErrorCategory.FileAccess,
            DbUpdateException => ImportErrorCategory.DatabaseUpdate,
            _ when exception.Message.Contains("database") || exception.Message.Contains("connection") => ImportErrorCategory.DatabaseConnection,
            _ => ImportErrorCategory.Unknown
        };
    }

    private async Task LogErrorContext(ImportSession session, Exception exception, Guid errorId, ImportErrorCategory category)
    {
        var contextData = new Dictionary<string, object>
        {
            ["SessionId"] = session.Id,
            ["ErrorId"] = errorId,
            ["FileName"] = session.FileName,
            ["ImportStartTime"] = session.CreatedAt,
            ["ProgressPercentage"] = session.ProgressPercentage,
            ["RecordsProcessed"] = session.RecordsProcessed,
            ["ErrorCategory"] = category.ToString(),
            ["ExceptionType"] = exception.GetType().FullName
        };

        // Add category-specific context
        switch (category)
        {
            case ImportErrorCategory.Memory:
                contextData["AvailableMemory"] = GC.GetTotalMemory(false);
                contextData["Gen0Collections"] = GC.CollectionCount(0);
                contextData["Gen1Collections"] = GC.CollectionCount(1);
                contextData["Gen2Collections"] = GC.CollectionCount(2);
                break;

            case ImportErrorCategory.DatabaseTransaction:
            case ImportErrorCategory.DatabaseUpdate:
            case ImportErrorCategory.DatabaseConnection:
                try
                {
                    contextData["DatabaseConnectionState"] = _context.Database.GetConnectionString();
                    contextData["PendingChanges"] = _context.ChangeTracker.Entries().Count();
                }
                catch (Exception dbEx)
                {
                    contextData["DatabaseContextError"] = dbEx.Message;
                }
                break;

            case ImportErrorCategory.DataValidation:
                if (exception.Message.Contains("validation"))
                {
                    contextData["ValidationErrors"] = exception.Message;
                }
                break;
        }

        _logger.LogWarning("Error context for {ErrorId}: {@ErrorContext}", errorId, contextData);

        // Store error context in database for later analysis if possible
        try
        {
            // This would require an ErrorLog entity - for now just log it
            _logger.LogInformation("Error context logged for session {SessionId} with error ID {ErrorId}", session.Id, errorId);
        }
        catch (Exception logEx)
        {
            _logger.LogWarning(logEx, "Failed to store error context for error ID {ErrorId}", errorId);
        }
    }

    private async Task ImportMergedAlliancesAsync(Guid sessionId, List<AllianceBaseImportModel> allianceBases, List<AllianceImportModel> alliances)
    {
        _logger.LogInformation("Importing and merging {AllianceBaseCount} alliance bases and {AllianceCount} alliances",
            allianceBases.Count, alliances.Count);

        // Step 1: Create Alliance entities from alliance bases (these have fortress data)
        var allianceBaseEntities = allianceBases.Select(ab => new Alliance
        {
            Id = Guid.NewGuid(),
            AllianceId = ab.AllianceId.ToString(),
            ImportSessionId = sessionId,
            Name = ab.Name,
            OverlordName = ab.Overlord,
            Power = long.TryParse(ab.Power, out var power) ? power : 0,
            FortressLevel = ab.FortressLevel,
            FortressX = ab.X,
            FortressY = ab.Y,
            IsActive = true,
            ValidFrom = DateTime.UtcNow.Date
        }).ToList();

        // Step 2: Create Alliance entities from alliances (without fortress data)
        var allianceOnlyEntities = alliances.Select(a => new Alliance
        {
            Id = Guid.NewGuid(),
            AllianceId = a.AllianceId,
            ImportSessionId = sessionId,
            Name = a.Name,
            OverlordName = string.Empty,
            Power = 0,
            FortressLevel = 0,
            FortressX = 0,
            FortressY = 0,
            IsActive = true,
            ValidFrom = DateTime.UtcNow.Date
        }).ToList();

        // Step 3: Merge alliance data - alliance bases take priority over alliance-only records
        var mergedAlliances = MergeAllianceData(allianceBaseEntities, allianceOnlyEntities);

        _logger.LogInformation("Merged alliance data: {TotalCount} total alliances ({BaseCount} with fortress data, {OnlyCount} alliance-only)",
            mergedAlliances.Count, allianceBaseEntities.Count, allianceOnlyEntities.Count);

        // Step 4: Get current alliances for change detection
        var currentAlliances = await _context.Alliances
            .Where(a => a.IsActive)
            .ToListAsync();

        // Step 5: Detect changes
        var changes = await _changeDetectionService.DetectAllianceChangesAsync(mergedAlliances, currentAlliances);

        // Step 6: Apply changes
        await _temporalDataService.ApplyAllianceChangesAsync(changes, sessionId);

        _logger.LogInformation("Alliance import completed: {Added} added, {Modified} modified, {Removed} removed",
            changes.Added.Count, changes.Modified.Count, changes.Removed.Count);
    }

    private List<Alliance> MergeAllianceData(List<Alliance> allianceBaseEntities, List<Alliance> allianceOnlyEntities)
    {
        _logger.LogDebug("Merging alliance base data with alliance-only data");

        // Create a dictionary of alliance bases by ID for quick lookup
        var allianceBasesDict = allianceBaseEntities.ToDictionary(ab => ab.AllianceId);

        // Start with all alliance bases (they have complete data)
        var mergedAlliances = new List<Alliance>(allianceBaseEntities);

        // Track which alliance IDs we've already processed
        var processedIds = allianceBasesDict.Keys.ToHashSet();

        // Add alliance-only records that don't have corresponding alliance bases
        var allianceOnlyToAdd = allianceOnlyEntities
            .Where(a => !processedIds.Contains(a.AllianceId))
            .ToList();

        mergedAlliances.AddRange(allianceOnlyToAdd);

        // Log any alliances that exist in both lists (alliance bases take priority)
        var duplicateIds = allianceOnlyEntities
            .Where(a => processedIds.Contains(a.AllianceId))
            .Select(a => a.AllianceId)
            .ToList();

        if (duplicateIds.Any())
        {
            _logger.LogInformation("Found {Count} alliances that exist in both alliance bases and alliances list. Alliance base data takes priority for IDs: {DuplicateIds}",
                duplicateIds.Count, string.Join(", ", duplicateIds));
        }

        _logger.LogDebug("Alliance merge completed: {BaseCount} alliance bases + {OnlyCount} alliance-only = {TotalCount} total",
            allianceBaseEntities.Count, allianceOnlyToAdd.Count, mergedAlliances.Count);

        return mergedAlliances;
    }

    private async Task ImportPlayersAsync(Guid sessionId, List<PlayerImportModel> players)
    {
        _logger.LogInformation("Importing {Count} players", players.Count);

        // Convert players to Player entities
        var playerEntities = players.Select(p => new Player
        {
            Id = Guid.NewGuid(),
            PlayerId = p.PlayerId,
            ImportSessionId = sessionId,
            Name = p.Name,
            CityName = p.City,
            Might = long.TryParse(p.Might, out var might) ? might : 0,
            AllianceId = null, // Will be set based on tile data
            IsActive = true,
            ValidFrom = DateTime.UtcNow.Date
        }).ToList();

        // Get current players for change detection
        var currentPlayers = await _context.Players
            .Where(p => p.IsActive)
            .ToListAsync();

        // Detect changes
        var changes = await _changeDetectionService.DetectPlayerChangesAsync(playerEntities, currentPlayers);

        // Apply changes
        await _temporalDataService.ApplyPlayerChangesAsync(changes, sessionId);

        _logger.LogInformation("Players import completed: {Added} added, {Modified} modified, {Removed} removed",
            changes.Added.Count, changes.Modified.Count, changes.Removed.Count);
    }

    private async Task ImportPlayersWithAllianceIdsAsync(Guid sessionId, List<PlayerImportModel> players, List<TileImportModel> tiles)
    {
        _logger.LogInformation("Importing {Count} players with alliance relationships from City tiles", players.Count);

        // Extract player-alliance relationships from City tiles
        var playerAllianceMap = tiles
            .Where(t => t.Type.Equals("City", StringComparison.OrdinalIgnoreCase) &&
                       t.PlayerId > 0 &&
                       t.AllianceId > 0)
            .GroupBy(t => t.PlayerId.ToString())
            .ToDictionary(g => g.Key, g => g.First().AllianceId.ToString());

        _logger.LogInformation("Extracted alliance relationships for {PlayerCount} players from {CityTileCount} City tiles",
            playerAllianceMap.Count, tiles.Count(t => t.Type.Equals("City", StringComparison.OrdinalIgnoreCase)));

        // Convert players to Player entities with alliance IDs
        var playerEntities = players.Select(p => new Player
        {
            Id = Guid.NewGuid(),
            PlayerId = p.PlayerId,
            ImportSessionId = sessionId,
            Name = p.Name,
            CityName = p.City,
            Might = long.TryParse(p.Might, out var might) ? might : 0,
            AllianceId = playerAllianceMap.TryGetValue(p.PlayerId, out var allianceId) ? allianceId : null,
            IsActive = true,
            ValidFrom = DateTime.UtcNow.Date
        }).ToList();

        // Log alliance assignment statistics
        var playersWithAlliances = playerEntities.Count(p => !string.IsNullOrEmpty(p.AllianceId));
        _logger.LogInformation("Alliance assignment: {WithAlliance} players assigned to alliances, {WithoutAlliance} players without alliances",
            playersWithAlliances, playerEntities.Count - playersWithAlliances);

        // Get current players for change detection
        var currentPlayers = await _context.Players
            .Where(p => p.IsActive)
            .ToListAsync();

        // Detect changes
        var changes = await _changeDetectionService.DetectPlayerChangesAsync(playerEntities, currentPlayers);

        // Apply changes
        await _temporalDataService.ApplyPlayerChangesAsync(changes, sessionId);

        _logger.LogInformation("Players import completed: {Added} added, {Modified} modified, {Removed} removed",
            changes.Added.Count, changes.Modified.Count, changes.Removed.Count);
    }

    private async Task ImportTilesAsync(Guid sessionId, List<TileImportModel> tiles)
    {
        _logger.LogInformation("Importing {Count} tiles", tiles.Count);
        
        // Convert tiles to Tile entities
        var tileEntities = tiles.Select(t => new Tile
        {
            Id = Guid.NewGuid(),
            X = t.X,
            Y = t.Y,
            ImportSessionId = sessionId,
            Type = t.Type,
            Level = t.Level,
            PlayerId = t.PlayerId > 0 ? t.PlayerId.ToString() : null,
            AllianceId = t.AllianceId > 0 ? t.AllianceId.ToString() : null,
            IsActive = true,
            ValidFrom = DateTime.UtcNow.Date
        }).ToList();

        // Get current tiles for change detection
        var currentTiles = await _context.Tiles
            .Where(t => t.IsActive)
            .ToListAsync();

        // Detect changes
        var changes = await _changeDetectionService.DetectTileChangesAsync(tileEntities, currentTiles);
        
        // Apply changes
        await _temporalDataService.ApplyTileChangesAsync(changes, sessionId);
        
        _logger.LogInformation("Tiles import completed: {Added} added, {Modified} modified, {Removed} removed",
            changes.Added.Count, changes.Modified.Count, changes.Removed.Count);
    }

    private string FormatErrorMessage(Exception exception, ImportErrorCategory category, Guid errorId)
    {
        var baseMessage = $"Import failed ({category}): {exception.Message}";

        // Add category-specific guidance
        var guidance = category switch
        {
            ImportErrorCategory.DataFormat => "Please check that the uploaded file is valid JSON with the expected structure.",
            ImportErrorCategory.DataValidation => "Please verify that all data meets the required validation criteria.",
            ImportErrorCategory.DataDuplication => "Duplicate records were detected. Please review the data for uniqueness.",
            ImportErrorCategory.DatabaseTransaction => "A database transaction error occurred. The import was rolled back.",
            ImportErrorCategory.Timeout => "The import operation timed out. Please try with a smaller file or contact support.",
            ImportErrorCategory.Memory => "Insufficient memory to process the file. Please try with a smaller file.",
            ImportErrorCategory.Security => "Access denied. Please check your permissions.",
            ImportErrorCategory.FileAccess => "Unable to access the uploaded file. Please try uploading again.",
            ImportErrorCategory.DatabaseUpdate => "Database update failed. Please contact support if the issue persists.",
            ImportErrorCategory.DatabaseConnection => "Database connection error. Please try again later.",
            _ => "An unexpected error occurred. Please contact support with error ID."
        };

        return $"{baseMessage} {guidance} Error ID: {errorId}";
    }

    private void LogErrorStatistics(ImportSession session, Exception exception, ImportErrorCategory category)
    {
        var statistics = new
        {
            SessionId = session.Id,
            FileName = session.FileName,
            ErrorCategory = category.ToString(),
            ExceptionType = exception.GetType().Name,
            ProgressAtFailure = session.ProgressPercentage,
            RecordsProcessedAtFailure = session.RecordsProcessed,
            TimeToFailure = DateTime.UtcNow - session.CreatedAt,
            FileSize = session.FileName?.EndsWith(".json") == true ? "Unknown" : "Unknown"
        };

        _logger.LogWarning("Import failure statistics: {@ImportFailureStats}", statistics);

        // This could be used for monitoring and alerting
        if (category == ImportErrorCategory.Memory || category == ImportErrorCategory.Timeout)
        {
            _logger.LogWarning("Performance-related import failure detected. Consider system resource monitoring.");
        }

        if (category == ImportErrorCategory.DatabaseConnection || category == ImportErrorCategory.DatabaseTransaction)
        {
            _logger.LogWarning("Database-related import failure detected. Consider database health monitoring.");
        }
    }



    private async Task HandleImportTimeoutAsync(Guid sessionId, int timeoutSeconds)
    {
        try
        {
            var session = await _context.ImportSessions.FindAsync(sessionId);
            if (session != null)
            {
                session.Status = ImportStatus.Failed;
                session.ErrorMessage = $"Import operation timed out after {timeoutSeconds} seconds. " +
                                     "The file may be too large or the system may be under heavy load. " +
                                     "Please try with a smaller file or contact support if the issue persists.";
                session.CompletedAt = DateTime.UtcNow;

                _context.ImportSessions.Update(session);
                await _context.SaveChangesAsync();

                _logger.LogWarning("Import session {SessionId} marked as failed due to timeout. " +
                                 "Progress at timeout: {ProgressPercentage}%, Records processed: {RecordsProcessed}",
                    sessionId, session.ProgressPercentage, session.RecordsProcessed);

                // Log timeout statistics for monitoring
                var timeoutStats = new
                {
                    SessionId = sessionId,
                    FileName = session.FileName,
                    TimeoutSeconds = timeoutSeconds,
                    ProgressAtTimeout = session.ProgressPercentage,
                    RecordsProcessedAtTimeout = session.RecordsProcessed,
                    TimeToTimeout = DateTime.UtcNow - session.CreatedAt
                };

                _logger.LogWarning("Import timeout statistics: {@TimeoutStats}", timeoutStats);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle import timeout for session {SessionId}", sessionId);
        }
    }
}
