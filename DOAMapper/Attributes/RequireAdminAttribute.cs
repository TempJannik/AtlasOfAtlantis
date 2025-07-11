using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DOAMapper.Attributes;

/// <summary>
/// Authorization attribute that requires admin password in request headers
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminAttribute : Attribute, IActionFilter
{
    private const string AdminPassword = "accutane";
    private const string AuthHeaderName = "X-Admin-Password";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Check if the request contains the admin password header
        if (!context.HttpContext.Request.Headers.TryGetValue(AuthHeaderName, out var headerValue))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                message = "Admin authorization required",
                error = "Missing admin password header"
            });
            return;
        }

        var providedPassword = headerValue.ToString();
        
        if (string.IsNullOrWhiteSpace(providedPassword))
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                message = "Admin authorization required",
                error = "Empty admin password"
            });
            return;
        }

        if (providedPassword != AdminPassword)
        {
            context.Result = new UnauthorizedObjectResult(new 
            { 
                message = "Invalid admin credentials",
                error = "Incorrect admin password"
            });
            return;
        }

        // Authorization successful - continue with the action
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No action needed after execution
    }
}
