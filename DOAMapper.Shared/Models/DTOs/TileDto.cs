namespace DOAMapper.Shared.Models.DTOs;

public class TileDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? PlayerId { get; set; }
    public string? PlayerName { get; set; }
    public string? AllianceId { get; set; }
    public string? AllianceName { get; set; }
    public DateTime DataDate { get; set; }
}
