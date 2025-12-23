using Heartbeat.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Heartbeat.Server.Tests.Data ;

  /// <summary>
  ///   Tests for AppDbContext schema constraints.
  ///   Verifies unique indexes, cascade deletes, and required fields.
  /// </summary>
  [TestFixture]
  public class AppDbContextTests
  {
    #region Setup/Teardown

    [SetUp]
    public void SetUp()
    {
      connection = new SqliteConnection("DataSource=:memory:");
      connection.Open();

      DbContextOptions<AppDbContext> options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

      context = new AppDbContext(options);
      context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
      context.Dispose();
      connection.Close();
    }

    #endregion

    private SqliteConnection connection = null!;
    private AppDbContext context = null!;

    [Test]
    public async Task DailyActivity_DifferentUsersSameDate_Succeeds()
    {
      // Arrange
      User user1 = new() { DeviceId = "device-1", PairCode = User.GeneratePairCode() };
      User user2 = new() { DeviceId = "device-2", PairCode = User.GeneratePairCode() };
      context.Users.AddRange(user1, user2);
      await context.SaveChangesAsync();

      DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

      // Act
      context.DailyActivities.Add(new DailyActivity { UserId = user1.Id, Date = today });
      context.DailyActivities.Add(new DailyActivity { UserId = user2.Id, Date = today });
      await context.SaveChangesAsync();

      // Assert
      Assert.That(await context.DailyActivities.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task DailyActivity_DuplicateUserIdAndDate_ThrowsDbUpdateException()
    {
      // Arrange
      User user = new() { DeviceId = "device-123", PairCode = User.GeneratePairCode() };
      context.Users.Add(user);
      await context.SaveChangesAsync();

      DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
      DailyActivity activity1 = new() { UserId = user.Id, Date = today };
      DailyActivity activity2 = new() { UserId = user.Id, Date = today }; // Same user + date

      context.DailyActivities.Add(activity1);
      await context.SaveChangesAsync();

      // Act & Assert
      context.DailyActivities.Add(activity2);
      Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
    }

    [Test]
    public void DailyActivity_InvalidUserId_ThrowsDbUpdateException()
    {
      // Arrange - activity with non-existent user
      DailyActivity activity = new() { UserId = 9999, Date = DateOnly.FromDateTime(DateTime.UtcNow) };

      // Act & Assert
      context.DailyActivities.Add(activity);
      Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
    }

    [Test]
    public async Task DailyActivity_SameUserDifferentDates_Succeeds()
    {
      // Arrange
      User user = new() { DeviceId = "device-123", PairCode = User.GeneratePairCode() };
      context.Users.Add(user);
      await context.SaveChangesAsync();

      DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
      DateOnly yesterday = today.AddDays(-1);

      // Act
      context.DailyActivities.Add(new DailyActivity { UserId = user.Id, Date = today });
      context.DailyActivities.Add(new DailyActivity { UserId = user.Id, Date = yesterday });
      await context.SaveChangesAsync();

      // Assert
      Assert.That(await context.DailyActivities.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task User_Delete_CascadesToDailyActivities()
    {
      // Arrange
      User user = new() { DeviceId = "device-123", PairCode = User.GeneratePairCode() };
      context.Users.Add(user);
      await context.SaveChangesAsync();

      DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
      context.DailyActivities.Add(new DailyActivity { UserId = user.Id, Date = today });
      context.DailyActivities.Add(new DailyActivity { UserId = user.Id, Date = today.AddDays(-1) });
      await context.SaveChangesAsync();

      Assert.That(await context.DailyActivities.CountAsync(), Is.EqualTo(2));

      // Act
      context.Users.Remove(user);
      await context.SaveChangesAsync();

      // Assert - activities should be deleted
      Assert.That(await context.DailyActivities.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task User_Delete_DoesNotAffectOtherUsersActivities()
    {
      // Arrange
      User user1 = new() { DeviceId = "device-1", PairCode = User.GeneratePairCode() };
      User user2 = new() { DeviceId = "device-2", PairCode = User.GeneratePairCode() };
      context.Users.AddRange(user1, user2);
      await context.SaveChangesAsync();

      DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
      context.DailyActivities.Add(new DailyActivity { UserId = user1.Id, Date = today });
      context.DailyActivities.Add(new DailyActivity { UserId = user2.Id, Date = today });
      await context.SaveChangesAsync();

      // Act - delete user1
      context.Users.Remove(user1);
      await context.SaveChangesAsync();

      // Assert - only user2's activity remains
      Assert.That(await context.DailyActivities.CountAsync(), Is.EqualTo(1));
      Assert.That(await context.DailyActivities.AnyAsync(a => a.UserId == user2.Id), Is.True);
    }

    [Test]
    public async Task User_DuplicateDeviceId_ThrowsDbUpdateException()
    {
      // Arrange
      User user1 = new() { DeviceId = "device-123", PairCode = User.GeneratePairCode() };
      User user2 = new() { DeviceId = "device-123", PairCode = User.GeneratePairCode() }; // Same DeviceId

      context.Users.Add(user1);
      await context.SaveChangesAsync();

      // Act & Assert
      context.Users.Add(user2);
      Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
    }

    [Test]
    public async Task User_DuplicatePairCode_ThrowsDbUpdateException()
    {
      // Arrange
      string sharedPairCode = "ABC123";
      User user1 = new() { DeviceId = "device-1", PairCode = sharedPairCode };
      User user2 = new() { DeviceId = "device-2", PairCode = sharedPairCode }; // Same PairCode

      context.Users.Add(user1);
      await context.SaveChangesAsync();

      // Act & Assert
      context.Users.Add(user2);
      Assert.ThrowsAsync<DbUpdateException>(async () => await context.SaveChangesAsync());
    }

    [Test]
    public async Task User_UniqueDeviceIds_Succeeds()
    {
      // Arrange
      User user1 = new() { DeviceId = "device-1", PairCode = User.GeneratePairCode() };
      User user2 = new() { DeviceId = "device-2", PairCode = User.GeneratePairCode() };

      // Act
      context.Users.AddRange(user1, user2);
      await context.SaveChangesAsync();

      // Assert
      Assert.That(await context.Users.CountAsync(), Is.EqualTo(2));
    }
  }