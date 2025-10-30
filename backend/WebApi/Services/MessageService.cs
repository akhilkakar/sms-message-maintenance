using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmsMessageMaintenanceData;
using SmsMessageMaintenanceModels;
using SmsMessageMaintenanceSecurity;
using SmsMessageMaintenanceValidation;

namespace SmsMessageMaintenanceServices
{
    /// <summary>
    /// Service interface for message operations
    /// Follows Interface Segregation Principle
    /// </summary>
    public interface IMessageService
    {
        Task<MessageResponse> CreateMessageAsync(MessageDto messageDto);
    }

    /// <summary>
    /// Service layer for message business logic
    /// Follows Single Responsibility Principle - orchestrates validation, sanitization, and persistence
    /// Follows Dependency Inversion Principle - depends on abstractions, not concrete implementations
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly IMessageRepository _repository;
        private readonly IMessageValidator _validator;
        private readonly IInputSanitizer _sanitizer;
        private readonly ILogger<MessageService> _logger;

        public MessageService(
            IMessageRepository repository,
            IMessageValidator validator,
            IInputSanitizer sanitizer,
            ILogger<MessageService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));
            _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<MessageResponse> CreateMessageAsync(MessageDto messageDto)
        {
            // Step 1: Validate input
            var validationErrors = _validator.Validate(messageDto);
            if (validationErrors.Count > 0)
            {
                _logger.LogWarning($"Validation failed: {string.Join(", ", validationErrors)}");
                throw new ValidationException("Validation failed", validationErrors);
            }

            // Step 2: Sanitize inputs
            var sanitizedMessage = _sanitizer.SanitizeMessage(messageDto.Message);
            var sanitizedTo = _sanitizer.SanitizePhoneNumber(messageDto.To);
            var sanitizedFrom = _sanitizer.SanitizePhoneNumber(messageDto.From);

            // Step 3: Parse phone numbers
            if (!long.TryParse(sanitizedTo, out long toPhone))
            {
                _logger.LogWarning($"Invalid 'to' phone number format: {messageDto.To}");
                throw new ArgumentException("Invalid 'to' phone number format");
            }

            if (!long.TryParse(sanitizedFrom, out long fromPhone))
            {
                _logger.LogWarning($"Invalid 'from' phone number format: {messageDto.From}");
                throw new ArgumentException("Invalid 'from' phone number format");
            }

            // Step 4: Persist to database
            long messageId = await _repository.CreateMessageAsync(toPhone, fromPhone, sanitizedMessage);

            // Step 5: Build response
            var response = new MessageResponse
            {
                Id = messageId,
                To = toPhone.ToString(),
                From = fromPhone.ToString(),
                Message = sanitizedMessage,
                Status = "Pending",
                CreatedDateTime = DateTime.UtcNow
            };

            _logger.LogInformation($"Message created successfully with ID: {messageId}");
            
            return response;
        }
    }
}