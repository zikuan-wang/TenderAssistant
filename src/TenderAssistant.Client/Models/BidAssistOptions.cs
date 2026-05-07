namespace TenderAssistant.Client.Models;

public sealed record PdfQualityOption(string Name, int Dpi, string UseCase)
{
    public string DisplayName => $"{Name} ({Dpi} DPI)";
}

public sealed record WordInsertModeOption(string Name, string Code);
