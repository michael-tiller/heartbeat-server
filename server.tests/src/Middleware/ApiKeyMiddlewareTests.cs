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
  ///   Tests for API key authentication middleware.
  ///   Verifies authorization, public endpoint bypass, and key validation behavior.
  /// </summary>
  [TestFixture]
  [NonParallelizable]
  public class ApiKeyMiddlewareTests
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
          // Remove any existing DbContext registration
          ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
          if (descriptor != null)
            services.Remove(descriptor);

          services.AddDbContext<AppDbContext>(options => { options.UseSqlite(connection); });

          // Remove existing ApiKeySettings and register test settings with auth enabled
          ServiceDescriptor? apiKeyDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ApiKeySettings));
          if (apiKeyDescriptor != null)
            services.Remove(apiKeyDescriptor);

          ApiKeySettings testSettings = new()
          {
            Enabled = true,
            Keys = [ValidApiKey,SecondValidApiKey]
          };
          services.AddSingleton(testSettings);
        });
      });

      client = factory.CreateClient();

      // Ensure database is created
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

    private const string ValidApiKey = "test-api-key-12345";
    private const string SecondValidApiKey = "test-api-key-67890";
    private const string InvalidApiKey = "wrong-key";

    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private SqliteConnection? connection;

    [Test]
    public async Task ApiKey_IsCaseSensitive_MixedCaseFails()
    {
      // Arrange - using mixed case version
      RegisterRequest request = new("test-device-mixed-case");
      string mixedCaseKey = "Test-Api-Key-12345"; // Different from "test-api-key-12345"
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add("X-API-Key", mixedCaseKey);
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ApiKey_IsCaseSensitive_UppercaseFails()
    {
      // Arrange - using uppercase version of valid key
      RegisterRequest request = new("test-device-case");
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add("X-API-Key", ValidApiKey.ToUpperInvariant());
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert - should fail because keys are case-sensitive
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ApiKeyHeader_CaseInsensitiveHeaderName_Succeeds()
    {
      // Note: HTTP header names are case-insensitive per RFC 7230
      // This test verifies ASP.NET Core handles this correctly
      RegisterRequest request = new("test-device-header-case");

      // Using lowercase header name (ASP.NET normalizes this)
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.TryAddWithoutValidation("x-api-key", ValidApiKey);
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert - should succeed because HTTP headers are case-insensitive
      response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task AuthDisabled_AllowsProtectedEndpointWithoutKey()
    {
      // Arrange - create a new factory with auth disabled
      await using SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
      {
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");

        builder.ConfigureServices(services =>
        {
          ServiceDescriptor? descriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
          if (descriptor != null)
            services.Remove(descriptor);

          services.AddDbContext<AppDbContext>(options => { options.UseSqlite(connection); });

          ServiceDescriptor? apiKeyDescriptor = services.FirstOrDefault(
            d => d.ServiceType == typeof(ApiKeySettings));
          if (apiKeyDescriptor != null)
            services.Remove(apiKeyDescriptor);

          ApiKeySettings testSettings = new()
          {
            Enabled = false, // Auth disabled
            Keys = [ValidApiKey]
          };
          services.AddSingleton(testSettings);
        });
      });

      using HttpClient client = factory.CreateClient();

      using IServiceScope scope = factory.Services.CreateScope();
      AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      await dbContext.Database.EnsureCreatedAsync();

      RegisterRequest request = new("test-device-no-auth");

      // Act - no API key header, but auth is disabled
      HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/register", request);

      // Assert - should succeed because auth is disabled
      response.EnsureSuccessStatusCode();
      RegisterResponse? content = await response.Content.ReadFromJsonAsync<RegisterResponse>();
      Assert.That(content?.UserCode, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Health_Endpoint_DoesNotRequireApiKey()
    {
      // Act - no API key header
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      response.EnsureSuccessStatusCode();
      HealthResponse? content = await response.Content.ReadFromJsonAsync<HealthResponse>();
      Assert.That(content?.Status, Is.EqualTo("ok"));
    }

    [Test]
    public async Task HealthLive_Endpoint_DoesNotRequireApiKey()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/live");

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task HealthReady_Endpoint_DoesNotRequireApiKey()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/ready");

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task HealthWithTrailingSlash_DoesNotRequireApiKey()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health/");

      // Assert - trailing slash should be normalized and bypass auth, endpoint should work
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task ProtectedEndpoint_WithInvalidApiKey_Returns401()
    {
      // Arrange
      RegisterRequest request = new("test-device-123");
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add("X-API-Key", InvalidApiKey);
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithInvalidApiKey_ReturnsProblemDetails()
    {
      // Arrange
      RegisterRequest request = new("test-device-123");
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add("X-API-Key", InvalidApiKey);
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert - RFC 7807 Problem Details
      ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
      Assert.That(problem?.Title, Is.EqualTo("Unauthorized"));
      Assert.That(problem?.Status, Is.EqualTo(401));
      Assert.That(problem?.Detail, Does.Contain("invalid"));
    }

    [Test]
    public async Task ProtectedEndpoint_WithoutApiKey_Returns401()
    {
      // Arrange
      RegisterRequest request = new("test-device-123");

      // Act - no API key header
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ProtectedEndpoint_WithoutApiKey_ReturnsProblemDetails()
    {
      // Arrange
      RegisterRequest request = new("test-device-123");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert - RFC 7807 Problem Details
      ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
      Assert.That(problem?.Title, Is.EqualTo("Unauthorized"));
      Assert.That(problem?.Status, Is.EqualTo(401));
      Assert.That(problem?.Detail, Does.Contain("API key is required"));
    }

    [Test]
    public async Task ProtectedEndpoint_WithSecondValidApiKey_Succeeds()
    {
      // Arrange - using second configured key
      RegisterRequest request = new("test-device-second-key");
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add("X-API-Key", SecondValidApiKey);
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert
      response.EnsureSuccessStatusCode();
      RegisterResponse? content = await response.Content.ReadFromJsonAsync<RegisterResponse>();
      Assert.That(content?.UserCode, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task ProtectedEndpoint_WithValidApiKey_Succeeds()
    {
      // Arrange
      RegisterRequest request = new("test-device-valid-key");
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add("X-API-Key", ValidApiKey);
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert
      response.EnsureSuccessStatusCode();
      RegisterResponse? content = await response.Content.ReadFromJsonAsync<RegisterResponse>();
      Assert.That(content?.UserCode, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task ProtectedEndpoint_WithWhitespaceApiKey_Returns401()
    {
      // Arrange - using whitespace instead of empty string (more reliable)
      RegisterRequest request = new("test-device-123");
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add("X-API-Key", "   ");
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SwaggerJson_Endpoint_DoesNotRequireApiKey()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/swagger/v1/swagger.json");

      // Assert
      Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SwaggerSubPath_DoesNotRequireApiKey()
    {
      // Act - any swagger sub-path should be public
      HttpResponseMessage response = await client!.GetAsync("/swagger/something");

      // Assert
      Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task WrongHeaderName_Returns401()
    {
      // Arrange - using wrong header name
      RegisterRequest request = new("test-device-wrong-header");
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add("Authorization", $"Bearer {ValidApiKey}");
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert - should fail because correct header is not present
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
  }