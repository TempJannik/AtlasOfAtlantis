using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Services.Interfaces;
using DOAMapper.Shared.Services;
using DOAMapper.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PlayersController : ControllerBase
{
    private readonly IPlayerService _playerService;
    private readonly IRealmService _realmService;
    private readonly ILogger<PlayersController> _logger;

    public PlayersController(IPlayerService playerService, IRealmService realmService, ILogger<PlayersController> logger)
    {
        _playerService = playerService;
        _realmService = realmService;
        _logger = logger;
    }

    [HttpGet("search")]
    public async Task<ActionResult<PagedResult<PlayerDto>>> SearchPlayers(
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
            var availableDates = await _playerService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var result = await _playerService.SearchPlayersAsync(query, realmId, date.Value, page, size);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching players with query '{Query}'", query);
            return StatusCode(500, "An error occurred while searching players");
        }
    }

    [HttpGet("{realmId}/{playerId}")]
    public async Task<ActionResult<PlayerDetailDto>> GetPlayer(
        string realmId,
        string playerId,
        [FromQuery] DateTime? date = null)
    {

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _playerService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var player = await _playerService.GetPlayerAsync(playerId, realmId, date.Value);
            if (player == null)
            {
                return NotFound($"Player with ID '{playerId}' not found for date {date.Value:yyyy-MM-dd}");
            }

            return Ok(player);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting player {PlayerId}", playerId);
            return StatusCode(500, "An error occurred while retrieving the player");
        }
    }

    [HttpGet("{realmId}/{playerId}/tiles")]
    public async Task<ActionResult<List<TileDto>>> GetPlayerTiles(
        string realmId,
        string playerId,
        [FromQuery] DateTime? date = null)
    {

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _playerService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var tiles = await _playerService.GetPlayerTilesAsync(playerId, realmId, date.Value);
            return Ok(tiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tiles for player {PlayerId}", playerId);
            return StatusCode(500, "An error occurred while retrieving player tiles");
        }
    }

    [HttpGet("{realmId}/{playerId}/history")]
    public async Task<ActionResult<List<HistoryEntryDto<PlayerDto>>>> GetPlayerHistory(
        string realmId,
        string playerId)
    {
        try
        {
            var history = await _playerService.GetPlayerHistoryAsync(playerId, realmId);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for player {PlayerId}", playerId);
            return StatusCode(500, "An error occurred while retrieving player history");
        }
    }

    [HttpGet("dates")]
    public async Task<ActionResult<List<DateTime>>> GetAvailableDates([FromQuery] string? realmId = null)
    {
        // Require realm ID
        if (string.IsNullOrEmpty(realmId))
        {
            return BadRequest("RealmId is required");
        }

        try
        {
            var dates = await _playerService.GetAvailableDatesAsync(realmId);
            return Ok(dates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available dates");
            return StatusCode(500, "An error occurred while retrieving available dates");
        }
    }

    [HttpGet("{realmId}/{playerId}/rank")]
    public async Task<ActionResult<int>> GetPlayerRank(
        string realmId,
        string playerId,
        [FromQuery] DateTime? date = null)
    {
        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _playerService.GetAvailableDatesAsync(realmId);
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var rank = await _playerService.GetPlayerRankAsync(playerId, realmId, date.Value);
            if (rank == 0)
            {
                return NotFound($"Player with ID '{playerId}' not found for date {date.Value:yyyy-MM-dd}");
            }

            return Ok(rank);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting rank for player {PlayerId}", playerId);
            return StatusCode(500, "An error occurred while retrieving player rank");
        }
    }
}
