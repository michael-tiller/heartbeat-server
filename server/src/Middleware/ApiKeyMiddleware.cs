using Heartbeat.Contracts;
using Serilog;

namespace Heartbeat.Server.Middleware;

/// <summary>
/// API key authentication middleware.
/// Validates X-API-Key header against configured API keys.
/// Returns RFC 7807 Problem Details for auth errors.
/// </summary>
public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeySettings _settings;
    private const string ApiKeyHeaderName = "X-API-Key";
    private const string ProblemJsonContentType = "application/problem+json";

    // Endpoints that don't require authentication
    private static readonly HashSet<string> PublicEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/swagger",
        "/swagger/index.html",
        "/swagger/v1/swagger.json"
    };

    public ApiKeyMiddleware(RequestDelegate next, ApiKeySettings settings)
    {
        _next = next;
        _settings = settings;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        
        // Allow public endpoints without authentication
        if (IsPublicEndpoint(path))
        {
            await _next(context);
            return;
        }
        
        // Skip authentication if disabled (allows runtime control for testing)
        if (!_settings.Enabled)
        {
            await _next(context);
            return;
        }

        var correlationId = context.TraceIdentifier;

        // Check for API key header
        if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
        {
            Log.Warning("API request without API key from {RemoteIp}. CorrelationId: {CorrelationId}", 
                context.Connection.RemoteIpAddress, correlationId);
            
            var problem = Problems.Unauthorized(
                detail: "API key is required. Include X-API-Key header.",
                instance: path,
                correlationId: correlationId
            );
            await WriteProblemDetailsAsync(context, problem);
            return;
        }

        // Validate API key
        if (!_settings.IsValidApiKey(extractedApiKey!))
        {
            Log.Warning("Invalid API key attempt from {RemoteIp}. CorrelationId: {CorrelationId}", 
                context.Connection.RemoteIpAddress, correlationId);
            
            var problem = Problems.Unauthorized(
                detail: "The provided API key is invalid.",
                instance: path,
                correlationId: correlationId
            );
            await WriteProblemDetailsAsync(context, problem);
            return;
        }

        await _next(context);
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status;
        var json = System.Text.Json.JsonSerializer.Serialize(problem, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        context.Response.ContentType = ProblemJsonContentType;
        await context.Response.WriteAsync(json);
    }

    private static bool IsPublicEndpoint(string path)
    {
        // Normalize path (remove trailing slash) for exact matching
        var normalizedPath = path.TrimEnd('/');
        
        // Exact matches
        if (PublicEndpoints.Contains(normalizedPath))
            return true;
        
        // Prefix matches for swagger assets
        if (path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase))
            return true;
        
        // Prefix matches for health endpoints (includes /health/live, /health/ready)
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase))
            return true;
            
        return false;
    }
}

