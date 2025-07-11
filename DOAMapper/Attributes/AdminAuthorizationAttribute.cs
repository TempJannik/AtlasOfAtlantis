using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DOAMapper.Attributes;

public class AdminAuthorizationAttribute : Attribute, IAuthorizationFilter
{
    private const string AdminPassword = "accutane";
    private const string AuthHeaderName = "X-Admin-Password";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Check if the request contains the admin password header
        if (!context.HttpContext.Request.Headers.TryGetValue(AuthHeaderName, out var headerValue))
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Admin authorization required" });
            return;
        }

        var providedPassword = headerValue.ToString();
        
        if (string.IsNullOrWhiteSpace(providedPassword) || providedPassword != AdminPassword)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Invalid admin credentials" });
            return;
        }

        // Authorization successful - continue with the request
    }
}
