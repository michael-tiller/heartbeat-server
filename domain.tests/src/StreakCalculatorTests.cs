using NUnit.Framework;
using Heartbeat.Domain;

namespace Heartbeat.Domain.Tests;

[TestFixture]
public class StreakCalculatorTests
{
    [Test]
    public void CalculateStreak_WithNoActivities_ReturnsZero()
    {
        // Arrange
        var calculator = new StreakCalculator();
        var activities = new List<DailyActivity>();
        var today = new DateOnly(2024, 1, 15);

        // Act
        var result = calculator.CalculateStreak(activities, today);

        // Assert
        Assert.That(result.CurrentStreak, Is.EqualTo(0));
        Assert.That(result.LongestStreak, Is.EqualTo(0));
    }

    [Test]
    public void CalculateStreak_WithSingleActivityToday_ReturnsCurrentStreakOfOne()
    {
        // Arrange
        var calculator = new StreakCalculator();
        var today = new DateOnly(2024, 1, 15);
        var activities = new List<DailyActivity>
        {
            new() { Date = today }
        };

        // Act
        var result = calculator.CalculateStreak(activities, today);

        // Assert
        Assert.That(result.CurrentStreak, Is.EqualTo(1));
        Assert.That(result.LongestStreak, Is.EqualTo(1));
    }

    [Test]
    public void CalculateStreak_WithConsecutiveDays_CalculatesCurrentStreak()
    {
        // Arrange
        var calculator = new StreakCalculator();
        var today = new DateOnly(2024, 1, 15);
        var activities = new List<DailyActivity>
        {
            new() { Date = today },
            new() { Date = today.AddDays(-1) },
            new() { Date = today.AddDays(-2) },
            new() { Date = today.AddDays(-3) }
        };

        // Act
        var result = calculator.CalculateStreak(activities, today);

        // Assert
        Assert.That(result.CurrentStreak, Is.EqualTo(4));
        Assert.That(result.LongestStreak, Is.EqualTo(4));
    }

    [Test]
    public void CalculateStreak_WithGapInActivities_ResetsCurrentStreak()
    {
        // Arrange
        var calculator = new StreakCalculator();
        var today = new DateOnly(2024, 1, 15);
        var activities = new List<DailyActivity>
        {
            new() { Date = today },
            new() { Date = today.AddDays(-1) },
            // Gap here (missing -2)
            new() { Date = today.AddDays(-3) },
            new() { Date = today.AddDays(-4) }
        };

        // Act
        var result = calculator.CalculateStreak(activities, today);

        // Assert
        Assert.That(result.CurrentStreak, Is.EqualTo(2)); // Only today and yesterday
        Assert.That(result.LongestStreak, Is.EqualTo(2)); // The earlier streak of 2 days
    }

    [Test]
    public void CalculateStreak_WithMultipleStreaks_FindsLongestStreak()
    {
        // Arrange
        var calculator = new StreakCalculator();
        var today = new DateOnly(2024, 1, 15);
        var activities = new List<DailyActivity>
        {
            new() { Date = today },
            new() { Date = today.AddDays(-1) },
            // Gap
            new() { Date = today.AddDays(-5) },
            new() { Date = today.AddDays(-6) },
            new() { Date = today.AddDays(-7) },
            new() { Date = today.AddDays(-8) }
        };

        // Act
        var result = calculator.CalculateStreak(activities, today);

        // Assert
        Assert.That(result.CurrentStreak, Is.EqualTo(2)); // Current streak
        Assert.That(result.LongestStreak, Is.EqualTo(4)); // The earlier 4-day streak
    }

    [Test]
    public void CalculateStreak_WithActivityInFuture_IgnoresFutureDates()
    {
        // Arrange
        var calculator = new StreakCalculator();
        var today = new DateOnly(2024, 1, 15);
        var activities = new List<DailyActivity>
        {
            new() { Date = today },
            new() { Date = today.AddDays(1) }, // Future date - should be ignored
            new() { Date = today.AddDays(-1) }
        };

        // Act
        var result = calculator.CalculateStreak(activities, today);

        // Assert
        // Future dates are invalid data and should be ignored for both current and longest streak
        Assert.That(result.CurrentStreak, Is.EqualTo(2)); // Today and yesterday only
        Assert.That(result.LongestStreak, Is.EqualTo(2)); // The sequence: yesterday, today (future date ignored)
    }
}
