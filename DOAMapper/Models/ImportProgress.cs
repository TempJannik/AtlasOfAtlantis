using DOAMapper.Data;
using DOAMapper.Models.Entities;
using DOAMapper.Shared.Models.Enums;
using DOAMapper.Services;

namespace DOAMapper.Models;

/// <summary>
/// Represents the progress of an import operation with detailed phase tracking
/// </summary>
public class ImportProgress
{
    public Guid SessionId { get; set; }
    public string CurrentPhase { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public int TotalPhases { get; set; }
    public int CurrentPhaseNumber { get; set; }
    public int OverallProgressPercentage { get; set; }
    public int CurrentPhaseProgressPercentage { get; set; }
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int ChangedRecords { get; set; }
    public DateTime LastUpdated { get; set; }
    
    /// <summary>
    /// Phase-specific record counts for detailed progress tracking
    /// </summary>
    public Dictionary<string, PhaseProgress> PhaseDetails { get; set; } = new();
}

/// <summary>
/// Progress details for a specific import phase
/// </summary>
public class PhaseProgress
{
    public string PhaseName { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int ChangedRecords { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Callback interface for progress updates during import
/// </summary>
public interface IImportProgressCallback
{
    Task UpdateProgressAsync(ImportProgress progress);
    Task UpdatePhaseProgressAsync(string phaseName, int processed, int total, string? statusMessage = null);
    Task CompletePhaseAsync(string phaseName, int totalProcessed, int totalChanged);
    Task ReportErrorAsync(string phaseName, Exception exception);

    /// <summary>
    /// Gets the final accumulated counts for processed and changed records
    /// </summary>
    (int ProcessedRecords, int ChangedRecords) GetFinalCounts();
}

/// <summary>
/// Implementation of progress callback that updates the database
/// </summary>
public class DatabaseImportProgressCallback : IImportProgressCallback
{
    private readonly IServiceScope _scope;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DatabaseImportProgressCallback> _logger;
    private readonly ImportProgress _progress;

    public DatabaseImportProgressCallback(IServiceScope scope, Guid sessionId, ILogger<DatabaseImportProgressCallback> logger)
    {
        _scope = scope;
        _context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _logger = logger;
        _progress = new ImportProgress
        {
            SessionId = sessionId,
            TotalPhases = 5, // Alliance Bases, Alliances, Players, Tiles, Player Alliance Updates
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task UpdateProgressAsync(ImportProgress progress)
    {
        try
        {
            // Use the ImportStatusService for consistent updates
            var statusService = _scope.ServiceProvider.GetRequiredService<ImportStatusService>();
            await statusService.UpdateImportProgressAsync(progress.SessionId, progress);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update import progress for session {SessionId}", progress.SessionId);
        }
    }

    public async Task UpdatePhaseProgressAsync(string phaseName, int processed, int total, string? statusMessage = null)
    {
        _progress.CurrentPhase = phaseName;
        _progress.StatusMessage = statusMessage ?? $"Processing {phaseName}...";
        _progress.LastUpdated = DateTime.UtcNow;

        // Update phase details
        if (!_progress.PhaseDetails.ContainsKey(phaseName))
        {
            _progress.PhaseDetails[phaseName] = new PhaseProgress
            {
                PhaseName = phaseName,
                StartTime = DateTime.UtcNow
            };
            _progress.CurrentPhaseNumber = _progress.PhaseDetails.Count;
        }

        var phaseProgress = _progress.PhaseDetails[phaseName];
        phaseProgress.TotalRecords = total;
        phaseProgress.ProcessedRecords = processed;

        // Calculate current phase progress
        _progress.CurrentPhaseProgressPercentage = total > 0 ? (processed * 100) / total : 0;

        // Calculate overall progress based on completed phases and current phase progress
        var completedPhases = _progress.PhaseDetails.Values.Count(p => p.IsCompleted);
        var currentPhaseContribution = _progress.CurrentPhaseProgressPercentage / (double)_progress.TotalPhases;
        var completedPhasesContribution = (completedPhases * 100.0) / _progress.TotalPhases;
        _progress.OverallProgressPercentage = Math.Min(100, (int)(completedPhasesContribution + currentPhaseContribution));

        // Update total processed records
        _progress.ProcessedRecords = _progress.PhaseDetails.Values.Sum(p => p.ProcessedRecords);
        _progress.ChangedRecords = _progress.PhaseDetails.Values.Sum(p => p.ChangedRecords);

        await UpdateProgressAsync(_progress);
    }

    public async Task CompletePhaseAsync(string phaseName, int totalProcessed, int totalChanged)
    {
        if (_progress.PhaseDetails.ContainsKey(phaseName))
        {
            var phaseProgress = _progress.PhaseDetails[phaseName];
            phaseProgress.ProcessedRecords = totalProcessed;
            phaseProgress.ChangedRecords = totalChanged;
            phaseProgress.IsCompleted = true;
            phaseProgress.EndTime = DateTime.UtcNow;

            _logger.LogInformation("Phase {PhaseName} completed: {Processed} processed, {Changed} changed",
                phaseName, totalProcessed, totalChanged);
        }

        // Update overall progress using the same calculation as UpdatePhaseProgressAsync
        var completedPhases = _progress.PhaseDetails.Values.Count(p => p.IsCompleted);
        var completedPhasesContribution = (completedPhases * 100.0) / _progress.TotalPhases;
        _progress.OverallProgressPercentage = Math.Min(100, (int)completedPhasesContribution);
        _progress.ProcessedRecords = _progress.PhaseDetails.Values.Sum(p => p.ProcessedRecords);
        _progress.ChangedRecords = _progress.PhaseDetails.Values.Sum(p => p.ChangedRecords);

        await UpdateProgressAsync(_progress);

        // Log when all phases are completed, but don't mark as completed here
        // The completion will be handled by the transaction completion in ImportService
        if (completedPhases == _progress.TotalPhases)
        {
            _logger.LogInformation("All {TotalPhases} phases completed for session {SessionId} - {Processed} processed, {Changed} changed",
                _progress.TotalPhases, _progress.SessionId, _progress.ProcessedRecords, _progress.ChangedRecords);
        }
    }

    public async Task ReportErrorAsync(string phaseName, Exception exception)
    {
        if (_progress.PhaseDetails.ContainsKey(phaseName))
        {
            _progress.PhaseDetails[phaseName].ErrorMessage = exception.Message;
        }

        _logger.LogError(exception, "Error in phase {PhaseName}: {ErrorMessage}", phaseName, exception.Message);

        // Update session with error status using ImportStatusService
        try
        {
            var statusService = _scope.ServiceProvider.GetRequiredService<ImportStatusService>();
            await statusService.FailImportAsync(_progress.SessionId, $"Failed in {phaseName}: {exception.Message}", phaseName);
        }
        catch (Exception updateEx)
        {
            _logger.LogError(updateEx, "Failed to update session error status for session {SessionId}", _progress.SessionId);
        }
    }

    public (int ProcessedRecords, int ChangedRecords) GetFinalCounts()
    {
        return (_progress.ProcessedRecords, _progress.ChangedRecords);
    }

    public void Dispose()
    {
        _scope?.Dispose();
    }
}
