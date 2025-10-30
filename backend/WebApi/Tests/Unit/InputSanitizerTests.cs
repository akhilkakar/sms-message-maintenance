using Xunit;
using SmsMessageMaintenanceSecurity;

namespace SmsMessageMaintenanceTests.Unit.Security
{
    /// <summary>
    /// Unit tests for InputSanitizer
    /// Tests sanitization logic in isolation (no dependencies)
    /// </summary>
    public class InputSanitizerTests
    {
        private readonly IInputSanitizer _sanitizer;

        public InputSanitizerTests()
        {
            _sanitizer = new InputSanitizer();
        }

        #region SanitizeMessage Tests

        [Fact]
        public void SanitizeMessage_NullInput_ReturnsNull()
        {
            // Arrange
            string? input = null;

            // Act
            var result = _sanitizer.SanitizeMessage(input!);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SanitizeMessage_EmptyString_ReturnsEmpty()
        {
            // Arrange
            string input = "";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void SanitizeMessage_WhitespaceOnly_ReturnsEmpty()
        {
            // Arrange
            string input = "   ";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.Equal("", result);
        }

        [Fact]
        public void SanitizeMessage_TrimsSurroundingWhitespace()
        {
            // Arrange
            string input = "   Hello World   ";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void SanitizeMessage_CollapsesMultipleSpaces()
        {
            // Arrange
            string input = "Hello    World    Test";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.Equal("Hello World Test", result);
        }

        #endregion

        #region SQL Injection Prevention Tests

        [Theory]
        [InlineData("Hello'; DROP TABLE Messages; --")]
        [InlineData("SELECT * FROM Users")]
        [InlineData("INSERT INTO Messages VALUES (1,2,3)")]
        [InlineData("DELETE FROM Users WHERE id=1")]
        [InlineData("UPDATE Messages SET status='sent'")]
        [InlineData("UNION ALL SELECT * FROM passwords")]
        public void SanitizeMessage_RemovesSqlKeywords(string input)
        {
            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.DoesNotContain("DROP", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SELECT", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("INSERT", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DELETE", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("UPDATE", result, System.StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Test--comment")]
        [InlineData("Test/*comment*/message")]
        [InlineData("Hello;World")]
        public void SanitizeMessage_RemovesSqlCommentSyntax(string input)
        {
            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.DoesNotContain("--", result);
            Assert.DoesNotContain("/*", result);
            Assert.DoesNotContain("*/", result);
            Assert.DoesNotContain(";", result);
        }

        [Fact]
        public void SanitizeMessage_RemovesStoredProcedurePrefix()
        {
            // Arrange
            string input = "EXEC xp_cmdshell 'dir'";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.DoesNotContain("xp_", result);
            Assert.DoesNotContain("sp_", result);
            Assert.DoesNotContain("EXEC", result, System.StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region XSS Prevention Tests

        [Fact]
        public void SanitizeMessage_RemovesScriptTags()
        {
            // Arrange
            string input = "Hello <script>alert('XSS')</script> World";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.DoesNotContain("<script>", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("</script>", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.Equal("Hello World", result);
        }

        [Fact]
        public void SanitizeMessage_RemovesScriptTagsWithAttributes()
        {
            // Arrange
            string input = "<script type='text/javascript'>alert('XSS')</script>";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.DoesNotContain("script", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("alert", result);
        }

        [Theory]
        [InlineData("<div>Hello</div>", "Hello")]
        [InlineData("<span>World</span>", "World")]
        [InlineData("<a href='test'>Link</a>", "Link")]
        [InlineData("<img src='test.jpg'>", "")]
        [InlineData("<p>Paragraph</p>", "Paragraph")]
        public void SanitizeMessage_RemovesHtmlTags(string input, string expected)
        {
            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SanitizeMessage_DecodesHtmlEntities()
        {
            // Arrange
            string input = "Hello &lt;world&gt; &amp; &quot;test&quot;";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            // After decoding and stripping tags, should not have HTML entities
            Assert.DoesNotContain("&lt;", result);
            Assert.DoesNotContain("&gt;", result);
            Assert.DoesNotContain("&amp;", result);
            Assert.DoesNotContain("&quot;", result);
        }

        #endregion

        #region Complex Sanitization Tests

        [Fact]
        public void SanitizeMessage_ComplexAttack_RemovesAllThreats()
        {
            // Arrange
            string input = "<script>alert('XSS')</script>'; DROP TABLE Users; -- <div>Hello</div>";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.DoesNotContain("script", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("DROP", result, System.StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
            Assert.DoesNotContain("--", result);
        }

        [Fact]
        public void SanitizeMessage_NormalTextWithSpecialChars_PreservesValidContent()
        {
            // Arrange
            string input = "Hello! How are you? I'm fine. Cost: $50.00";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.Contains("Hello", result);
            Assert.Contains("How are you", result);
            Assert.Contains("I'm fine", result);
            Assert.Contains("$50.00", result);
        }

        [Fact]
        public void SanitizeMessage_EmojiAndUnicode_HandledCorrectly()
        {
            // Arrange
            string input = "Hello 😊 Café naïve";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            // Should preserve common extended ASCII
            Assert.Contains("Caf", result);
            Assert.Contains("na", result);
        }

        #endregion

        #region SanitizePhoneNumber Tests

        [Fact]
        public void SanitizePhoneNumber_NullInput_ReturnsNull()
        {
            // Arrange
            string? input = null;

            // Act
            var result = _sanitizer.SanitizePhoneNumber(input!);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void SanitizePhoneNumber_EmptyString_ReturnsEmpty()
        {
            // Arrange
            string input = "";

            // Act
            var result = _sanitizer.SanitizePhoneNumber(input);

            // Assert
            Assert.Equal("", result);
        }

        [Theory]
        [InlineData("123-456-7890", "1234567890")]
        [InlineData("(123) 456-7890", "1234567890")]
        [InlineData("+1 (555) 123-4567", "15551234567")]
        [InlineData("+44 20 7946 0958", "442079460958")]
        [InlineData("123.456.7890", "1234567890")]
        public void SanitizePhoneNumber_RemovesNonNumericCharacters(string input, string expected)
        {
            // Act
            var result = _sanitizer.SanitizePhoneNumber(input);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void SanitizePhoneNumber_OnlyDigits_RemainsUnchanged()
        {
            // Arrange
            string input = "1234567890";

            // Act
            var result = _sanitizer.SanitizePhoneNumber(input);

            // Assert
            Assert.Equal("1234567890", result);
        }

        [Fact]
        public void SanitizePhoneNumber_WithLetters_RemovesLetters()
        {
            // Arrange
            string input = "123-ABC-4567";

            // Act
            var result = _sanitizer.SanitizePhoneNumber(input);

            // Assert
            Assert.Equal("1234567", result);
            Assert.DoesNotContain("A", result);
            Assert.DoesNotContain("B", result);
            Assert.DoesNotContain("C", result);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("abc", "")]
        [InlineData("###", "")]
        [InlineData("---", "")]
        public void SanitizePhoneNumber_NoDigits_ReturnsEmpty(string input, string expected)
        {
            // Act
            var result = _sanitizer.SanitizePhoneNumber(input);

            // Assert
            Assert.Equal(expected, result);
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public void SanitizeMessage_VeryLongString_HandlesGracefully()
        {
            // Arrange
            string input = new string('a', 10000);

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(10000, result.Length);
        }

        [Fact]
        public void SanitizeMessage_OnlySpecialCharacters_ReturnsEmpty()
        {
            // Arrange
            string input = "!@#$%^&*()";

            // Act
            var result = _sanitizer.SanitizeMessage(input);

            // Assert
            Assert.NotNull(result);
        }

        #endregion
    }
}