namespace Carpediem.Web.Errors;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly Action<ILogger, string, Exception?> LogUnhandledRequestFailure =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1, nameof(LogUnhandledRequestFailure)),
            "Unhandled request failure {TraceId}");

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status400BadRequest)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ApiProblemDetailsService.Problem(
                context,
                StatusCodes.Status400BadRequest,
                "malformed_request",
                "The request body is malformed.")
                .ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            LogUnhandledRequestFailure(logger, context.TraceIdentifier, exception);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await ApiProblemDetailsService.Problem(
                context,
                StatusCodes.Status500InternalServerError,
                "unexpected_error",
                "The request could not be completed.",
                "Use the trace identifier when contacting support.")
                .ExecuteAsync(context);
        }
    }
}
