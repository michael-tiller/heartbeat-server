using System.Text.Json;
using System.Text.Json.Serialization;
using Heartbeat.Contracts.DTOs;
using Microsoft.Extensions.Primitives;
using Serilog;

namespace Heartbeat.Server.Middleware ;

  /// <summary>
  ///   API key authentication middleware.
  ///   Validates X-API-Key header against configured API keys.
  ///   Returns RFC 7807 Problem Details for auth errors.
  /// </summary>
  public sealed class ApiKeyMiddleware
  {
    private const string ApiKeyHeaderName = "X-API-Key";
    private const string ProblemJsonContentType = "application/problem+json";

    // Endpoints that don't require authentication
    private static readonly HashSet<string> publicEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
      "/health",
      "/swagger",
      "/swagger/index.html",
      "/swagger/v1/swagger.json"
    };

    private readonly RequestDelegate next;
    private readonly ApiKeySettings settings;

    /// <summary>
    ///   Initializes a new instance of the <see cref="ApiKeyMiddleware" /> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    /// <param name="settings">The API key settings.</param>
    public ApiKeyMiddleware(RequestDelegate next, ApiKeySettings settings)
    {
      this.next = next;
      this.settings = settings;
    }

    /// <summary>
    ///   Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
      string path = context.Request.Path.Value ?? "";

      // Allow public endpoints without authentication
      if (IsPublicEndpoint(path))
      {
        await next(context);
        return;
      }

      // Skip authentication if disabled (allows runtime control for testing)
      if (!settings.Enabled)
      {
        await next(context);
        return;
      }

      string correlationId = context.TraceIdentifier;

      // Check for API key header
      if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out StringValues extractedApiKey))
      {
        Log.Warning("API request without API key from {RemoteIp}. CorrelationId: {CorrelationId}",
          context.Connection.RemoteIpAddress, correlationId);

        ProblemDetails problem = Problems.Unauthorized(
          "API key is required. Include X-API-Key header.",
          path,
          correlationId
          );
        await WriteProblemDetailsAsync(context, problem);
        return;
      }

      // Validate API key
      if (!settings.IsValidApiKey(extractedApiKey!))
      {
        Log.Warning("Invalid API key attempt from {RemoteIp}. CorrelationId: {CorrelationId}",
          context.Connection.RemoteIpAddress, correlationId);

        ProblemDetails problem = Problems.Unauthorized(
          "The provided API key is invalid.",
          path,
          correlationId
          );
        await WriteProblemDetailsAsync(context, problem);
        return;
      }

      await next(context);
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, ProblemDetails problem)
    {
      context.Response.StatusCode = problem.Status;
      string json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
      });
      context.Response.ContentType = ProblemJsonContentType;
      await context.Response.WriteAsync(json);
    }

    private static bool IsPublicEndpoint(string path)
    {
      // Normalize path (remove trailing slash) for exact matching
      string normalizedPath = path.TrimEnd('/');

      // Exact matches
      if (publicEndpoints.Contains(normalizedPath))
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