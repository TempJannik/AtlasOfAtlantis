using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Models.Entities;

namespace DOAMapper.Services.Interfaces;

public interface IImportService
{
    Task<ImportSession> StartImportAsync(Stream jsonStream, string fileName, DateTime? importDate = null);
    Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId);
    Task<List<ImportSessionDto>> GetImportHistoryAsync();
    Task<List<DateTime>> GetAvailableImportDatesAsync();
}
