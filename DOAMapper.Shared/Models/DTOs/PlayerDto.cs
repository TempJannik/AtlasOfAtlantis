namespace DOAMapper.Shared.Models.DTOs;

public class PlayerDto
{
    public string PlayerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    // City tile coordinates (if available) for the player's city at the queried date/realm
    public int? CityX { get; set; }
    public int? CityY { get; set; }
    public long Might { get; set; }
    public int Rank { get; set; }
    public string? AllianceId { get; set; }
    public string? AllianceName { get; set; }
    public DateTime DataDate { get; set; }
}
