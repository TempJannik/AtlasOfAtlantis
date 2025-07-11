using System.Net.Http.Json;
using DOAMapper.Shared.Models.DTOs;
using Microsoft.AspNetCore.Components.Forms;

namespace DOAMapper.Client.Services;

public class ImportService : IImportService
{
    private readonly HttpClient _httpClient;

    public ImportService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ImportSessionDto> StartImportAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB
        using var streamContent = new StreamContent(fileStream);
        
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Add(streamContent, "file", file.Name);

        var response = await _httpClient.PostAsync("api/import/upload", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportSessionDto>();
        return result ?? throw new InvalidOperationException("Failed to start import");
    }

    public async Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId)
    {
        var response = await _httpClient.GetFromJsonAsync<ImportSessionDto>($"api/import/status/{sessionId}");
        return response ?? throw new InvalidOperationException($"Import session {sessionId} not found");
    }

    public async Task<List<ImportSessionDto>> GetImportHistoryAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<List<ImportSessionDto>>("api/import/history");
        return response ?? new List<ImportSessionDto>();
    }

    public async Task<List<DateTime>> GetAvailableImportDatesAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<List<DateTime>>("api/import/dates");
        return response ?? new List<DateTime>();
    }
}
