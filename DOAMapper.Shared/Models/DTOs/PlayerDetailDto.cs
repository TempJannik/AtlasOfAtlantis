namespace DOAMapper.Shared.Models.DTOs;

public class PlayerDetailDto : PlayerDto
{
    public AllianceDto? Alliance { get; set; }
    public int TileCount { get; set; }
    public Dictionary<string, int> TilesByType { get; set; } = new();
}
