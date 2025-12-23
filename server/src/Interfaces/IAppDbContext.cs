using Heartbeat.Domain;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Interfaces ;

  public interface IAppDbContext
  {
    /// <summary>
    ///   Gets or sets the users.
    /// </summary>
    /// <value>The users.</value>
    DbSet<User> Users { get; set; }

    /// <summary>
    ///   Gets or sets the daily activities.
    /// </summary>
    /// <value>The daily activities.</value>
    DbSet<DailyActivity> DailyActivities { get; set; }
  }