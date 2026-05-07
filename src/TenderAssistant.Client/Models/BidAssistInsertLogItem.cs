namespace TenderAssistant.Client.Models;

public sealed class BidAssistInsertLogItem
{
    public BidAssistInsertLogItem(DateTime occurredAt, string fileName, string targetSoftware, string result, string message)
    {
        OccurredAt = occurredAt;
        FileName = fileName;
        TargetSoftware = targetSoftware;
        Result = result;
        Message = message;
    }

    public DateTime OccurredAt { get; }

    public string FileName { get; }

    public string TargetSoftware { get; }

    public string Result { get; }

    public string Message { get; }
}
