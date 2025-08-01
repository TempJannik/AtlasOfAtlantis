using DOAMapper.Data;
using DOAMapper.Models;
using DOAMapper.Models.Entities;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Shared.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace DOAMapper.Services;

/// <summary>
/// Service for managing import status tracking and reporting
/// </summary>
public class ImportStatusService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImportStatusService> _logger;

    public ImportStatusService(ApplicationDbContext context, ILogger<ImportStatusService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current status of an import session with detailed progress information
    /// </summary>
    public async Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId)
    {
        var session = await _context.ImportSessions.FindAsync(sessionId);
        if (session == null)
        {
            throw new ArgumentException($"Import session {sessionId} not found", nameof(sessionId));
        }

        return MapToDto(session);
    }

    /// <summary>
    /// Updates the import session with enhanced progress information
    /// </summary>
    public async Task UpdateImportProgressAsync(Guid sessionId, ImportProgress progress)
    {
        try
        {
            var session = await _context.ImportSessions.FindAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("Attempted to update progress for non-existent session {SessionId}", sessionId);
                return;
            }

            // Update basic progress fields
            session.ProgressPercentage = progress.OverallProgressPercentage;
            session.RecordsProcessed = progress.ProcessedRecords;
            session.RecordsChanged = progress.ChangedRecords;
            session.LastProgressUpdate = DateTime.UtcNow;

            // Update enhanced tracking fields
            session.CurrentPhase = progress.CurrentPhase;
            session.StatusMessage = TruncateStatusMessage(progress.StatusMessage);
            session.CurrentPhaseNumber = progress.CurrentPhaseNumber;
            session.CurrentPhaseProgressPercentage = progress.CurrentPhaseProgressPercentage;
            session.TotalPhases = progress.TotalPhases;

            // Serialize phase details to JSON
            if (progress.PhaseDetails.Any())
            {
                session.PhaseDetailsJson = JsonSerializer.Serialize(progress.PhaseDetails, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            _context.ImportSessions.Update(session);
            await _context.SaveChangesAsync();

            _logger.LogDebug("Updated import progress for session {SessionId}: {Progress}% - {Phase} - {Status}",
                sessionId, progress.OverallProgressPercentage, progress.CurrentPhase, progress.StatusMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update import progress for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Marks an import session as completed with final statistics
    /// </summary>
    public async Task CompleteImportAsync(Guid sessionId, int totalProcessed, int totalChanged)
    {
        try
        {
            var session = await _context.ImportSessions.FindAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("Attempted to complete non-existent session {SessionId}", sessionId);
                return;
            }

            session.Status = ImportStatus.Completed;
            session.ProgressPercentage = 100;
            session.RecordsProcessed = totalProcessed;
            session.RecordsChanged = totalChanged;
            session.CompletedAt = DateTime.UtcNow;
            session.LastProgressUpdate = DateTime.UtcNow;
            session.CurrentPhase = "Completed";
            session.StatusMessage = TruncateStatusMessage($"Import completed successfully. {totalProcessed} records processed, {totalChanged} changed.");
            session.CurrentPhaseNumber = session.TotalPhases;
            session.CurrentPhaseProgressPercentage = 100;

            _context.ImportSessions.Update(session);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Import session {SessionId} completed: {Processed} processed, {Changed} changed",
                sessionId, totalProcessed, totalChanged);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete import session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Marks an import session as failed with error details
    /// </summary>
    public async Task FailImportAsync(Guid sessionId, string errorMessage, string? currentPhase = null)
    {
        try
        {
            var session = await _context.ImportSessions.FindAsync(sessionId);
            if (session == null)
            {
                _logger.LogWarning("Attempted to fail non-existent session {SessionId}", sessionId);
                return;
            }

            session.Status = ImportStatus.Failed;
            session.ErrorMessage = errorMessage;
            session.CompletedAt = DateTime.UtcNow;
            session.LastProgressUpdate = DateTime.UtcNow;
            session.StatusMessage = TruncateStatusMessage($"Import failed: {errorMessage}");
            
            if (!string.IsNullOrEmpty(currentPhase))
            {
                session.CurrentPhase = currentPhase;
            }

            _context.ImportSessions.Update(session);
            await _context.SaveChangesAsync();

            _logger.LogError("Import session {SessionId} failed in phase {Phase}: {ErrorMessage}",
                sessionId, currentPhase ?? "Unknown", errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update failed import session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Gets import history with enhanced status information
    /// </summary>
    public async Task<List<ImportSessionDto>> GetImportHistoryAsync(int limit = 50)
    {
        var sessions = await _context.ImportSessions
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return sessions.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Truncates a status message to ensure it fits within the database field limit
    /// </summary>
    /// <param name="message">The original status message</param>
    /// <returns>A truncated message that fits within 500 characters</returns>
    private static string TruncateStatusMessage(string message)
    {
        const int maxLength = 500;
        const string truncationSuffix = "...";

        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
        {
            return message ?? string.Empty;
        }

        // Reserve space for the truncation suffix
        var maxContentLength = maxLength - truncationSuffix.Length;

        // Truncate and add suffix
        return message.Substring(0, maxContentLength) + truncationSuffix;
    }

    /// <summary>
    /// Maps ImportSession entity to DTO with enhanced progress information
    /// </summary>
    private ImportSessionDto MapToDto(ImportSession session)
    {
        var dto = new ImportSessionDto
        {
            Id = session.Id,
            ImportDate = session.ImportDate,
            FileName = session.FileName,
            Status = session.Status,
            RecordsProcessed = session.RecordsProcessed,
            RecordsChanged = session.RecordsChanged,
            ProgressPercentage = session.ProgressPercentage,
            ErrorMessage = session.ErrorMessage,
            CurrentPhase = session.CurrentPhase,
            StatusMessage = session.StatusMessage,
            TotalPhases = session.TotalPhases,
            CurrentPhaseNumber = session.CurrentPhaseNumber,
            CurrentPhaseProgressPercentage = session.CurrentPhaseProgressPercentage,
            LastProgressUpdate = session.LastProgressUpdate
        };

        // Deserialize phase details if available
        if (!string.IsNullOrEmpty(session.PhaseDetailsJson))
        {
            try
            {
                var phaseDetails = JsonSerializer.Deserialize<Dictionary<string, PhaseProgress>>(
                    session.PhaseDetailsJson, 
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                if (phaseDetails != null)
                {
                    dto.PhaseDetails = phaseDetails.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new PhaseProgressDto
                        {
                            PhaseName = kvp.Value.PhaseName,
                            TotalRecords = kvp.Value.TotalRecords,
                            ProcessedRecords = kvp.Value.ProcessedRecords,
                            ChangedRecords = kvp.Value.ChangedRecords,
                            IsCompleted = kvp.Value.IsCompleted,
                            StartTime = kvp.Value.StartTime,
                            EndTime = kvp.Value.EndTime,
                            ErrorMessage = kvp.Value.ErrorMessage
                        });
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize phase details for session {SessionId}", session.Id);
            }
        }

        return dto;
    }
}
