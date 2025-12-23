using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heartbeat.Contracts.DTOs;
using Heartbeat.Server.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Heartbeat.Server.Tests ;

  /// <summary>
  ///   Tests for health endpoints.
  ///   Covers liveness, readiness, and public access enforcement.
  /// </summary>
  [TestFixture]
  [NonParallelizable]
  public class HealthEndpointTests
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

          // Enable API key auth to test public access
          ServiceDescriptor? apiKeyDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ApiKeySettings));
          if (apiKeyDescriptor != null)
            services.Remove(apiKeyDescriptor);

          ApiKeySettings testSettings = new()
          {
            Enabled = true,
            Keys = ["test-api-key-12345"]
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

    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private SqliteConnection? connection;

    [Test]
    public async Task Health_IsPublicEndpoint_NoApiKeyRequired()
    {
      // Act - no API key header (auth is enabled in this fixture)
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert - should still succeed
      response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Health_Returns200Ok()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Health_ReturnsHealthResponse()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      HealthResponse? result = await response.Content.ReadFromJsonAsync<HealthResponse>();
      Assert.That(result, Is.Not.Null);
      Assert.That(result!.Status, Is.EqualTo("ok"));
    }

    [Test]
    public async Task HealthLive_ContainsStatusField()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/live");
      string content = await response.Content.ReadAsStringAsync();

      // Assert
      Assert.That(content, Does.Contain("\"status\""));
    }

    [Test]
    public async Task HealthLive_ContainsTimestamp()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/live");
      string content = await response.Content.ReadAsStringAsync();

      // Assert
      Assert.That(content, Does.Contain("\"timestamp\""));
    }

    [Test]
    public async Task HealthLive_IsPublicEndpoint_NoApiKeyRequired()
    {
      // Act - no API key header
      HttpResponseMessage response = await client!.GetAsync("/health/live");

      // Assert - should still succeed
      response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task HealthLive_Returns200Ok()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/live");

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task HealthLive_ReturnsJsonContentType()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/live");

      // Assert
      Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public async Task HealthReady_ChecksHaveDuration()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");
      string content = await response.Content.ReadAsStringAsync();

      // Assert
      Assert.That(content, Does.Contain("\"duration\""));
    }

    [Test]
    public async Task HealthReady_ChecksHaveName()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");
      string content = await response.Content.ReadAsStringAsync();

      // Assert
      Assert.That(content, Does.Contain("\"name\""));
    }

    [Test]
    public async Task HealthReady_ContainsChecksArray()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");
      string content = await response.Content.ReadAsStringAsync();

      // Assert
      Assert.That(content, Does.Contain("\"checks\""));
    }

    [Test]
    public async Task HealthReady_ContainsStatusField()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");
      string content = await response.Content.ReadAsStringAsync();

      // Assert
      Assert.That(content, Does.Contain("\"status\""));
    }

    [Test]
    public async Task HealthReady_ContainsTimestamp()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");
      string content = await response.Content.ReadAsStringAsync();

      // Assert
      Assert.That(content, Does.Contain("\"timestamp\""));
    }

    [Test]
    public async Task HealthReady_DatabaseCheckIsHealthy()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");
      string content = await response.Content.ReadAsStringAsync();

      // Assert - status should indicate healthy
      JsonDocument doc = JsonDocument.Parse(content);
      string? status = doc.RootElement.GetProperty("status").GetString();
      Assert.That(status, Is.EqualTo("Healthy"));
    }

    [Test]
    public async Task HealthReady_IncludesDatabaseCheck()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");
      string content = await response.Content.ReadAsStringAsync();

      // Assert - should include database health check
      Assert.That(content, Does.Contain("\"database\""));
    }

    [Test]
    public async Task HealthReady_IsPublicEndpoint_NoApiKeyRequired()
    {
      // Act - no API key header
      HttpResponseMessage response = await client!.GetAsync("/health/ready");

      // Assert - should still succeed
      response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task HealthReady_Returns200Ok()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task HealthReady_ReturnsJsonContentType()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");

      // Assert
      Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }
  }