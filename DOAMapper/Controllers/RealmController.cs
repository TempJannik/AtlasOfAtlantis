using DOAMapper.Shared.Models.DTOs;
using DOAMapper.Shared.Services;
using DOAMapper.Attributes;
using DOAMapper.Shared.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;


namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RealmController : ControllerBase
{
    private readonly IRealmService _realmService;
    private readonly ILogger<RealmController> _logger;

    public RealmController(IRealmService realmService, ILogger<RealmController> logger)
    {
        _realmService = realmService;
        _logger = logger;
    }

    [HttpGet]
    [OutputCache(PolicyName = "LongLivedData")]
    public async Task<ActionResult<List<RealmDto>>> GetAvailableRealms()
    {
        try
        {
            var realms = await _realmService.GetAvailableRealmsAsync();
            return Ok(realms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available realms");
            return StatusCode(500, "An error occurred while retrieving realms");
        }
    }

    [HttpGet("{realmId}")]
    public async Task<ActionResult<RealmDto>> GetRealm(string realmId)
    {
        try
        {
            var realm = await _realmService.GetRealmAsync(realmId);
            if (realm == null)
            {
                return NotFound($"Realm with ID '{realmId}' not found");
            }

            return Ok(realm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting realm {RealmId}", realmId);
            return StatusCode(500, "An error occurred while retrieving the realm");
        }
    }

    [HttpPost]
    [RequireAdmin]
    public async Task<ActionResult<RealmDto>> CreateRealm([FromBody] CreateRealmRequest request)
    {
        // Validate request
        var validationResult = RealmControllerExtensions.ValidateCreateRealmRequest(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        try
        {
            var realm = await _realmService.CreateRealmAsync(request.RealmId, request.Name);
            return CreatedAtAction(nameof(GetRealm), new { realmId = realm.RealmId }, realm);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating realm {RealmId}", request.RealmId);
            return StatusCode(500, "An error occurred while creating the realm");
        }
    }

    [HttpPut("{realmId}")]
    [RequireAdmin]
    public async Task<ActionResult<RealmDto>> UpdateRealm(string realmId, [FromBody] UpdateRealmRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("Name is required");
        }

        try
        {
            var realm = await _realmService.UpdateRealmAsync(realmId, request.Name, request.IsActive);
            if (realm == null)
            {
                return NotFound($"Realm with ID '{realmId}' not found");
            }

            return Ok(realm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating realm {RealmId}", realmId);
            return StatusCode(500, "An error occurred while updating the realm");
        }
    }

    [HttpDelete("{realmId}")]
    [RequireAdmin]
    public async Task<ActionResult> DeleteRealm(string realmId)
    {
        try
        {
            var deleted = await _realmService.DeleteRealmAsync(realmId);
            if (!deleted)
            {
                return NotFound($"Realm with ID '{realmId}' not found");
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting realm {RealmId}", realmId);
            return StatusCode(500, "An error occurred while deleting the realm");
        }
    }


}

public class CreateRealmRequest
{
    public string RealmId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class UpdateRealmRequest
{
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public static class RealmControllerExtensions
{
    public static ActionResult? ValidateCreateRealmRequest(CreateRealmRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RealmId))
            return new BadRequestObjectResult("RealmId is required");

        if (string.IsNullOrWhiteSpace(request.Name))
            return new BadRequestObjectResult("Name is required");

        if (request.RealmId.Length > RealmConstants.MaxRealmIdLength)
            return new BadRequestObjectResult($"RealmId cannot exceed {RealmConstants.MaxRealmIdLength} characters");

        if (request.Name.Length > RealmConstants.MaxRealmNameLength)
            return new BadRequestObjectResult($"Name cannot exceed {RealmConstants.MaxRealmNameLength} characters");

        if (RealmConstants.ReservedRealmIds.Contains(request.RealmId))
            return new BadRequestObjectResult($"RealmId '{request.RealmId}' is reserved and cannot be used");

        return null;
    }
}
