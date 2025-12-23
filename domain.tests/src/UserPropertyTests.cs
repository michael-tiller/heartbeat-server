using FsCheck;
using FsCheck.NUnit;
using NUnit.Framework;
using Heartbeat.Domain;

using Property = FsCheck.NUnit.PropertyAttribute;

namespace Heartbeat.Domain.Tests;

/// <summary>
/// Property-based tests for User domain invariants.
/// These test rules that must never break in the domain model.
/// </summary>
[TestFixture]
public class UserPropertyTests
{
    /// <summary>
    /// Invariant: Pair codes are always generated with consistent format.
    /// All generated codes must be 6 characters and use only valid characters.
    /// </summary>
    [Property]
    public bool GeneratePairCode_AlwaysHasConsistentFormat()
    {
        const string validChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        const string confusingChars = "IO01";
        
        // Generate one code per property run - let FsCheck handle repetition
        var code = User.GeneratePairCode();
        
        var hasCorrectLength = code.Length == 6;
        var usesOnlyValidChars = code.All(ch => validChars.Contains(ch));
        var excludesConfusingChars = !code.Any(ch => confusingChars.Contains(ch));
        
        return hasCorrectLength && usesOnlyValidChars && excludesConfusingChars;
    }

}

