using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;

namespace DOAMapper.Services.Interfaces;

public interface ITemporalDataService
{
    Task ApplyTileChangesAsync(ChangeSet<Tile> changes, Guid importSessionId, DateTime importDate);
    Task ApplyPlayerChangesAsync(ChangeSet<Player> changes, Guid importSessionId, DateTime importDate);
    Task ApplyAllianceChangesAsync(ChangeSet<Alliance> changes, Guid importSessionId, DateTime importDate);
}
