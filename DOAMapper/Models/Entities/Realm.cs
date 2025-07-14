namespace DOAMapper.Models.Entities;

public class Realm
{
    public Guid Id { get; set; }
    public string RealmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
