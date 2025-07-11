using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Services.Interfaces;
using DOAMapper.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireAdmin]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(IImportService importService, ILogger<ImportController> logger)
    {
        _importService = importService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ImportSessionDto>> UploadFile(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded");
        }

        if (!file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("Only JSON files are allowed");
        }

        if (file.Length > 100 * 1024 * 1024) // 100MB limit
        {
            return BadRequest("File size exceeds 100MB limit");
        }

        try
        {
            using var stream = file.OpenReadStream();
            var session = await _importService.StartImportAsync(stream, file.FileName);
            var sessionDto = await _importService.GetImportStatusAsync(session.Id);
            
            _logger.LogInformation("Import started for file {FileName} with session {SessionId}", 
                file.FileName, session.Id);
            
            return Ok(sessionDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start import for file {FileName}", file.FileName);
            return StatusCode(500, "Failed to start import");
        }
    }

    [HttpGet("status/{sessionId}")]
    public async Task<ActionResult<ImportSessionDto>> GetImportStatus(Guid sessionId)
    {
        try
        {
            var status = await _importService.GetImportStatusAsync(sessionId);
            return Ok(status);
        }
        catch (ArgumentException)
        {
            return NotFound("Import session not found");
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<ImportSessionDto>>> GetImportHistory()
    {
        var history = await _importService.GetImportHistoryAsync();
        return Ok(history);
    }

    [HttpGet("dates")]
    public async Task<ActionResult<List<DateTime>>> GetAvailableDates()
    {
        var dates = await _importService.GetAvailableImportDatesAsync();
        return Ok(dates);
    }
}
