using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heartbeat.Contracts;
using Heartbeat.Server;
using Heartbeat.Server.Middleware;

namespace Heartbeat.Server.Tests;

/// <summary>
/// Tests for health endpoints.
/// Covers liveness, readiness, and public access enforcement.
/// </summary>
[TestFixture]
[NonParallelizable]
public class HealthEndpointTests
{
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
                
                // Enable API key auth to test public access
                var apiKeyDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ApiKeySettings));
                if (apiKeyDescriptor != null)
                {
                    services.Remove(apiKeyDescriptor);
                }
                
                var testSettings = new ApiKeySettings
                {
                    Enabled = true,
                    Keys = new List<string> { "test-api-key-12345" }
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

    #region /health (Basic)

    [Test]
    public async Task Health_Returns200Ok()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Health_ReturnsHealthResponse()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Status, Is.EqualTo("ok"));
    }

    [Test]
    public async Task Health_IsPublicEndpoint_NoApiKeyRequired()
    {
        // Act - no API key header (auth is enabled in this fixture)
        var response = await _client!.GetAsync("/health");

        // Assert - should still succeed
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region /health/live (Liveness)

    [Test]
    public async Task HealthLive_Returns200Ok()
    {
        // Act
        var response = await _client!.GetAsync("/health/live");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task HealthLive_ReturnsJsonContentType()
    {
        // Act
        var response = await _client!.GetAsync("/health/live");

        // Assert
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public async Task HealthLive_ContainsStatusField()
    {
        // Act
        var response = await _client!.GetAsync("/health/live");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(content, Does.Contain("\"status\""));
    }

    [Test]
    public async Task HealthLive_ContainsTimestamp()
    {
        // Act
        var response = await _client!.GetAsync("/health/live");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(content, Does.Contain("\"timestamp\""));
    }

    [Test]
    public async Task HealthLive_IsPublicEndpoint_NoApiKeyRequired()
    {
        // Act - no API key header
        var response = await _client!.GetAsync("/health/live");

        // Assert - should still succeed
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region /health/ready (Readiness)

    [Test]
    public async Task HealthReady_Returns200Ok()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task HealthReady_ReturnsJsonContentType()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");

        // Assert
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public async Task HealthReady_ContainsStatusField()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(content, Does.Contain("\"status\""));
    }

    [Test]
    public async Task HealthReady_ContainsChecksArray()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(content, Does.Contain("\"checks\""));
    }

    [Test]
    public async Task HealthReady_ContainsTimestamp()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(content, Does.Contain("\"timestamp\""));
    }

    [Test]
    public async Task HealthReady_IncludesDatabaseCheck()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - should include database health check
        Assert.That(content, Does.Contain("\"database\""));
    }

    [Test]
    public async Task HealthReady_DatabaseCheckIsHealthy()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - status should indicate healthy
        var doc = JsonDocument.Parse(content);
        var status = doc.RootElement.GetProperty("status").GetString();
        Assert.That(status, Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task HealthReady_IsPublicEndpoint_NoApiKeyRequired()
    {
        // Act - no API key header
        var response = await _client!.GetAsync("/health/ready");

        // Assert - should still succeed
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Response Structure

    [Test]
    public async Task HealthReady_ChecksHaveDuration()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(content, Does.Contain("\"duration\""));
    }

    [Test]
    public async Task HealthReady_ChecksHaveName()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(content, Does.Contain("\"name\""));
    }

    #endregion
}

