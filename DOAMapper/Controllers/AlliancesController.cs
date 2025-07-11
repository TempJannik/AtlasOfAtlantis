using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlliancesController : ControllerBase
{
    private readonly IAllianceService _allianceService;
    private readonly ILogger<AlliancesController> _logger;

    public AlliancesController(IAllianceService allianceService, ILogger<AlliancesController> logger)
    {
        _allianceService = allianceService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<AllianceDto>>> GetAlliances(
        [FromQuery] DateTime? date = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync();
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var result = await _allianceService.GetAlliancesAsync(date.Value, page, size);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alliances");
            return StatusCode(500, "An error occurred while retrieving alliances");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<AllianceDto>>> SearchAlliances(
        [FromQuery] string query = "",
        [FromQuery] DateTime? date = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync();
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var result = await _allianceService.SearchAlliancesAsync(query, date.Value, page, size);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching alliances with query '{Query}'", query);
            return StatusCode(500, "An error occurred while searching alliances");
        }
    }

    [HttpGet("{allianceId}")]
    public async Task<ActionResult<AllianceDto>> GetAlliance(
        string allianceId,
        [FromQuery] DateTime? date = null)
    {
        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync();
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var alliance = await _allianceService.GetAllianceAsync(allianceId, date.Value);
            if (alliance == null)
            {
                return NotFound($"Alliance with ID '{allianceId}' not found for date {date.Value:yyyy-MM-dd}");
            }

            return Ok(alliance);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alliance {AllianceId}", allianceId);
            return StatusCode(500, "An error occurred while retrieving the alliance");
        }
    }

    [HttpGet("{allianceId}/members")]
    public async Task<ActionResult<PagedResult<PlayerDto>>> GetAllianceMembers(
        string allianceId,
        [FromQuery] DateTime? date = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync();
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var members = await _allianceService.GetAllianceMembersAsync(allianceId, date.Value, page, size);
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members for alliance {AllianceId}", allianceId);
            return StatusCode(500, "An error occurred while retrieving alliance members");
        }
    }

    [HttpGet("{allianceId}/tiles")]
    public async Task<ActionResult<List<TileDto>>> GetAllianceTiles(
        string allianceId,
        [FromQuery] DateTime? date = null)
    {
        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync();
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var tiles = await _allianceService.GetAllianceTilesAsync(allianceId, date.Value);
            return Ok(tiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tiles for alliance {AllianceId}", allianceId);
            return StatusCode(500, "An error occurred while retrieving alliance tiles");
        }
    }

    [HttpGet("{allianceId}/history")]
    public async Task<ActionResult<List<HistoryEntryDto<AllianceDto>>>> GetAllianceHistory(string allianceId)
    {
        try
        {
            var history = await _allianceService.GetAllianceHistoryAsync(allianceId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for alliance {AllianceId}", allianceId);
            return StatusCode(500, "An error occurred while retrieving alliance history");
        }
    }

    [HttpGet("dates")]
    public async Task<ActionResult<List<DateTime>>> GetAvailableDates()
    {
        try
        {
            var dates = await _allianceService.GetAvailableDatesAsync();
            return Ok(dates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available dates");
            return StatusCode(500, "An error occurred while retrieving available dates");
        }
    }
}
