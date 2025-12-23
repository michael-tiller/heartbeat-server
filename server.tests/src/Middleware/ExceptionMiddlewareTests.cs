using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Heartbeat.Contracts;
using Heartbeat.Server;
using Heartbeat.Server.Exceptions;
using Heartbeat.Server.Middleware;

namespace Heartbeat.Server.Tests.Middleware;

/// <summary>
/// Tests for exception handling middleware.
/// Verifies exception-to-status mapping, error response shape, correlation ID propagation,
/// and information hiding guarantees.
/// 
/// Tests ValidationException via invalid input to /register endpoint.
/// Generic exceptions and UnauthorizedAccessException require isolated middleware tests.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ExceptionMiddlewareTests
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    
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

    #region ValidationException Handling

    [Test]
    public async Task ValidationException_Returns400BadRequest()
    {
        // Arrange - send request with invalid (too short) device ID
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ValidationException_ReturnsProblemJsonContentType()
    {
        // Arrange
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert - RFC 7807 requires application/problem+json
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
    }

    [Test]
    public async Task ValidationException_ReturnsProblemDetailsShape()
    {
        // Arrange
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert - RFC 7807 Problem Details shape
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem, Is.Not.Null);
        Assert.That(problem!.Title, Is.EqualTo("Bad Request"));
        Assert.That(problem.Status, Is.EqualTo(400));
        Assert.That(problem.Detail, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task ValidationException_ExposesValidationMessage_TooShort()
    {
        // Arrange - device ID too short
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem?.Detail, Does.Contain("at least"));
        Assert.That(problem?.Detail, Does.Contain("characters"));
    }

    [Test]
    public async Task ValidationException_ExposesValidationMessage_Empty()
    {
        // Arrange - empty device ID triggers "DeviceId is required"
        var request = new RegisterRequest("");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem?.Detail, Does.Contain("required"));
    }

    [Test]
    public async Task ValidationException_ExposesValidationMessage_ControlCharacters()
    {
        // Arrange - device ID with control characters
        var request = new RegisterRequest("device\0id\nwith\tcontrol");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problem?.Detail, Does.Contain("invalid characters"));
    }

    #endregion

    #region Correlation ID on Validation Errors

    [Test]
    public async Task ValidationException_ResponseHeaderContainsCorrelationId()
    {
        // Arrange
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);

        // Assert
        Assert.That(response.Headers.Contains(CorrelationIdHeader), Is.True);
    }

    [Test]
    public async Task ValidationException_PreservesClientCorrelationId()
    {
        // Arrange
        var clientCorrelationId = "validation-error-trace-12345";
        var request = new RegisterRequest("short");
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/register");
        httpRequest.Headers.Add(CorrelationIdHeader, clientCorrelationId);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client!.SendAsync(httpRequest);

        // Assert
        var responseCorrelationId = response.Headers.GetValues(CorrelationIdHeader).FirstOrDefault();
        Assert.That(responseCorrelationId, Is.EqualTo(clientCorrelationId));
    }

    #endregion

    #region Response Structure Validation

    [Test]
    public async Task ValidationException_ResponseIsCamelCase()
    {
        // Arrange
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert - JSON should use camelCase (RFC 7807 field names)
        Assert.That(content, Does.Contain("\"detail\""));
        Assert.That(content, Does.Contain("\"status\""));
        Assert.That(content, Does.Contain("\"title\""));
    }

    [Test]
    public async Task ValidationException_ResponseIsValidJson()
    {
        // Arrange
        var request = new RegisterRequest("short");

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/register", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert - should be parseable JSON
        Assert.DoesNotThrow(() => System.Text.Json.JsonDocument.Parse(content));
    }

    #endregion
}

/// <summary>
/// Isolated unit tests for ExceptionMiddleware exception-to-status mapping.
/// Tests exception types that cannot be easily triggered via HTTP endpoints.
/// </summary>
[TestFixture]
public class ExceptionMiddlewareUnitTests
{
    [Test]
    public async Task ValidationException_IsMappedTo400()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new ValidationException("Test error"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(context.Response.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task ValidationException_MessageIsExposed()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new ValidationException("Specific validation error"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var body = await ReadResponseBody(context);
        Assert.That(body, Does.Contain("Specific validation error"));
    }

    [Test]
    public async Task UnauthorizedAccessException_IsMappedTo401()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new UnauthorizedAccessException("Secret details"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(context.Response.StatusCode, Is.EqualTo(401));
    }

    [Test]
    public async Task UnauthorizedAccessException_MessageIsHidden()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new UnauthorizedAccessException("Secret details"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert - should NOT expose actual exception message
        var body = await ReadResponseBody(context);
        Assert.That(body, Does.Not.Contain("Secret details"));
        Assert.That(body, Does.Contain("\"title\":\"Unauthorized\""));
    }

    [Test]
    public async Task GenericException_IsMappedTo500()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new InvalidOperationException("Internal error"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(context.Response.StatusCode, Is.EqualTo(500));
    }

    [Test]
    public async Task GenericException_MessageIsHidden()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new InvalidOperationException("Database connection failed"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert - internal details should NEVER leak
        var body = await ReadResponseBody(context);
        Assert.That(body, Does.Not.Contain("Database"));
        Assert.That(body, Does.Not.Contain("connection"));
        Assert.That(body, Does.Contain("unexpected error"));
    }

    [Test]
    public async Task GenericException_IncludesCorrelationId()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new Exception("Error"));
        var context = CreateHttpContext();
        context.TraceIdentifier = "test-correlation-id-12345";

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var body = await ReadResponseBody(context);
        Assert.That(body, Does.Contain("test-correlation-id-12345"));
    }

    [Test]
    public async Task GenericException_ReturnsProblemDetailsShape()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new Exception("Error"));
        var context = CreateHttpContext();
        context.TraceIdentifier = "trace-123";

        // Act
        await middleware.InvokeAsync(context);

        // Assert - RFC 7807 ProblemDetails shape
        var body = await ReadResponseBody(context);
        Assert.That(body, Does.Contain("\"title\""));
        Assert.That(body, Does.Contain("\"status\""));
        Assert.That(body, Does.Contain("\"detail\""));
        Assert.That(body, Does.Contain("\"correlationId\""));
    }

    [Test]
    public async Task GenericException_DoesNotExposeStackTrace()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => ThrowWithStackTrace());
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var body = await ReadResponseBody(context);
        Assert.That(body, Does.Not.Contain("at "));
        Assert.That(body, Does.Not.Contain("ThrowWithStackTrace"));
        Assert.That(body, Does.Not.Contain(".cs"));
    }

    [Test]
    public async Task GenericException_DoesNotExposeExceptionType()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new ArgumentNullException("param"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var body = await ReadResponseBody(context);
        Assert.That(body, Does.Not.Contain("ArgumentNullException"));
        Assert.That(body, Does.Not.Contain("param"));
    }

    [Test]
    public async Task AllExceptions_SetProblemJsonContentType()
    {
        // Arrange
        var middleware = new ExceptionMiddleware(_ => throw new Exception("Error"));
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert - RFC 7807 requires application/problem+json
        Assert.That(context.Response.ContentType, Does.StartWith("application/problem+json"));
    }

    [Test]
    public async Task NoException_PassesThrough()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new ExceptionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateHttpContext();

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.That(nextCalled, Is.True);
        Assert.That(context.Response.StatusCode, Is.EqualTo(200)); // Default
    }

    private static Task ThrowWithStackTrace()
    {
        throw new Exception("Sensitive info: password=secret123");
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}
