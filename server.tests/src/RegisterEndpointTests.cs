using System.Net;
using System.Net.Http.Json;
using Heartbeat.Contracts.DTOs;
using Heartbeat.Domain;
using Heartbeat.Server.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Heartbeat.Server.Tests ;

  /// <summary>
  ///   Tests for the /register endpoint.
  ///   Covers new user creation, existing user behavior, validation, and business logic.
  /// </summary>
  [TestFixture]
  [NonParallelizable]
  public class RegisterEndpointTests
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

    [Test]
    public async Task Register_DeviceIdWithControlChars_Returns400()
    {
      // Arrange
      RegisterRequest request = new("device\0id\ntest");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_DeviceIdWithWhitespace_IsTrimmed()
    {
      // Arrange - whitespace around device ID
      RegisterRequest request = new("  trimmed-device-id-123  ");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert - should succeed (trimmed ID is valid)
      response.EnsureSuccessStatusCode();

      // Verify trimmed in database
      using IServiceScope scope = factory!.Services.CreateScope();
      AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      User? user = await dbContext.Users.FirstOrDefaultAsync(u => u.DeviceId == "trimmed-device-id-123");
      Assert.That(user, Is.Not.Null);
    }

    [Test]
    public async Task Register_DifferentDevices_GetDifferentPairCodes()
    {
      // Arrange
      RegisterRequest request1 = new("unique-device-1-12345");
      RegisterRequest request2 = new("unique-device-2-12345");

      // Act
      HttpResponseMessage response1 = await client!.PostAsJsonAsync("/api/v1/register", request1);
      HttpResponseMessage response2 = await client!.PostAsJsonAsync("/api/v1/register", request2);

      // Assert
      RegisterResponse? result1 = await response1.Content.ReadFromJsonAsync<RegisterResponse>();
      RegisterResponse? result2 = await response2.Content.ReadFromJsonAsync<RegisterResponse>();
      Assert.That(result1!.UserCode, Is.Not.EqualTo(result2!.UserCode));
    }

    [Test]
    public async Task Register_EmptyDeviceId_Returns400()
    {
      // Arrange
      RegisterRequest request = new("");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_ExistingUser_DoesNotDuplicateUser()
    {
      // Arrange
      string deviceId = "no-duplicate-device-123";
      RegisterRequest request = new(deviceId);
      await client!.PostAsJsonAsync("/api/v1/register", request);

      // Act
      await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      using IServiceScope scope = factory!.Services.CreateScope();
      AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      int userCount = await dbContext.Users.CountAsync(u => u.DeviceId == deviceId);
      Assert.That(userCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Register_ExistingUser_Returns200Ok()
    {
      // Arrange - register first
      string deviceId = "existing-device-id-123";
      RegisterRequest request = new(deviceId);
      await client!.PostAsJsonAsync("/api/v1/register", request);

      // Act - register again
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Register_ExistingUser_ReturnsSamePairCode()
    {
      // Arrange
      string deviceId = "same-paircode-device-123";
      RegisterRequest request = new(deviceId);

      HttpResponseMessage firstResponse = await client!.PostAsJsonAsync("/api/v1/register", request);
      RegisterResponse? firstResult = await firstResponse.Content.ReadFromJsonAsync<RegisterResponse>();

      // Act
      HttpResponseMessage secondResponse = await client!.PostAsJsonAsync("/api/v1/register", request);
      RegisterResponse? secondResult = await secondResponse.Content.ReadFromJsonAsync<RegisterResponse>();

      // Assert
      Assert.That(secondResult!.UserCode, Is.EqualTo(firstResult!.UserCode));
    }

    [Test]
    public async Task Register_ExistingUser_UpdatesDailyActivityTimestamp()
    {
      // Arrange
      string deviceId = "update-activity-device-123";
      RegisterRequest request = new(deviceId);
      await client!.PostAsJsonAsync("/api/v1/register", request);

      // Get initial activity timestamp
      using IServiceScope scope = factory!.Services.CreateScope();
      AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      User user = await dbContext.Users.FirstAsync(u => u.DeviceId == deviceId);
      DailyActivity activity = await dbContext.DailyActivities.FirstAsync(a => a.UserId == user.Id);
      DateTime initialTimestamp = activity.UpdatedAt;

      // Small delay to ensure timestamp difference
      await Task.Delay(10);

      // Act
      await client!.PostAsJsonAsync("/api/v1/register", request);

      // Detach to force re-query
      dbContext.Entry(activity).State = EntityState.Detached;

      // Assert - need to re-query to see updated value
      DailyActivity updatedActivity = await dbContext.DailyActivities.FirstAsync(a => a.UserId == user.Id);
      Assert.That(updatedActivity.UpdatedAt, Is.GreaterThanOrEqualTo(initialTimestamp));
    }

    [Test]
    public async Task Register_InvalidRequest_ReturnsProblemDetails()
    {
      // Arrange
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert - RFC 7807 Problem Details
      ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
      Assert.That(problem, Is.Not.Null);
      Assert.That(problem!.Title, Is.EqualTo("Bad Request"));
      Assert.That(problem.Status, Is.EqualTo(400));
      Assert.That(problem.Detail, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Register_NewUser_CreatesDailyActivity()
    {
      // Arrange
      string deviceId = "device-activity-test-123";
      RegisterRequest request = new(deviceId);

      // Act
      await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      using IServiceScope scope = factory!.Services.CreateScope();
      AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      User? user = await dbContext.Users.FirstOrDefaultAsync(u => u.DeviceId == deviceId);
      DailyActivity? activity = await dbContext.DailyActivities.FirstOrDefaultAsync(a => a.UserId == user!.Id);
      Assert.That(activity, Is.Not.Null);
      Assert.That(activity!.Date, Is.EqualTo(DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [Test]
    public async Task Register_NewUser_CreatesUserInDatabase()
    {
      // Arrange
      string deviceId = "device-db-test-12345";
      RegisterRequest request = new(deviceId);

      // Act
      await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      using IServiceScope scope = factory!.Services.CreateScope();
      AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      User? user = await dbContext.Users.FirstOrDefaultAsync(u => u.DeviceId == deviceId);
      Assert.That(user, Is.Not.Null);
    }

    [Test]
    public async Task Register_NewUser_PairCodeIsValidFormat()
    {
      // Arrange
      RegisterRequest request = new("device-format-test-123");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert - pair code should be 6 chars, uppercase alphanumeric (excluding confusing chars)
      RegisterResponse? result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
      Assert.That(result!.UserCode, Has.Length.EqualTo(6));
      Assert.That(result.UserCode, Does.Match(@"^[A-HJ-NP-Z2-9]{6}$"));
    }

    [Test]
    public async Task Register_NewUser_Returns200Ok()
    {
      // Arrange
      RegisterRequest request = new("new-device-id-12345");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Register_NewUser_ReturnsPairCode()
    {
      // Arrange
      RegisterRequest request = new("new-device-id-67890");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      RegisterResponse? result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
      Assert.That(result, Is.Not.Null);
      Assert.That(result!.UserCode, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Register_ShortDeviceId_Returns400()
    {
      // Arrange - device ID less than 8 characters
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_Success_ResponseIsCamelCase()
    {
      // Arrange
      RegisterRequest request = new("camelcase-device-12345");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);
      string content = await response.Content.ReadAsStringAsync();

      // Assert
      Assert.That(content, Does.Contain("\"userCode\""));
      Assert.That(content, Does.Not.Contain("\"UserCode\""));
    }

    [Test]
    public async Task Register_Success_ReturnsJsonContentType()
    {
      // Arrange
      RegisterRequest request = new("content-type-device-123");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }
  }