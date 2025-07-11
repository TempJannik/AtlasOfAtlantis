using DOAMapper.Shared.Models.Enums;

namespace DOAMapper.Shared.Models.DTOs;

public class ImportSessionDto
{
    public Guid Id { get; set; }
    public DateTime ImportDate { get; set; }
    public string FileName { get; set; } = string.Empty;
    public ImportStatus Status { get; set; }
    public int RecordsProcessed { get; set; }
    public int RecordsChanged { get; set; }
    public int ProgressPercentage { get; set; }
    public string? ErrorMessage { get; set; }

    // Enhanced status tracking fields
    public string CurrentPhase { get; set; } = string.Empty;
    public string StatusMessage { get; set; } = string.Empty;
    public int TotalPhases { get; set; } = 5;
    public int CurrentPhaseNumber { get; set; } = 0;
    public int CurrentPhaseProgressPercentage { get; set; } = 0;
    public DateTime LastProgressUpdate { get; set; }

    // Phase details for detailed progress display
    public Dictionary<string, PhaseProgressDto>? PhaseDetails { get; set; }
}

public class PhaseProgressDto
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
