using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Heartbeat.Server;
using Heartbeat.Server.Health;

namespace Heartbeat.Server.Tests.Health;

/// <summary>
/// Tests for DatabaseHealthCheck.
/// Covers healthy path, failure path, exception handling, and error details.
/// </summary>
[TestFixture]
public class DatabaseHealthCheckTests
{
    #region Healthy Path

    [Test]
    public async Task CheckHealthAsync_DatabaseAccessible_ReturnsHealthy()
    {
        // Arrange
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        using var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task CheckHealthAsync_DatabaseAccessible_ReturnsDescriptiveMessage()
    {
        // Arrange
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        using var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.That(result.Description, Does.Contain("accessible"));
    }

    [Test]
    public async Task CheckHealthAsync_DatabaseAccessible_NoException()
    {
        // Arrange
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        using var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.That(result.Exception, Is.Null);
    }

    #endregion

    #region Failure Path - Connection Closed

    [Test]
    public async Task CheckHealthAsync_ConnectionClosed_ReturnsUnhealthy()
    {
        // Arrange
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        
        // Close connection to simulate database unavailable
        connection.Close();
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    public async Task CheckHealthAsync_ConnectionClosed_ReturnsExceptionDetails()
    {
        // Arrange
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        
        // Close connection to simulate database unavailable
        connection.Close();
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert - should have exception details when connection fails
        Assert.That(result.Exception, Is.Not.Null);
    }

    #endregion

    #region Failure Path - Invalid Connection String

    [Test]
    public async Task CheckHealthAsync_InvalidConnectionString_ReturnsUnhealthy()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=nonexistent_path/invalid.db")
            .Options;
        
        using var dbContext = new AppDbContext(options);
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    #endregion

    #region Exception Handling

    [Test]
    public async Task CheckHealthAsync_ExceptionThrown_ReturnsUnhealthy()
    {
        // Arrange - disposed context will throw
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        dbContext.Dispose(); // Dispose to cause exception
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    public async Task CheckHealthAsync_ExceptionThrown_CapturesException()
    {
        // Arrange - disposed context will throw
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        dbContext.Dispose();
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.That(result.Exception, Is.Not.Null);
    }

    [Test]
    public async Task CheckHealthAsync_ExceptionThrown_MessageIndicatesFailure()
    {
        // Arrange
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        dbContext.Dispose();
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        Assert.That(result.Description, Does.Contain("failed"));
    }

    #endregion

    #region Cancellation Token

    [Test]
    public async Task CheckHealthAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        
        using var dbContext = new AppDbContext(options);
        dbContext.Database.EnsureCreated();
        
        var healthCheck = new DatabaseHealthCheck(dbContext);
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("database", healthCheck, null, null)
        };
        
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - should handle or propagate cancellation
        // Note: The actual behavior depends on implementation
        // Some implementations catch all exceptions, some propagate cancellation
        try
        {
            var result = await healthCheck.CheckHealthAsync(context, cts.Token);
            // If it catches the cancellation, it should return unhealthy
            Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
        }
        catch (OperationCanceledException)
        {
            // If it propagates cancellation, that's also acceptable
            Assert.Pass("Cancellation was propagated correctly");
        }
    }

    #endregion
}

