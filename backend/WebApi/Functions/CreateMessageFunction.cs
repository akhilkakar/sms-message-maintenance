using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SmsMessageMaintenanceModels;
using SmsMessageMaintenanceServices;

namespace SmsMessageMaintenanceFunctions
{
    /// <summary>
    /// Azure Function for creating messages
    /// Follows Single Responsibility Principle - only handles HTTP concerns
    /// Follows Dependency Inversion Principle - depends on IMessageService abstraction
    /// </summary>
    public class CreateMessageFunction
    {
        private readonly IMessageService _messageService;
        private readonly ILogger<CreateMessageFunction> _logger;

        public CreateMessageFunction(
            IMessageService messageService,
            ILogger<CreateMessageFunction> logger)
        {
            _messageService = messageService ?? throw new ArgumentNullException(nameof(messageService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Function("CreateMessage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "messages")] HttpRequestData req)
        {
            _logger.LogInformation("CreateMessage function processing request.");

            try
            {
                // Parse request body
                MessageDto messageDto = await ParseRequestBodyAsync(req);
                if (messageDto == null)
                {
                    return await CreateBadRequestResponse(req, "Invalid JSON format");
                }

                // Delegate business logic to service layer
                var response = await _messageService.CreateMessageAsync(messageDto);

                // Build successful HTTP response
                return await CreateSuccessResponse(req, response);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning($"Validation failed: {ex.Message}");
                return await CreateValidationErrorResponse(req, ex.Errors);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning($"Invalid argument: {ex.Message}");
                return await CreateBadRequestResponse(req, ex.Message);
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"Timeout: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.RequestTimeout, 
                    "The request timed out. Please try again.");
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError($"Unauthorized: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.ServiceUnavailable, 
                    "Service temporarily unavailable. Please contact support.");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError($"Operation error: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.ServiceUnavailable, 
                    ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error in CreateMessage: {ex.Message}");
                return await CreateErrorResponse(req, HttpStatusCode.InternalServerError, 
                    "An unexpected error occurred while processing the request");
            }
        }

        private async Task<MessageDto?> ParseRequestBodyAsync(HttpRequestData req)
        {
            try
            {
                return await req.ReadFromJsonAsync<MessageDto>();
            }
            catch (JsonException ex)
            {
                _logger.LogWarning($"JSON parsing error: {ex.Message}");
                return null;
            }
        }

        private async Task<HttpResponseData> CreateSuccessResponse(
            HttpRequestData req, 
            MessageResponse messageResponse)
        {
            var httpResponse = req.CreateResponse(HttpStatusCode.Created);
            httpResponse.Headers.Add("Location", $"/api/messages/{messageResponse.Id}");
            await httpResponse.WriteAsJsonAsync(messageResponse);
            return httpResponse;
        }

        private async Task<HttpResponseData> CreateBadRequestResponse(
            HttpRequestData req, 
            string errorMessage)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { error = errorMessage });
            return response;
        }

        private async Task<HttpResponseData> CreateValidationErrorResponse(
            HttpRequestData req, 
            System.Collections.Generic.List<string> errors)
        {
            var response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteAsJsonAsync(new { errors });
            return response;
        }

        private async Task<HttpResponseData> CreateErrorResponse(
            HttpRequestData req, 
            HttpStatusCode statusCode, 
            string errorMessage)
        {
            var response = req.CreateResponse(statusCode);
            await response.WriteAsJsonAsync(new { error = errorMessage });
            return response;
        }
    }
}