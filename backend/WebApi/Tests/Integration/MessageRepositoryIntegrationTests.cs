using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using SmsMessageMaintenanceData;

namespace SmsMessageMaintenanceTests.Integration.Data
{
    /// <summary>
    /// Integration tests for MessageRepository
    /// These tests require a real database connection
    /// Can use testcontainers or a test database
    /// </summary>
    [Collection("Database")]
    public class MessageRepositoryIntegrationTests : IDisposable
    {
        private readonly string _connectionString;
        private readonly ILogger<MessageRepository> _logger;
        private readonly MessageRepository _repository;

        public MessageRepositoryIntegrationTests()
        {
            // Get connection string from environment or use default test database
            _connectionString = Environment.GetEnvironmentVariable("TestSqlConnectionString") 
                ?? "Server=localhost;Database=SmsMessagingTest;Integrated Security=true;TrustServerCertificate=true";
            
            var mockLogger = new Mock<ILogger<MessageRepository>>();
            _logger = mockLogger.Object;
            
            _repository = new MessageRepository(_connectionString, _logger);
            
            // Setup test database
            SetupTestDatabase();
        }

        private void SetupTestDatabase()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();

                // Create Messages table if not exists
                string createTableQuery = @"
                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Messages')
                    BEGIN
                        CREATE TABLE [dbo].[Messages] (
                            [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
                            [To] BIGINT NOT NULL,
                            [From] BIGINT NOT NULL,
                            [Message] NVARCHAR(1000) NOT NULL,
                            [Status] NVARCHAR(50) NULL,
                            [StatusReason] NVARCHAR(500) NULL,
                            [CreatedDateTime] DATETIME2 NOT NULL,
                            [ModifiedDateTime] DATETIME2 NOT NULL,
                            [QueuedDateTime] DATETIME2 NULL,
                            [ProcessedDateTime] DATETIME2 NULL
                        )
                    END";

                using (var command = new SqlCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        private void CleanupTestData()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string deleteQuery = "DELETE FROM [dbo].[Messages]";
                using (var command = new SqlCommand(deleteQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        public void Dispose()
        {
            CleanupTestData();
        }

        #region Happy Path Tests

        [Fact]
        public async Task CreateMessageAsync_ValidInput_InsertsSuccessfully()
        {
            // Arrange
            long to = 1234567890;
            long from = 9876543210;
            string message = "Integration test message";

            // Act
            long messageId = await _repository.CreateMessageAsync(to, from, message);

            // Assert
            Assert.True(messageId > 0);

            // Verify in database
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT [To], [From], [Message], [Status] FROM [dbo].[Messages] WHERE [ID] = @Id";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", messageId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        Assert.True(await reader.ReadAsync());
                        Assert.Equal(to, reader.GetInt64(0));
                        Assert.Equal(from, reader.GetInt64(1));
                        Assert.Equal(message, reader.GetString(2));
                        Assert.Equal("Pending", reader.GetString(3));
                    }
                }
            }
        }

        [Fact]
        public async Task CreateMessageAsync_LongMessage_InsertsSuccessfully()
        {
            // Arrange
            long to = 1111111111;
            long from = 2222222222;
            string message = new string('A', 1000); // 1000 character message

            // Act
            long messageId = await _repository.CreateMessageAsync(to, from, message);

            // Assert
            Assert.True(messageId > 0);
        }

        [Fact]
        public async Task CreateMessageAsync_MultipleMessages_IncrementingIds()
        {
            // Arrange & Act
            long id1 = await _repository.CreateMessageAsync(111, 222, "Message 1");
            long id2 = await _repository.CreateMessageAsync(333, 444, "Message 2");
            long id3 = await _repository.CreateMessageAsync(555, 666, "Message 3");

            // Assert
            Assert.True(id2 > id1);
            Assert.True(id3 > id2);
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CreateMessageAsync_InvalidConnectionString_ThrowsException()
        {
            // Arrange
            var badConnectionString = "Server=nonexistent;Database=test;Integrated Security=true";
            var badRepository = new MessageRepository(badConnectionString, _logger);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => badRepository.CreateMessageAsync(123, 456, "Test")
            );
        }

        [Fact]
        public async Task CreateMessageAsync_MessageTooLong_ThrowsException()
        {
            // Arrange
            long to = 1234567890;
            long from = 9876543210;
            string message = new string('A', 1001); // Exceeds 1000 character limit

            // Act & Assert
            // SQL Server will throw an error for string truncation
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _repository.CreateMessageAsync(to, from, message)
            );
        }

        #endregion

        #region Concurrent Access Tests

        [Fact]
        public async Task CreateMessageAsync_ConcurrentInserts_AllSucceed()
        {
            // Arrange
            var tasks = new Task<long>[10];

            // Act - Create 10 messages concurrently
            for (int i = 0; i < 10; i++)
            {
                int index = i;
                tasks[i] = Task.Run(async () =>
                    await _repository.CreateMessageAsync(
                        1000 + index,
                        2000 + index,
                        $"Concurrent message {index}"
                    )
                );
            }

            var messageIds = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(10, messageIds.Length);
            Assert.All(messageIds, id => Assert.True(id > 0));
            Assert.Equal(messageIds.Length, messageIds.Distinct().Count()); // All IDs are unique
        }

