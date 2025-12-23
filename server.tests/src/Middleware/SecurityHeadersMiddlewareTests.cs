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
  ///   Tests for security headers middleware.
  ///   Verifies presence and correctness of all security headers on all responses.
  /// </summary>
  [TestFixture]
  [NonParallelizable]
  public class SecurityHeadersMiddlewareTests
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

    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private SqliteConnection? connection;

    private static void AssertAllSecurityHeadersPresent(HttpResponseMessage response)
    {
      Assert.Multiple(() =>
      {
        Assert.That(response.Headers.Contains("X-Content-Type-Options"), Is.True,
          "X-Content-Type-Options header missing");
        Assert.That(response.Headers.Contains("X-Frame-Options"), Is.True,
          "X-Frame-Options header missing");
        Assert.That(response.Headers.Contains("X-XSS-Protection"), Is.True,
          "X-XSS-Protection header missing");
        Assert.That(response.Headers.Contains("Referrer-Policy"), Is.True,
          "Referrer-Policy header missing");
        Assert.That(response.Headers.Contains("Content-Security-Policy"), Is.True,
          "Content-Security-Policy header missing");
        Assert.That(response.Headers.Contains("Permissions-Policy"), Is.True,
          "Permissions-Policy header missing");
      });
    }

    [Test]
    public async Task ContentSecurityPolicy_HasCorrectValue()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      string? value = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
      Assert.That(value, Is.EqualTo("default-src 'none'; frame-ancestors 'none'"));
    }

    [Test]
    public async Task ContentSecurityPolicy_IsPresent()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      Assert.That(response.Headers.Contains("Content-Security-Policy"), Is.True);
    }

    [Test]
    public async Task PermissionsPolicy_DisablesAllFeatures()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      string? value = response.Headers.GetValues("Permissions-Policy").FirstOrDefault();
      Assert.That(value, Does.Contain("accelerometer=()"));
      Assert.That(value, Does.Contain("camera=()"));
      Assert.That(value, Does.Contain("geolocation=()"));
      Assert.That(value, Does.Contain("microphone=()"));
      Assert.That(value, Does.Contain("payment=()"));
    }

    [Test]
    public async Task PermissionsPolicy_IsPresent()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      Assert.That(response.Headers.Contains("Permissions-Policy"), Is.True);
    }

    [Test]
    public async Task ReferrerPolicy_IsPresent()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      Assert.That(response.Headers.Contains("Referrer-Policy"), Is.True);
    }

    [Test]
    public async Task ReferrerPolicy_IsStrictOriginWhenCrossOrigin()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      string? value = response.Headers.GetValues("Referrer-Policy").FirstOrDefault();
      Assert.That(value, Is.EqualTo("strict-origin-when-cross-origin"));
    }

    [Test]
    public async Task SecurityHeaders_AppliedTo400Response()
    {
      // Arrange - invalid request triggers validation error
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
      AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedTo404Response()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/nonexistent-endpoint");

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
      AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToHealthEndpoint()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToHealthLiveEndpoint()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/live");

      // Assert
      AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToHealthReadyEndpoint()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");

      // Assert
      AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToRegisterEndpoint()
    {
      // Arrange
      RegisterRequest request = new("valid-device-id-12345");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      response.EnsureSuccessStatusCode();
      AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToSwaggerEndpoint()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/swagger/v1/swagger.json");

      // Assert
      AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task XContentTypeOptions_IsNosniff()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      string? value = response.Headers.GetValues("X-Content-Type-Options").FirstOrDefault();
      Assert.That(value, Is.EqualTo("nosniff"));
    }

    [Test]
    public async Task XContentTypeOptions_IsPresent()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      Assert.That(response.Headers.Contains("X-Content-Type-Options"), Is.True);
    }

    [Test]
    public async Task XFrameOptions_IsDeny()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      string? value = response.Headers.GetValues("X-Frame-Options").FirstOrDefault();
      Assert.That(value, Is.EqualTo("DENY"));
    }

    [Test]
    public async Task XFrameOptions_IsPresent()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      Assert.That(response.Headers.Contains("X-Frame-Options"), Is.True);
    }

    [Test]
    public async Task XXssProtection_IsEnabledWithBlock()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      string? value = response.Headers.GetValues("X-XSS-Protection").FirstOrDefault();
      Assert.That(value, Is.EqualTo("1; mode=block"));
    }

    [Test]
    public async Task XXssProtection_IsPresent()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      Assert.That(response.Headers.Contains("X-XSS-Protection"), Is.True);
    }
  }