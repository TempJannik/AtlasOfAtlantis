using DOAMapper.Models;

namespace DOAMapper.Services;

/// <summary>
/// Utility class for calculating and reporting import progress across phases
/// </summary>
public static class ImportProgressReporter
{
    /// <summary>
    /// Phase names in execution order
    /// </summary>
    public static readonly string[] PhaseNames = new[]
    {
        "Alliance Bases",
        "Alliances", 
        "Players",
        "Tiles",
        "Player Alliance Updates"
    };

    /// <summary>
    /// Calculates overall progress percentage based on completed phases and current phase progress
    /// </summary>
    public static int CalculateOverallProgress(int completedPhases, int currentPhaseProgress, int totalPhases = 5)
    {
        if (totalPhases <= 0) return 0;

        var completedPhasesContribution = (completedPhases * 100.0) / totalPhases;
        var currentPhaseContribution = (double)currentPhaseProgress / totalPhases;

        return Math.Min(100, (int)(completedPhasesContribution + currentPhaseContribution));
    }

    /// <summary>
    /// Calculates phase progress percentage based on processed vs total records
    /// </summary>
    public static int CalculatePhaseProgress(int processed, int total)
    {
        if (total <= 0) return 0;
        return Math.Min(100, (processed * 100) / total);
    }

    /// <summary>
    /// Creates a progress update for a specific phase
    /// </summary>
    public static async Task ReportPhaseProgressAsync(
        IImportProgressCallback callback,
        string phaseName,
        int processed,
        int total,
        string? customMessage = null)
    {
        var message = customMessage ?? $"Processing {phaseName}: {processed:N0} of {total:N0} records";
        await callback.UpdatePhaseProgressAsync(phaseName, processed, total, message);
    }

    /// <summary>
    /// Reports the start of a new phase
    /// </summary>
    public static async Task ReportPhaseStartAsync(
        IImportProgressCallback callback,
        string phaseName,
        int totalRecords)
    {
        await callback.UpdatePhaseProgressAsync(phaseName, 0, totalRecords, $"Starting {phaseName}...");
    }

    /// <summary>
    /// Reports the completion of a phase
    /// </summary>
    public static async Task ReportPhaseCompletionAsync(
        IImportProgressCallback callback,
        string phaseName,
        int totalProcessed,
        int totalChanged)
    {
        await callback.CompletePhaseAsync(phaseName, totalProcessed, totalChanged);
    }

    /// <summary>
    /// Reports progress during batch processing within a phase
    /// </summary>
    public static async Task ReportBatchProgressAsync(
        IImportProgressCallback callback,
        string phaseName,
        int batchNumber,
        int totalBatches,
        int recordsInBatch,
        int totalRecordsProcessed,
        int totalRecords)
    {
        var message = $"Processing {phaseName} - Batch {batchNumber} of {totalBatches} ({recordsInBatch:N0} records)";
        await callback.UpdatePhaseProgressAsync(phaseName, totalRecordsProcessed, totalRecords, message);
    }

    /// <summary>
    /// Creates a standardized status message for the current import state
    /// </summary>
    public static string CreateStatusMessage(string phaseName, int processed, int total, bool isCompleted = false)
    {
        if (isCompleted)
        {
            return $"{phaseName} completed: {processed:N0} records processed";
        }

        if (total > 0)
        {
            var percentage = CalculatePhaseProgress(processed, total);
            return $"{phaseName}: {processed:N0} of {total:N0} records ({percentage}%)";
        }

        return $"{phaseName}: {processed:N0} records processed";
    }

    /// <summary>
    /// Estimates remaining time based on current progress and elapsed time
    /// </summary>
    public static TimeSpan? EstimateRemainingTime(DateTime startTime, int completedPhases, int currentPhaseProgress, int totalPhases = 5)
    {
        var elapsed = DateTime.UtcNow - startTime;
        if (elapsed.TotalSeconds < 10) return null; // Not enough data for estimation

        var overallProgress = CalculateOverallProgress(completedPhases, currentPhaseProgress, totalPhases);
        if (overallProgress <= 0) return null;

        var estimatedTotal = elapsed.TotalSeconds * (100.0 / overallProgress);
        var remaining = estimatedTotal - elapsed.TotalSeconds;

        return remaining > 0 ? TimeSpan.FromSeconds(remaining) : TimeSpan.Zero;
    }

    /// <summary>
    /// Formats a time span for display in status messages
    /// </summary>
    public static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays >= 1)
            return $"{timeSpan.Days}d {timeSpan.Hours}h {timeSpan.Minutes}m";
        
        if (timeSpan.TotalHours >= 1)
            return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
        
        if (timeSpan.TotalMinutes >= 1)
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        
        return $"{timeSpan.Seconds}s";
    }

    /// <summary>
    /// Creates a comprehensive progress report with all available information
    /// </summary>
    public static string CreateDetailedProgressReport(
        ImportProgress progress,
        DateTime startTime,
        TimeSpan? estimatedRemaining = null)
    {
        var elapsed = DateTime.UtcNow - startTime;
        var report = $"Import Progress Report:\n";
        report += $"Overall: {progress.OverallProgressPercentage}% ({progress.CurrentPhaseNumber}/{progress.TotalPhases} phases)\n";
        report += $"Current Phase: {progress.CurrentPhase} ({progress.CurrentPhaseProgressPercentage}%)\n";
        report += $"Records: {progress.ProcessedRecords:N0} processed, {progress.ChangedRecords:N0} changed\n";
        report += $"Elapsed: {FormatTimeSpan(elapsed)}\n";
        
        if (estimatedRemaining.HasValue)
        {
            report += $"Estimated Remaining: {FormatTimeSpan(estimatedRemaining.Value)}\n";
        }

        if (progress.PhaseDetails.Any())
        {
            report += "\nPhase Details:\n";
            foreach (var phase in progress.PhaseDetails.Values.OrderBy(p => Array.IndexOf(PhaseNames, p.PhaseName)))
            {
                var status = phase.IsCompleted ? "✓" : "⏳";
                var phaseProgress = CalculatePhaseProgress(phase.ProcessedRecords, phase.TotalRecords);
                report += $"  {status} {phase.PhaseName}: {phase.ProcessedRecords:N0}/{phase.TotalRecords:N0} ({phaseProgress}%)\n";
            }
        }

        return report;
    }
}
