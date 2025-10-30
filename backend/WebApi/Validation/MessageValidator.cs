using System.Collections.Generic;
using System.Text.RegularExpressions;
using SmsMessageMaintenanceModels;

namespace SmsMessageMaintenanceValidation
{
    /// <summary>
    /// Validates message DTOs according to business rules
    /// Follows Single Responsibility Principle - only handles validation
    /// </summary>
    public interface IMessageValidator
    {
        List<string> Validate(MessageDto message);
    }

    public class MessageValidator : IMessageValidator
    {
        private const int MAX_MESSAGE_LENGTH = 1000;
        private const int MAX_PHONE_LENGTH = 15;

        public List<string> Validate(MessageDto message)
        {
            var errors = new List<string>();

            if (message == null)
            {
                errors.Add("Request body is required");
                return errors;
            }

            ValidatePhoneNumber(message.To, "to", errors);
            ValidatePhoneNumber(message.From, "from", errors);
            ValidateMessageText(message.Message, errors);

            return errors;
        }

        private void ValidatePhoneNumber(string phoneNumber, string fieldName, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
            {
                errors.Add($"'{fieldName}' field is required");
                return;
            }

            if (!Regex.IsMatch(phoneNumber, @"^[\d\s\-\(\)\+]+$"))
            {
                errors.Add($"'{fieldName}' field contains invalid characters");
            }

            phoneNumber = PhoneUtils.Normalize(phoneNumber);

            if (phoneNumber.Length > MAX_PHONE_LENGTH)
            {
                errors.Add($"'{fieldName}' field must be {MAX_PHONE_LENGTH} characters or less");
            }
        }

        private void ValidateMessageText(string message, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                errors.Add("'message' field is required");
                return;
            }

            if (message.Length > MAX_MESSAGE_LENGTH)
            {
                errors.Add($"'message' must be {MAX_MESSAGE_LENGTH} characters or less");
            }
        }
    }

    public static class PhoneUtils
    {
        public static string Normalize(string phoneNumber)
            => Regex.Replace(phoneNumber ?? string.Empty, "[^0-9+]", "");
    }
}