using System.Net;
using System.Net.Http.Json;
using Heartbeat.Contracts.DTOs;
using Heartbeat.Server.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Heartbeat.Server.Tests.Middleware ;

  /// <summary>
  ///   Tests for correlation ID middleware.
  ///   Verifies header passthrough, fallback generation, and response header injection.
  /// </summary>
  [TestFixture]
  [NonParallelizable]
  public class CorrelationIdMiddlewareTests
  {
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
      connection = new SqliteConnection("Filename=:memory:");
      connection.Open();

      factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
      {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");

        builder.ConfigureServices(services =>
        {
          ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
          if (descriptor != null)
            services.Remove(descriptor);

          services.AddDbContext<AppDbContext>(options => { options.UseSqlite(connection); });

          // Disable API key auth for these tests
          ServiceDescriptor? apiKeyDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ApiKeySettings));
          if (apiKeyDescriptor != null)
            services.Remove(apiKeyDescriptor);

          ApiKeySettings testSettings = new()
          {
            Enabled = false,
            Keys = []
          };
          services.AddSingleton(testSettings);
        });
      });

      client = factory.CreateClient();

      using IServiceScope scope = factory.Services.CreateScope();
      AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      dbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
      client?.Dispose();
      factory?.Dispose();
      connection?.Close();
      connection?.Dispose();
    }

    #endregion

    private const string CorrelationIdHeader = "X-Correlation-ID";

    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private SqliteConnection? connection;

    [Test]
    public async Task ClientCorrelationId_IsPreservedOnErrorResponse()
    {
      // Arrange
      const string clientCorrelationId = "error-trace-99999";
      using HttpRequestMessage request = new(HttpMethod.Get, "/nonexistent-endpoint");
      request.Headers.Add(CorrelationIdHeader, clientCorrelationId);

      // Act
      HttpResponseMessage response = await client!.SendAsync(request);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
      string? responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    [Test]
    public async Task ClientProvidedCorrelationId_IsEchoedInResponse()
    {
      // Arrange
      const string clientCorrelationId = "client-trace-12345";
      using HttpRequestMessage request = new(HttpMethod.Get, "/health");
      request.Headers.Add(CorrelationIdHeader, clientCorrelationId);

      // Act
      HttpResponseMessage response = await client!.SendAsync(request);

      // Assert
      response.EnsureSuccessStatusCode();
      string? responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    [Test]
    public async Task ClientProvidedCorrelationId_IsPreservedExactly()
    {
      // Arrange - using a GUID-like format
      const string clientCorrelationId = "550e8400-e29b-41d4-a716-446655440000";
      using HttpRequestMessage request = new(HttpMethod.Get, "/health");
      request.Headers.Add(CorrelationIdHeader, clientCorrelationId);

      // Act
      HttpResponseMessage response = await client!.SendAsync(request);

      // Assert
      string? responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    [Test]
    public async Task ClientProvidedCorrelationId_WithSpecialCharacters_IsPreserved()
    {
      // Arrange - correlation IDs may contain various characters
      const string clientCorrelationId = "trace:abc-123_xyz";
      using HttpRequestMessage request = new(HttpMethod.Get, "/health");
      request.Headers.Add(CorrelationIdHeader, clientCorrelationId);

      // Act
      HttpResponseMessage response = await client!.SendAsync(request);

      // Assert
      string? responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    [Test]
    public async Task CorrelationId_IsReturnedOnAllEndpoints_Health()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task CorrelationId_IsReturnedOnAllEndpoints_HealthLive()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/live");

      // Assert
      Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task CorrelationId_IsReturnedOnAllEndpoints_HealthReady()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");

      // Assert
      Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task CorrelationId_IsReturnedOnNotFoundResponse()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/nonexistent-endpoint");

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
      Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task CorrelationId_IsReturnedOnProtectedEndpoint()
    {
      // Arrange
      RegisterRequest request = new("test-device-correlation");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      response.EnsureSuccessStatusCode();
      Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task EmptyCorrelationIdHeader_GeneratesNewId()
    {
      // Arrange - empty header value should be treated as missing
      using HttpRequestMessage request = new(HttpMethod.Get, "/health");
      request.Headers.TryAddWithoutValidation(CorrelationIdHeader, "");

      // Act
      HttpResponseMessage response = await client!.SendAsync(request);

      // Assert
      string? correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
      Assert.That(correlationId, Is.Not.EqualTo("")); // Should be generated, not empty
    }

    [Test]
    public async Task GeneratedCorrelationIds_AreDifferentPerRequest()
    {
      // Act
      HttpResponseMessage response1 = await client!.GetAsync("/health");
      HttpResponseMessage response2 = await client!.GetAsync("/health");

      // Assert
      string? correlationId1 = response1.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      string? correlationId2 = response2.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();

      Assert.That(correlationId1, Is.Not.EqualTo(correlationId2));
    }

    [Test]
    public async Task NoClientCorrelationId_GeneratesNewId()
    {
      // Act - no correlation ID header provided
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      string? correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Response_AlwaysContainsCorrelationIdHeader()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      response.EnsureSuccessStatusCode();
      Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task Response_CorrelationIdIsNotEmpty()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      string? correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task WhitespaceCorrelationIdHeader_GeneratesNewId()
    {
      // Arrange - whitespace-only header should be treated as missing
      using HttpRequestMessage request = new(HttpMethod.Get, "/health");
      request.Headers.TryAddWithoutValidation(CorrelationIdHeader, "   ");

      // Act
      HttpResponseMessage response = await client!.SendAsync(request);

      // Assert
      string? correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
      Assert.That(correlationId?.Trim(), Is.Not.Empty); // Should be a real ID
    }
  }