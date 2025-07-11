using DOAMapper.Data;
using DOAMapper.Models;
using DOAMapper.Models.Entities;
using DOAMapper.Shared.Models.Enums;
using DOAMapper.Services.Interfaces;
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
    public async Task<ImportSession> StartBackgroundImportAsync(Stream jsonStream, string fileName)
    {
        // Check for active imports (business rule: only one import at a time)
        lock (_lockObject)
        {
            if (_activeImports.Any())
            {
                throw new InvalidOperationException("Another import is already in progress. Please wait for it to complete.");
            }
        }

        // Create import session
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
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

        context.ImportSessions.Add(importSession);
        await context.SaveChangesAsync();

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
                await ProcessImportInBackgroundAsync(importSession.Id, memoryStream, cancellationTokenSource.Token);
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

        _logger.LogInformation("Background import started for session {SessionId} with file {FileName}", 
            importSession.Id, fileName);

        return importSession;
    }

    /// <summary>
    /// Processes the import in a background task with proper scoping
    /// </summary>
    private async Task ProcessImportInBackgroundAsync(Guid sessionId, Stream jsonStream, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<BackgroundImportService>>();
        var progressCallback = new DatabaseImportProgressCallback(scope, sessionId, 
            scope.ServiceProvider.GetRequiredService<ILogger<DatabaseImportProgressCallback>>());

        try
        {
            logger.LogInformation("Starting background import processing for session {SessionId}", sessionId);

            // Get required services from scope
            var importService = scope.ServiceProvider.GetRequiredService<IImportService>();
            
            // Cast to concrete type to access internal methods
            if (importService is ImportService concreteImportService)
            {
                await concreteImportService.ProcessImportWithProgressAsync(sessionId, jsonStream, progressCallback, cancellationToken);
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
