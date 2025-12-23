using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heartbeat.Contracts;
using Heartbeat.Server;
using Heartbeat.Server.Middleware;

namespace Heartbeat.Server.Tests.Middleware;

/// <summary>
/// Tests for correlation ID middleware.
/// Verifies header passthrough, fallback generation, and response header injection.
/// </summary>
[TestFixture]
[NonParallelizable]
public class CorrelationIdMiddlewareTests
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private SqliteConnection? _connection;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
            
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });
                
                // Disable API key auth for these tests
                var apiKeyDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ApiKeySettings));
                if (apiKeyDescriptor != null)
                {
                    services.Remove(apiKeyDescriptor);
                }
                
                var testSettings = new ApiKeySettings
                {
                    Enabled = false,
                    Keys = new List<string>()
                };
                services.AddSingleton(testSettings);
            });
        });

        _client = _factory.CreateClient();
        
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }

    #region Response Header Injection

    [Test]
    public async Task Response_AlwaysContainsCorrelationIdHeader()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task Response_CorrelationIdIsNotEmpty()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        var correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
    }

    #endregion

    #region Header Passthrough

    [Test]
    public async Task ClientProvidedCorrelationId_IsEchoedInResponse()
    {
        // Arrange
        var clientCorrelationId = "client-trace-12345";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdHeader, clientCorrelationId);

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        response.EnsureSuccessStatusCode();
        var responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    [Test]
    public async Task ClientProvidedCorrelationId_IsPreservedExactly()
    {
        // Arrange - using a GUID-like format
        var clientCorrelationId = "550e8400-e29b-41d4-a716-446655440000";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdHeader, clientCorrelationId);

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        var responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    [Test]
    public async Task ClientProvidedCorrelationId_WithSpecialCharacters_IsPreserved()
    {
        // Arrange - correlation IDs may contain various characters
        var clientCorrelationId = "trace:abc-123_xyz";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add(CorrelationIdHeader, clientCorrelationId);

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        var responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    #endregion

    #region Generation Fallback

    [Test]
    public async Task NoClientCorrelationId_GeneratesNewId()
    {
        // Act - no correlation ID header provided
        var response = await _client!.GetAsync("/health");

        // Assert
        var correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task GeneratedCorrelationIds_AreDifferentPerRequest()
    {
        // Act
        var response1 = await _client!.GetAsync("/health");
        var response2 = await _client!.GetAsync("/health");

        // Assert
        var correlationId1 = response1.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        var correlationId2 = response2.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        
        Assert.That(correlationId1, Is.Not.EqualTo(correlationId2));
    }

    [Test]
    public async Task EmptyCorrelationIdHeader_GeneratesNewId()
    {
        // Arrange - empty header value should be treated as missing
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, "");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        var correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
        Assert.That(correlationId, Is.Not.EqualTo("")); // Should be generated, not empty
    }

    [Test]
    public async Task WhitespaceCorrelationIdHeader_GeneratesNewId()
    {
        // Arrange - whitespace-only header should be treated as missing
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.TryAddWithoutValidation(CorrelationIdHeader, "   ");

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        var correlationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(correlationId, Is.Not.Null.And.Not.Empty);
        Assert.That(correlationId?.Trim(), Is.Not.Empty); // Should be a real ID
    }

    #endregion

    #region Cross-Endpoint Consistency

    [Test]
    public async Task CorrelationId_IsReturnedOnAllEndpoints_Health()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task CorrelationId_IsReturnedOnAllEndpoints_HealthLive()
    {
        // Act
        var response = await _client!.GetAsync("/health/live");

        // Assert
        Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task CorrelationId_IsReturnedOnAllEndpoints_HealthReady()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");

        // Assert
        Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task CorrelationId_IsReturnedOnProtectedEndpoint()
    {
        // Arrange
        var request = new RegisterRequest("test-device-correlation");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    #endregion

    #region Error Response Correlation

    [Test]
    public async Task CorrelationId_IsReturnedOnNotFoundResponse()
    {
        // Act
        var response = await _client!.GetAsync("/nonexistent-endpoint");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task ClientCorrelationId_IsPreservedOnErrorResponse()
    {
        // Arrange
        var clientCorrelationId = "error-trace-99999";
        using var request = new HttpRequestMessage(HttpMethod.Get, "/nonexistent-endpoint");
        request.Headers.Add(CorrelationIdHeader, clientCorrelationId);

        // Act
        var response = await _client!.SendAsync(request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    #endregion
}

