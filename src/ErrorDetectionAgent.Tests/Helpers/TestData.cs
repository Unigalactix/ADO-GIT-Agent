using ErrorDetectionAgent.Core.Models;

namespace ErrorDetectionAgent.Tests.Helpers;

/// <summary>
/// Provides reusable sample data for tests — mirrors the sample data
/// from sql/setup-log-table.sql.
/// </summary>
public static class TestData
{
    /// <summary>
    /// Returns a set of sample error log entries that mimic the SQL sample data.
    /// </summary>
    public static List<ErrorLogEntry> GetSampleErrors() =>
    [
        new()
        {
            Id = 1,
            Timestamp = DateTime.UtcNow.AddHours(-2),
            Severity = "Error",
            Message = "NullReferenceException: Object reference not set to an instance of an object.",
            StackTrace = """
                at MyApp.Services.OrderService.ProcessOrder(Int32 orderId) in OrderService.cs:line 45
                at MyApp.Controllers.OrderController.Post(OrderRequest request) in OrderController.cs:line 22
                """,
            Source = "OrderService",
            ErrorCode = "NRE-001"
        },
        new()
        {
            Id = 2,
            Timestamp = DateTime.UtcNow.AddHours(-1),
            Severity = "Error",
            Message = "NullReferenceException: Object reference not set to an instance of an object.",
            StackTrace = """
                at MyApp.Services.OrderService.ProcessOrder(Int32 orderId) in OrderService.cs:line 45
                at MyApp.Controllers.OrderController.Post(OrderRequest request) in OrderController.cs:line 22
                """,
            Source = "OrderService",
            ErrorCode = "NRE-001"
        },
        new()
        {
            Id = 3,
            Timestamp = DateTime.UtcNow.AddMinutes(-30),
            Severity = "Critical",
            Message = "SqlException: Timeout expired. The timeout period elapsed prior to completion of the operation.",
            StackTrace = """
                at Microsoft.Data.SqlClient.SqlConnection.Open()
                at MyApp.Repositories.UserRepository.GetById(Int32 userId) in UserRepository.cs:line 18
                """,
            Source = "UserRepository",
            ErrorCode = "SQL-TIMEOUT"
        },
        new()
        {
            Id = 4,
            Timestamp = DateTime.UtcNow.AddMinutes(-15),
            Severity = "Exception",
            Message = "InvalidOperationException: Sequence contains no elements",
            StackTrace = """
                at System.Linq.ThrowHelper.ThrowNoElementsException()
                at MyApp.Services.ReportService.GenerateMonthlyReport() in ReportService.cs:line 67
                """,
            Source = "ReportService",
            ErrorCode = "LINQ-001"
        }
    ];

    /// <summary>
    /// Returns a single sample aggregated error for use in service-level tests.
    /// </summary>
    public static AggregatedError GetSampleAggregatedError() => new()
    {
        Fingerprint = "A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2C3D4E5F6A1B2",
        Message = "NullReferenceException: Object reference not set to an instance of an object.",
        Severity = "Error",
        Source = "OrderService",
        OccurrenceCount = 2,
        FirstSeen = DateTime.UtcNow.AddHours(-2),
        LastSeen = DateTime.UtcNow.AddHours(-1),
        StackTrace = "at MyApp.Services.OrderService.ProcessOrder(Int32 orderId)",
        Entries = GetSampleErrors().Take(2).ToList()
    };

    /// <summary>
    /// Returns a sample work item result.
    /// </summary>
    public static WorkItemResult GetSampleWorkItemResult() => new()
    {
        Id = 42,
        Url = "https://dev.azure.com/testorg/testproject/_workitems/edit/42",
        Title = "Error: NullReferenceException: Object reference not set to an instance of an object.",
        State = "New"
    };

    /// <summary>
    /// Returns a sample fix proposal from the LLM.
    /// </summary>
    public static FixProposal GetSampleFixProposal() => new()
    {
        Summary = "Add null check before calling ProcessOrder",
        SuggestedDiff = "if (order == null) throw new ArgumentNullException(nameof(order));",
        AffectedFiles = ["src/Services/OrderService.cs"],
        Confidence = 0.85,
        BranchName = "fix/a1b2c3d4-nullreferenceexception--object-reference-n"
    };
}
