namespace Heartbeat.Domain.Interfaces;

public interface IStreakCalculator
{
    (int CurrentStreak, int LongestStreak) CalculateStreak(List<DailyActivity> activities, DateOnly today);
}