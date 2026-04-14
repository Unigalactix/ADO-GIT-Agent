-- ══════════════════════════════════════════════════════════════════════
--  Azure SQL DB — Application Log Table Schema
--
--  Run this script against your Azure SQL Database to create the
--  log table that the Error Detection Agent reads from.
--
--  Your application's logging framework (Serilog, NLog, etc.) should
--  be configured to write error-level entries to this table.
-- ══════════════════════════════════════════════════════════════════════

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApplicationLog')
BEGIN
    CREATE TABLE dbo.ApplicationLog
    (
        Id          BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Timestamp] DATETIME2           NOT NULL DEFAULT SYSUTCDATETIME(),
        Severity    NVARCHAR(50)        NOT NULL,
        [Message]   NVARCHAR(MAX)       NOT NULL,
        StackTrace  NVARCHAR(MAX)       NULL,
        [Source]    NVARCHAR(200)       NULL,
        ErrorCode   NVARCHAR(100)       NULL
    );

    -- Index for the agent's time-range + severity query
    CREATE NONCLUSTERED INDEX IX_ApplicationLog_Severity_Timestamp
        ON dbo.ApplicationLog (Severity, [Timestamp] DESC)
        INCLUDE ([Message], StackTrace, [Source], ErrorCode);

    PRINT 'Table dbo.ApplicationLog created successfully.';
END
ELSE
BEGIN
    PRINT 'Table dbo.ApplicationLog already exists — skipping.';
END
GO

-- ══════════════════════════════════════════════════════════════════════
--  Sample data (optional — for testing purposes)
-- ══════════════════════════════════════════════════════════════════════

INSERT INTO dbo.ApplicationLog ([Timestamp], Severity, [Message], StackTrace, [Source], ErrorCode)
VALUES
    (DATEADD(HOUR, -2, SYSUTCDATETIME()), 'Error',
     'NullReferenceException: Object reference not set to an instance of an object.',
     'at MyApp.Services.OrderService.ProcessOrder(Int32 orderId) in OrderService.cs:line 45
at MyApp.Controllers.OrderController.Post(OrderRequest request) in OrderController.cs:line 22',
     'OrderService', 'NRE-001'),

    (DATEADD(HOUR, -1, SYSUTCDATETIME()), 'Error',
     'NullReferenceException: Object reference not set to an instance of an object.',
     'at MyApp.Services.OrderService.ProcessOrder(Int32 orderId) in OrderService.cs:line 45
at MyApp.Controllers.OrderController.Post(OrderRequest request) in OrderController.cs:line 22',
     'OrderService', 'NRE-001'),

    (DATEADD(MINUTE, -30, SYSUTCDATETIME()), 'Critical',
     'SqlException: Timeout expired. The timeout period elapsed prior to completion of the operation.',
     'at Microsoft.Data.SqlClient.SqlConnection.Open()
at MyApp.Repositories.UserRepository.GetById(Int32 userId) in UserRepository.cs:line 18',
     'UserRepository', 'SQL-TIMEOUT'),

    (DATEADD(MINUTE, -15, SYSUTCDATETIME()), 'Exception',
     'InvalidOperationException: Sequence contains no elements',
     'at System.Linq.ThrowHelper.ThrowNoElementsException()
at MyApp.Services.ReportService.GenerateMonthlyReport() in ReportService.cs:line 67',
     'ReportService', 'LINQ-001');

PRINT 'Sample error log data inserted.';
GO
