using DOAMapper.Models.Interfaces;

namespace DOAMapper.Models.Entities;

public class Player : ITemporalEntity
{
    public Guid Id { get; set; }
    public string PlayerId { get; set; } = string.Empty;
    public Guid ImportSessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public long Might { get; set; }
    public string? AllianceId { get; set; }
    public bool IsActive { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    
    // Navigation properties
    public ImportSession ImportSession { get; set; } = null!;
    public Alliance? Alliance { get; set; }
    public ICollection<Tile> Tiles { get; set; } = new List<Tile>();
}
