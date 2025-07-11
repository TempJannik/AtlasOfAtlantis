using DOAMapper.Models.Interfaces;

namespace DOAMapper.Models.Entities;

public class Tile : ITemporalEntity
{
    public Guid Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public Guid ImportSessionId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? PlayerId { get; set; }
    public string? AllianceId { get; set; }
    public bool IsActive { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    
    // Navigation properties
    public ImportSession ImportSession { get; set; } = null!;
    public Player? Player { get; set; }
    public Alliance? Alliance { get; set; }
}
