using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heartbeat.Contracts;
using Heartbeat.Server;

namespace Heartbeat.Server.Tests;

/// <summary>
/// HTTP smoke tests for API endpoints.
/// Tests routing, JSON binding, DI wiring, and basic end-to-end flows.
/// Business rule edge cases belong in domain/service tests.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ApiIntegrationTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IServiceScope? _scope;
    private AppDbContext? _dbContext;
    private SqliteConnection? _connection;

    [SetUp]
    public void SetUp()
    {
        // SQLite in-memory requires connection to stay open
        // Note: If test parallelization is enabled later, consider isolating database per test
        // or disabling parallelization for this fixture via [NonParallelizable]
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
                
                // Use SQLite in-memory for relational database behavior
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite(_connection);
                });
            });
        });

        _client = _factory.CreateClient();
        
        _scope = _factory.Services.CreateScope();
        _dbContext = _scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _dbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext?.Database.EnsureDeleted();
        _dbContext?.Dispose();
        _scope?.Dispose();
        _client?.Dispose();
        _factory?.Dispose();
        _connection?.Close();
        _connection?.Dispose();
    }

    [Test]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client!.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.That(content, Is.Not.Null);
        Assert.That(content!.Status, Is.EqualTo("ok"));
    }
}
