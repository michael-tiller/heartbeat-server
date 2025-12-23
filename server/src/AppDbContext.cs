using Microsoft.EntityFrameworkCore;
using Heartbeat.Domain;
using Heartbeat.Server.Interfaces;

namespace Heartbeat.Server;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<DailyActivity> DailyActivities { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User configuration
        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(u => u.DeviceId).IsUnique();
            e.HasIndex(u => u.PairCode).IsUnique();
        });

        // DailyActivity configuration
        modelBuilder.Entity<DailyActivity>(e =>
        {
            e.HasIndex(a => a.Date);
            // Unique constraint: one activity record per user per day
            e.HasIndex(a => new { a.UserId, a.Date }).IsUnique();
            e.Property(a => a.UserId).IsRequired();
            e.Property(a => a.Date).IsRequired();
            e.Property(a => a.UpdatedAt).IsRequired();
            
            // Configure relationship to User
            e.HasOne<User>()
             .WithMany()
             .HasForeignKey(a => a.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

