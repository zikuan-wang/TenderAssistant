namespace TenderAssistant.Client.Models;

public sealed class BidAssistFileItem
{
    public required string Id { get; init; }

    public required string CategoryCode { get; init; }

    public required string CategoryName { get; init; }

    public required string FileName { get; init; }

    public required string FullPath { get; init; }

    public required BidAssistFileType FileType { get; init; }

    public required string InsertMode { get; init; }

    public required string SourceLabel { get; init; }

    public required string SizeText { get; init; }

    public int? PageCount { get; init; }

    public bool SyncToLocal { get; init; }

    public DateTime LastModified { get; init; }

    public DateTimeOffset? ExpiresAtUtc { get; init; }

    public string PreviewText { get; init; } = string.Empty;

    public string? PreviewImagePath { get; init; }

    public bool HasImagePreview => (FileType == BidAssistFileType.Image || FileType == BidAssistFileType.Pdf) && !string.IsNullOrWhiteSpace(PreviewImagePath);

    public bool HasTextPreview => !HasImagePreview;

    public string PageCountText => PageCount is null ? "-" : $"{PageCount} 页";

    public string FileTypeText => FileType switch
    {
        BidAssistFileType.Pdf => "PDF",
        BidAssistFileType.Image => "图片",
        BidAssistFileType.Word => "Word",
        BidAssistFileType.Text => "文本",
        _ => "未知"
    };

    public string ExpiresAtText => ExpiresAtUtc is null ? "长期有效" : ExpiresAtUtc.Value.ToLocalTime().ToString("yyyy-MM-dd");

}

public enum BidAssistFileType
{
    Pdf,
    Image,
    Word,
    Text,
    Unsupported
}
