using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;

namespace DOAMapper.Services.Interfaces;

public interface IImportService
{
    Task<ImportSession> StartImportAsync(Stream jsonStream, string fileName, string realmId, DateTime? importDate = null);
    Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId);
    Task<List<ImportSessionDto>> GetImportHistoryAsync(string realmId);
    Task<List<DateTime>> GetAvailableImportDatesAsync(string realmId);
}
