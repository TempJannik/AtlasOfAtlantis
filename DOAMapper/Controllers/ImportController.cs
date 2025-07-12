using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Services.Interfaces;
using DOAMapper.Services;
using DOAMapper.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireAdmin]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly BackgroundImportService _backgroundImportService;
    private readonly ImportStatusService _importStatusService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        IImportService importService,
        BackgroundImportService backgroundImportService,
        ImportStatusService importStatusService,
        ILogger<ImportController> logger)
    {
        _importService = importService;
        _backgroundImportService = backgroundImportService;
        _importStatusService = importStatusService;
        _logger = logger;
    }

    [HttpPost("upload")]
    [EnableRateLimiting("import")]
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

            // Use BackgroundImportService for background processing
            var session = await _backgroundImportService.StartBackgroundImportAsync(stream, file.FileName);
            var sessionDto = await _importStatusService.GetImportStatusAsync(session.Id);

            _logger.LogInformation("Background import started for file {FileName} with session {SessionId}",
                file.FileName, session.Id);

            return Ok(sessionDto);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already in progress"))
        {
            _logger.LogWarning("Import upload rejected - another import is already in progress");
            return Conflict("Another import is already in progress. Please wait for it to complete.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start import for file {FileName}", file.FileName);
            return StatusCode(500, "Failed to start import");
        }
    }

    [HttpGet("status/{sessionId}")]
    [EnableRateLimiting("api")]
    public async Task<ActionResult<ImportSessionDto>> GetImportStatus(Guid sessionId)
    {
        try
        {
            var status = await _importStatusService.GetImportStatusAsync(sessionId);
            return Ok(status);
        }
        catch (ArgumentException)
        {
            return NotFound("Import session not found");
        }
    }

    [HttpGet("history")]
    [EnableRateLimiting("api")]
    public async Task<ActionResult<List<ImportSessionDto>>> GetImportHistory()
    {
        var history = await _importStatusService.GetImportHistoryAsync();
        return Ok(history);
    }

    [HttpGet("dates")]
    [EnableRateLimiting("api")]
    public async Task<ActionResult<List<DateTime>>> GetAvailableDates()
    {
        var dates = await _importService.GetAvailableImportDatesAsync();
        return Ok(dates);
    }

    [HttpPost("cancel/{sessionId}")]
    [EnableRateLimiting("import")]
    public ActionResult CancelImport(Guid sessionId)
    {
        try
        {
            var cancelled = _backgroundImportService.CancelImport(sessionId);
            if (cancelled)
            {
                _logger.LogInformation("Import cancellation requested for session {SessionId}", sessionId);
                return Ok(new { message = "Import cancellation requested" });
            }
            else
            {
                return NotFound("Import session not found or not active");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel import for session {SessionId}", sessionId);
            return StatusCode(500, "Failed to cancel import");
        }
    }

    [HttpGet("active")]
    [EnableRateLimiting("api")]
    public ActionResult<List<Guid>> GetActiveImports()
    {
        try
        {
            var activeImports = _backgroundImportService.GetActiveImportSessions();
            return Ok(activeImports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active imports");
            return StatusCode(500, "Failed to get active imports");
        }
    }
}
