using DOAMapper.Shared.Models.DTOs;
using Microsoft.AspNetCore.Components.Forms;

namespace DOAMapper.Client.Services;

public interface IImportService
{
    Task<ImportSessionDto> StartImportAsync(IBrowserFile file, string realmId, DateTime? importDate = null);
    Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId);
    Task<List<ImportSessionDto>> GetImportHistoryAsync(string realmId);
    Task<List<DateTime>> GetAvailableImportDatesAsync(string realmId);
}
