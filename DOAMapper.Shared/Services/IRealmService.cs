using DOAMapper.Shared.Models.DTOs;

namespace DOAMapper.Shared.Services;

public interface IRealmService
{
    Task<List<RealmDto>> GetAvailableRealmsAsync();
    Task<RealmDto?> GetRealmAsync(string realmId);
    Task<RealmDto> CreateRealmAsync(string realmId, string name);
    Task<RealmDto?> UpdateRealmAsync(string realmId, string name, bool isActive);
    Task<bool> DeleteRealmAsync(string realmId);
    Task<bool> RealmExistsAsync(string realmId);
}
