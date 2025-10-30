using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmsMessageMaintenanceModels
{
    /// <summary>
    /// Data Transfer Object for incoming message requests
    /// </summary>
    public class MessageDto
    {
        [JsonPropertyName("to")]
        public required string To { get; set; }

        [JsonPropertyName("from")]
        public required string From { get; set; }

        [JsonPropertyName("message")]
        public required string Message { get; set; }
    }

    /// <summary>
    /// Response object for created messages
    /// </summary>
    public class MessageResponse
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("to")]
        public required string To { get; set; }

        [JsonPropertyName("from")]
        public required string From { get; set; }

        [JsonPropertyName("message")]
        public required string Message { get; set; }

        [JsonPropertyName("status")]
        public required string Status { get; set; }

        [JsonPropertyName("createdDateTime")]
        public DateTime CreatedDateTime { get; set; }
    }

    /// <summary>
    /// Custom exception for validation errors
    /// Provides structured error information
    /// </summary>
    public class ValidationException : Exception
    {
        public List<string> Errors { get; }

        public ValidationException(string message, List<string> errors) 
            : base(message)
        {
            Errors = errors ?? new List<string>();
        }
    }
}