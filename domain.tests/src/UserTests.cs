using NUnit.Framework;
using Heartbeat.Domain;

namespace Heartbeat.Domain.Tests;

[TestFixture]
public class UserTests
{
    [Test]
    public void GeneratePairCode_ReturnsSixCharacterCode()
    {
        // Act
        var code = User.GeneratePairCode();

        // Assert
        Assert.That(code.Length, Is.EqualTo(6));
    }
}
