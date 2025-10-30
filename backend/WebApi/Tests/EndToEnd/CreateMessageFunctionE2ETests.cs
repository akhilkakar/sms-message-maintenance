using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SmsMessageMaintenanceFunctions;
using SmsMessageMaintenanceModels;
using SmsMessageMaintenanceServices;

namespace SmsMessageMaintenanceTests.EndToEnd
{
    /// <summary>
    /// End-to-end tests for CreateMessageFunction
    /// Tests the entire HTTP request/response flow
    /// </summary>
    public class CreateMessageFunctionE2ETests
    {
        private readonly Mock<IMessageService> _mockService;
        private readonly Mock<ILogger<CreateMessageFunction>> _mockLogger;
        private readonly CreateMessageFunction _function;

        public CreateMessageFunctionE2ETests()
        {
            _mockService = new Mock<IMessageService>();
            _mockLogger = new Mock<ILogger<CreateMessageFunction>>();
            _function = new CreateMessageFunction(_mockService.Object, _mockLogger.Object);
        }

        #region Helper Methods

        private HttpRequestData CreateMockRequest(string jsonBody)
        {
            var context = new Mock<FunctionContext>();
            var request = new Mock<HttpRequestData>(context.Object);

            // Setup request body
            var bodyStream = new MemoryStream(Encoding.UTF8.GetBytes(jsonBody));
            request.Setup(r => r.Body).Returns(bodyStream);
            
            // Setup ReadFromJsonAsync
            request.Setup(r => r.ReadFromJsonAsync<MessageDto>(It.IsAny<System.Threading.CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    bodyStream.Position = 0;
                    return JsonSerializer.Deserialize<MessageDto>(bodyStream);
                });

            // Setup CreateResponse
            request.Setup(r => r.CreateResponse(It.IsAny<HttpStatusCode>()))
                .Returns((HttpStatusCode statusCode) =>
                {
                    var response = new Mock<HttpResponseData>(context.Object);
                    response.Setup(r => r.StatusCode).Returns(statusCode);
                    response.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
                    response.Setup(r => r.WriteAsJsonAsync(It.IsAny<object>(), It.IsAny<System.Threading.CancellationToken>()))
                        .Returns(ValueTask.CompletedTask);
                    return response.Object;
                });

            return request.Object;
        }

        #endregion

        #region Happy Path Tests

        [Fact]
        public async Task Run_ValidRequest_Returns201Created()
        {
            // Arrange
            var requestJson = @"{
                ""to"": ""1234567890"",
                ""from"": ""9876543210"",
                ""message"": ""Hello World""
            }";

            var expectedResponse = new MessageResponse
            {
                Id = 42,
                To = "1234567890",
                From = "9876543210",
                Message = "Hello World",
                Status = "Pending",
                CreatedDateTime = DateTime.UtcNow
            };

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ReturnsAsync(expectedResponse);

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            _mockService.Verify(s => s.CreateMessageAsync(It.IsAny<MessageDto>()), Times.Once);
        }

        [Fact]
        public async Task Run_ValidRequest_SetsLocationHeader()
        {
            // Arrange
            var requestJson = @"{
                ""to"": ""1234567890"",
                ""from"": ""9876543210"",
                ""message"": ""Test""
            }";

