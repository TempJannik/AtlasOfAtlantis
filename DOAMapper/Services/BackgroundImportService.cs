using DOAMapper.Data;
using DOAMapper.Models;
using DOAMapper.Models.Entities;
using DOAMapper.Shared.Models.Enums;
using DOAMapper.Services.Interfaces;
using DOAMapper.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace DOAMapper.Services;

/// <summary>
/// Service for managing background import operations with proper progress tracking
/// </summary>
public class BackgroundImportService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundImportService> _logger;
    private static readonly Dictionary<Guid, CancellationTokenSource> _activeImports = new();
    private static readonly object _lockObject = new();

    public BackgroundImportService(IServiceScopeFactory scopeFactory, ILogger<BackgroundImportService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Starts a background import operation
    /// </summary>
    public async Task<ImportSession> StartBackgroundImportAsync(Stream jsonStream, string fileName, string realmId, DateTime? importDate = null)
    {
        // Check for active imports (business rule: only one import at a time)
        lock (_lockObject)
        {
            if (_activeImports.Any())
            {
                throw new InvalidOperationException("Another import is already in progress. Please wait for it to complete.");
            }
        }

        // Use provided date or default to current date, normalized to midnight UTC
        var effectiveImportDate = importDate?.Date ?? DateTime.UtcNow.Date;
        var utcImportDate = DateTime.SpecifyKind(effectiveImportDate, DateTimeKind.Utc);

        // Create import session
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var realmService = scope.ServiceProvider.GetRequiredService<IRealmService>();

        // Get the realm to ensure it exists and get the internal ID
        _logger.LogInformation("Looking up realm with RealmId: {RealmId}", realmId);
        var realm = await realmService.GetRealmAsync(realmId);
        if (realm == null)
        {
            _logger.LogError("Realm '{RealmId}' not found", realmId);
            throw new InvalidOperationException($"Realm '{realmId}' not found");
        }
        _logger.LogInformation("Found realm: Id={RealmInternalId}, RealmId={RealmId}, Name={RealmName}", realm.Id, realm.RealmId, realm.Name);

        var importSession = new ImportSession
        {
            Id = Guid.NewGuid(),
            ImportDate = utcImportDate,
            FileName = fileName,
            Status = ImportStatus.Processing,
            RecordsProcessed = 0,
            RecordsChanged = 0,
            ProgressPercentage = 0,
            CreatedAt = DateTime.UtcNow,
            RealmId = realm.Id
        };

        _logger.LogInformation("Creating ImportSession with RealmId: {RealmInternalId} for realm '{RealmId}'", realm.Id, realmId);
        context.ImportSessions.Add(importSession);

        try
        {
            // Check if the realm actually exists in the current context
            var realmExistsInContext = await context.Realms.AnyAsync(r => r.Id == realm.Id);
            _logger.LogInformation("Realm exists in current context: {RealmExists}", realmExistsInContext);

            // Check for any existing ImportSessions with invalid RealmIds
            var invalidImportSessions = await context.ImportSessions
                .Where(i => !context.Realms.Any(r => r.Id == i.RealmId))
                .CountAsync();
            _logger.LogInformation("Found {InvalidCount} ImportSessions with invalid RealmIds", invalidImportSessions);

            await context.SaveChangesAsync();
            _logger.LogInformation("ImportSession created successfully with Id: {ImportSessionId}", importSession.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save ImportSession. RealmId: {RealmInternalId}, RealmExists: {RealmExists}",
                realm.Id, await context.Realms.AnyAsync(r => r.Id == realm.Id));
            throw;
        }

        // Copy stream to memory to avoid disposal issues
        var memoryStream = new MemoryStream();
        await jsonStream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        // Start background processing
        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(30)); // 30 minute timeout

        lock (_lockObject)
        {
            _activeImports[importSession.Id] = cancellationTokenSource;
        }

        // Start background task
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessImportInBackgroundAsync(importSession.Id, memoryStream, utcImportDate, cancellationTokenSource.Token);
            }
            finally
            {
                memoryStream.Dispose();
                lock (_lockObject)
                {
                    _activeImports.Remove(importSession.Id);
                }
                cancellationTokenSource.Dispose();
            }
        }, cancellationTokenSource.Token);

        _logger.LogInformation("Background import started for session {SessionId} with file {FileName} for date {ImportDate}",
            importSession.Id, fileName, utcImportDate.ToString("yyyy-MM-dd"));

        return importSession;
    }

    /// <summary>
    /// Processes the import in a background task with proper scoping
    /// </summary>
    private async Task ProcessImportInBackgroundAsync(Guid sessionId, Stream jsonStream, DateTime importDate, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<BackgroundImportService>>();
        var progressCallback = new DatabaseImportProgressCallback(scope, sessionId,
            scope.ServiceProvider.GetRequiredService<ILogger<DatabaseImportProgressCallback>>());

        try
        {
            logger.LogInformation("Starting background import processing for session {SessionId} with import date {ImportDate}",
                sessionId, importDate.ToString("yyyy-MM-dd"));

            // Get required services from scope
            var importService = scope.ServiceProvider.GetRequiredService<IImportService>();

            // Cast to concrete type to access internal methods
            if (importService is ImportService concreteImportService)
            {
                await concreteImportService.ProcessImportWithProgressAsync(sessionId, jsonStream, importDate, progressCallback, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException("Import service is not of expected type");
            }

            logger.LogInformation("Background import completed successfully for session {SessionId}", sessionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Background import was cancelled for session {SessionId}", sessionId);
            await HandleImportCancellationAsync(scope, sessionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background import failed for session {SessionId}: {ErrorMessage}", sessionId, ex.Message);
            await progressCallback.ReportErrorAsync("Background Processing", ex);
        }
        finally
        {
            progressCallback.Dispose();
        }
    }

    /// <summary>
    /// Handles import cancellation (timeout or manual cancellation)
    /// </summary>
    private async Task HandleImportCancellationAsync(IServiceScope scope, Guid sessionId)
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var session = await context.ImportSessions.FindAsync(sessionId);
            
            if (session != null)
            {
                session.Status = ImportStatus.Cancelled;
                session.ErrorMessage = "Import operation was cancelled due to timeout or user request";
                session.CompletedAt = DateTime.UtcNow;
                
                context.ImportSessions.Update(session);
                await context.SaveChangesAsync();
                
                _logger.LogInformation("Import session {SessionId} marked as cancelled", sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update cancelled import session {SessionId}", sessionId);
        }
    }

    /// <summary>
    /// Cancels an active import operation
    /// </summary>
    public bool CancelImport(Guid sessionId)
    {
        lock (_lockObject)
        {
            if (_activeImports.TryGetValue(sessionId, out var cancellationTokenSource))
            {
                cancellationTokenSource.Cancel();
                _logger.LogInformation("Import cancellation requested for session {SessionId}", sessionId);
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// Gets the list of currently active import session IDs
    /// </summary>
    public List<Guid> GetActiveImportSessions()
    {
        lock (_lockObject)
        {
            return _activeImports.Keys.ToList();
        }
    }

    /// <summary>
    /// Checks if an import is currently active
    /// </summary>
    public bool IsImportActive(Guid sessionId)
    {
        lock (_lockObject)
        {
            return _activeImports.ContainsKey(sessionId);
        }
    }
}
