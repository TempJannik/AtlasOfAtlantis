using Microsoft.AspNetCore.Mvc;
using DOAMapper.Shared.Models.Authentication;

namespace DOAMapper.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private const string UserPassword = "mm25";
    private const string AdminPassword = "accutane";
    private readonly ILogger<AuthController> _logger;

    public AuthController(ILogger<AuthController> logger)
    {
        _logger = logger;
    }

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Message = "Password is required"
            });
        }

        if (request.Password == AdminPassword)
        {
            _logger.LogInformation("Admin login successful");
            return Ok(new LoginResponse
            {
                Success = true,
                Role = UserRole.Admin,
                Message = "Admin login successful"
            });
        }

        if (request.Password == UserPassword)
        {
            _logger.LogInformation("User login successful");
            return Ok(new LoginResponse
            {
                Success = true,
                Role = UserRole.User,
                Message = "User login successful"
            });
        }

        _logger.LogWarning("Invalid login attempt");
        return Unauthorized(new LoginResponse
        {
            Success = false,
            Message = "Invalid password"
        });
    }
}
