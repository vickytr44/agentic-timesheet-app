namespace backend.Middleware;

/// <summary>
/// Logs incoming HTTP requests and outgoing responses using structured logging.
/// Replaces the inline Console.WriteLine middleware from Program.cs.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        _logger.LogInformation("--> [{Method}] {Path}", context.Request.Method, context.Request.Path);
        await _next(context);
        _logger.LogInformation("<-- [{Method}] {Path} : {StatusCode}",
            context.Request.Method, context.Request.Path, context.Response.StatusCode);
    }
}
