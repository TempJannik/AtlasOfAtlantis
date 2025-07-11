using DOAMapper.Models.Entities;
using DOAMapper.Services.Interfaces;

namespace DOAMapper.Services.Interfaces;

public interface ITemporalDataService
{
    Task ApplyTileChangesAsync(ChangeSet<Tile> changes, Guid importSessionId);
    Task ApplyPlayerChangesAsync(ChangeSet<Player> changes, Guid importSessionId);
    Task ApplyAllianceChangesAsync(ChangeSet<Alliance> changes, Guid importSessionId);
}