        #endregion

        #region Database State Tests

        [Fact]
        public async Task CreateMessageAsync_SetsDefaultStatus_ToPending()
        {
            // Arrange
            long to = 5555555555;
            long from = 6666666666;
            string message = "Status test";

            // Act
            long messageId = await _repository.CreateMessageAsync(to, from, message);

            // Assert
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT [Status] FROM [dbo].[Messages] WHERE [ID] = @Id";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", messageId);
                    var status = await command.ExecuteScalarAsync();
                    Assert.Equal("Pending", status);
                }
            }
        }

        [Fact]
        public async Task CreateMessageAsync_SetsTimestamps_ToUtcNow()
        {
            // Arrange
            long to = 7777777777;
            long from = 8888888888;
            string message = "Timestamp test";
            var beforeCreate = DateTime.UtcNow.AddSeconds(-1);

            // Act
            long messageId = await _repository.CreateMessageAsync(to, from, message);
            var afterCreate = DateTime.UtcNow.AddSeconds(1);

            // Assert
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT [CreatedDateTime], [ModifiedDateTime] FROM [dbo].[Messages] WHERE [ID] = @Id";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", messageId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        Assert.True(await reader.ReadAsync());
                        var createdDateTime = reader.GetDateTime(0);
                        var modifiedDateTime = reader.GetDateTime(1);

                        Assert.True(createdDateTime >= beforeCreate && createdDateTime <= afterCreate);
                        Assert.True(modifiedDateTime >= beforeCreate && modifiedDateTime <= afterCreate);
                        Assert.Equal(createdDateTime, modifiedDateTime);
                    }
                }
            }
        }

        #endregion

        #region Performance Tests

        [Fact]
        public async Task CreateMessageAsync_100Messages_CompletesInReasonableTime()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            for (int i = 0; i < 100; i++)
            {
                await _repository.CreateMessageAsync(1000 + i, 2000 + i, $"Performance test {i}");
            }

            stopwatch.Stop();

            // Assert - Should complete in less than 10 seconds
            Assert.True(stopwatch.ElapsedMilliseconds < 10000, 
                $"Performance test took {stopwatch.ElapsedMilliseconds}ms, expected < 10000ms");
        }

        #endregion

        #region Special Character Tests

        [Fact]
        public async Task CreateMessageAsync_MessageWithSpecialCharacters_InsertsCorrectly()
        {
            // Arrange
            long to = 1010101010;
            long from = 2020202020;
            string message = "Special chars: !@#$%^&*()_+-=[]{}|;':\",./<>?";

            // Act
            long messageId = await _repository.CreateMessageAsync(to, from, message);

            // Assert
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT [Message] FROM [dbo].[Messages] WHERE [ID] = @Id";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", messageId);
                    var retrievedMessage = await command.ExecuteScalarAsync();
                    Assert.Equal(message, retrievedMessage);
                }
            }
        }

        [Fact]
        public async Task CreateMessageAsync_MessageWithUnicode_InsertsCorrectly()
        {
            // Arrange
            long to = 3030303030;
            long from = 4040404040;
            string message = "Unicode: 你好 مرحبا こんにちは 😊";

            // Act
            long messageId = await _repository.CreateMessageAsync(to, from, message);

            // Assert
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                string query = "SELECT [Message] FROM [dbo].[Messages] WHERE [ID] = @Id";
                using (var command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@Id", messageId);
                    var retrievedMessage = await command.ExecuteScalarAsync();
                    Assert.Equal(message, retrievedMessage);
                }
            }
        }

        #endregion

        #region Edge Case Tests

        [Fact]
        public async Task CreateMessageAsync_MaxPhoneNumbers_InsertsSuccessfully()
        {
            // Arrange
            long maxPhone = long.MaxValue;
            long from = 1234567890;
            string message = "Max phone test";

            // Act
            long messageId = await _repository.CreateMessageAsync(maxPhone, from, message);

            // Assert
            Assert.True(messageId > 0);
        }

        [Fact]
        public async Task CreateMessageAsync_MinimalMessage_InsertsSuccessfully()
        {
            // Arrange
            long to = 1;
            long from = 2;
            string message = "X"; // Single character

            // Act
            long messageId = await _repository.CreateMessageAsync(to, from, message);

            // Assert
            Assert.True(messageId > 0);
        }

        #endregion
    }

    /// <summary>
    /// Collection definition for database tests
    /// Ensures tests run sequentially to avoid database conflicts
    /// </summary>
    [CollectionDefinition("Database")]
    public class DatabaseCollection : ICollectionFixture<DatabaseFixture>
    {
        // This class has no code, and is never created
    }

    /// <summary>
    /// Fixture for database setup and teardown
    /// </summary>
    public class DatabaseFixture : IDisposable
    {
        public DatabaseFixture()
        {
            // Setup code if needed (e.g., ensure test database exists)
        }

        public void Dispose()
        {
            // Cleanup code if needed
        }
    }
}