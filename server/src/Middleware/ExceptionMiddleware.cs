using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Heartbeat.Contracts.DTOs;
using Heartbeat.Server.Exceptions;
using Serilog;

namespace Heartbeat.Server.Middleware ;

  /// <summary>
  ///   Global exception handling middleware.
  ///   Returns RFC 7807 Problem Details for all errors.
  /// </summary>
  public sealed class ExceptionMiddleware
  {
    private const string ProblemJsonContentType = "application/problem+json";
    private readonly RequestDelegate next;

    /// <summary>
    ///   Initializes a new instance of the <see cref="ExceptionMiddleware" /> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    public ExceptionMiddleware(RequestDelegate next)
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
      try
      {
        await next(context);
      }
      catch (ValidationException ex)
      {
        await HandleValidationExceptionAsync(context, ex);
      }
      catch (UnauthorizedAccessException ex)
      {
        await HandleUnauthorizedExceptionAsync(context, ex);
      }
      catch (Exception ex)
      {
        await HandleExceptionAsync(context, ex);
      }
    }

    private static async Task HandleValidationExceptionAsync(HttpContext context, ValidationException ex)
    {
      string correlationId = context.TraceIdentifier;
      Log.Warning(ex, "Validation error: {Message}. CorrelationId: {CorrelationId}", ex.Message, correlationId);

      ProblemDetails problem = Problems.BadRequest(
        ex.Message,
        context.Request.Path,
        correlationId
        );
      await WriteProblemDetailsAsync(context, problem, HttpStatusCode.BadRequest);
    }

    private static async Task HandleUnauthorizedExceptionAsync(HttpContext context, UnauthorizedAccessException ex)
    {
      string correlationId = context.TraceIdentifier;
      Log.Warning("Unauthorized access attempt: {Message}. CorrelationId: {CorrelationId}", ex.Message, correlationId);

      ProblemDetails problem = Problems.Unauthorized(
        instance: context.Request.Path,
        correlationId: correlationId
        );
      await WriteProblemDetailsAsync(context, problem, HttpStatusCode.Unauthorized);
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
      string correlationId = context.TraceIdentifier;
      Log.Error(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);

      // Never expose internal error details to clients
      ProblemDetails problem = Problems.InternalServerError(
        correlationId,
        context.Request.Path
        );
      await WriteProblemDetailsAsync(context, problem, HttpStatusCode.InternalServerError);
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, ProblemDetails problem, HttpStatusCode statusCode)
    {
      context.Response.StatusCode = (int)statusCode;
      // Write manually to control content type (WriteAsJsonAsync overrides it)
      string json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
      {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
      });
      context.Response.ContentType = ProblemJsonContentType;
      await context.Response.WriteAsync(json);
    }
  }