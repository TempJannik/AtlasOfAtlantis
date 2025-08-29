using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Services.Interfaces;
using DOAMapper.Shared.Services;
using DOAMapper.Attributes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AlliancesController : ControllerBase
{
    private readonly IAllianceService _allianceService;
    private readonly IRealmService _realmService;
    private readonly ILogger<AlliancesController> _logger;

    public AlliancesController(IAllianceService allianceService, IRealmService realmService, ILogger<AlliancesController> logger)
    {
        _allianceService = allianceService;
        _realmService = realmService;
        _logger = logger;
    }

    [HttpGet]
    [OutputCache(PolicyName = "LongLivedData")]
    public async Task<ActionResult<PagedResult<AllianceDto>>> GetAlliances(
        [FromQuery] string? realmId = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        // Require realm ID
        if (string.IsNullOrEmpty(realmId))
        {
            return BadRequest("RealmId is required");
        }

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var result = await _allianceService.GetAlliancesAsync(realmId, date.Value, page, size);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting alliances");
            return StatusCode(500, "An error occurred while retrieving alliances");
        }
    }

    [HttpGet("search")]
    [OutputCache(PolicyName = "LongLivedData")]
    public async Task<ActionResult<PagedResult<AllianceDto>>> SearchAlliances(
        [FromQuery] string query = "",
        [FromQuery] string? realmId = null,
        [FromQuery] DateTime? date = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        if (page < 1) page = 1;
        if (size < 1 || size > 100) size = 20;

        // Require realm ID
        if (string.IsNullOrEmpty(realmId))
        {
            return BadRequest("RealmId is required");
        }

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var result = await _allianceService.SearchAlliancesAsync(query, realmId, date.Value, page, size);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching alliances with query '{Query}'", query);
            return StatusCode(500, "An error occurred while searching alliances");
        }
    }

    [HttpGet("{realmId}/{allianceId}")]
    [OutputCache(PolicyName = "LongLivedData")]
    public async Task<ActionResult<AllianceDto>> GetAlliance(
        string realmId,
        string allianceId,
        [FromQuery] DateTime? date = null)
    {

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var alliance = await _allianceService.GetAllianceAsync(allianceId, realmId, date.Value);
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

    [HttpGet("{realmId}/{allianceId}/members")]
    [OutputCache(PolicyName = "LongLivedData")]
    public async Task<ActionResult<PagedResult<PlayerDto>>> GetAllianceMembers(
        string realmId,
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
            var availableDates = await _allianceService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var members = await _allianceService.GetAllianceMembersAsync(allianceId, realmId, date.Value, page, size);
            return Ok(members);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting members for alliance {AllianceId}", allianceId);
            return StatusCode(500, "An error occurred while retrieving alliance members");
        }
    }

    [HttpGet("{realmId}/{allianceId}/tiles")]
    public async Task<ActionResult<List<TileDto>>> GetAllianceTiles(
        string realmId,
        string allianceId,
        [FromQuery] DateTime? date = null)
    {

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _allianceService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var tiles = await _allianceService.GetAllianceTilesAsync(allianceId, realmId, date.Value);
            return Ok(tiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tiles for alliance {AllianceId}", allianceId);
            return StatusCode(500, "An error occurred while retrieving alliance tiles");
        }
    }

    [HttpGet("{realmId}/{allianceId}/history")]
    public async Task<ActionResult<List<HistoryEntryDto<AllianceDto>>>> GetAllianceHistory(
        string realmId,
        string allianceId)
    {

        try
        {
            var history = await _allianceService.GetAllianceHistoryAsync(allianceId, realmId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for alliance {AllianceId}", allianceId);
            return StatusCode(500, "An error occurred while retrieving alliance history");
        }
    }

    [HttpGet("dates")]
    [OutputCache(PolicyName = "LongLivedData")]
    public async Task<ActionResult<List<DateTime>>> GetAvailableDates([FromQuery] string? realmId = null)
    {
        // Require realm ID
        if (string.IsNullOrEmpty(realmId))
        {
            return BadRequest("RealmId is required");
        }

        try
        {
            var dates = await _allianceService.GetAvailableDatesAsync(realmId);
            return Ok(dates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available dates");
            return StatusCode(500, "An error occurred while retrieving available dates");
        }
    }
}
