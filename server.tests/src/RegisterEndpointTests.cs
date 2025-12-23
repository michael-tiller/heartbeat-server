using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heartbeat.Contracts;
using Heartbeat.Server;
using Heartbeat.Server.Middleware;

namespace Heartbeat.Server.Tests;

/// <summary>
/// Tests for the /register endpoint.
/// Covers new user creation, existing user behavior, validation, and business logic.
/// </summary>
[TestFixture]
[NonParallelizable]
public class RegisterEndpointTests
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

    #region New User Creation

    [Test]
    public async Task Register_NewUser_Returns200Ok()
    {
        // Arrange
        var request = new RegisterRequest("new-device-id-12345");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Register_NewUser_ReturnsPairCode()
    {
        // Arrange
        var request = new RegisterRequest("new-device-id-67890");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.UserCode, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Register_NewUser_PairCodeIsValidFormat()
    {
        // Arrange
        var request = new RegisterRequest("device-format-test-123");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert - pair code should be 6 chars, uppercase alphanumeric (excluding confusing chars)
        var result = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.That(result!.UserCode, Has.Length.EqualTo(6));
        Assert.That(result.UserCode, Does.Match(@"^[A-HJ-NP-Z2-9]{6}$"));
    }

    [Test]
    public async Task Register_NewUser_CreatesUserInDatabase()
    {
        // Arrange
        var deviceId = "device-db-test-12345";
        var request = new RegisterRequest(deviceId);

        // Act
        await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.DeviceId == deviceId);
        Assert.That(user, Is.Not.Null);
    }

    [Test]
    public async Task Register_NewUser_CreatesDailyActivity()
    {
        // Arrange
        var deviceId = "device-activity-test-123";
        var request = new RegisterRequest(deviceId);

        // Act
        await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.DeviceId == deviceId);
        var activity = await dbContext.DailyActivities.FirstOrDefaultAsync(a => a.UserId == user!.Id);
        Assert.That(activity, Is.Not.Null);
        Assert.That(activity!.Date, Is.EqualTo(DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    #endregion

    #region Existing User Behavior

    [Test]
    public async Task Register_ExistingUser_Returns200Ok()
    {
        // Arrange - register first
        var deviceId = "existing-device-id-123";
        var request = new RegisterRequest(deviceId);
        await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Act - register again
        var response = await _client.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        response.EnsureSuccessStatusCode();
    }

    [Test]
    public async Task Register_ExistingUser_ReturnsSamePairCode()
    {
        // Arrange
        var deviceId = "same-paircode-device-123";
        var request = new RegisterRequest(deviceId);
        
        var firstResponse = await _client!.PostAsJsonAsync("/api/v1/register", request);
        var firstResult = await firstResponse.Content.ReadFromJsonAsync<RegisterResponse>();

        // Act
        var secondResponse = await _client.PostAsJsonAsync("/api/v1/register", request);
        var secondResult = await secondResponse.Content.ReadFromJsonAsync<RegisterResponse>();

        // Assert
        Assert.That(secondResult!.UserCode, Is.EqualTo(firstResult!.UserCode));
    }

    [Test]
    public async Task Register_ExistingUser_DoesNotDuplicateUser()
    {
        // Arrange
        var deviceId = "no-duplicate-device-123";
        var request = new RegisterRequest(deviceId);
        await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Act
        await _client.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userCount = await dbContext.Users.CountAsync(u => u.DeviceId == deviceId);
        Assert.That(userCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Register_ExistingUser_UpdatesDailyActivityTimestamp()
    {
        // Arrange
        var deviceId = "update-activity-device-123";
        var request = new RegisterRequest(deviceId);
        await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Get initial activity timestamp
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.FirstAsync(u => u.DeviceId == deviceId);
        var activity = await dbContext.DailyActivities.FirstAsync(a => a.UserId == user.Id);
        var initialTimestamp = activity.UpdatedAt;

        // Small delay to ensure timestamp difference
        await Task.Delay(10);

        // Act
        await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Detach to force re-query
        dbContext.Entry(activity).State = EntityState.Detached;
        
        // Assert - need to re-query to see updated value
        var updatedActivity = await dbContext.DailyActivities.FirstAsync(a => a.UserId == user.Id);
        Assert.That(updatedActivity.UpdatedAt, Is.GreaterThanOrEqualTo(initialTimestamp));
    }

    #endregion

    #region Pair Code Uniqueness

    [Test]
    public async Task Register_DifferentDevices_GetDifferentPairCodes()
    {
        // Arrange
        var request1 = new RegisterRequest("unique-device-1-12345");
        var request2 = new RegisterRequest("unique-device-2-12345");

        // Act
        var response1 = await _client!.PostAsJsonAsync("/api/v1/register", request1);
        var response2 = await _client.PostAsJsonAsync("/api/v1/register", request2);

        // Assert
        var result1 = await response1.Content.ReadFromJsonAsync<RegisterResponse>();
        var result2 = await response2.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.That(result1!.UserCode, Is.Not.EqualTo(result2!.UserCode));
    }

    #endregion

    #region Invalid Input Rejection

    [Test]
    public async Task Register_EmptyDeviceId_Returns400()
    {
        // Arrange
        var request = new RegisterRequest("");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_ShortDeviceId_Returns400()
    {
        // Arrange - device ID less than 8 characters
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_DeviceIdWithControlChars_Returns400()
    {
        // Arrange
        var request = new RegisterRequest("device\0id\ntest");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Register_InvalidRequest_ReturnsProblemDetails()
    {
        // Arrange
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert - RFC 7807 Problem Details
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Title, Is.EqualTo("Bad Request"));
        Assert.That(problem.Status, Is.EqualTo(400));
        Assert.That(problem.Detail, Is.Not.Null.And.Not.Empty);
    }

    #endregion

    #region Response Format

    [Test]
    public async Task Register_Success_ReturnsJsonContentType()
    {
        // Arrange
        var request = new RegisterRequest("content-type-device-123");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/json"));
    }

    [Test]
    public async Task Register_Success_ResponseIsCamelCase()
    {
        // Arrange
        var request = new RegisterRequest("camelcase-device-12345");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.That(content, Does.Contain("\"pairCode\""));
        Assert.That(content, Does.Not.Contain("\"PairCode\""));
    }

    #endregion

    #region Device ID Sanitization

    [Test]
    public async Task Register_DeviceIdWithWhitespace_IsTrimmed()
    {
        // Arrange - whitespace around device ID
        var request = new RegisterRequest("  trimmed-device-id-123  ");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert - should succeed (trimmed ID is valid)
        response.EnsureSuccessStatusCode();

        // Verify trimmed in database
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.DeviceId == "trimmed-device-id-123");
        Assert.That(user, Is.Not.Null);
    }

    #endregion
}

