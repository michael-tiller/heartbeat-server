using Polly;
using Polly.Extensions.Http;
using Serilog;

namespace Heartbeat.Server.Resilience;

/// <summary>
/// Configures HTTP resilience policies using Polly.
/// Provides retry, circuit breaker, and timeout policies for external HTTP calls.
/// </summary>
public static class HttpResiliencePolicy
{
    /// <summary>
    /// Creates a retry policy for transient HTTP failures.
    /// Retries on 5xx errors and network exceptions.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff: 2s, 4s, 8s
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var errorMessage = outcome.Exception?.Message 
                        ?? outcome.Result?.StatusCode.ToString() 
                        ?? "Unknown error";
                    
                    Log.Warning(
                        "HTTP request failed. Retry {RetryCount} after {Delay}ms. Error: {Error}",
                        retryCount,
                        timespan.TotalMilliseconds,
                        errorMessage
                    );
                }
            );
    }

    /// <summary>
    /// Creates a circuit breaker policy to prevent cascading failures.
    /// Opens circuit after 5 consecutive failures, closes after 30 seconds.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (result, duration) =>
                {
                    var errorMessage = result.Exception?.Message 
                        ?? result.Result?.StatusCode.ToString() 
                        ?? "Unknown error";
                    
                    Log.Error(
                        "Circuit breaker opened for {Duration}s. Error: {Error}",
                        duration.TotalSeconds,
                        errorMessage
                    );
                },
                onReset: () =>
                {
                    Log.Information("Circuit breaker reset - service is healthy again");
                },
                onHalfOpen: () =>
                {
                    Log.Information("Circuit breaker half-open - testing service health");
                }
            );
    }

    /// <summary>
    /// Creates a timeout policy for HTTP requests.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(30));
    }

    /// <summary>
    /// Creates a combined policy with retry, circuit breaker, and timeout.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateCombinedPolicy()
    {
        return Policy.WrapAsync(
            CreateTimeoutPolicy(),
            CreateCircuitBreakerPolicy(),
            CreateRetryPolicy()
        );
    }
}

