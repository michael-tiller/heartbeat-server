using System.Reflection;
using System.Text.Json;
using Heartbeat.Contracts.DTOs;
using Heartbeat.Domain;
using Heartbeat.Server;
using Heartbeat.Server.Health;
using Heartbeat.Server.Middleware;
using Heartbeat.Server.Resilience;
using Heartbeat.Server.Validation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

  Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

  builder.Host.UseSerilog();

  builder.Services.Configure<JsonOptions>(o => { o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });

  builder.Services.AddCors(options =>
  {
    options.AddDefaultPolicy(policy =>
    {
      if (builder.Environment.IsDevelopment())
      {
        policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader();
      }
      else
      {
        string[] allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                                  ?? Array.Empty<string>();

        if (allowedOrigins.Length > 0)
        {
          policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
        }
        else
        {
          // Fallback: allow all if not configured (should be configured in Production)
          Log.Warning("CORS allowed origins not configured. Allowing all origins.");
          policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
        }
      }
    });
  });

  builder.Services.AddHttpClient("resilient").AddPolicyHandler(HttpResiliencePolicy.CreateCombinedPolicy());

  builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", HealthStatus.Unhealthy, [ "ready", "live" ]);

  ApiKeySettings apiKeySettings = builder.Configuration.GetSection(ApiKeySettings.SectionName).Get<ApiKeySettings>()
                                  ?? new ApiKeySettings();

  if (builder.Environment.IsDevelopment())
  {
    IConfigurationSection apiKeySection = builder.Configuration.GetSection(ApiKeySettings.SectionName);
    // Only enable if explicitly set to true AND has keys configured
    if (!apiKeySection.Exists() || !apiKeySection.GetValue<bool>("Enabled") || apiKeySettings.Keys.Count == 0)
    {
      apiKeySettings.Enabled = false;
      Log.Information("API key authentication is disabled in Development mode");
    }
  }

// Sanity check: if enabled, must have at least one key
  if (apiKeySettings.Enabled && apiKeySettings.Keys.Count == 0)
  {
    Log.Warning("API key authentication is enabled but no keys are configured. Disabling authentication.");
    apiKeySettings.Enabled = false;
  }

  builder.Services.AddSingleton(apiKeySettings);

  if (!builder.Environment.IsDevelopment())
    builder.Services.AddHttpsRedirection(options => { options.HttpsPort = 443; });

// Database configuration - supports PostgreSQL (via connection string) or SQLite (fallback)
// Tests will register InMemory database instead
  string environmentName = builder.Configuration["ASPNETCORE_ENVIRONMENT"] ?? builder.Environment.EnvironmentName;
  if (environmentName != "Testing")
  {
    string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (!string.IsNullOrEmpty(connectionString))
    {
      builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));
      Log.Information("Using PostgreSQL database");
    }
    else
    {
      builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=heartbeat.db"));
      Log.Information("Using SQLite database (local development)");
    }
  }

  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen(c =>
  {
    c.SwaggerDoc("v1", new OpenApiInfo
    {
      Title = "Heartbeat API",
      Version = "v1",
      Description = "API for Heartbeat mobile app. All endpoints are prefixed with /api/v1."
    });
    c.AddServer(new OpenApiServer { Url = "http://localhost:5166", Description = "Development server" });

    c.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
      Type = SecuritySchemeType.ApiKey,
      Name = "X-API-Key",
      In = ParameterLocation.Header,
      Description = "API key for authentication"
    });

    // use a lambda to create the security scheme
    c.AddSecurityRequirement(doc =>
    {
      OpenApiSecurityRequirement requirement = new();
      OpenApiSecuritySchemeReference schemeRef = new("ApiKey", doc);
      requirement.Add(schemeRef, []);
      return requirement;
    });

    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    // Mark record positional parameters as required
    c.SchemaFilter<RecordSchemaFilter>();

    // Include XML comments if available
    string xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
      c.IncludeXmlComments(xmlPath);
  });

  WebApplication app = builder.Build();

