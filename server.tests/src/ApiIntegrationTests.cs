using System.Net.Http.Json;
using Heartbeat.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Heartbeat.Server.Tests ;

  /// <summary>
  ///   HTTP smoke tests for API endpoints.
  ///   Tests routing, JSON binding, DI wiring, and basic end-to-end flows.
  ///   Business rule edge cases belong in domain/service tests.
  /// </summary>
  [TestFixture]
  [NonParallelizable]
  public class ApiIntegrationTests
  {
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
      // SQLite in-memory requires connection to stay open
      // Note: If test parallelization is enabled later, consider isolating database per test
      // or disabling parallelization for this fixture via [NonParallelizable]
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

          // Use SQLite in-memory for relational database behavior
          services.AddDbContext<AppDbContext>(options => { options.UseSqlite(connection); });
        });
      });

      client = factory.CreateClient();

      scope = factory.Services.CreateScope();
      dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
      dbContext.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
      dbContext?.Database.EnsureDeleted();
      dbContext?.Dispose();
      scope?.Dispose();
      client?.Dispose();
      factory?.Dispose();
      connection?.Close();
      connection?.Dispose();
    }

    #endregion

    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private IServiceScope? scope;
    private AppDbContext? dbContext;
    private SqliteConnection? connection;

    [Test]
    public async Task Health_ReturnsOk()
    {
      // Act
      HttpResponseMessage response = await client!.GetAsync("/health");

      // Assert
      response.EnsureSuccessStatusCode();
      HealthResponse? content = await response.Content.ReadFromJsonAsync<HealthResponse>();
      Assert.That(content, Is.Not.Null);
      Assert.That(content!.Status, Is.EqualTo("ok"));
    }
  }