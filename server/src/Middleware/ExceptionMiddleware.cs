using System.Net;
using Serilog;
using Heartbeat.Contracts;
using Heartbeat.Server.Exceptions;

namespace Heartbeat.Server.Middleware;

/// <summary>
/// Global exception handling middleware.
/// Returns RFC 7807 Problem Details for all errors.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private const string ProblemJsonContentType = "application/problem+json";

    public ExceptionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
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
        var correlationId = context.TraceIdentifier;
        Log.Warning(ex, "Validation error: {Message}. CorrelationId: {CorrelationId}", ex.Message, correlationId);
        
        var problem = Problems.BadRequest(
            detail: ex.Message,
            instance: context.Request.Path,
            correlationId: correlationId
        );
        await WriteProblemDetailsAsync(context, problem, HttpStatusCode.BadRequest);
    }

    private static async Task HandleUnauthorizedExceptionAsync(HttpContext context, UnauthorizedAccessException ex)
    {
        var correlationId = context.TraceIdentifier;
        Log.Warning("Unauthorized access attempt: {Message}. CorrelationId: {CorrelationId}", ex.Message, correlationId);
        
        var problem = Problems.Unauthorized(
            instance: context.Request.Path,
            correlationId: correlationId
        );
        await WriteProblemDetailsAsync(context, problem, HttpStatusCode.Unauthorized);
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var correlationId = context.TraceIdentifier;
        Log.Error(ex, "Unhandled exception. CorrelationId: {CorrelationId}", correlationId);
        
        // Never expose internal error details to clients
        var problem = Problems.InternalServerError(
            correlationId: correlationId,
            instance: context.Request.Path
        );
        await WriteProblemDetailsAsync(context, problem, HttpStatusCode.InternalServerError);
    }

    private static async Task WriteProblemDetailsAsync(HttpContext context, ProblemDetails problem, HttpStatusCode statusCode)
    {
        context.Response.StatusCode = (int)statusCode;
        // Write manually to control content type (WriteAsJsonAsync overrides it)
        var json = System.Text.Json.JsonSerializer.Serialize(problem, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        context.Response.ContentType = ProblemJsonContentType;
        await context.Response.WriteAsync(json);
    }
}

