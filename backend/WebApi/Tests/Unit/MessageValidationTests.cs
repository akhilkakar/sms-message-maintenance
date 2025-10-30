using System.Collections.Generic;
using Xunit;
using SmsMessageMaintenanceValidation;
using SmsMessageMaintenanceModels;

namespace SmsMessageMaintenanceTests.Unit.Validation
{
    /// <summary>
    /// Unit tests for MessageValidator
    /// Tests validation logic in isolation (no dependencies)
    /// </summary>
    public class MessageValidatorTests
    {
        private readonly IMessageValidator _validator;

        public MessageValidatorTests()
        {
            _validator = new MessageValidator();
        }

        #region Null and Empty Tests

        [Fact]
        public void Validate_NullMessage_ReturnsRequiredError()
        {
            // Arrange
            MessageDto message = null;

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Single(errors);
            Assert.Contains("Request body is required", errors);
        }

        [Fact]
        public void Validate_AllFieldsEmpty_ReturnsThreeErrors()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "",
                From = "",
                Message = ""
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Equal(3, errors.Count);
            Assert.Contains("'to' field is required", errors);
            Assert.Contains("'from' field is required", errors);
            Assert.Contains("'message' field is required", errors);
        }

        [Fact]
        public void Validate_AllFieldsNull_ReturnsThreeErrors()
        {
            // Arrange
            var message = new MessageDto
            {
                To = null,
                From = null,
                Message = null
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Equal(3, errors.Count);
        }

        [Fact]
        public void Validate_AllFieldsWhitespace_ReturnsThreeErrors()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "   ",
                From = "   ",
                Message = "   "
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Equal(3, errors.Count);
        }

        #endregion

        #region Phone Number Validation Tests

        [Fact]
        public void Validate_MissingToField_ReturnsError()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "",
                From = "1234567890",
                Message = "Hello"
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Single(errors);
            Assert.Contains("'to' field is required", errors);
        }

        [Fact]
        public void Validate_MissingFromField_ReturnsError()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "1234567890",
                From = "",
                Message = "Hello"
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Single(errors);
            Assert.Contains("'from' field is required", errors);
        }

        [Fact]
        public void Validate_PhoneNumberTooLong_ReturnsError()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "12345678901234567890", // 20 digits (max is 15)
                From = "1234567890",
                Message = "Hello"
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Single(errors);
            Assert.Contains("'to' field must be 15 characters or less", errors);
        }

        [Fact]
        public void Validate_PhoneNumberWithInvalidCharacters_ReturnsError()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "123-456-7890abc", // Contains letters
                From = "1234567890",
                Message = "Hello"
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Single(errors);
            Assert.Contains("'to' field contains invalid characters", errors);
        }

        [Fact]
        public void Validate_PhoneNumberWithSpecialChars_ReturnsError()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "123@456#7890", // Invalid special chars
                From = "1234567890",
                Message = "Hello"
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Single(errors);
            Assert.Contains("'to' field contains invalid characters", errors);
        }

        [Theory]
        [InlineData("1234567890")]           // Plain digits
        [InlineData("123-456-7890")]         // With dashes
        [InlineData("(123) 456-7890")]       // With parentheses
        [InlineData("+1 (555) 123-4567")]    // International format
        [InlineData("+44 20 7946 0958")]     // UK format
        public void Validate_ValidPhoneFormats_ReturnsNoErrors(string phoneNumber)
        {
            // Arrange
            var message = new MessageDto
            {
                To = phoneNumber,
                From = "1234567890",
                Message = "Hello"
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Empty(errors);
        }

        #endregion

        #region Message Text Validation Tests

        [Fact]
        public void Validate_MissingMessage_ReturnsError()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "1234567890",
                From = "1234567890",
                Message = ""
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Single(errors);
            Assert.Contains("'message' field is required", errors);
        }

        [Fact]
        public void Validate_MessageTooLong_ReturnsError()
        {
            // Arrange
            var longMessage = new string('a', 1001); // 1001 characters (max is 1000)
            var message = new MessageDto
            {
                To = "1234567890",
                From = "1234567890",
                Message = longMessage
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Single(errors);
            Assert.Contains("'message' must be 1000 characters or less", errors);
        }

        [Fact]
        public void Validate_MessageExactly1000Chars_ReturnsNoError()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "1234567890",
                From = "1234567890",
                Message = new string('a', 1000) // Exactly 1000 characters
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Empty(errors);
        }

        #endregion

        #region Valid Input Tests

        [Fact]
        public void Validate_ValidMessage_ReturnsNoErrors()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "1234567890",
                From = "0987654321",
                Message = "Hello, this is a test message!"
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void Validate_MinimalValidMessage_ReturnsNoErrors()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "1",
                From = "2",
                Message = "Hi"
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Empty(errors);
        }

        #endregion

        #region Multiple Error Tests

        [Fact]
        public void Validate_MultipleErrors_ReturnsAllErrors()
        {
            // Arrange
            var message = new MessageDto
            {
                To = "", // Missing
                From = "12345678901234567890", // Too long
                Message = new string('a', 1001) // Too long
            };

            // Act
            var errors = _validator.Validate(message);

            // Assert
            Assert.Equal(3, errors.Count);
            Assert.Contains("'to' field is required", errors);
            Assert.Contains("'from' field must be 15 characters or less", errors);
            Assert.Contains("'message' must be 1000 characters or less", errors);
        }

        #endregion
    }
}