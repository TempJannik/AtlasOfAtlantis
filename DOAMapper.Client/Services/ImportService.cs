using System.Net.Http.Json;
using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Shared.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace DOAMapper.Client.Services;

public class ImportService : IImportService
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationService _authService;

    public ImportService(HttpClient httpClient, IAuthenticationService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<ImportSessionDto> StartImportAsync(IBrowserFile file)
    {
        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream(maxAllowedSize: 100 * 1024 * 1024); // 100MB
        using var streamContent = new StreamContent(fileStream);

        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        content.Add(streamContent, "file", file.Name);

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/import/upload")
        {
            Content = content
        };

        await AddAuthHeadersAsync(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportSessionDto>();
        return result ?? throw new InvalidOperationException("Failed to start import");
    }

    public async Task<ImportSessionDto> GetImportStatusAsync(Guid sessionId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/import/status/{sessionId}");
        await AddAuthHeadersAsync(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ImportSessionDto>();
        return result ?? throw new InvalidOperationException($"Import session {sessionId} not found");
    }

    public async Task<List<ImportSessionDto>> GetImportHistoryAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/import/history");
        await AddAuthHeadersAsync(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<ImportSessionDto>>();
        return result ?? new List<ImportSessionDto>();
    }

    public async Task<List<DateTime>> GetAvailableImportDatesAsync()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/import/dates");
        await AddAuthHeadersAsync(request);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<List<DateTime>>();
        return result ?? new List<DateTime>();
    }

    private async Task AddAuthHeadersAsync(HttpRequestMessage request)
    {
        var isAdmin = await _authService.IsAdminAsync();
        if (isAdmin)
        {
            // Get the admin password from the authentication service
            var adminPassword = _authService.GetAdminPassword();
            if (!string.IsNullOrEmpty(adminPassword))
            {
                request.Headers.Add("X-Admin-Password", adminPassword);
            }
        }
    }
}
