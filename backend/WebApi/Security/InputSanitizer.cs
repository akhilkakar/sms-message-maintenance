using System;
using System.Text.RegularExpressions;

namespace SmsMessageMaintenanceSecurity
{
    /// <summary>
    /// Sanitizes user inputs to prevent security vulnerabilities
    /// Follows Single Responsibility Principle - only handles sanitization
    /// </summary>
    public interface IInputSanitizer
    {
        string SanitizeMessage(string input);
        string SanitizePhoneNumber(string phoneNumber);
    }

    public class InputSanitizer : IInputSanitizer
    {
        private static readonly string[] SqlInjectionPatterns = new[]
        {
            @"(\b(ALTER|CREATE|DELETE|DROP|EXEC(UTE)?|INSERT( +INTO)?|MERGE|SELECT|UPDATE|UNION( +ALL)?)\b)",
            @"(--|;|\/\*|\*\/|xp_|sp_)"
        };

        /// <summary>
        /// Sanitizes message text by removing dangerous content
        /// </summary>
        public string SanitizeMessage(string input)
        {
            if (input == null)
                return null;

            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            input = input.Trim();

            input = RemoveSqlInjectionPatterns(input);
            input = RemoveHtmlTags(input);
            input = System.Net.WebUtility.HtmlDecode(input);

            input = Regex.Replace(input, @"[ ]{2,}", " ");

            return input.Trim();
        }

        /// <summary>
        /// Sanitizes phone number by extracting only digits
        /// </summary>
        public string SanitizePhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                return phoneNumber;

            // Remove all non-numeric characters
            return Regex.Replace(phoneNumber, @"[^\d]", string.Empty);
        }

        private string RemoveSqlInjectionPatterns(string input)
        {
            foreach (var pattern in SqlInjectionPatterns)
            {
                input = Regex.Replace(input, pattern, string.Empty, RegexOptions.IgnoreCase);
            }
            return input;
        }

        private string RemoveHtmlTags(string input)
        {
            // Remove script tags with content
            input = Regex.Replace(input, @"<script[^>]*>.*?</script>", string.Empty, 
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            // Remove all HTML tags
            input = Regex.Replace(input, @"<[^>]+>", string.Empty);
            
            return input;
        }
    }
}