namespace Heartbeat.Domain.Tests ;

  [TestFixture]
  public class StreakCalculatorTests
  {
    [Test]
    public void CalculateStreak_WithActivityInFuture_IgnoresFutureDates()
    {
      // Arrange
      StreakCalculator calculator = new();
      DateOnly today = new(2024, 1, 15);
      List<DailyActivity> activities =
        [
          new() { Date = today },
          new() { Date = today.AddDays(1) },
          new() { Date = today.AddDays(-1) }
          ];

      // Act
      (int CurrentStreak, int LongestStreak) result = calculator.CalculateStreak(activities, today);

      // Assert
      // Future dates are invalid data and should be ignored for both current and longest streak
      Assert.That(result.CurrentStreak, Is.EqualTo(2)); // Today and yesterday only
      Assert.That(result.LongestStreak, Is.EqualTo(2)); // The sequence: yesterday, today (future date ignored)
    }

    [Test]
    public void CalculateStreak_WithConsecutiveDays_CalculatesCurrentStreak()
    {
      // Arrange
      StreakCalculator calculator = new();
      DateOnly today = new(2024, 1, 15);
      List<DailyActivity> activities =
        [
          new() { Date = today },
          new() { Date = today.AddDays(-1) },
          new() { Date = today.AddDays(-2) },
          new() { Date = today.AddDays(-3) }
          ];

      // Act
      (int CurrentStreak, int LongestStreak) result = calculator.CalculateStreak(activities, today);

      // Assert
      Assert.That(result.CurrentStreak, Is.EqualTo(4));
      Assert.That(result.LongestStreak, Is.EqualTo(4));
    }

    [Test]
    public void CalculateStreak_WithGapInActivities_ResetsCurrentStreak()
    {
      // Arrange
      StreakCalculator calculator = new();
      DateOnly today = new(2024, 1, 15);
      List<DailyActivity> activities =
        [
          new() { Date = today },
          new() { Date = today.AddDays(-1) },
          // Gap here (missing -2)
          new() { Date = today.AddDays(-3) },
          new() { Date = today.AddDays(-4) }
          ];

      // Act
      (int CurrentStreak, int LongestStreak) result = calculator.CalculateStreak(activities, today);

      // Assert
      Assert.That(result.CurrentStreak, Is.EqualTo(2)); // Only today and yesterday
      Assert.That(result.LongestStreak, Is.EqualTo(2)); // The earlier streak of 2 days
    }

    [Test]
    public void CalculateStreak_WithMultipleStreaks_FindsLongestStreak()
    {
      // Arrange
      StreakCalculator calculator = new();
      DateOnly today = new(2024, 1, 15);
      List<DailyActivity> activities =
        [
          new() { Date = today },
          new() { Date = today.AddDays(-1) },
          // Gap
          new() { Date = today.AddDays(-5) },
          new() { Date = today.AddDays(-6) },
          new() { Date = today.AddDays(-7) },
          new() { Date = today.AddDays(-8) }
          ];

      // Act
      (int CurrentStreak, int LongestStreak) result = calculator.CalculateStreak(activities, today);

      // Assert
      Assert.That(result.CurrentStreak, Is.EqualTo(2)); // Current streak
      Assert.That(result.LongestStreak, Is.EqualTo(4)); // The earlier 4-day streak
    }

    [Test]
    public void CalculateStreak_WithNoActivities_ReturnsZero()
    {
      // Arrange
      StreakCalculator calculator = new();
      List<DailyActivity> activities = [];
      DateOnly today = new(2024, 1, 15);

      // Act
      (int CurrentStreak, int LongestStreak) result = calculator.CalculateStreak(activities, today);

      // Assert
      Assert.That(result.CurrentStreak, Is.EqualTo(0));
      Assert.That(result.LongestStreak, Is.EqualTo(0));
    }

    [Test]
    public void CalculateStreak_WithSingleActivityToday_ReturnsCurrentStreakOfOne()
    {
      // Arrange
      StreakCalculator calculator = new();
      DateOnly today = new(2024, 1, 15);
      List<DailyActivity> activities = [new() { Date = today }];

      // Act
      (int CurrentStreak, int LongestStreak) result = calculator.CalculateStreak(activities, today);

      // Assert
      Assert.That(result.CurrentStreak, Is.EqualTo(1));
      Assert.That(result.LongestStreak, Is.EqualTo(1));
    }
  }