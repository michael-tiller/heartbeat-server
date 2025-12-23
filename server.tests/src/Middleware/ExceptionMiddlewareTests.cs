using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Heartbeat.Contracts.DTOs;
using Heartbeat.Server.Exceptions;
using Heartbeat.Server.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Heartbeat.Server.Tests.Middleware ;

  /// <summary>
  ///   Tests for exception handling middleware.
  ///   Verifies exception-to-status mapping, error response shape, correlation ID propagation,
  ///   and information hiding guarantees.
  ///   Tests ValidationException via invalid input to /register endpoint.
  ///   Generic exceptions and UnauthorizedAccessException require isolated middleware tests.
  /// </summary>
  [TestFixture]
  [NonParallelizable]
  public class ExceptionMiddlewareTests
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

    private const string CorrelationIdHeader = "X-Correlation-ID";

    private WebApplicationFactory<Program>? factory;
    private HttpClient? client;
    private SqliteConnection? connection;

    [Test]
    public async Task ValidationException_ExposesValidationMessage_ControlCharacters()
    {
      // Arrange - device ID with control characters
      RegisterRequest request = new("device\0id\nwith\tcontrol");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
      Assert.That(problem?.Detail, Does.Contain("invalid characters"));
    }

    [Test]
    public async Task ValidationException_ExposesValidationMessage_Empty()
    {
      // Arrange - empty device ID triggers "DeviceId is required"
      RegisterRequest request = new("");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
      Assert.That(problem?.Detail, Does.Contain("required"));
    }

    [Test]
    public async Task ValidationException_ExposesValidationMessage_TooShort()
    {
      // Arrange - device ID too short
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
      Assert.That(problem?.Detail, Does.Contain("at least"));
      Assert.That(problem?.Detail, Does.Contain("characters"));
    }

    [Test]
    public async Task ValidationException_PreservesClientCorrelationId()
    {
      // Arrange
      const string clientCorrelationId = "validation-error-trace-12345";
      RegisterRequest request = new("short");
      using HttpRequestMessage httpRequest = new(HttpMethod.Post, "/api/v1/register");
      httpRequest.Headers.Add(CorrelationIdHeader, clientCorrelationId);
      httpRequest.Content = JsonContent.Create(request);

      // Act
      HttpResponseMessage response = await client!.SendAsync(httpRequest);

      // Assert
      string? responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
      Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    [Test]
    public async Task ValidationException_ResponseHeaderContainsCorrelationId()
    {
      // Arrange
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task ValidationException_ResponseIsCamelCase()
    {
      // Arrange
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);
      string content = await response.Content.ReadAsStringAsync();

      // Assert - JSON should use camelCase (RFC 7807 field names)
      Assert.That(content, Does.Contain("\"detail\""));
      Assert.That(content, Does.Contain("\"status\""));
      Assert.That(content, Does.Contain("\"title\""));
    }

    [Test]
    public async Task ValidationException_ResponseIsValidJson()
    {
      // Arrange
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);
      string content = await response.Content.ReadAsStringAsync();

      // Assert - should be parseable JSON
      Assert.DoesNotThrow(() => JsonDocument.Parse(content));
    }

    [Test]
    public async Task ValidationException_Returns400BadRequest()
    {
      // Arrange - send request with invalid (too short) device ID
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert
      Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ValidationException_ReturnsProblemDetailsShape()
    {
      // Arrange
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert - RFC 7807 Problem Details shape
      ProblemDetails? problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
      Assert.That(problem, Is.Not.Null);
      Assert.That(problem!.Title, Is.EqualTo("Bad Request"));
      Assert.That(problem.Status, Is.EqualTo(400));
      Assert.That(problem.Detail, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task ValidationException_ReturnsProblemJsonContentType()
    {
      // Arrange
      RegisterRequest request = new("short");

      // Act
      HttpResponseMessage response = await client!.PostAsJsonAsync("/api/v1/register", request);

      // Assert - RFC 7807 requires application/problem+json
      Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }
  }

  /// <summary>
  ///   Isolated unit tests for ExceptionMiddleware exception-to-status mapping.
  ///   Tests exception types that cannot be easily triggered via HTTP endpoints.
  /// </summary>
  [TestFixture]
  public class ExceptionMiddlewareUnitTests
  {
    private static Task ThrowWithStackTrace()
    {
      throw new Exception("Sensitive info: password=secret123");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
      DefaultHttpContext context = new();
      context.Response.Body = new MemoryStream();
      return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
      context.Response.Body.Seek(0, SeekOrigin.Begin);
      using StreamReader reader = new(context.Response.Body);
      return await reader.ReadToEndAsync();
    }

    [Test]
    public async Task AllExceptions_SetProblemJsonContentType()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new Exception("Error"));
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert - RFC 7807 requires application/problem+json
      Assert.That(context.Response.ContentType, Does.StartWith("application/problem+json"));
    }

    [Test]
    public async Task GenericException_DoesNotExposeExceptionType()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new ArgumentNullException("param"));
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      string body = await ReadResponseBody(context);
      Assert.That(body, Does.Not.Contain("ArgumentNullException"));
      Assert.That(body, Does.Not.Contain("param"));
    }

    [Test]
    public async Task GenericException_DoesNotExposeStackTrace()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => ThrowWithStackTrace());
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      string body = await ReadResponseBody(context);
      Assert.That(body, Does.Not.Contain("at "));
      Assert.That(body, Does.Not.Contain("ThrowWithStackTrace"));
      Assert.That(body, Does.Not.Contain(".cs"));
    }

    [Test]
    public async Task GenericException_IncludesCorrelationId()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new Exception("Error"));
      DefaultHttpContext context = CreateHttpContext();
      context.TraceIdentifier = "test-correlation-id-12345";

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      string body = await ReadResponseBody(context);
      Assert.That(body, Does.Contain("test-correlation-id-12345"));
    }

    [Test]
    public async Task GenericException_IsMappedTo500()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new InvalidOperationException("Internal error"));
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      Assert.That(context.Response.StatusCode, Is.EqualTo(500));
    }

    [Test]
    public async Task GenericException_MessageIsHidden()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new InvalidOperationException("Database connection failed"));
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert - internal details should NEVER leak
      string body = await ReadResponseBody(context);
      Assert.That(body, Does.Not.Contain("Database"));
      Assert.That(body, Does.Not.Contain("connection"));
      Assert.That(body, Does.Contain("unexpected error"));
    }

    [Test]
    public async Task GenericException_ReturnsProblemDetailsShape()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new Exception("Error"));
      DefaultHttpContext context = CreateHttpContext();
      context.TraceIdentifier = "trace-123";

      // Act
      await middleware.InvokeAsync(context);

      // Assert - RFC 7807 ProblemDetails shape
      string body = await ReadResponseBody(context);
      Assert.That(body, Does.Contain("\"title\""));
      Assert.That(body, Does.Contain("\"status\""));
      Assert.That(body, Does.Contain("\"detail\""));
      Assert.That(body, Does.Contain("\"correlationId\""));
    }

    [Test]
    public async Task NoException_PassesThrough()
    {
      // Arrange
      bool nextCalled = false;
      ExceptionMiddleware middleware = new(_ =>
      {
        nextCalled = true;
        return Task.CompletedTask;
      });
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      Assert.That(nextCalled, Is.True);
      Assert.That(context.Response.StatusCode, Is.EqualTo(200)); // Default
    }

    [Test]
    public async Task UnauthorizedAccessException_IsMappedTo401()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new UnauthorizedAccessException("Secret details"));
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      Assert.That(context.Response.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task UnauthorizedAccessException_MessageIsHidden()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new UnauthorizedAccessException("Secret details"));
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert - should NOT expose actual exception message
      string body = await ReadResponseBody(context);
      Assert.That(body, Does.Not.Contain("Secret details"));
      Assert.That(body, Does.Contain("\"title\":\"Unauthorized\""));
    }

    [Test]
    public async Task ValidationException_IsMappedTo400()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new ValidationException("Test error"));
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      Assert.That(context.Response.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task ValidationException_MessageIsExposed()
    {
      // Arrange
      ExceptionMiddleware middleware = new(_ => throw new ValidationException("Specific validation error"));
      DefaultHttpContext context = CreateHttpContext();

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      string body = await ReadResponseBody(context);
      Assert.That(body, Does.Contain("Specific validation error"));
    }
  }