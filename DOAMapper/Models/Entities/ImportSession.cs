using DOAMapper.Shared.Models.Enums;

namespace DOAMapper.Models.Entities;

public class ImportSession
{
    public Guid Id { get; set; }
    public DateTime ImportDate { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ImportStatus Status { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsChanged { get; set; }
    public int ProgressPercentage { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Enhanced status tracking fields
    public string CurrentPhase { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public int TotalPhases { get; set; } = 5;
    public int CurrentPhaseNumber { get; set; } = 0;
    public int CurrentPhaseProgressPercentage { get; set; } = 0;
    public DateTime LastProgressUpdate { get; set; }

    // Detailed phase tracking (JSON serialized)
    public string? PhaseDetailsJson { get; set; }
}
