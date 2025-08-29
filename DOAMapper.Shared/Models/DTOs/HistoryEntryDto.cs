using DOAMapper.Shared.Models.Enums;

namespace DOAMapper.Shared.Models.DTOs;

public class HistoryEntryDto<T>
{
    // Base snapshot data for this entry
    public T Data { get; set; } = default!;
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }

    // New structured change info
    public HistoryState State { get; set; } // Added, Changed, Removed
    public List<string> Changes { get; set; } = new(); // Individual change descriptions
}
