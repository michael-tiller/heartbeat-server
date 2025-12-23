using Heartbeat.Domain;
using Heartbeat.Server.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server ;

  public class AppDbContext : DbContext, IAppDbContext
  {
    /// <summary>
    ///   Initializes a new instance of the <see cref="AppDbContext" /> class.
    /// </summary>
    /// <param name="options">The options for the database context.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    #region IAppDbContext Members

    public DbSet<User> Users { get; set; }
    public DbSet<DailyActivity> DailyActivities { get; set; }

    #endregion

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