// Middleware pipeline order is important!

// 1. Correlation ID middleware (must be early to capture all logs)
// 2. CORS (before other middleware that might set headers)
// 3. Exception handling (must be early to catch all errors)
// 4. Security headers (add to all responses)
// 5. HTTPS redirection (Production only)
// 6. API key authentication (always registered, checks enabled flag at runtime for testability)
// Apply database migrations (skip in test environment)

  app.UseMiddleware<CorrelationIdMiddleware>();
  app.UseCors();
  app.UseMiddleware<ExceptionMiddleware>();
  app.UseMiddleware<SecurityHeadersMiddleware>();

  if (!app.Environment.IsDevelopment() && !app.Environment.IsEnvironment("Testing"))
    app.UseHttpsRedirection();

  app.UseMiddleware<ApiKeyMiddleware>();
  if (apiKeySettings.Enabled)
    Log.Information("API key authentication is enabled");

  if (!app.Environment.IsEnvironment("Testing"))
  {
    using IServiceScope scope = app.Services.CreateScope();
    AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
  }

  app.UseSwagger();
  app.UseSwaggerUI(c =>
  {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Heartbeat API v1");
    c.RoutePrefix = "swagger";
  });

// Health endpoints - always public (no auth required)

// Basic health check (backward compatibility)
  app.MapGet("/health", () => Results.Ok(new HealthResponse("ok")))
    .WithName("CheckHealth")
    .WithTags("Health")
    .Produces<HealthResponse>();

// Liveness probe - indicates the app is running (Kubernetes)
  app.MapHealthChecks("/health/live", new HealthCheckOptions
  {
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = async (context, report) =>
    {
      context.Response.ContentType = "application/json";
      string result = JsonSerializer.Serialize(new
      {
        status = report.Status.ToString(),
        timestamp = DateTime.UtcNow
      });
      await context.Response.WriteAsync(result);
    }
  })
    .WithName("HealthLive")
    .WithTags("Health");

// Readiness probe - indicates the app is ready to serve traffic (Kubernetes)
  app.MapHealthChecks("/health/ready", new HealthCheckOptions
  {
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
      context.Response.ContentType = "application/json";
      string result = JsonSerializer.Serialize(new
      {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new
        {
          name = e.Key,
          status = e.Value.Status.ToString(),
          description = e.Value.Description,
          duration = e.Value.Duration.TotalMilliseconds
        }),
        timestamp = DateTime.UtcNow
      });
      await context.Response.WriteAsync(result);
    }
  })
    .WithName("HealthReady")
    .WithTags("Health");

// API v1 endpoints
  RouteGroupBuilder v1 = app.MapGroup("/api/v1");

