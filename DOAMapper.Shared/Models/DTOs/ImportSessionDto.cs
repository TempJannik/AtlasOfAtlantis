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
}
