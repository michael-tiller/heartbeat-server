namespace Heartbeat.Domain.Tests ;

  [TestFixture]
  public class DailyActivityTests
  {
    [Test]
    public void DailyActivity_DefaultValuesAreSetCorrectly()
    {
      // Arrange & Act
      DailyActivity activity = new();

      // Assert
      Assert.That(activity.UpdatedAt, Is.Not.EqualTo(default(DateTime)));
    }
  }