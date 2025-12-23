using System.Net;
using Heartbeat.Server.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Heartbeat.Server.Tests.Resilience ;

  /// <summary>
  ///   Tests for HTTP resilience policies.
  ///   Verifies retry, circuit breaker, and timeout behavior.
  ///   Note: These policies are registered but not yet used in production.
  ///   Tests ensure correct behavior when future external API calls are added.
  /// </summary>
  [TestFixture]
  public class HttpResiliencePolicyTests
  {
    [Test]
    public async Task CircuitBreakerPolicy_ConsecutiveFailures_OpensCircuit()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateCircuitBreakerPolicy();

      // Act - cause 5 consecutive failures to open circuit
      // Circuit breaker silently records failures, then opens after the 5th
      for (int i = 0; i < 5; i++)
        await policy.ExecuteAsync(() =>
          Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

      // Assert - the 6th call should throw because circuit is now open
      Assert.ThrowsAsync<BrokenCircuitException<HttpResponseMessage>>(async () =>
      {
        await policy.ExecuteAsync(() =>
          Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
      });
    }

    [Test]
    public async Task CircuitBreakerPolicy_SuccessfulRequest_CircuitRemainsClosed()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateCircuitBreakerPolicy();

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(() =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));

      // Assert
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task CombinedPolicy_SuccessfulRequest_Succeeds()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateCombinedPolicy();

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(async ct =>
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
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateCombinedPolicy();
      int callCount = 0;

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(ct =>
      {
        callCount++;
        if (callCount < 2)
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
      }, CancellationToken.None);

      // Assert
      Assert.That(callCount, Is.EqualTo(2));
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public void CreateCircuitBreakerPolicy_ReturnsNonNullPolicy()
    {
      // Act
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateCircuitBreakerPolicy();

      // Assert
      Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public void CreateCombinedPolicy_ReturnsNonNullPolicy()
    {
      // Act
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateCombinedPolicy();

      // Assert
      Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public void CreateRetryPolicy_ReturnsNonNullPolicy()
    {
      // Act
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateRetryPolicy();

      // Assert
      Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public void CreateTimeoutPolicy_ReturnsNonNullPolicy()
    {
      // Act
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateTimeoutPolicy();

      // Assert
      Assert.That(policy, Is.Not.Null);
    }

    [Test]
    public async Task RetryPolicy_ClientError_DoesNotRetry()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateRetryPolicy();
      int callCount = 0;

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(() =>
      {
        callCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
      });

      // Assert - client errors (4xx) should not be retried
      Assert.That(callCount, Is.EqualTo(1));
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task RetryPolicy_MaxRetries_StopsAfterThreeRetries()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateRetryPolicy();
      int callCount = 0;

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(() =>
      {
        callCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
      });

      // Assert - 1 initial + 3 retries = 4 total
      Assert.That(callCount, Is.EqualTo(4));
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task RetryPolicy_NotFound_DoesNotRetry()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateRetryPolicy();
      int callCount = 0;

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(() =>
      {
        callCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
      });

      // Assert
      Assert.That(callCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RetryPolicy_SuccessfulRequest_ReturnsImmediately()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateRetryPolicy();
      int callCount = 0;

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(() =>
      {
        callCount++;
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
      });

      // Assert
      Assert.That(callCount, Is.EqualTo(1));
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task RetryPolicy_TooManyRequests_Retries()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateRetryPolicy();
      int callCount = 0;

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(() =>
      {
        callCount++;
        if (callCount < 2)
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
      });

      // Assert
      Assert.That(callCount, Is.EqualTo(2));
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task RetryPolicy_TransientFailureThenSuccess_Retries()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateRetryPolicy();
      int callCount = 0;

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(() =>
      {
        callCount++;
        if (callCount < 2)
          return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
      });

      // Assert
      Assert.That(callCount, Is.EqualTo(2));
      Assert.That(result.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task TimeoutPolicy_FastRequest_Succeeds()
    {
      // Arrange
      IAsyncPolicy<HttpResponseMessage> policy = HttpResiliencePolicy.CreateTimeoutPolicy();

      // Act
      HttpResponseMessage? result = await policy.ExecuteAsync(async ct =>
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
      AsyncTimeoutPolicy<HttpResponseMessage>? shortTimeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMilliseconds(50));

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
  }