using System.Net;

namespace DOAMapper.Services;

public class ErrorHandlingService
{
    public event Action<string>? GlobalErrorOccurred;

    public string GetUserFriendlyErrorMessage(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx when httpEx.Message.Contains("404") => 
                "The requested data was not found. Please check your selection and try again.",
            HttpRequestException httpEx when httpEx.Message.Contains("500") => 
                "A server error occurred. Please try again later.",
            HttpRequestException httpEx when httpEx.Message.Contains("timeout") => 
                "The request timed out. Please check your connection and try again.",
            TaskCanceledException => 
                "The operation was cancelled or timed out. Please try again.",
            UnauthorizedAccessException => 
                "You don't have permission to access this resource.",
            ArgumentException argEx => 
                $"Invalid input: {argEx.Message}",
            InvalidOperationException invEx => 
                $"Operation failed: {invEx.Message}",
            _ => "An unexpected error occurred. Please try again."
        };
    }

    public void HandleError(Exception exception, string context = "")
    {
        var userMessage = GetUserFriendlyErrorMessage(exception);
        if (!string.IsNullOrEmpty(context))
        {
            userMessage = $"{context}: {userMessage}";
        }
        
        // Log the actual exception for debugging (in a real app, this would go to a logging service)
        Console.WriteLine($"Error in {context}: {exception}");
        
        // Notify subscribers of the global error
        GlobalErrorOccurred?.Invoke(userMessage);
    }

    public async Task<T?> ExecuteWithErrorHandling<T>(
        Func<Task<T>> operation, 
        string context = "",
        T? defaultValue = default)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            HandleError(ex, context);
            return defaultValue;
        }
    }

    public async Task ExecuteWithErrorHandling(
        Func<Task> operation, 
        string context = "")
    {
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            HandleError(ex, context);
        }
    }
}
