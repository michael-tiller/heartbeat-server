using Heartbeat.Server.Exceptions;
using Heartbeat.Server.Validation;

namespace Heartbeat.Server.Tests.Validation;

/// <summary>
/// Unit tests for RequestValidator.
/// Tests all validation paths, rejection cases, and sanitization behavior.
/// </summary>
[TestFixture]
public class RequestValidatorTests
{
    #region ValidateDeviceId - Rejection Cases

    [Test]
    public void ValidateDeviceId_Null_ThrowsValidationException()
    {
        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(null));
        Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidateDeviceId_Empty_ThrowsValidationException()
    {
        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(""));
        Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidateDeviceId_WhitespaceOnly_ThrowsValidationException()
    {
        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId("   "));
        Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidateDeviceId_TooShort_ThrowsValidationException()
    {
        // Arrange - less than 8 characters
        var shortId = "short";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(shortId));
        Assert.That(ex!.Message, Does.Contain("at least"));
        Assert.That(ex!.Message, Does.Contain("8"));
    }

    [Test]
    public void ValidateDeviceId_ExactlyMinLength_DoesNotThrow()
    {
        // Arrange - exactly 8 characters
        var validId = "12345678";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_TooLong_ThrowsValidationException()
    {
        // Arrange - more than 256 characters
        var longId = new string('a', 257);

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(longId));
        Assert.That(ex!.Message, Does.Contain("not exceed"));
        Assert.That(ex!.Message, Does.Contain("256"));
    }

    [Test]
    public void ValidateDeviceId_ExactlyMaxLength_DoesNotThrow()
    {
        // Arrange - exactly 256 characters
        var validId = new string('a', 256);

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_ContainsNullChar_ThrowsValidationException()
    {
        // Arrange
        var idWithNull = "device\0id12345";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(idWithNull));
        Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    [Test]
    public void ValidateDeviceId_ContainsNewline_ThrowsValidationException()
    {
        // Arrange
        var idWithNewline = "device\nid12345";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(idWithNewline));
        Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    [Test]
    public void ValidateDeviceId_ContainsTab_ThrowsValidationException()
    {
        // Arrange
        var idWithTab = "device\tid12345";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(idWithTab));
        Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    [Test]
    public void ValidateDeviceId_ContainsCarriageReturn_ThrowsValidationException()
    {
        // Arrange
        var idWithCr = "device\rid12345";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidateDeviceId(idWithCr));
        Assert.That(ex!.Message, Does.Contain("invalid characters"));
    }

    #endregion

    #region ValidateDeviceId - Accept Cases

    [Test]
    public void ValidateDeviceId_ValidSimpleId_DoesNotThrow()
    {
        // Arrange
        var validId = "valid-device-id-12345";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_ValidUuid_DoesNotThrow()
    {
        // Arrange
        var validId = "550e8400-e29b-41d4-a716-446655440000";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_ValidWithSpecialChars_DoesNotThrow()
    {
        // Arrange - special chars that are NOT control chars
        var validId = "device_id-with.special@chars!";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    [Test]
    public void ValidateDeviceId_ValidWithUnicode_DoesNotThrow()
    {
        // Arrange - unicode characters are allowed
        var validId = "device-日本語-12345";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidateDeviceId(validId));
    }

    #endregion

    #region ValidatePairCode - Rejection Cases

    [Test]
    public void ValidatePairCode_Null_ThrowsValidationException()
    {
        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(null));
        Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidatePairCode_Empty_ThrowsValidationException()
    {
        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(""));
        Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidatePairCode_WhitespaceOnly_ThrowsValidationException()
    {
        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode("   "));
        Assert.That(ex!.Message, Does.Contain("required"));
    }

    [Test]
    public void ValidatePairCode_TooShort_ThrowsValidationException()
    {
        // Arrange - less than 6 characters
        var shortCode = "ABC12";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(shortCode));
        Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_TooLong_ThrowsValidationException()
    {
        // Arrange - more than 6 characters
        var longCode = "ABC1234";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(longCode));
        Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_Lowercase_ThrowsValidationException()
    {
        // Arrange - lowercase not allowed
        var lowercaseCode = "abc123";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(lowercaseCode));
        Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsConfusingChars_O_ThrowsValidationException()
    {
        // Arrange - O (letter O) is excluded to avoid confusion with 0
        var codeWithO = "ABCDO1";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWithO));
        Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsConfusingChars_I_ThrowsValidationException()
    {
        // Arrange - I (letter I) is excluded to avoid confusion with 1
        var codeWithI = "ABCDI2";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWithI));
        Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsConfusingChars_0_ThrowsValidationException()
    {
        // Arrange - 0 (zero) is excluded
        var codeWith0 = "ABCD01";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWith0));
        Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsConfusingChars_1_ThrowsValidationException()
    {
        // Arrange - 1 (one) is excluded
        var codeWith1 = "ABCD12";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWith1));
        Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    [Test]
    public void ValidatePairCode_ContainsSpecialChars_ThrowsValidationException()
    {
        // Arrange
        var codeWithSpecial = "ABC-23";

        // Act & Assert
        var ex = Assert.Throws<ValidationException>(() => RequestValidator.ValidatePairCode(codeWithSpecial));
        Assert.That(ex!.Message, Does.Contain("Invalid pair code format"));
    }

    #endregion

    #region ValidatePairCode - Accept Cases

    [Test]
    public void ValidatePairCode_ValidFormat_DoesNotThrow()
    {
        // Arrange - valid: uppercase A-H, J-N, P-Z and 2-9
        var validCode = "ABC234";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidatePairCode(validCode));
    }

    [Test]
    public void ValidatePairCode_AllAllowedLetters_DoesNotThrow()
    {
        // Arrange - using allowed letters only (A-H, J-N, P-Z)
        var validCode = "AHJNPZ";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidatePairCode(validCode));
    }

    [Test]
    public void ValidatePairCode_AllAllowedDigits_DoesNotThrow()
    {
        // Arrange - using allowed digits only (2-9)
        var validCode = "234567";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidatePairCode(validCode));
    }

    [Test]
    public void ValidatePairCode_MixedAllowedChars_DoesNotThrow()
    {
        // Arrange
        var validCode = "XY7Z89";

        // Act & Assert
        Assert.DoesNotThrow(() => RequestValidator.ValidatePairCode(validCode));
    }

    #endregion

    #region Sanitize

    [Test]
    public void Sanitize_Null_ReturnsEmptyString()
    {
        // Act
        var result = RequestValidator.Sanitize(null);

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Sanitize_EmptyString_ReturnsEmptyString()
    {
        // Act
        var result = RequestValidator.Sanitize("");

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Sanitize_WhitespaceOnly_ReturnsEmptyString()
    {
        // Act
        var result = RequestValidator.Sanitize("   ");

        // Assert
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void Sanitize_LeadingWhitespace_Trimmed()
    {
        // Act
        var result = RequestValidator.Sanitize("   hello");

        // Assert
        Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_TrailingWhitespace_Trimmed()
    {
        // Act
        var result = RequestValidator.Sanitize("hello   ");

        // Assert
        Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_LeadingAndTrailingWhitespace_Trimmed()
    {
        // Act
        var result = RequestValidator.Sanitize("   hello   ");

        // Assert
        Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_InternalWhitespace_Preserved()
    {
        // Act
        var result = RequestValidator.Sanitize("hello world");

        // Assert
        Assert.That(result, Is.EqualTo("hello world"));
    }

    [Test]
    public void Sanitize_NormalString_Unchanged()
    {
        // Act
        var result = RequestValidator.Sanitize("hello");

        // Assert
        Assert.That(result, Is.EqualTo("hello"));
    }

    [Test]
    public void Sanitize_TabsAndNewlines_TrimmedAtEnds()
    {
        // Act
        var result = RequestValidator.Sanitize("\t\nhello\r\n");

        // Assert
        Assert.That(result, Is.EqualTo("hello"));
    }

    #endregion
}

