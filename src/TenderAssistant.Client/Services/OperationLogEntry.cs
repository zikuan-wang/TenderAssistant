namespace TenderAssistant.Client.Services;

public sealed record OperationLogEntry(
    DateTimeOffset OccurredAt,
    string Level,
    string Category,
    string Action,
    string Message);
