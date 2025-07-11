using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Services.Interfaces;
using DOAMapper.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MapController : ControllerBase
{
    private readonly IMapService _mapService;
    private readonly ILogger<MapController> _logger;

    public MapController(IMapService mapService, ILogger<MapController> logger)
    {
        _mapService = mapService;
        _logger = logger;
    }

    [HttpGet("region")]
    public async Task<ActionResult<List<TileDto>>> GetRegionTiles(
        [FromQuery] int x1,
        [FromQuery] int y1,
        [FromQuery] int x2,
        [FromQuery] int y2,
        [FromQuery] DateTime? date = null)
    {
        // Validate coordinates
        if (x1 < 0 || x1 > 749 || x2 < 0 || x2 > 749 ||
            y1 < 0 || y1 > 749 || y2 < 0 || y2 > 749)
        {
            return BadRequest("Coordinates must be between 0 and 749");
        }

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _mapService.GetAvailableDatesAsync();
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var tiles = await _mapService.GetRegionTilesAsync(x1, y1, x2, y2, date.Value);
            return Ok(tiles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting region tiles ({X1},{Y1}) to ({X2},{Y2})", x1, y1, x2, y2);
            return StatusCode(500, "An error occurred while retrieving region tiles");
        }
    }

    [HttpGet("tile")]
    public async Task<ActionResult<TileDto>> GetTile(
        [FromQuery] int x,
        [FromQuery] int y,
        [FromQuery] DateTime? date = null)
    {
        // Validate coordinates
        if (x < 0 || x > 749 || y < 0 || y > 749)
        {
            return BadRequest("Coordinates must be between 0 and 749");
        }

        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _mapService.GetAvailableDatesAsync();
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var tile = await _mapService.GetTileAsync(x, y, date.Value);
            if (tile == null)
            {
                return NotFound($"Tile at ({x},{y}) not found for date {date.Value:yyyy-MM-dd}");
            }

            return Ok(tile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tile at ({X},{Y})", x, y);
            return StatusCode(500, "An error occurred while retrieving the tile");
        }
    }

    [HttpGet("tile/history")]
    [RequireAdmin]
    public async Task<ActionResult<List<HistoryEntryDto<TileDto>>>> GetTileHistory(
        [FromQuery] int x,
        [FromQuery] int y)
    {
        // Validate coordinates
        if (x < 0 || x > 749 || y < 0 || y > 749)
        {
            return BadRequest("Coordinates must be between 0 and 749");
        }

        try
        {
            var history = await _mapService.GetTileHistoryAsync(x, y);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting history for tile at ({X},{Y})", x, y);
            return StatusCode(500, "An error occurred while retrieving tile history");
        }
    }

    [HttpGet("statistics")]
    [RequireAdmin]
    public async Task<ActionResult<Dictionary<string, int>>> GetTileStatistics(
        [FromQuery] DateTime? date = null)
    {
        // Use latest available date if not specified
        if (!date.HasValue)
        {
            var availableDates = await _mapService.GetAvailableDatesAsync();
            if (!availableDates.Any())
            {
                return BadRequest("No data available. Please import data first.");
            }
            date = availableDates.First();
        }

        try
        {
            var statistics = await _mapService.GetTileStatisticsAsync(date.Value);
            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tile statistics");
            return StatusCode(500, "An error occurred while retrieving tile statistics");
        }
    }

    [HttpGet("dates")]
    public async Task<ActionResult<List<DateTime>>> GetAvailableDates()
    {
        try
        {
            var dates = await _mapService.GetAvailableDatesAsync();
            return Ok(dates);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available dates");
            return StatusCode(500, "An error occurred while retrieving available dates");
        }
    }
}
