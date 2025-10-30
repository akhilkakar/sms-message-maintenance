using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SmsMessageMaintenanceData;
using SmsMessageMaintenanceModels;
using SmsMessageMaintenanceSecurity;
using SmsMessageMaintenanceServices;
using SmsMessageMaintenanceValidation;

namespace SmsMessageMaintenanceTests.Unit.Services
{
    /// <summary>
    /// Unit tests for MessageService
    /// Tests business logic with mocked dependencies
    /// </summary>
    public class MessageServiceTests
    {
        private readonly Mock<IMessageRepository> _mockRepository;
        private readonly Mock<IMessageValidator> _mockValidator;
        private readonly Mock<IInputSanitizer> _mockSanitizer;
        private readonly Mock<ILogger<MessageService>> _mockLogger;
        private readonly MessageService _service;

        public MessageServiceTests()
        {
            _mockRepository = new Mock<IMessageRepository>();
            _mockValidator = new Mock<IMessageValidator>();
            _mockSanitizer = new Mock<IInputSanitizer>();
            _mockLogger = new Mock<ILogger<MessageService>>();

            _service = new MessageService(
                _mockRepository.Object,
                _mockValidator.Object,
                _mockSanitizer.Object,
                _mockLogger.Object
            );
        }

        #region Validation Tests

