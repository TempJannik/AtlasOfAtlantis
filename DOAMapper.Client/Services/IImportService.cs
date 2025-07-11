using DOAMapper.Shared.Models.DTOs;
using Microsoft.AspNetCore.Components.Forms;

namespace DOAMapper.Client.Services;

public interface IImportService
{
    Task<ImportSessionDto> StartImportAsync(IBrowserFile file);
    Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId);
    Task<List<ImportSessionDto>> GetImportHistoryAsync();
    Task<List<DateTime>> GetAvailableImportDatesAsync();
}
