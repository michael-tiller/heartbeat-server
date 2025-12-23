using Heartbeat.Server.Middleware;

namespace Heartbeat.Server.Tests.Middleware ;

  /// <summary>
  ///   Unit tests for ApiKeySettings.IsValidApiKey logic.
  ///   These are pure unit tests that do not require a web host.
  /// </summary>
  [TestFixture]
  public class ApiKeySettingsTests
  {
    private const string ValidKey1 = "test-key-12345";
    private const string ValidKey2 = "test-key-67890";
    private const string InvalidKey = "wrong-key";

    [Test]
    public void IsValidApiKey_CaseMismatch_Fails()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1]
      };

      // Act
      bool result = settings.IsValidApiKey(ValidKey1.ToUpperInvariant());

      // Assert - keys are case-sensitive
      Assert.That(result, Is.False);
    }

    [Test]
    public void IsValidApiKey_EmptyList_Fails()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = []
      };

      // Act
      bool result = settings.IsValidApiKey(ValidKey1);

      // Assert
      Assert.That(result, Is.False);
    }

    [Test]
    public void IsValidApiKey_EmptyString_Fails()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1]
      };

      // Act
      bool result = settings.IsValidApiKey(string.Empty);

      // Assert
      Assert.That(result, Is.False);
    }

    [Test]
    public void IsValidApiKey_ExactMatch_Succeeds()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1]
      };

      // Act
      bool result = settings.IsValidApiKey(ValidKey1);

      // Assert
      Assert.That(result, Is.True);
    }

    [Test]
    public void IsValidApiKey_MultipleKeys_FirstKeySucceeds()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1,ValidKey2]
      };

      // Act
      bool result = settings.IsValidApiKey(ValidKey1);

      // Assert
      Assert.That(result, Is.True);
    }

    [Test]
    public void IsValidApiKey_MultipleKeys_InvalidKeyFails()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1,ValidKey2]
      };

      // Act
      bool result = settings.IsValidApiKey(InvalidKey);

      // Assert
      Assert.That(result, Is.False);
    }

    [Test]
    public void IsValidApiKey_MultipleKeys_SecondKeySucceeds()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1,ValidKey2]
      };

      // Act
      bool result = settings.IsValidApiKey(ValidKey2);

      // Assert
      Assert.That(result, Is.True);
    }

    [Test]
    public void IsValidApiKey_NullKey_Fails()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1]
      };

      // Act
      bool result = settings.IsValidApiKey(null!);

      // Assert
      Assert.That(result, Is.False);
    }

    [Test]
    public void IsValidApiKey_PartialMatch_Fails()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1]
      };

      // Act - key is a substring but not exact match
      bool result = settings.IsValidApiKey(ValidKey1.Substring(0, 5));

      // Assert
      Assert.That(result, Is.False);
    }

    [Test]
    public void IsValidApiKey_WhitespaceKey_Fails()
    {
      // Arrange
      ApiKeySettings settings = new()
      {
        Keys = [ValidKey1]
      };

      // Act
      bool result = settings.IsValidApiKey("   ");

      // Assert
      Assert.That(result, Is.False);
    }
  }