        [Fact]
        public async Task CreateMessageAsync_ValidationFails_ThrowsValidationException()
        {
            // Arrange
            var dto = new MessageDto { To = "", From = "123", Message = "Hi" };
            var errors = new List<string> { "'to' field is required" };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(errors);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ValidationException>(
                () => _service.CreateMessageAsync(dto)
            );

            Assert.Equal(errors, exception.Errors);
            
            // Verify validator was called
            _mockValidator.Verify(v => v.Validate(dto), Times.Once);
            
            // Verify nothing else was called after validation failed
            _mockSanitizer.Verify(s => s.SanitizeMessage(It.IsAny<string>()), Times.Never);
            _mockRepository.Verify(r => r.CreateMessageAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task CreateMessageAsync_ValidationPasses_CallsSanitizer()
        {
            // Arrange
            var dto = new MessageDto { To = "123", From = "456", Message = "Hello" };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizeMessage(dto.Message)).Returns("Hello");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.To)).Returns("123");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.From)).Returns("456");
            _mockRepository.Setup(r => r.CreateMessageAsync(123, 456, "Hello")).ReturnsAsync(1);

            // Act
            await _service.CreateMessageAsync(dto);

            // Assert
            _mockSanitizer.Verify(s => s.SanitizeMessage(dto.Message), Times.Once);
            _mockSanitizer.Verify(s => s.SanitizePhoneNumber(dto.To), Times.Once);
            _mockSanitizer.Verify(s => s.SanitizePhoneNumber(dto.From), Times.Once);
        }

        #endregion

        #region Sanitization Tests

        [Fact]
        public async Task CreateMessageAsync_SanitizesAllInputs_BeforeProcessing()
        {
            // Arrange
            var dto = new MessageDto 
            { 
                To = "123-456-7890", 
                From = "+1 (555) 123-4567", 
                Message = "Hello <script>alert('xss')</script>" 
            };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizeMessage(dto.Message)).Returns("Hello");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.To)).Returns("1234567890");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.From)).Returns("15551234567");
            _mockRepository.Setup(r => r.CreateMessageAsync(1234567890, 15551234567, "Hello")).ReturnsAsync(1);

            // Act
            await _service.CreateMessageAsync(dto);

            // Assert - sanitized values are used
            _mockRepository.Verify(
                r => r.CreateMessageAsync(1234567890, 15551234567, "Hello"), 
                Times.Once
            );
        }

        #endregion

        #region Phone Number Parsing Tests

        [Fact]
        public async Task CreateMessageAsync_InvalidToPhoneNumber_ThrowsArgumentException()
        {
            // Arrange
            var dto = new MessageDto { To = "abc", From = "123", Message = "Hi" };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.To)).Returns("abc"); // Not numeric
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.From)).Returns("123");
            _mockSanitizer.Setup(s => s.SanitizeMessage(dto.Message)).Returns("Hi");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateMessageAsync(dto)
            );

            Assert.Contains("Invalid 'to' phone number format", exception.Message);
            
            // Repository should not be called
            _mockRepository.Verify(
                r => r.CreateMessageAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()), 
                Times.Never
            );
        }

        [Fact]
        public async Task CreateMessageAsync_InvalidFromPhoneNumber_ThrowsArgumentException()
        {
            // Arrange
            var dto = new MessageDto { To = "123", From = "xyz", Message = "Hi" };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.To)).Returns("123");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.From)).Returns("xyz"); // Not numeric
            _mockSanitizer.Setup(s => s.SanitizeMessage(dto.Message)).Returns("Hi");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateMessageAsync(dto)
            );

            Assert.Contains("Invalid 'from' phone number format", exception.Message);
        }

        [Fact]
        public async Task CreateMessageAsync_ValidPhoneNumbers_ParsesCorrectly()
        {
            // Arrange
            var dto = new MessageDto { To = "1234567890", From = "9876543210", Message = "Hi" };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.To)).Returns("1234567890");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.From)).Returns("9876543210");
            _mockSanitizer.Setup(s => s.SanitizeMessage(dto.Message)).Returns("Hi");
            _mockRepository.Setup(r => r.CreateMessageAsync(1234567890, 9876543210, "Hi")).ReturnsAsync(42);

            // Act
            var result = await _service.CreateMessageAsync(dto);

            // Assert
            _mockRepository.Verify(
                r => r.CreateMessageAsync(1234567890, 9876543210, "Hi"), 
                Times.Once
            );
        }

        #endregion

        #region Repository Interaction Tests

        [Fact]
        public async Task CreateMessageAsync_CallsRepository_WithCorrectParameters()
        {
            // Arrange
            var dto = new MessageDto { To = "123", From = "456", Message = "Test message" };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.To)).Returns("123");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.From)).Returns("456");
            _mockSanitizer.Setup(s => s.SanitizeMessage(dto.Message)).Returns("Test message");
            _mockRepository.Setup(r => r.CreateMessageAsync(123, 456, "Test message")).ReturnsAsync(999);

            // Act
            await _service.CreateMessageAsync(dto);

            // Assert
            _mockRepository.Verify(
                r => r.CreateMessageAsync(123, 456, "Test message"),
                Times.Once
            );
        }

        [Fact]
        public async Task CreateMessageAsync_RepositoryReturnsId_BuildsCorrectResponse()
        {
            // Arrange
            var dto = new MessageDto { To = "123", From = "456", Message = "Hello" };
            long expectedId = 42;
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.To)).Returns("123");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(dto.From)).Returns("456");
            _mockSanitizer.Setup(s => s.SanitizeMessage(dto.Message)).Returns("Hello");
            _mockRepository.Setup(r => r.CreateMessageAsync(123, 456, "Hello")).ReturnsAsync(expectedId);

            // Act
            var result = await _service.CreateMessageAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedId, result.Id);
            Assert.Equal("123", result.To);
            Assert.Equal("456", result.From);
            Assert.Equal("Hello", result.Message);
            Assert.Equal("Pending", result.Status);
            Assert.True(DateTime.UtcNow.Subtract(result.CreatedDateTime).TotalSeconds < 2);
        }

        #endregion

        #region Exception Propagation Tests

        [Fact]
        public async Task CreateMessageAsync_RepositoryThrowsException_PropagatesException()
        {
            // Arrange
            var dto = new MessageDto { To = "123", From = "456", Message = "Hi" };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(It.IsAny<string>())).Returns<string>(s => s);
            _mockSanitizer.Setup(s => s.SanitizeMessage(It.IsAny<string>())).Returns<string>(s => s);
            _mockRepository
                .Setup(r => r.CreateMessageAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("Database error"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateMessageAsync(dto)
            );
        }

        [Fact]
        public async Task CreateMessageAsync_RepositoryThrowsTimeout_PropagatesTimeoutException()
        {
            // Arrange
            var dto = new MessageDto { To = "123", From = "456", Message = "Hi" };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber(It.IsAny<string>())).Returns<string>(s => s);
            _mockSanitizer.Setup(s => s.SanitizeMessage(It.IsAny<string>())).Returns<string>(s => s);
            _mockRepository
                .Setup(r => r.CreateMessageAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
                .ThrowsAsync(new TimeoutException("Database timeout"));

            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(
                () => _service.CreateMessageAsync(dto)
            );
        }

        #endregion

        #region Workflow Order Tests

        [Fact]
        public async Task CreateMessageAsync_ExecutesInCorrectOrder()
        {
            // Arrange
            var dto = new MessageDto { To = "123", From = "456", Message = "Hi" };
            var callOrder = new List<string>();
            
            _mockValidator
                .Setup(v => v.Validate(dto))
                .Callback(() => callOrder.Add("Validate"))
                .Returns(new List<string>());
            
            _mockSanitizer
                .Setup(s => s.SanitizeMessage(dto.Message))
                .Callback(() => callOrder.Add("SanitizeMessage"))
                .Returns("Hi");
            
            _mockSanitizer
                .Setup(s => s.SanitizePhoneNumber(dto.To))
                .Callback(() => callOrder.Add("SanitizeTo"))
                .Returns("123");
            
            _mockSanitizer
                .Setup(s => s.SanitizePhoneNumber(dto.From))
                .Callback(() => callOrder.Add("SanitizeFrom"))
                .Returns("456");
            
            _mockRepository
                .Setup(r => r.CreateMessageAsync(123, 456, "Hi"))
                .Callback(() => callOrder.Add("Repository"))
                .ReturnsAsync(1);

            // Act
            await _service.CreateMessageAsync(dto);

            // Assert
            Assert.Equal("Validate", callOrder[0]);
            Assert.Contains("SanitizeMessage", callOrder);
            Assert.Contains("SanitizeTo", callOrder);
            Assert.Contains("SanitizeFrom", callOrder);
            Assert.Equal("Repository", callOrder[callOrder.Count - 1]);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_NullRepository_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MessageService(null, _mockValidator.Object, _mockSanitizer.Object, _mockLogger.Object)
            );
        }

        [Fact]
        public void Constructor_NullValidator_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MessageService(_mockRepository.Object, null, _mockSanitizer.Object, _mockLogger.Object)
            );
        }

        [Fact]
        public void Constructor_NullSanitizer_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MessageService(_mockRepository.Object, _mockValidator.Object, null, _mockLogger.Object)
            );
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new MessageService(_mockRepository.Object, _mockValidator.Object, _mockSanitizer.Object, null)
            );
        }

        #endregion

        #region Integration-Like Tests (Multiple Scenarios)

        [Fact]
        public async Task CreateMessageAsync_FullHappyPath_ReturnsValidResponse()
        {
            // Arrange
            var dto = new MessageDto 
            { 
                To = "+1 (555) 123-4567", 
                From = "987-654-3210", 
                Message = "Hello World!" 
            };
            
            _mockValidator.Setup(v => v.Validate(dto)).Returns(new List<string>());
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber("+1 (555) 123-4567")).Returns("15551234567");
            _mockSanitizer.Setup(s => s.SanitizePhoneNumber("987-654-3210")).Returns("9876543210");
            _mockSanitizer.Setup(s => s.SanitizeMessage("Hello World!")).Returns("Hello World!");
            _mockRepository.Setup(r => r.CreateMessageAsync(15551234567, 9876543210, "Hello World!")).ReturnsAsync(100);

            // Act
            var result = await _service.CreateMessageAsync(dto);

            // Assert
            Assert.Equal(100, result.Id);
            Assert.Equal("15551234567", result.To);
            Assert.Equal("9876543210", result.From);
            Assert.Equal("Hello World!", result.Message);
            Assert.Equal("Pending", result.Status);
            Assert.NotEqual(default(DateTime), result.CreatedDateTime);
        }

        #endregion
    }
}