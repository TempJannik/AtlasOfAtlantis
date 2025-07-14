namespace DOAMapper.Shared.Models.DTOs;

public class RealmDto
{
    public Guid Id { get; set; }
    public string RealmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public int ImportSessionCount { get; set; }
}
