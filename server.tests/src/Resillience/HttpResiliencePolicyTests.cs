using System.Net;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Heartbeat.Server.Resilience;

namespace Heartbeat.Server.Tests.Resilience;

/// <summary>
/// Tests for HTTP resilience policies.
/// Verifies retry, circuit breaker, and timeout behavior.
/// Note: These policies are registered but not yet used in production.
/// Tests ensure correct behavior when future external API calls are added.
/// </summary>
[TestFixture]
public class HttpResiliencePolicyTests
{
    #region Retry Policy

    [Test]
    public void CreateRetryPolicy_ReturnsNonNullPolicy()
    {
        // Act
        var policy = HttpResiliencePolicy.CreateRetryPolicy();

        // Assert
        Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public async Task RetryPolicy_SuccessfulRequest_ReturnsImmediately()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateRetryPolicy();
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        // Assert
        Assert.That(callCount, Is.EqualTo(1));
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task RetryPolicy_TransientFailureThenSuccess_Retries()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateRetryPolicy();
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            callCount++;
            if (callCount < 2)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        // Assert
        Assert.That(callCount, Is.EqualTo(2));
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task RetryPolicy_TooManyRequests_Retries()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateRetryPolicy();
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            callCount++;
            if (callCount < 2)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        // Assert
        Assert.That(callCount, Is.EqualTo(2));
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task RetryPolicy_MaxRetries_StopsAfterThreeRetries()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateRetryPolicy();
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        });

        // Assert - 1 initial + 3 retries = 4 total
        Assert.That(callCount, Is.EqualTo(4));
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task RetryPolicy_ClientError_DoesNotRetry()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateRetryPolicy();
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
        });

        // Assert - client errors (4xx) should not be retried
        Assert.That(callCount, Is.EqualTo(1));
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task RetryPolicy_NotFound_DoesNotRetry()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateRetryPolicy();
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(() =>
        {
            callCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        });

        // Assert
        Assert.That(callCount, Is.EqualTo(1));
    }

    #endregion

    #region Circuit Breaker Policy

    [Test]
    public void CreateCircuitBreakerPolicy_ReturnsNonNullPolicy()
    {
        // Act
        var policy = HttpResiliencePolicy.CreateCircuitBreakerPolicy();

        // Assert
        Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public async Task CircuitBreakerPolicy_SuccessfulRequest_CircuitRemainsClosed()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateCircuitBreakerPolicy();

        // Act
        var result = await policy.ExecuteAsync(() =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

        // Assert
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CircuitBreakerPolicy_ConsecutiveFailures_OpensCircuit()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateCircuitBreakerPolicy();

        // Act - cause 5 consecutive failures to open circuit
        // Circuit breaker silently records failures, then opens after the 5th
        for (int i = 0; i < 5; i++)
        {
            await policy.ExecuteAsync(() =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        }

        // Assert - the 6th call should throw because circuit is now open
        Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(async () =>
        {
            await policy.ExecuteAsync(() =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        });
    }

    #endregion

    #region Timeout Policy

    [Test]
    public void CreateTimeoutPolicy_ReturnsNonNullPolicy()
    {
        // Act
        var policy = HttpResiliencePolicy.CreateTimeoutPolicy();

        // Assert
        Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public async Task TimeoutPolicy_FastRequest_Succeeds()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateTimeoutPolicy();

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public void TimeoutPolicy_SlowRequest_ThrowsTimeoutException()
    {
        // Arrange - use a short timeout for testing
        var shortTimeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMilliseconds(50));

        // Act & Assert
        Assert.ThrowsAsync<TimeoutRejectedException>(async () =>
        {
            await shortTimeoutPolicy.ExecuteAsync(async ct =>
            {
                await Task.Delay(5000, ct); // Much longer than 50ms timeout
                return new HttpResponseMessage(HttpStatusCode.OK);
            }, CancellationToken.None);
        });
    }

    #endregion

    #region Combined Policy

    [Test]
    public void CreateCombinedPolicy_ReturnsNonNullPolicy()
    {
        // Act
        var policy = HttpResiliencePolicy.CreateCombinedPolicy();

        // Assert
        Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public async Task CombinedPolicy_SuccessfulRequest_Succeeds()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateCombinedPolicy();

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            await Task.Delay(10, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CombinedPolicy_TransientFailure_Retries()
    {
        // Arrange
        var policy = HttpResiliencePolicy.CreateCombinedPolicy();
        var callCount = 0;

        // Act
        var result = await policy.ExecuteAsync(async ct =>
        {
            callCount++;
            if (callCount < 2)
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }, CancellationToken.None);

        // Assert
        Assert.That(callCount, Is.EqualTo(2));
        Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    #endregion

}

