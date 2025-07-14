using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Services.Interfaces;
using DOAMapper.Services;
using DOAMapper.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
[RequireAdmin]
public class ImportController : ControllerBase
{
    private readonly IImportService _importService;
    private readonly IRealmService _realmService;
    private readonly BackgroundImportService _backgroundImportService;
    private readonly ImportStatusService _importStatusService;
    private readonly ILogger<ImportController> _logger;

    public ImportController(
        IImportService importService,
        IRealmService realmService,
        BackgroundImportService backgroundImportService,
        ImportStatusService importStatusService,
        ILogger<ImportController> logger)
    {
        _importService = importService;
        _realmService = realmService;
        _backgroundImportService = backgroundImportService;
        _importStatusService = importStatusService;
        _logger = logger;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ImportSessionDto>> UploadFile(IFormFile file, [FromForm] string? realmId = null, [FromForm] DateTime? importDate = null)
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

        // Validate import date if provided
        if (importDate.HasValue)
        {
            var dateOnly = importDate.Value.Date;
            var today = DateTime.UtcNow.Date;

            // Allow dates from 1 year ago to 1 week in the future
            if (dateOnly < today.AddYears(-1) || dateOnly > today.AddDays(7))
            {
                return BadRequest("Import date must be within the last year and not more than a week in the future");
            }
        }

        // Use default realm if not specified
        if (string.IsNullOrEmpty(realmId))
        {
            var defaultRealm = await _realmService.GetOrCreateDefaultRealmAsync();
            realmId = defaultRealm.RealmId;
        }
        else
        {
            // Validate that the specified realm exists
            var realmExists = await _realmService.RealmExistsAsync(realmId);
            if (!realmExists)
            {
                return BadRequest($"Realm '{realmId}' does not exist");
            }
        }

        try
        {
            using var stream = file.OpenReadStream();

            // Use BackgroundImportService for background processing
            var session = await _backgroundImportService.StartBackgroundImportAsync(stream, file.FileName, realmId, importDate);
            var sessionDto = await _importStatusService.GetImportStatusAsync(session.Id);

            _logger.LogInformation("Background import started for file {FileName} with session {SessionId} for realm {RealmId} and date {ImportDate}",
                file.FileName, session.Id, realmId, importDate?.ToString("yyyy-MM-dd") ?? "current");

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
    public async Task<ActionResult<List<ImportSessionDto>>> GetImportHistory([FromQuery] string? realmId = null)
    {
        // Use default realm if not specified
        if (string.IsNullOrEmpty(realmId))
        {
            var defaultRealm = await _realmService.GetOrCreateDefaultRealmAsync();
            realmId = defaultRealm.RealmId;
        }

        var history = await _importService.GetImportHistoryAsync(realmId);
        return Ok(history);
    }

    [HttpGet("dates")]
    public async Task<ActionResult<List<DateTime>>> GetAvailableDates([FromQuery] string? realmId = null)
    {
        // Use default realm if not specified
        if (string.IsNullOrEmpty(realmId))
        {
            var defaultRealm = await _realmService.GetOrCreateDefaultRealmAsync();
            realmId = defaultRealm.RealmId;
        }

        var dates = await _importService.GetAvailableImportDatesAsync(realmId);
        return Ok(dates);
    }

    [HttpPost("cancel/{sessionId}")]
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
