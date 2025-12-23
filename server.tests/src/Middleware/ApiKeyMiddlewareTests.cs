using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Heartbeat.Contracts;
using Heartbeat.Server;
using Heartbeat.Server.Middleware;

namespace Heartbeat.Server.Tests.Middleware;

/// <summary>
/// Tests for API key authentication middleware.
/// Verifies authorization, public endpoint bypass, and key validation behavior.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ApiKeyMiddlewareTests
{
    private const string ValidApiKey = "test-api-key-12345";
    private const string SecondValidApiKey = "test-api-key-67890";
    private const string InvalidApiKey = "wrong-key";
    
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
                // Remove any existing DbContext registration
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
                
                // Remove existing ApiKeySettings and register test settings with auth enabled
                var apiKeyDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ApiKeySettings));
                if (apiKeyDescriptor != null)
                {
                    services.Remove(apiKeyDescriptor);
                }
                
                var testSettings = new ApiKeySettings
                {
                    Enabled = true,
                    Keys = new List<string> { ValidApiKey, SecondValidApiKey }
                };
                services.AddSingleton(testSettings);
            });
        });

        _client = _factory.CreateClient();
        
        // Ensure database is created
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

    #region Public Endpoint Bypass

    [Test]
    public async Task Health_Endpoint_DoesNotRequireApiKey()
    {
        // Act - no API key header
        var response = await _client!.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.That(content?.Status, Is.EqualTo("ok"));
    }

    [Test]
    public async Task HealthLive_Endpoint_DoesNotRequireApiKey()
    {
        // Act
        var response = await _client!.GetAsync("/health/live");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task HealthReady_Endpoint_DoesNotRequireApiKey()
    {
        // Act
        var response = await _client!.GetAsync("/health/ready");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task SwaggerJson_Endpoint_DoesNotRequireApiKey()
    {
        // Act
        var response = await _client!.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized));
    }

    #endregion

    #region Missing API Key Header

    [Test]
    public async Task ProtectedEndpoint_WithoutApiKey_Returns401()
    {
        // Arrange
        var request = new RegisterRequest("test-device-123");

        // Act - no API key header
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithoutApiKey_ReturnsProblemDetails()
    {
        // Arrange
        var request = new RegisterRequest("test-device-123");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert - RFC 7807 Problem Details
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem?.Title, Is.EqualTo("Unauthorized"));
        Assert.That(problem?.Status, Is.EqualTo(401));
        Assert.That(problem?.Detail, Does.Contain("API key is required"));
    }

    #endregion

    #region Invalid API Key

    [Test]
    public async Task ProtectedEndpoint_WithInvalidApiKey_Returns401()
    {
        // Arrange
        var request = new RegisterRequest("test-device-123");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add("X-API-Key", InvalidApiKey);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithInvalidApiKey_ReturnsProblemDetails()
    {
        // Arrange
        var request = new RegisterRequest("test-device-123");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add("X-API-Key", InvalidApiKey);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert - RFC 7807 Problem Details
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem?.Title, Is.EqualTo("Unauthorized"));
        Assert.That(problem?.Status, Is.EqualTo(401));
        Assert.That(problem?.Detail, Does.Contain("invalid"));
    }

    [Test]
    public async Task ProtectedEndpoint_WithWhitespaceApiKey_Returns401()
    {
        // Arrange - using whitespace instead of empty string (more reliable)
        var request = new RegisterRequest("test-device-123");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add("X-API-Key", "   ");
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    #endregion

    #region Valid API Key

    [Test]
    public async Task ProtectedEndpoint_WithValidApiKey_Succeeds()
    {
        // Arrange
        var request = new RegisterRequest("test-device-valid-key");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add("X-API-Key", ValidApiKey);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.That(content?.UserCode, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task ProtectedEndpoint_WithSecondValidApiKey_Succeeds()
    {
        // Arrange - using second configured key
        var request = new RegisterRequest("test-device-second-key");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add("X-API-Key", SecondValidApiKey);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.That(content?.UserCode, Is.Not.Null.And.Not.Empty);
    }

    #endregion

    #region Case Sensitivity

    [Test]
    public async Task ApiKey_IsCaseSensitive_UppercaseFails()
    {
        // Arrange - using uppercase version of valid key
        var request = new RegisterRequest("test-device-case");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add("X-API-Key", ValidApiKey.ToUpperInvariant());
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert - should fail because keys are case-sensitive
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ApiKey_IsCaseSensitive_MixedCaseFails()
    {
        // Arrange - using mixed case version
        var request = new RegisterRequest("test-device-mixed-case");
        var mixedCaseKey = "Test-Api-Key-12345"; // Different from "test-api-key-12345"
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add("X-API-Key", mixedCaseKey);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    #endregion

    #region Header Name Handling

    [Test]
    public async Task WrongHeaderName_Returns401()
    {
        // Arrange - using wrong header name
        var request = new RegisterRequest("test-device-wrong-header");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add("Authorization", $"Bearer {ValidApiKey}");
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert - should fail because correct header is not present
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ApiKeyHeader_CaseInsensitiveHeaderName_Succeeds()
    {
        // Note: HTTP header names are case-insensitive per RFC 7230
        // This test verifies ASP.NET Core handles this correctly
        var request = new RegisterRequest("test-device-header-case");
        
        // Using lowercase header name (ASP.NET normalizes this)
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", ValidApiKey);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert - should succeed because HTTP headers are case-insensitive
        response.EnsureSuccessStatusCode();
    }

    #endregion

    #region Auth Disabled

    [Test]
    public async Task AuthDisabled_AllowsProtectedEndpointWithoutKey()
    {
        // Arrange - create a new factory with auth disabled
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
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
                    options.UseSqlite(connection);
                });
                
                var apiKeyDescriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(ApiKeySettings));
                if (apiKeyDescriptor != null)
                {
                    services.Remove(apiKeyDescriptor);
                }
                
                var testSettings = new ApiKeySettings
                {
                    Enabled = false, // Auth disabled
                    Keys = new List<string> { ValidApiKey }
                };
                services.AddSingleton(testSettings);
            });
        });

        using var client = factory.CreateClient();
        
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.EnsureCreated();

        var request = new RegisterRequest("test-device-no-auth");

        // Act - no API key header, but auth is disabled
        var response = await client.PostAsJsonAsync("/api/v1/register", request);

        // Assert - should succeed because auth is disabled
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.That(content?.UserCode, Is.Not.Null.And.Not.Empty);
    }

    #endregion

    #region Public Endpoint Path Matching

    [Test]
    public async Task HealthWithTrailingSlash_DoesNotRequireApiKey()
    {
        // Act
        var response = await _client!.GetAsync("/health/");

        // Assert - trailing slash should be normalized and bypass auth, endpoint should work
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task SwaggerSubPath_DoesNotRequireApiKey()
    {
        // Act - any swagger sub-path should be public
        var response = await _client!.GetAsync("/swagger/something");

        // Assert
        Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized));
    }

    #endregion
}

