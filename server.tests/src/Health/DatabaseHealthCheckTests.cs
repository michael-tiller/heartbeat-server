using Heartbeat.Server.Health;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heartbeat.Server.Tests.Health ;

  /// <summary>
  ///   Tests for DatabaseHealthCheck.
  ///   Covers healthy path, failure path, exception handling, and error details.
  /// </summary>
  [TestFixture]
  public class DatabaseHealthCheckTests
  {
    [Test]
    public async Task CheckHealthAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
      // Arrange
      await using SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      await using AppDbContext dbContext = new(options);
      dbContext.Database.EnsureCreated();

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      using CancellationTokenSource cts = new();
      cts.Cancel();

      // Act & Assert - should handle or propagate cancellation
      // Note: The actual behavior depends on implementation
      // Some implementations catch all exceptions, some propagate cancellation
      try
      {
        HealthCheckResult result = await healthCheck.CheckHealthAsync(context, cts.Token);
        // If it catches the cancellation, it should return unhealthy
        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
      }
      catch (OperationCanceledException)
      {
        // If it propagates cancellation, that's also acceptable
        Assert.Pass("Cancellation was propagated correctly");
      }
    }

    [Test]
    public async Task CheckHealthAsync_ConnectionClosed_ReturnsExceptionDetails()
    {
      // Arrange
      SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      AppDbContext dbContext = new(options);
      dbContext.Database.EnsureCreated();

      // Close connection to simulate database unavailable
      connection.Close();

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert - should have exception details when connection fails
      Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public async Task CheckHealthAsync_ConnectionClosed_ReturnsUnhealthy()
    {
      // Arrange
      SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      AppDbContext dbContext = new(options);
      dbContext.Database.EnsureCreated();

      // Close connection to simulate database unavailable
      connection.Close();

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert
      Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    public async Task CheckHealthAsync_DatabaseAccessible_NoException()
    {
      // Arrange
      await using SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      await using AppDbContext dbContext = new(options);
      dbContext.Database.EnsureCreated();

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert
      Assert.That(result.Exception, Is.Null);
    }

    [Test]
    public async Task CheckHealthAsync_DatabaseAccessible_ReturnsDescriptiveMessage()
    {
      // Arrange
      await using SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      await using AppDbContext dbContext = new(options);
      dbContext.Database.EnsureCreated();

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert
      Assert.That(result.Description, Does.Contain("accessible"));
    }

    [Test]
    public async Task CheckHealthAsync_DatabaseAccessible_ReturnsHealthy()
    {
      // Arrange
      await using SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      await using AppDbContext dbContext = new(options);
      dbContext.Database.EnsureCreated();

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert
      Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task CheckHealthAsync_ExceptionThrown_CapturesException()
    {
      // Arrange - disposed context will throw
      SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      AppDbContext dbContext = new(options);
      dbContext.Database.EnsureCreated();
      dbContext.Dispose();

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert
      Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public async Task CheckHealthAsync_ExceptionThrown_MessageIndicatesFailure()
    {
      // Arrange
      SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      AppDbContext dbContext = new(options);
      await dbContext.Database.EnsureCreatedAsync();
      await dbContext.DisposeAsync();

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert
      Assert.That(result.Description, Does.Contain("failed"));
    }

    [Test]
    public async Task CheckHealthAsync_ExceptionThrown_ReturnsUnhealthy()
    {
      // Arrange - disposed context will throw
      SqliteConnection connection = new("Filename=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      AppDbContext dbContext = new(options);
      await dbContext.Database.EnsureCreatedAsync();
      await dbContext.DisposeAsync(); // Dispose to cause exception

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert
      Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    public async Task CheckHealthAsync_InvalidConnectionString_ReturnsUnhealthy()
    {
      // Arrange
      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite("Data Source=nonexistent_path/invalid.db")
        .Options;

      await using AppDbContext dbContext = new(options);

      DatabaseHealthCheck healthCheck = new(dbContext);
      HealthCheckContext context = new()
      {
        Registration = new HealthCheckRegistration("database", healthCheck, null, null)
      };

      // Act
      HealthCheckResult result = await healthCheck.CheckHealthAsync(context);

      // Assert
      Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }
  }