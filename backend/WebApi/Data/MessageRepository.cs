using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace SmsMessageMaintenanceData
{
    /// <summary>
    /// Repository interface for message data access
    /// Follows Interface Segregation Principle and Dependency Inversion Principle
    /// </summary>
    public interface IMessageRepository
    {
        Task<long> CreateMessageAsync(long to, long from, string message);
    }

    /// <summary>
    /// SQL Server implementation of message repository
    /// Follows Single Responsibility Principle - only handles database operations
    /// </summary>
    public class MessageRepository : IMessageRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<MessageRepository> _logger;
        private const int COMMAND_TIMEOUT_SECONDS = 30;
        private const int MAX_MESSAGE_LENGTH = 1000;

        public MessageRepository(string connectionString, ILogger<MessageRepository> logger)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<long> CreateMessageAsync(long to, long from, string message)
        {
            SqlConnection? connection = null;
            
            try
            {
                connection = new SqlConnection(_connectionString);
                connection.Open();
                
                _logger.LogInformation("Database connection established successfully");

                string query = @"
                    INSERT INTO [dbo].[Messages] ([To], [From], [Message], [Status], [CreatedDateTime], [ModifiedDateTime])
                    OUTPUT INSERTED.ID
                    VALUES (@To, @From, @Message, 'Pending', GETUTCDATE(), GETUTCDATE())";

                using (var command = new SqlCommand(query, connection))
                {
                    command.CommandTimeout = COMMAND_TIMEOUT_SECONDS;
                    
                    command.Parameters.Add("@To", SqlDbType.BigInt).Value = to;
                    command.Parameters.Add("@From", SqlDbType.BigInt).Value = from;
                    command.Parameters.Add("@Message", SqlDbType.NVarChar, MAX_MESSAGE_LENGTH).Value = message;

                    var result = await command.ExecuteScalarAsync();
                    
                    if (result == null)
                    {
                        _logger.LogError("Database insert did not return an ID");
                        throw new InvalidOperationException("Failed to insert message - no ID returned");
                    }

                    _logger.LogInformation($"Message inserted successfully with ID: {result}");
                    return (long)result;
                }
            }
            catch (SqlException ex)
            {
                throw HandleSqlException(ex);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError($"Invalid operation: {ex.Message}");
                throw new InvalidOperationException("Database configuration error.", ex);
            }
            catch (TimeoutException ex)
            {
                _logger.LogError($"Operation timeout: {ex.Message}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Unexpected error during database insert: {ex.GetType().Name} - {ex.Message}");
                throw new InvalidOperationException("An unexpected error occurred while saving the message.", ex);
            }
            finally
            {
                CloseConnection(connection);
            }
        }

        private Exception HandleSqlException(SqlException ex)
        {
            switch (ex.Number)
            {
                case -2: // Timeout
                    _logger.LogError($"Database timeout error: {ex.Message}");
                    return new TimeoutException("Database operation timed out. Please try again.", ex);

                case 2627: // Unique constraint violation
                case 2601: // Duplicate key
                    _logger.LogWarning($"Duplicate message detected: {ex.Message}");
                    return new InvalidOperationException("A message with these details already exists.", ex);

                case 547: // Foreign key constraint
                    _logger.LogError($"Foreign key constraint violation: {ex.Message}");
                    return new InvalidOperationException("Referenced data does not exist.", ex);

                case 515: // Cannot insert null
                    _logger.LogError($"Required field is missing: {ex.Message}");
                    return new InvalidOperationException("Required field is missing.", ex);

                case 1205: // Deadlock
                    _logger.LogWarning($"Database deadlock detected: {ex.Message}");
                    return new InvalidOperationException("Database is busy. Please try again.", ex);

                case 18456: // Login failed
                    _logger.LogError($"Database authentication failed: {ex.Message}");
                    return new UnauthorizedAccessException("Database authentication failed.", ex);

                case -1: // Connection failed
                case 53: // Network path not found
                    _logger.LogError($"Database connection failed: {ex.Message}");
                    return new InvalidOperationException("Could not connect to database. Please try again later.", ex);

                default:
                    _logger.LogError($"Database error (Code: {ex.Number}): {ex.Message}");
                    return new InvalidOperationException($"Database error occurred. Error code: {ex.Number}", ex);
            }
        }

        private void CloseConnection(SqlConnection? connection)
        {
            if (connection != null)
            {
                try
                {
                    if (connection.State == ConnectionState.Open)
                    {
                        connection.Close();
                        _logger.LogInformation("Database connection closed");
                    }
                    connection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error closing database connection: {ex.Message}");
                }
            }
        }
    }
}