using System.Diagnostics;
using Microsoft.Extensions.Primitives;
using Serilog.Context;

namespace Heartbeat.Server.Middleware ;

  /// <summary>
  ///   Middleware to ensure all requests have a correlation ID for distributed tracing.
  ///   Uses existing TraceIdentifier if present, otherwise generates a new one.
  /// </summary>
  public sealed class CorrelationIdMiddleware
  {
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate next;

    /// <summary>
    ///   Initializes a new instance of the <see cref="CorrelationIdMiddleware" /> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
      this.next = next;
    }

    /// <summary>
    ///   Invokes the middleware.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
      // Try to get correlation ID from header (for distributed tracing)
      string correlationId;
      if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out StringValues headerValue) &&
          !string.IsNullOrWhiteSpace(headerValue))
        correlationId = headerValue.ToString();
      else
      // Use ASP.NET Core's TraceIdentifier as correlation ID
        correlationId = context.TraceIdentifier;

      // Add correlation ID to response headers for client tracking
      context.Response.Headers[CorrelationIdHeader] = correlationId;

      // Add to log context for all logs in this request
      using (LogContext.PushProperty("CorrelationId", correlationId))
      {
        // Add to Activity for distributed tracing (if available)
        Activity.Current?.SetTag("correlation.id", correlationId);

        await next(context);
      }
    }
  }