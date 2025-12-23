using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Heartbeat.Server;

namespace Heartbeat.Server.Health;

/// <summary>
/// Health check for database connectivity.
/// </summary>
public sealed class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DatabaseHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple connectivity check - try to query the database
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            
            if (canConnect)
            {
                // Additional check: try a simple query
                await _dbContext.Users.CountAsync(cancellationToken);
                return HealthCheckResult.Healthy("Database is accessible");
            }
            
            return HealthCheckResult.Unhealthy("Database is not accessible");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed", ex);
        }
    }
}

