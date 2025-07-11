namespace DOAMapper.Shared.Models.DTOs;

public class HistoryEntryDto<T>
{
    public T Data { get; set; } = default!;
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string ChangeType { get; set; } = string.Empty; // Added, Modified, Removed
}
