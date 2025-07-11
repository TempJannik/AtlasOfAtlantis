using DOAMapper.Models.Interfaces;

namespace DOAMapper.Models.Entities;

public class Alliance : ITemporalEntity
{
    public Guid Id { get; set; }
    public string AllianceId { get; set; } = string.Empty;
    public Guid ImportSessionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OverlordName { get; set; } = string.Empty;
    public long Power { get; set; }
    public int FortressLevel { get; set; }
    public int FortressX { get; set; }
    public int FortressY { get; set; }
    public bool IsActive { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    
    // Navigation properties
    public ImportSession ImportSession { get; set; } = null!;
    public ICollection<Player> Members { get; set; } = new List<Player>();
    public ICollection<Tile> Tiles { get; set; } = new List<Tile>();
}
