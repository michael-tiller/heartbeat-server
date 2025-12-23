namespace Heartbeat.Domain.Interfaces;


public interface IDailyActivity
{
    int Id { get; set; }
    int UserId { get; set; }
    DateOnly Date { get; set; }
    DateTime UpdatedAt { get; set; }
}