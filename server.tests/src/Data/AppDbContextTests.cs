using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Heartbeat.Domain;

namespace Heartbeat.Server.Tests.Data;

/// <summary>
/// Tests for AppDbContext schema constraints.
/// Verifies unique indexes, cascade deletes, and required fields.
/// </summary>
[TestFixture]
public class AppDbContextTests
{
    private SqliteConnection _connection = null!;
    private AppDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new AppDbContext(options);
        _context.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Close();
    }

    #region User Unique Constraints

    [Test]
    public async Task User_DuplicateDeviceId_ThrowsDbUpdateException()
    {
        // Arrange
        var user1 = new User { DeviceId = "device-123", PairCode = User.GeneratePairCode() };
        var user2 = new User { DeviceId = "device-123", PairCode = User.GeneratePairCode() }; // Same DeviceId

        _context.Users.Add(user1);
        await _context.SaveChangesAsync();

        // Act & Assert
        _context.Users.Add(user2);
        Assert.ThrowsAsync<DbUpdateException>(async () => await _context.SaveChangesAsync());
    }

    [Test]
    public async Task User_DuplicatePairCode_ThrowsDbUpdateException()
    {
        // Arrange
        var sharedPairCode = "ABC123";
        var user1 = new User { DeviceId = "device-1", PairCode = sharedPairCode };
        var user2 = new User { DeviceId = "device-2", PairCode = sharedPairCode }; // Same PairCode

        _context.Users.Add(user1);
        await _context.SaveChangesAsync();

        // Act & Assert
        _context.Users.Add(user2);
        Assert.ThrowsAsync<DbUpdateException>(async () => await _context.SaveChangesAsync());
    }

    [Test]
    public async Task User_UniqueDeviceIds_Succeeds()
    {
        // Arrange
        var user1 = new User { DeviceId = "device-1", PairCode = User.GeneratePairCode() };
        var user2 = new User { DeviceId = "device-2", PairCode = User.GeneratePairCode() };

        // Act
        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        // Assert
        Assert.That(await _context.Users.CountAsync(), Is.EqualTo(2));
    }

    #endregion

    #region DailyActivity Unique Constraints

    [Test]
    public async Task DailyActivity_DuplicateUserIdAndDate_ThrowsDbUpdateException()
    {
        // Arrange
        var user = new User { DeviceId = "device-123", PairCode = User.GeneratePairCode() };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var activity1 = new DailyActivity { UserId = user.Id, Date = today };
        var activity2 = new DailyActivity { UserId = user.Id, Date = today }; // Same user + date

        _context.DailyActivities.Add(activity1);
        await _context.SaveChangesAsync();

        // Act & Assert
        _context.DailyActivities.Add(activity2);
        Assert.ThrowsAsync<DbUpdateException>(async () => await _context.SaveChangesAsync());
    }

    [Test]
    public async Task DailyActivity_SameUserDifferentDates_Succeeds()
    {
        // Arrange
        var user = new User { DeviceId = "device-123", PairCode = User.GeneratePairCode() };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);

        // Act
        _context.DailyActivities.Add(new DailyActivity { UserId = user.Id, Date = today });
        _context.DailyActivities.Add(new DailyActivity { UserId = user.Id, Date = yesterday });
        await _context.SaveChangesAsync();

        // Assert
        Assert.That(await _context.DailyActivities.CountAsync(), Is.EqualTo(2));
    }

    [Test]
    public async Task DailyActivity_DifferentUsersSameDate_Succeeds()
    {
        // Arrange
        var user1 = new User { DeviceId = "device-1", PairCode = User.GeneratePairCode() };
        var user2 = new User { DeviceId = "device-2", PairCode = User.GeneratePairCode() };
        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Act
        _context.DailyActivities.Add(new DailyActivity { UserId = user1.Id, Date = today });
        _context.DailyActivities.Add(new DailyActivity { UserId = user2.Id, Date = today });
        await _context.SaveChangesAsync();

        // Assert
        Assert.That(await _context.DailyActivities.CountAsync(), Is.EqualTo(2));
    }

    #endregion

    #region Cascade Delete

    [Test]
    public async Task User_Delete_CascadesToDailyActivities()
    {
        // Arrange
        var user = new User { DeviceId = "device-123", PairCode = User.GeneratePairCode() };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _context.DailyActivities.Add(new DailyActivity { UserId = user.Id, Date = today });
        _context.DailyActivities.Add(new DailyActivity { UserId = user.Id, Date = today.AddDays(-1) });
        await _context.SaveChangesAsync();

        Assert.That(await _context.DailyActivities.CountAsync(), Is.EqualTo(2));

        // Act
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        // Assert - activities should be deleted
        Assert.That(await _context.DailyActivities.CountAsync(), Is.EqualTo(0));
    }

    [Test]
    public async Task User_Delete_DoesNotAffectOtherUsersActivities()
    {
        // Arrange
        var user1 = new User { DeviceId = "device-1", PairCode = User.GeneratePairCode() };
        var user2 = new User { DeviceId = "device-2", PairCode = User.GeneratePairCode() };
        _context.Users.AddRange(user1, user2);
        await _context.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        _context.DailyActivities.Add(new DailyActivity { UserId = user1.Id, Date = today });
        _context.DailyActivities.Add(new DailyActivity { UserId = user2.Id, Date = today });
        await _context.SaveChangesAsync();

        // Act - delete user1
        _context.Users.Remove(user1);
        await _context.SaveChangesAsync();

        // Assert - only user2's activity remains
        Assert.That(await _context.DailyActivities.CountAsync(), Is.EqualTo(1));
        Assert.That(await _context.DailyActivities.AnyAsync(a => a.UserId == user2.Id), Is.True);
    }

    #endregion

    #region Foreign Key Constraints

    [Test]
    public void DailyActivity_InvalidUserId_ThrowsDbUpdateException()
    {
        // Arrange - activity with non-existent user
        var activity = new DailyActivity { UserId = 9999, Date = DateOnly.FromDateTime(DateTime.UtcNow) };

        // Act & Assert
        _context.DailyActivities.Add(activity);
        Assert.ThrowsAsync<DbUpdateException>(async () => await _context.SaveChangesAsync());
    }

    #endregion
}

