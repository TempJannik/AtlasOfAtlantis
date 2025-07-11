using DOAMapper.Models.Entities;

namespace DOAMapper.Services.Interfaces;

public interface IChangeDetectionService
{
    Task<ChangeSet<Tile>> DetectTileChangesAsync(List<Tile> incoming, List<Tile> current);
    Task<ChangeSet<Player>> DetectPlayerChangesAsync(List<Player> incoming, List<Player> current);
    Task<ChangeSet<Alliance>> DetectAllianceChangesAsync(List<Alliance> incoming, List<Alliance> current);
}

public class ChangeSet<T>
{
    public List<T> Added { get; set; } = new();
    public List<T> Modified { get; set; } = new();
    public List<T> Removed { get; set; } = new();
    public List<(T Old, T New)> Changes { get; set; } = new();
    
    public int TotalChanges => Added.Count + Modified.Count + Removed.Count;
    public bool HasChanges => TotalChanges > 0;
}
