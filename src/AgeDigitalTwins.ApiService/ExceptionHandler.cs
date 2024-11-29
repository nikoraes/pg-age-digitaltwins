using AgeDigitalTwins.Exceptions;
using DTDLParser;
using Microsoft.AspNetCore.Mvc;

namespace AgeDigitalTwins.ApiService;

public class ExceptionHandler : Microsoft.AspNetCore.Diagnostics.IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is DigitalTwinNotFoundException || exception is ModelNotFoundException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
        }
        else if (exception is AgeDigitalTwinsException || exception is ResolutionException)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
        else
        {
            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Title = "An error occurred",
            Detail = $"{exception.Message}",
            Type = exception.GetType().Name,
            Status = httpContext.Response.StatusCode,
        }, cancellationToken: cancellationToken);

        return true;
    }
}

public class ExceptionResponses
{
    /* public Dictionary<Type, ProblemDetails> ExceptionResponsesMap { get; } = new Dictionary<Type, ProblemDetails>
        {
            { typeof(ModelNotFoundException), new ProblemDetails
                {
                    Title = "An error occurred",
                    Detail = exception.Message,
                    Type = exception.GetType().Name,
                    Status = StatusCodes.Status400BadRequest
                }



            Results.BadRequest("Model not found") },
            { typeof(DigitalTwinNotFoundException), Results.NotFound("Digital twin not found") },
            { typeof(ValidationFailedException), Results.BadRequest("Validation failed") },
            { typeof(InvalidAdtQueryException), Results.BadRequest("Invalid ADT query") }
        }; */
}
