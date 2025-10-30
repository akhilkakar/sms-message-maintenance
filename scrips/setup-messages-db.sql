-- Connect to your database and run this script

-- Create Messages table with enhanced schema
CREATE TABLE [dbo].[Messages] (
    [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
    [To] BIGINT NOT NULL,
    [From] BIGINT NOT NULL,
    [Message] NVARCHAR(1000) NOT NULL,
    [Status] NVARCHAR(100) NULL,
    [StatusReason] NVARCHAR(500) NULL,
    [RetryCount] INT NOT NULL DEFAULT 0,
    [QueuedDateTime] DATETIME2 NULL,
    [ProcessedDateTime] DATETIME2 NULL,
    [CreatedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    [ModifiedDateTime] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

-- Create indexes for performance
CREATE INDEX IX_Status_Created 
ON [dbo].[Messages] (Status, CreatedDateTime);

CREATE INDEX IX_ProcessedDateTime 
ON [dbo].[Messages] (ProcessedDateTime) 
WHERE ProcessedDateTime IS NOT NULL;

-- Insert sample data for testing
INSERT INTO [dbo].[Messages] 
    ([To], [From], [Message], [Status], [CreatedDateTime], [ModifiedDateTime])
VALUES 
    ('0412345678', '0498765432', 'Hello! This is a test message.', 'Successfully Sent', GETUTCDATE(), GETUTCDATE()),
    ('0412345679', '0498765432', 'Your appointment is confirmed for tomorrow.', 'Successfully Sent', GETUTCDATE(), GETUTCDATE()),
    ('0412345680', '0498765432', 'Your order #12345 has shipped!', 'Pending', GETUTCDATE(), GETUTCDATE()),
    ('0412345681', '0498765432', 'Invalid phone number test', 'Not Sent - Not a valid phone', GETUTCDATE(), GETUTCDATE()),
    ('0412345682', '0498765432', 'This is a test of timezone validation', 'Not Sent - Not valid by Time zone', GETUTCDATE(), GETUTCDATE()),
    ('0412345683', '0498765432', 'Another test message', 'Queued', GETUTCDATE(), GETUTCDATE()),
    ('0412345684', '0498765432', 'Password reset request received', 'Processing', GETUTCDATE(), GETUTCDATE()),
    ('0412345685', '0498765432', 'Your verification code is 123456', 'Successfully Sent', GETUTCDATE(), GETUTCDATE()),
    ('0412345686', '0498765432', 'Thank you for your purchase!', 'Successfully Sent', GETUTCDATE(), GETUTCDATE()),
    ('0412345687', '0498765432', 'Your subscription expires soon', 'Pending', GETUTCDATE(), GETUTCDATE());

-- Verify data
SELECT COUNT(*) as TotalMessages FROM [dbo].[Messages];
SELECT TOP 10 * FROM [dbo].[Messages] ORDER BY CreatedDateTime DESC;
