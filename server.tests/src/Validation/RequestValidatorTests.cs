using Heartbeat.Server.Exceptions;
using Heartbeat.Server.Validation;

namespace Heartbeat.Server.Tests.Validation ;

  /// <summary>
  ///   Unit tests for RequestValidator.
  ///   Tests all validation paths, rejection cases, and sanitization behavior.
  /// </summary>
  [TestFixture]
  public class RequestValidatorTests
  {
    [Test]
    public void Sanitize_EmptyString_ReturnsEmptyString()
    {
      // Act
      string result = RequestValidator.Sanitize("");

      // Assert
      Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Sanitize_InternalWhitespace_Preserved()
    {
      // Act
      string result = RequestValidator.Sanitize("hello world");

      // Assert
      Assert.That(result, Is.EqualTo("hello world"));
    }

    [Test]
    public void Sanitize_LeadingAndTrailingWhitespace_Trimmed()
    {
      // Act
      string result = RequestValidator.Sanitize("   hello   ");

      // Assert
      Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_LeadingWhitespace_Trimmed()
    {
      // Act
      string result = RequestValidator.Sanitize("   hello");

      // Assert
      Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_NormalString_Unchanged()
    {
      // Act
      string result = RequestValidator.Sanitize("hello");

      // Assert
      Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_Null_ReturnsEmptyString()
    {
      // Act
      string result = RequestValidator.Sanitize(null);

      // Assert
      Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Sanitize_TabsAndNewlines_TrimmedAtEnds()
    {
      // Act
      string result = RequestValidator.Sanitize("\t\nhello\r\n");

      // Assert
      Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_TrailingWhitespace_Trimmed()
    {
      // Act
      string result = RequestValidator.Sanitize("hello   ");

      // Assert
      Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_WhitespaceOnly_ReturnsEmptyString()
    {
      // Act
      string result = RequestValidator.Sanitize("   ");

      // Assert
      Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void ValidateDeviceId_ContainsCarriageReturn_ThrowsValidationException()
    {
      // Arrange
      string idWithCr = "device\rid12345";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(idWithCr));
      Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    [Test]
    public void ValidateDeviceId_ContainsNewline_ThrowsValidationException()
    {
      // Arrange
      string idWithNewline = "device\nid12345";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(idWithNewline));
      Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    [Test]
    public void ValidateDeviceId_ContainsNullChar_ThrowsValidationException()
    {
      // Arrange
      string idWithNull = "device\0id12345";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(idWithNull));
      Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    [Test]
    public void ValidateDeviceId_ContainsTab_ThrowsValidationException()
    {
      // Arrange
      string idWithTab = "device\tid12345";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(idWithTab));
      Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    [Test]
    public void ValidateDeviceId_Empty_ThrowsValidationException()
    {
      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(""));
      Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidateDeviceId_ExactlyMaxLength_DoesNotThrow()
    {
      // Arrange - exactly 256 characters
      string validId = new('a', 256);

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_ExactlyMinLength_DoesNotThrow()
    {
      // Arrange - exactly 8 characters
      string validId = "12345678";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_Null_ThrowsValidationException()
    {
      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(null));
      Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidateDeviceId_TooLong_ThrowsValidationException()
    {
      // Arrange - more than 256 characters
      string longId = new('a', 257);

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(longId));
      Assert.That(ex!.Message, Does.Contain("not exceed"));
      Assert.That(ex!.Message, Does.Contain("256"));
    }

    [Test]
    public void ValidateDeviceId_TooShort_ThrowsValidationException()
    {
      // Arrange - less than 8 characters
      string shortId = "short";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(shortId));
      Assert.That(ex!.Message, Does.Contain("at least"));
      Assert.That(ex!.Message, Does.Contain("8"));
    }

    [Test]
    public void ValidateDeviceId_ValidSimpleId_DoesNotThrow()
    {
      // Arrange
      string validId = "valid-device-id-12345";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_ValidUuid_DoesNotThrow()
    {
      // Arrange
      string validId = "550e8400-e29b-41d4-a716-446655440000";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_ValidWithSpecialChars_DoesNotThrow()
    {
      // Arrange - special chars that are NOT control chars
      string validId = "device_id-with.special@chars!";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_ValidWithUnicode_DoesNotThrow()
    {
      // Arrange - unicode characters are allowed
      string validId = "device-日本語-12345";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_WhitespaceOnly_ThrowsValidationException()
    {
      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId("   "));
      Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidatePairCode_AllAllowedDigits_DoesNotThrow()
    {
      // Arrange - using allowed digits only (2-9)
      string validCode = "234567";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidatePairCode(validCode));
    }

    [Test]
    public void ValidatePairCode_AllAllowedLetters_DoesNotThrow()
    {
      // Arrange - using allowed letters only (A-H, J-N, P-Z)
      string validCode = "AHJNPZ";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidatePairCode(validCode));
    }

    [Test]
    public void ValidatePairCode_ContainsConfusingChars_0_ThrowsValidationException()
    {
      // Arrange - 0 (zero) is excluded
      string codeWith0 = "ABCD01";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWith0));
      Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsConfusingChars_1_ThrowsValidationException()
    {
      // Arrange - 1 (one) is excluded
      string codeWith1 = "ABCD12";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWith1));
      Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsConfusingChars_I_ThrowsValidationException()
    {
      // Arrange - I (letter I) is excluded to avoid confusion with 1
      string codeWithI = "ABCDI2";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWithI));
      Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsConfusingChars_O_ThrowsValidationException()
    {
      // Arrange - O (letter O) is excluded to avoid confusion with 0
      string codeWithO = "ABCDO1";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWithO));
      Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsSpecialChars_ThrowsValidationException()
    {
      // Arrange
      string codeWithSpecial = "ABC-23";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWithSpecial));
      Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_Empty_ThrowsValidationException()
    {
      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(""));
      Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidatePairCode_Lowercase_ThrowsValidationException()
    {
      // Arrange - lowercase not allowed
      string lowercaseCode = "abc123";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(lowercaseCode));
      Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_MixedAllowedChars_DoesNotThrow()
    {
      // Arrange
      string validCode = "XY7Z89";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidatePairCode(validCode));
    }

    [Test]
    public void ValidatePairCode_Null_ThrowsValidationException()
    {
      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(null));
      Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidatePairCode_TooLong_ThrowsValidationException()
    {
      // Arrange - more than 6 characters
      string longCode = "ABC1234";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(longCode));
      Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_TooShort_ThrowsValidationException()
    {
      // Arrange - less than 6 characters
      string shortCode = "ABC12";

      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(shortCode));
      Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ValidFormat_DoesNotThrow()
    {
      // Arrange - valid: uppercase A-H, J-N, P-Z and 2-9
      string validCode = "ABC234";

      // Act & Assert
      Assert.DoesNotThrow(() => RequestValidator.ValidatePairCode(validCode));
    }

    [Test]
    public void ValidatePairCode_WhitespaceOnly_ThrowsValidationException()
    {
      // Act & Assert
      ValidationException? ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode("   "));
      Assert.That(ex!.Message, Does.Contain("required"));
    }
  }