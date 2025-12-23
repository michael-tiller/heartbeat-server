using System.Text.RegularExpressions;
using Heartbeat.Server.Exceptions;

namespace Heartbeat.Server.Validation;

/// <summary>
/// Centralized request validation utilities.
/// </summary>
public static partial class RequestValidator
{
    // Device ID constraints
    private const int DeviceIdMinLength = 8;
    private const int DeviceIdMaxLength = 256;
    
    // Pair code format: 6 alphanumeric characters (uppercase, excluding confusing chars)
    [GeneratedRegex(@"^[A-HJ-NP-Z2-9]{6}$", RegexOptions.Compiled)]
    private static partial Regex PairCodeRegex();

    /// <summary>
    /// Validates a device ID.
    /// </summary>
    public static void ValidateDeviceId(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ValidationException("DeviceId is required");
        }

        if (deviceId.Length < DeviceIdMinLength)
        {
            throw new ValidationException($"DeviceId must be at least {DeviceIdMinLength} characters");
        }

        if (deviceId.Length > DeviceIdMaxLength)
        {
            throw new ValidationException($"DeviceId must not exceed {DeviceIdMaxLength} characters");
        }

        // Basic sanity check - no control characters
        if (deviceId.Any(char.IsControl))
        {
            throw new ValidationException("DeviceId contains invalid characters");
        }
    }

    /// <summary>
    /// Validates a pair code format.
    /// </summary>
    public static void ValidatePairCode(string? pairCode)
    {
        if (string.IsNullOrWhiteSpace(pairCode))
        {
            throw new ValidationException("PairCode is required");
        }

        if (!PairCodeRegex().IsMatch(pairCode))
        {
            throw new ValidationException("Invalid pair code format");
        }
    }

    /// <summary>
    /// Sanitizes a string by trimming whitespace.
    /// </summary>
    public static string Sanitize(string? input)
    {
        return input?.Trim() ?? string.Empty;
    }
}

