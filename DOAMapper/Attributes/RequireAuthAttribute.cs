using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DOAMapper.Attributes;

/// <summary>
/// Authorization attribute that requires either user or admin password in request headers
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAuthAttribute : Attribute, IActionFilter
{
    private const string UserPassword = "mm25";
    private const string AdminPassword = "accutane";
    private const string AdminHeaderName = "X-Admin-Password";
    private const string UserHeaderName = "X-User-Password";

    public void OnActionExecuting(ActionExecutingContext context)
    {
        // Check for admin password first
        if (context.HttpContext.Request.Headers.TryGetValue(AdminHeaderName, out var adminHeaderValue))
        {
            var providedAdminPassword = adminHeaderValue.ToString();
            if (!string.IsNullOrWhiteSpace(providedAdminPassword) && providedAdminPassword == AdminPassword)
            {
                // Admin authorization successful - continue with the action
                return;
            }
        }

        // Check for user password
        if (context.HttpContext.Request.Headers.TryGetValue(UserHeaderName, out var userHeaderValue))
        {
            var providedUserPassword = userHeaderValue.ToString();
            if (!string.IsNullOrWhiteSpace(providedUserPassword) && providedUserPassword == UserPassword)
            {
                // User authorization successful - continue with the action
                return;
            }
        }

        // No valid authentication found
        context.Result = new UnauthorizedObjectResult(new 
        { 
            message = "Authentication required",
            error = "Missing or invalid authentication credentials"
        });
    }

    public void OnActionExecuted(ActionExecutedContext context)
    {
        // No action needed after execution
    }
}
