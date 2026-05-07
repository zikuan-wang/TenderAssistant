namespace TenderAssistant.Client.Models;

public sealed class BidAssistCategory
{
    public BidAssistCategory(string code, string name, bool syncToLocal, string description)
    {
        Code = code;
        Name = name;
        SyncToLocal = syncToLocal;
        Description = description;
    }

    public string Code { get; }

    public string Name { get; }

    public bool SyncToLocal { get; }

    public string Description { get; }
}