// Register endpoint with proper validation
  v1.MapPost("/register", async (RegisterRequest request, AppDbContext db) =>
  {
    // Validate and sanitize input
    string deviceId = RequestValidator.Sanitize(request.DeviceId);
    RequestValidator.ValidateDeviceId(deviceId);

    DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
    StreakCalculator calculator = new();

    // Check if user already exists
    User? existingUser = await db.Users.FirstOrDefaultAsync(u => u.DeviceId == deviceId);
    if (existingUser != null)
    {
      Log.Information("Returning existing user for device {DeviceId}", deviceId);

      // Update daily activity for existing user (if it's a new day)
      DailyActivity? existingActivity = await db.DailyActivities
        .FirstOrDefaultAsync(a => a.UserId == existingUser.Id && a.Date == today);

      if (existingActivity == null)
      {
        DailyActivity newActivity = new()
        {
          UserId = existingUser.Id,
          Date = today,
          UpdatedAt = DateTime.UtcNow
        };
        db.DailyActivities.Add(newActivity);
        await db.SaveChangesAsync();
        Log.Information("Created daily activity for user {UserId} on {Date}", existingUser.Id, today);
      }
      else
      {
        // Update existing activity timestamp
        existingActivity.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Log.Information("Updated daily activity for user {UserId} on {Date}", existingUser.Id, today);
      }

      // Calculate and log streak
      List<DailyActivity> userActivities = await db.DailyActivities
        .Where(a => a.UserId == existingUser.Id)
        .ToListAsync();
      (int currentStreak, int longestStreak) = calculator.CalculateStreak(userActivities, today);

      // Detect if streak was lost/reset
      if (currentStreak == 1 && userActivities.Count > 1)
      {
        // Find when the previous streak ended (most recent activity date before today)
        List<DailyActivity> previousActivities = userActivities
          .Where(a => a.Date < today)
          .OrderByDescending(a => a.Date)
          .ToList();

        if (previousActivities.Any())
        {
          DateOnly lastActivityDate = previousActivities.First().Date;
          DateOnly yesterday = today.AddDays(-1);

          // If last activity was before yesterday, streak was broken
          // currentStreak == 1 means they have activity today but not yesterday
          if (lastActivityDate < yesterday)
          {
            int gapDays = today.DayNumber - lastActivityDate.DayNumber;
            Log.Warning("User {UserId} streak reset! Previous streak ended on {StreakEndDate} (gap of {GapDays} days). Current streak: {CurrentStreak}",
              existingUser.Id, lastActivityDate, gapDays, currentStreak);
          }
          else if (lastActivityDate == yesterday)
          {
            // This shouldn't happen if currentStreak == 1, but log it just in case
            Log.Warning("User {UserId} streak reset detected but last activity was yesterday. This may indicate a data issue.",
              existingUser.Id);
          }
        }
      }

      Log.Information("User {UserId} streak: Current={CurrentStreak}, Longest={LongestStreak}",
        existingUser.Id, currentStreak, longestStreak);

      return Results.Ok(new RegisterResponse(existingUser.PairCode, currentStreak, longestStreak));
    }

    // Create new user with unique pair code
    string pairCode;
    bool isUnique = false;
    int attempts = 0;
    const int maxAttempts = 10;

    do
    {
      pairCode = User.GeneratePairCode();
      bool codeExists = await db.Users.AnyAsync(u => u.PairCode == pairCode);
      isUnique = !codeExists;
      attempts++;
    } while (!isUnique && attempts < maxAttempts);

    if (!isUnique)
    {
      Log.Error("Failed to generate unique pair code after {Attempts} attempts", maxAttempts);
      throw new InvalidOperationException("Failed to generate unique pair code");
    }

    User newUser = new()
    {
      DeviceId = deviceId,
      PairCode = pairCode,
      CreatedAt = DateTime.UtcNow
    };

    db.Users.Add(newUser);
    await db.SaveChangesAsync();

    // Create daily activity for new user
    DailyActivity newUserActivity = new()
    {
      UserId = newUser.Id,
      Date = today,
      UpdatedAt = DateTime.UtcNow
    };
    db.DailyActivities.Add(newUserActivity);
    await db.SaveChangesAsync();

    // Calculate streak for new user (will be 1, 1)
    (int newUserCurrentStreak, int newUserLongestStreak) = calculator.CalculateStreak(new List<DailyActivity> { newUserActivity }, today);
    Log.Information("Created new user for device {DeviceId} with code {UserCode}", deviceId, pairCode);
    Log.Information("Created daily activity for new user {UserId} on {Date}", newUser.Id, today);
    Log.Information("User {UserId} streak: Current={CurrentStreak}, Longest={LongestStreak}",
      newUser.Id, newUserCurrentStreak, newUserLongestStreak);

    return Results.Ok(new RegisterResponse(pairCode, newUserCurrentStreak, newUserLongestStreak));
  })
    .WithName("Register")
    .WithTags("Users")
    .Produces<RegisterResponse>()
    .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
    .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

  try
  {
    Log.Information("Starting Heartbeat server");
    app.Run("http://0.0.0.0:5166");
  }
  catch (Exception ex)
  {
    Log.Fatal(ex, "Application terminated unexpectedly");
  }
  finally
  {
    Log.CloseAndFlush();
  }