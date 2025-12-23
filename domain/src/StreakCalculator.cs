using Heartbeat.Domain.Interfaces;

namespace Heartbeat.Domain ;

  public class StreakCalculator : IStreakCalculator
  {
    #region IStreakCalculator Members

    public (int CurrentStreak, int LongestStreak) CalculateStreak(List<DailyActivity> activities, DateOnly today)
    {
      if (activities.Count == 0)
        return (0, 0);

      // Filter out invalid data: future dates
      HashSet<DateOnly> activitySet = activities
        .Where(a => a.Date <= today)
        .Select(a => a.Date)
        .ToHashSet();

      int currentStreak = 0;
      DateOnly checkDate = today;
      while (activitySet.Contains(checkDate))
      {
        currentStreak++;
        checkDate = checkDate.AddDays(-1);
      }

      int longestStreak = 0;
      int tempStreak = 0;
      List<DateOnly> sortedDates = activitySet.OrderByDescending(d => d).ToList();

      for (int i = 0; i < sortedDates.Count; i++)
        if (i == 0 || sortedDates[i - 1].AddDays(-1) == sortedDates[i])
        {
          tempStreak++;
          longestStreak = Math.Max(longestStreak, tempStreak);
        }
        else
        {
          tempStreak = 1;
        }

      return (currentStreak, longestStreak);
    }

    #endregion
  }