using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Heartbeat.Server.Health ;

  /// <summary>
  ///   Health check for database connectivity.
  /// </summary>
  public sealed class DatabaseHealthCheck : IHealthCheck
  {
    private readonly AppDbContext dbContext;

    /// <summary>
    ///   Initializes a new instance of the <see cref="DatabaseHealthCheck" /> class.
    /// </summary>
    /// <param name="dbContext">The database context to check.</param>
    public DatabaseHealthCheck(AppDbContext dbContext)
    {
      this.dbContext = dbContext;
    }

    #region IHealthCheck Members

    /// <summary>
    ///   Checks the health of the database.
    /// </summary>
    /// <param name="context">The health check context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The health check result.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(
      HealthCheckContext context,
      CancellationToken cancellationToken = default)
    {
      try
      {
        // Simple connectivity check - try to query the database
        bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        if (!canConnect) return HealthCheckResult.Unhealthy("Database is not accessible");
        // Additional check: try a simple query
        await dbContext.Users.CountAsync(cancellationToken);
        return HealthCheckResult.Healthy("Database is accessible");
      }
      catch (Exception ex)
      {
        return HealthCheckResult.Unhealthy("Database health check failed", ex);
      }
    }

    #endregion
  }