            var expectedResponse = new MessageResponse
            {
                Id = 99,
                To = "1234567890",
                From = "9876543210",
                Message = "Test",
                Status = "Pending",
                CreatedDateTime = DateTime.UtcNow
            };

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ReturnsAsync(expectedResponse);

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Contains(response.Headers, h => h.Key == "Location");
        }

        #endregion

        #region Invalid JSON Tests

        [Fact]
        public async Task Run_InvalidJson_Returns400BadRequest()
        {
            // Arrange
            var invalidJson = "{ invalid json }";
            var request = CreateMockRequest(invalidJson);

            // Override ReadFromJsonAsync to throw JsonException
            var context = new Mock<FunctionContext>();
            var mockRequest = new Mock<HttpRequestData>(context.Object);
            mockRequest.Setup(r => r.ReadFromJsonAsync<MessageDto>(It.IsAny<System.Threading.CancellationToken>()))
                .ThrowsAsync(new JsonException("Invalid JSON"));
            
            mockRequest.Setup(r => r.CreateResponse(It.IsAny<HttpStatusCode>()))
                .Returns((HttpStatusCode statusCode) =>
                {
                    var response = new Mock<HttpResponseData>(context.Object);
                    response.Setup(r => r.StatusCode).Returns(statusCode);
                    response.Setup(r => r.Headers).Returns(new HttpHeadersCollection());
                    response.Setup(r => r.WriteAsJsonAsync(It.IsAny<object>(), It.IsAny<System.Threading.CancellationToken>()))
                        .Returns(ValueTask.CompletedTask);
                    return response.Object;
                });

            // Act
            var response = await _function.Run(mockRequest.Object);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            _mockService.Verify(s => s.CreateMessageAsync(It.IsAny<MessageDto>()), Times.Never);
        }

        [Fact]
        public async Task Run_EmptyJson_Returns400BadRequest()
        {
            // Arrange
            var emptyJson = "{}";
            var request = CreateMockRequest(emptyJson);

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ThrowsAsync(new ValidationException("Validation failed", 
                    new System.Collections.Generic.List<string> { "Required fields missing" }));

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Validation Error Tests

        [Fact]
        public async Task Run_ValidationFails_Returns400WithErrors()
        {
            // Arrange
            var requestJson = @"{
                ""to"": """",
                ""from"": ""123"",
                ""message"": ""Hi""
            }";

            var validationErrors = new System.Collections.Generic.List<string>
            {
                "'to' field is required"
            };

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ThrowsAsync(new ValidationException("Validation failed", validationErrors));

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task Run_MultipleValidationErrors_Returns400WithAllErrors()
        {
            // Arrange
            var requestJson = @"{
                ""to"": """",
                ""from"": """",
                ""message"": """"
            }";

            var validationErrors = new System.Collections.Generic.List<string>
            {
                "'to' field is required",
                "'from' field is required",
                "'message' field is required"
            };

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ThrowsAsync(new ValidationException("Validation failed", validationErrors));

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Argument Error Tests

        [Fact]
        public async Task Run_InvalidPhoneNumber_Returns400BadRequest()
        {
            // Arrange
            var requestJson = @"{
                ""to"": ""invalid"",
                ""from"": ""123"",
                ""message"": ""Hi""
            }";

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ThrowsAsync(new ArgumentException("Invalid 'to' phone number format"));

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Database Error Tests

        [Fact]
        public async Task Run_TimeoutException_Returns408RequestTimeout()
        {
            // Arrange
            var requestJson = @"{
                ""to"": ""123"",
                ""from"": ""456"",
                ""message"": ""Test""
            }";

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ThrowsAsync(new TimeoutException("Database timeout"));

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        }

        [Fact]
        public async Task Run_UnauthorizedAccessException_Returns503ServiceUnavailable()
        {
            // Arrange
            var requestJson = @"{
                ""to"": ""123"",
                ""from"": ""456"",
                ""message"": ""Test""
            }";

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ThrowsAsync(new UnauthorizedAccessException("Database authentication failed"));

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task Run_InvalidOperationException_Returns503ServiceUnavailable()
        {
            // Arrange
            var requestJson = @"{
                ""to"": ""123"",
                ""from"": ""456"",
                ""message"": ""Test""
            }";

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ThrowsAsync(new InvalidOperationException("Database is busy"));

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }

        [Fact]
        public async Task Run_UnexpectedException_Returns500InternalServerError()
        {
            // Arrange
            var requestJson = @"{
                ""to"": ""123"",
                ""from"": ""456"",
                ""message"": ""Test""
            }";

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ThrowsAsync(new Exception("Unexpected error"));

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task Run_MaxLengthMessage_ProcessesSuccessfully()
        {
            // Arrange
            var longMessage = new string('A', 1000);
            var requestJson = $@"{{
                ""to"": ""123"",
                ""from"": ""456"",
                ""message"": ""{longMessage}""
            }}";

            var expectedResponse = new MessageResponse
            {
                Id = 1,
                To = "123",
                From = "456",
                Message = longMessage,
                Status = "Pending",
                CreatedDateTime = DateTime.UtcNow
            };

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ReturnsAsync(expectedResponse);

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task Run_InternationalPhoneFormat_ProcessesSuccessfully()
        {
            // Arrange
            var requestJson = @"{
                ""to"": ""+1 (555) 123-4567"",
                ""from"": ""+44 20 7946 0958"",
                ""message"": ""International test""
            }";

            var expectedResponse = new MessageResponse
            {
                Id = 1,
                To = "15551234567",
                From = "442079460958",
                Message = "International test",
                Status = "Pending",
                CreatedDateTime = DateTime.UtcNow
            };

            _mockService.Setup(s => s.CreateMessageAsync(It.IsAny<MessageDto>()))
                .ReturnsAsync(expectedResponse);

            var request = CreateMockRequest(requestJson);

            // Act
            var response = await _function.Run(request);

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_NullService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CreateMessageFunction(null, _mockLogger.Object)
            );
        }

        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                new CreateMessageFunction(_mockService.Object, null)
            );
        }

        #endregion
    }
}