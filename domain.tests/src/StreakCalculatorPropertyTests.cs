using FsCheck.NUnit;
using Heartbeat.Domain.Interfaces;
using Property = FsCheck.NUnit.PropertyAttribute;

namespace Heartbeat.Domain.Tests ;

  /// <summary>
  ///   Property-based tests for streak calculation invariants.
  ///   These test rules that must never break, not specific examples.
  ///   Uses custom FsCheck generators to provide DateOnly and DailyActivity types
  ///   (FsCheck has no built-in generator for System.DateOnly).
  /// </summary>
  [TestFixture]
  public class StreakCalculatorPropertyTests
  {
    #region Setup/Teardown

    [SetUp]
    public void Setup()
    {
      calculator = new StreakCalculator();
    }

    #endregion

    private IStreakCalculator calculator = null!;

    /// <summary>
    ///   Invariant: Longest streak is always >= current streak.
    ///   This must hold for any set of activities and any reference date.
    /// </summary>
    [Property(Arbitrary = [ typeof(Generators) ])]
    public bool LongestStreak_IsAlwaysGreaterThanOrEqualToCurrentStreak(
      List<DailyActivity> activities,
      DateOnly today)
    {
      (int CurrentStreak, int LongestStreak) result = calculator.CalculateStreak(activities, today);
      return result.LongestStreak >= result.CurrentStreak;
    }

    /// <summary>
    ///   Invariant: Streak calculation is idempotent.
    ///   Adding the same activity multiple times should not change the result.
    /// </summary>
    [Property(Arbitrary = [ typeof(Generators) ])]
    public bool StreakCalculation_IsIdempotent(
      List<DailyActivity> activities,
      DailyActivity duplicateActivity,
      DateOnly today)
    {
      // Normalize duplicate to ensure it's meaningful for streak calculation:
      // - Date must be <= today (future dates are filtered by calculator)
      duplicateActivity = new DailyActivity
      {
        Date = duplicateActivity.Date <= today ? duplicateActivity.Date : today
      };

      // Remove any existing activities with the same date as duplicateActivity
      // to ensure we're testing true idempotency (adding same activity multiple times)
      List<DailyActivity> baseActivities = activities
        .Where(a => a.Date != duplicateActivity.Date)
        .ToList();

      // Calculate result with the duplicate added once
      List<DailyActivity> activitiesWithOne = baseActivities.ToList();
      activitiesWithOne.Add(duplicateActivity);
      (int CurrentStreak, int LongestStreak) resultWithOne = calculator.CalculateStreak(activitiesWithOne, today);

      // Add the duplicate activity multiple times
      List<DailyActivity> activitiesWithDuplicates = baseActivities.ToList();
      activitiesWithDuplicates.Add(duplicateActivity);
      activitiesWithDuplicates.Add(duplicateActivity);
      activitiesWithDuplicates.Add(duplicateActivity);
      (int CurrentStreak, int LongestStreak) resultWithDuplicates = calculator.CalculateStreak(activitiesWithDuplicates, today);

      // Results should be identical - adding the same activity multiple times doesn't change the streak
      return resultWithOne.CurrentStreak == resultWithDuplicates.CurrentStreak &&
             resultWithOne.LongestStreak == resultWithDuplicates.LongestStreak;
    }


    /// <summary>
    ///   Invariant: Duplicate pokes on the same day do not create extra days in the streak.
    ///   Multiple DailyActivity objects with the same date should be treated as a single day.
    /// </summary>
    [Property(Arbitrary = [ typeof(Generators) ])]
    public bool DuplicatePokesOnSameDay_DoNotCreateExtraDays(
      List<DailyActivity> baseActivities,
      DateOnly duplicateDate,
      DateOnly today)
    {
      // Filter base activities to remove any that match duplicateDate (to test adding duplicates)
      List<DailyActivity> activitiesWithoutTarget = baseActivities
        .Where(a => a.Date != duplicateDate)
        .ToList();

      // Add multiple activities with the same date
      List<DailyActivity> activitiesWithDuplicates = activitiesWithoutTarget.ToList();
      activitiesWithDuplicates.Add(new DailyActivity
      {
        Date = duplicateDate
      });
      activitiesWithDuplicates.Add(new DailyActivity
      {
        Date = duplicateDate
      });

      (int CurrentStreak, int LongestStreak) resultWithDuplicates = calculator.CalculateStreak(activitiesWithDuplicates, today);

      // The streak should be the same as if we only added one activity for that date
      List<DailyActivity> activitiesWithSingle = activitiesWithoutTarget.ToList();
      activitiesWithSingle.Add(new DailyActivity
      {
        Date = duplicateDate
      });
      (int CurrentStreak, int LongestStreak) resultWithSingle = calculator.CalculateStreak(activitiesWithSingle, today);

      // Multiple activities on same date should produce same result as single activity
      return resultWithDuplicates.CurrentStreak == resultWithSingle.CurrentStreak &&
             resultWithDuplicates.LongestStreak == resultWithSingle.LongestStreak;
    }
  }