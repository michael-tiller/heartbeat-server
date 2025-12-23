using FsCheck;
using FsCheck.Fluent;

namespace Heartbeat.Domain.Tests ;

  /// <summary>
  ///   Custom FsCheck generators for types not automatically handled.
  ///   FsCheck discovers generators by looking for methods named after the type.
  ///   When using [Property(Arbitrary = new[] { typeof(Generators) })], FsCheck
  ///   automatically discovers Arbitrary&lt;T&gt; methods in this class.
  /// </summary>
  public static class Generators
  {
    /// <summary>
    ///   Generator for DateOnly values.
    ///   Generates dates within a reasonable range (1-9999 years).
    /// </summary>
    public static Arbitrary<DateOnly> DateOnly()
    {
      return Arb.From(
        Gen.Choose(1, 9999)
          .SelectMany(year =>
            Gen.Choose(1, 12)
              .SelectMany(month =>
                Gen.Choose(1, DateTime.DaysInMonth(year, month))
                  .Select(day => new DateOnly(year, month, day))
              )
          )
        );
    }

    /// <summary>
    ///   Generator for DailyActivity objects.
    ///   Ensures all properties are properly initialized.
    /// </summary>
    public static Arbitrary<DailyActivity> DailyActivity()
    {
      return Arb.From(
        from date in DateOnly().Generator
        select new DailyActivity
        {
          Date = date
        }
        );
    }
  }