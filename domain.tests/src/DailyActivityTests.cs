using NUnit.Framework;
using Heartbeat.Domain;

namespace Heartbeat.Domain.Tests;

[TestFixture]
public class DailyActivityTests
{
    [Test]
    public void DailyActivity_DefaultValuesAreSetCorrectly()
    {
        // Arrange & Act
        var activity = new DailyActivity();

        // Assert
        Assert.That(activity.UpdatedAt, Is.Not.EqualTo(default(DateTime)));
    }
}

