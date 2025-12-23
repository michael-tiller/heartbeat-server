using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Heartbeat.Contracts;

// Response DTOs
public sealed record HealthResponse([Required] string Status);

public sealed record RegisterResponse(
    [Required] string UserCode,
    [Required] int CurrentStreak,
    [Required] int LongestStreak
);

// Request DTOs
public sealed record RegisterRequest([Required] string DeviceId);

// Error DTOs (legacy, kept for backward compatibility)
public sealed record ApiError([Required] string Error);

/// <summary>
/// RFC 7807 Problem Details for HTTP APIs.
/// See: https://datatracker.ietf.org/doc/html/rfc7807
/// </summary>
public sealed record ProblemDetails
{
    /// <summary>
    /// A URI reference that identifies the problem type.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = "about:blank";

    /// <summary>
    /// A short, human-readable summary of the problem type.
    /// </summary>
    [JsonPropertyName("title")]
    [Required]
    public required string Title { get; init; }

    /// <summary>
    /// The HTTP status code.
    /// </summary>
    [JsonPropertyName("status")]
    [Required]
    public required int Status { get; init; }

    /// <summary>
    /// A human-readable explanation specific to this occurrence.
    /// </summary>
    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    /// <summary>
    /// A URI reference that identifies the specific occurrence.
    /// </summary>
    [JsonPropertyName("instance")]
    public string? Instance { get; init; }

    /// <summary>
    /// Correlation ID for request tracing.
    /// </summary>
    [JsonPropertyName("correlationId")]
    public string? CorrelationId { get; init; }
}

/// <summary>
/// Factory for creating standardized ProblemDetails responses.
/// </summary>
public static class Problems
{
    private const string BaseType = "https://httpstatuses.com";

    public static ProblemDetails BadRequest(string detail, string? instance = null, string? correlationId = null) => new()
    {
        Type = $"{BaseType}/400",
        Title = "Bad Request",
        Status = 400,
        Detail = detail,
        Instance = instance,
        CorrelationId = correlationId
    };

    public static ProblemDetails Unauthorized(string? detail = null, string? instance = null, string? correlationId = null) => new()
    {
        Type = $"{BaseType}/401",
        Title = "Unauthorized",
        Status = 401,
        Detail = detail ?? "Authentication is required to access this resource.",
        Instance = instance,
        CorrelationId = correlationId
    };

    public static ProblemDetails NotFound(string detail, string? instance = null, string? correlationId = null) => new()
    {
        Type = $"{BaseType}/404",
        Title = "Not Found",
        Status = 404,
        Detail = detail,
        Instance = instance,
        CorrelationId = correlationId
    };

    public static ProblemDetails Conflict(string detail, string? instance = null, string? correlationId = null) => new()
    {
        Type = $"{BaseType}/409",
        Title = "Conflict",
        Status = 409,
        Detail = detail,
        Instance = instance,
        CorrelationId = correlationId
    };

    public static ProblemDetails InternalServerError(string? correlationId = null, string? instance = null) => new()
    {
        Type = $"{BaseType}/500",
        Title = "Internal Server Error",
        Status = 500,
        Detail = "An unexpected error occurred. Please try again later.",
        Instance = instance,
        CorrelationId = correlationId
    };
}

