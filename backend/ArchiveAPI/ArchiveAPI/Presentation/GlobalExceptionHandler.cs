using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ArchiveAPI.Presentation;

public class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Default values
        int statusCode = StatusCodes.Status500InternalServerError;
        string title = "Server error";
        string detail = exception.Message;
        Dictionary<string, object> additionalData = new Dictionary<string, object>();

        logger.LogError(exception, "Custom exception occurred: {Message}", exception.Message);

        switch (exception)
        {
            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "CustomExceptionNotImplemented";
                detail = "An unexpected error occurred. Please try again later.";
                break;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Extensions = additionalData!
        };
        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}