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
/// Tests for security headers middleware.
/// Verifies presence and correctness of all security headers on all responses.
/// </summary>
[TestFixture]
[NonParallelizable]
public class SecurityHeadersMiddlewareTests
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

    #region X-Content-Type-Options

    [Test]
    public async Task XContentTypeOptions_IsPresent()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.That(response.Headers.Contains("X-Content-Type-Options"), Is.True);
    }

    [Test]
    public async Task XContentTypeOptions_IsNosniff()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        var value = response.Headers.GetValues("X-Content-Type-Options").FirstOrDefault();
        Assert.That(value, Is.EqualTo("nosniff"));
    }

    #endregion

    #region X-Frame-Options

    [Test]
    public async Task XFrameOptions_IsPresent()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.That(response.Headers.Contains("X-Frame-Options"), Is.True);
    }

    [Test]
    public async Task XFrameOptions_IsDeny()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        var value = response.Headers.GetValues("X-Frame-Options").FirstOrDefault();
        Assert.That(value, Is.EqualTo("DENY"));
    }

    #endregion

    #region X-XSS-Protection

    [Test]
    public async Task XXssProtection_IsPresent()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.That(response.Headers.Contains("X-XSS-Protection"), Is.True);
    }

    [Test]
    public async Task XXssProtection_IsEnabledWithBlock()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        var value = response.Headers.GetValues("X-XSS-Protection").FirstOrDefault();
        Assert.That(value, Is.EqualTo("1; mode=block"));
    }

    #endregion

    #region Referrer-Policy

    [Test]
    public async Task ReferrerPolicy_IsPresent()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.That(response.Headers.Contains("Referrer-Policy"), Is.True);
    }

    [Test]
    public async Task ReferrerPolicy_IsStrictOriginWhenCrossOrigin()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        var value = response.Headers.GetValues("Referrer-Policy").FirstOrDefault();
        Assert.That(value, Is.EqualTo("strict-origin-when-cross-origin"));
    }

    #endregion

    #region Content-Security-Policy

    [Test]
    public async Task ContentSecurityPolicy_IsPresent()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.That(response.Headers.Contains("Content-Security-Policy"), Is.True);
    }

    [Test]
    public async Task ContentSecurityPolicy_HasCorrectValue()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        var value = response.Headers.GetValues("Content-Security-Policy").FirstOrDefault();
        Assert.That(value, Is.EqualTo("default-src 'none'; frame-ancestors 'none'"));
    }

    #endregion

    #region Permissions-Policy

    [Test]
    public async Task PermissionsPolicy_IsPresent()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        Assert.That(response.Headers.Contains("Permissions-Policy"), Is.True);
    }

    [Test]
    public async Task PermissionsPolicy_DisablesAllFeatures()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        var value = response.Headers.GetValues("Permissions-Policy").FirstOrDefault();
        Assert.That(value, Does.Contain("accelerometer=()"));
        Assert.That(value, Does.Contain("camera=()"));
        Assert.That(value, Does.Contain("geolocation=()"));
        Assert.That(value, Does.Contain("microphone=()"));
        Assert.That(value, Does.Contain("payment=()"));
    }

    #endregion

    #region Headers Applied To All Responses

    [Test]
    public async Task SecurityHeaders_AppliedToHealthEndpoint()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToHealthLiveEndpoint()
    {
        // Act
        var response = await _client!.GetAsync("/health/live");

        // Assert
        AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToHealthReadyEndpoint()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");

        // Assert
        AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToRegisterEndpoint()
    {
        // Arrange
        var request = new RegisterRequest("valid-device-id-12345");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        response.EnsureSuccessStatusCode();
        AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedToSwaggerEndpoint()
    {
        // Act
        var response = await _client!.GetAsync("/swagger/v1/swagger.json");

        // Assert
        AssertAllSecurityHeadersPresent(response);
    }

    #endregion

    #region Headers Applied To Error Responses

    [Test]
    public async Task SecurityHeaders_AppliedTo404Response()
    {
        // Act
        var response = await _client!.GetAsync("/nonexistent-endpoint");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        AssertAllSecurityHeadersPresent(response);
    }

    [Test]
    public async Task SecurityHeaders_AppliedTo400Response()
    {
        // Arrange - invalid request triggers validation error
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        AssertAllSecurityHeadersPresent(response);
    }

    #endregion

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
}

