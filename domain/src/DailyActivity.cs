using Heartbeat.Domain.Interfaces;

namespace Heartbeat.Domain ;

  public class DailyActivity : IDailyActivity
  {
    #region IDailyActivity Members

    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    #endregion
  }