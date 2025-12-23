# Heartbeat Domain Testing Guide

**Stack:** NUnit + FsCheck + NSubstitute  
**Target:** Deterministic domain logic  
**Philosophy:** Property-first. Zero ceremony. Zero flakiness.

**Scope:**  
Applies **only** to domain tests (`domain.tests/`).  
Integration tests (`server.tests/`) are HTTP and persistence focused and explicitly **out of scope**.

---

## NON-NEGOTIABLE PRINCIPLES

1. **Properties first**  
   If behavior can be expressed as an invariant, it must be a property test.

2. **Properties return `bool`**  
   No `ToProperty()`.  
   No `Prop.ForAll()` unless a custom generator requires it.

3. **NUnit is the runner**  
   FsCheck integrates exclusively via `FsCheck.NUnit`.

4. **Mocks only at seams**  
   NSubstitute is allowed **only** for interfaces representing:
   - Time
   - Persistence
   - External messaging

   Never mock pure logic or value objects.

5. **Absolute determinism**  
   Domain tests must not use:
   - `DateTime.Now` / `UtcNow`
   - `Guid.NewGuid`
   - Randomness
   - IO

   All variability must be injected.

---

## REQUIRED PACKAGES

`domain.tests/Heartbeat.Domain.Tests.csproj`

```xml
<PackageReference Include="NUnit" Version="4.4.0" />
<PackageReference Include="NUnit3TestAdapter" Version="6.0.1" />
<PackageReference Include="FsCheck" Version="3.0.0" />
<PackageReference Include="FsCheck.NUnit" Version="3.0.0" />
<PackageReference Include="NSubstitute" Version="5.3.0" />
```

No additional test frameworks permitted.

---

## PROPERTY TEST SHAPE (CANONICAL)

FsCheck.NUnit treats `bool` returns as properties automatically.

**Location:** `domain.tests/src/{Entity}PropertyTests.cs`  
**Namespace:** `Heartbeat.Domain.Tests`

```csharp
using FsCheck.NUnit;
using NUnit.Framework;
using Heartbeat.Domain;

namespace Heartbeat.Domain.Tests;

[TestFixture]
public sealed class StreakCalculatorPropertyTests
{
    [Property(Arbitrary = new[] { typeof(Generators) })]
    public bool LongestStreak_IsAlwaysGreaterThanOrEqualToCurrentStreak(
        List<DailyActivity> activities,
        DateOnly today)
    {
        var calculator = new StreakCalculator();
        var result = calculator.CalculateStreak(activities, today);
        return result.LongestStreak >= result.CurrentStreak;
    }
}
```

Rules:
- No assertions
- No fluent APIs
- Return the invariant directly

---

## PROPERTIES WITH PRECONDITIONS

Use implication (`==>`) to constrain invalid input space.

```csharp
[Property(Arbitrary = new[] { typeof(Generators) })]
public bool Streak_values_are_non_negative(List<DailyActivity> activities, DateOnly today)
{
    var calculator = new StreakCalculator();
    var result = calculator.CalculateStreak(activities, today);
    
    return (result.CurrentStreak >= 0) && (result.LongestStreak >= 0);
}
```

Do not sanitize inputs unless the domain explicitly allows it.

---

## CUSTOM GENERATORS

Introduce **only when primitives are insufficient**.

Valid reasons:
- `DateOnly`
- Domain objects with structural invariants

**Location:** `domain.tests/src/Generators.cs`

```csharp
using FsCheck;

namespace Heartbeat.Domain.Tests;

public static class Generators
{
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
}
```

**Usage:**

```csharp
[Property(Arbitrary = new[] { typeof(Generators) })]
public bool DateOnly_is_valid(DateOnly date)
{
    return date.Year >= 1;
}
```

Generators must never weaken invariants to make tests pass.

---

## EXAMPLE-BASED TESTS (SECONDARY)

Used only for:
- Edge cases
- Known regressions
- FsCheck shrink results worth pinning

**Location:** `domain.tests/src/{Entity}Tests.cs`

```csharp
using NUnit.Framework;
using Heartbeat.Domain;

namespace Heartbeat.Domain.Tests;

[TestFixture]
public sealed class UserTests
{
    [Test]
    public void GeneratePairCode_ReturnsSixCharacterCode()
    {
        var code = User.GeneratePairCode();
        Assert.That(code.Length, Is.EqualTo(6));
    }
}
```

Example tests do not replace properties.

---

## NSUBSTITUTE RULES

**Allowed:**

```csharp
[Test]
public void Service_uses_time_provider()
{
    var timeProvider = Substitute.For<ITimeProvider>();
    timeProvider.UtcNow.Returns(new DateTime(2024, 1, 15));
    
    var service = new SomeService(timeProvider);
    service.DoSomething();
    
    timeProvider.Received(1).UtcNow;
}
```

**Forbidden:**
- Mocking value objects
- Mocking pure functions
- Verifying call order
- Verifying internal implementation details

---

## FILE STRUCTURE (ENFORCED)

```
domain.tests/
  src/
    DailyActivityPropertyTests.cs
    DailyActivityTests.cs
    StreakCalculatorPropertyTests.cs
    StreakCalculatorTests.cs
    UserPropertyTests.cs
    UserTests.cs
    Generators.cs
  Heartbeat.Domain.Tests.csproj
```

Naming:
- `{Entity}PropertyTests.cs`
- `{Entity}Tests.cs`
- One concept per file. No god tests.

---

## FAILURE DISCIPLINE

- A failing property blocks the branch.
- Shrunk counterexamples are promoted to example tests.
- Do not weaken generators to achieve green builds.
- Never test private members. Public contracts only.

---

## CURSOR / AI GENERATION RULES

Hard constraints:
- Generate **property tests first**
- Properties return `bool`
- Never emit `.ToProperty()`
- NUnit attributes only
- NSubstitute only at interface boundaries
- No nondeterministic APIs
- Enforce file placement and naming
- Namespace must be `Heartbeat.Domain.Tests`

This document is the contract.
