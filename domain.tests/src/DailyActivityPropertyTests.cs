using FsCheck.NUnit;
using Property = FsCheck.NUnit.PropertyAttribute;

namespace Heartbeat.Domain.Tests ;

  /// <summary>
  ///   Property-based tests for DailyActivity domain invariants.
  ///   These test rules that must never break in the domain model.
  ///   Uses custom FsCheck generators to provide DateOnly types
  ///   (FsCheck has no built-in generator for System.DateOnly).
  /// </summary>
  [TestFixture]
  public class DailyActivityPropertyTests
  {
    /// <summary>
    ///   Invariant: UpdatedAt is non-default on creation.
    ///   The default value ensures UpdatedAt is never uninitialized.
    /// </summary>
    [Property(Arbitrary = [ typeof(Generators) ])]
    public bool DailyActivity_UpdatedAtIsNonDefault(DateOnly date)
    {
      DailyActivity activity = new()
      {
        Date = date
      };

      return activity.UpdatedAt != default;
    }
  }