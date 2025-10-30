using System.Text.Json.Serialization;

namespace SmsMessageMaintenanceFunctions
{
    public class QueueMessageDto
    {
        [JsonPropertyName("messageId")]
        public long MessageId { get; set; }
        
        [JsonPropertyName("to")]
        public long To { get; set; }
        
        [JsonPropertyName("from")]
        public long From { get; set; }
        
        [JsonPropertyName("message")]
        public string Message { get; set; }
    }
}
