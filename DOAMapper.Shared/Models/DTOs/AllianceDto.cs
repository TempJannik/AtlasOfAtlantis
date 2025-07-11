namespace DOAMapper.Shared.Models.DTOs;

public class AllianceDto
{
    public string AllianceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string OverlordName { get; set; } = string.Empty;
    public long Power { get; set; }
    public int FortressLevel { get; set; }
    public int FortressX { get; set; }
    public int FortressY { get; set; }
    public int MemberCount { get; set; }
    public DateTime DataDate { get; set; }
}
