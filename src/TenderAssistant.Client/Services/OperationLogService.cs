using System.Text;
using System.Text.Json;
using System.IO;

namespace TenderAssistant.Client.Services;

public static class OperationLogService
{
    private const int AutoArchiveThreshold = 2000;
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TenderAssistant",
        "Logs");

    private static readonly string CurrentLogPath = Path.Combine(LogDirectory, "operation-log.jsonl");
    private static readonly string ArchiveDirectory = Path.Combine(LogDirectory, "Archive");

    public static void Info(string category, string action, string message)
    {
        Write("INFO", category, action, message);
    }

    public static void Warning(string category, string action, string message)
    {
        Write("WARN", category, action, message);
    }

    public static IReadOnlyList<OperationLogEntry> ReadRecent(int take = 300)
    {
        lock (SyncRoot)
        {
            if (!File.Exists(CurrentLogPath))
            {
                return Array.Empty<OperationLogEntry>();
            }

            return File.ReadLines(CurrentLogPath, Encoding.UTF8)
                .Reverse()
                .Take(Math.Clamp(take, 1, 1000))
                .Select(Deserialize)
                .Where(static entry => entry is not null)
                .Cast<OperationLogEntry>()
                .ToArray();
        }
    }

    public static IReadOnlyList<FileInfo> ListArchives()
    {
        Directory.CreateDirectory(ArchiveDirectory);
        return new DirectoryInfo(ArchiveDirectory)
            .GetFiles("client-operation-log-*.txt")
            .OrderByDescending(static file => file.CreationTimeUtc)
            .ToArray();
    }

    public static FileInfo ArchiveCurrent(string reason)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(ArchiveDirectory);

            var archivePath = Path.Combine(
                ArchiveDirectory,
                $"client-operation-log-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            var entries = File.Exists(CurrentLogPath)
                ? File.ReadLines(CurrentLogPath, Encoding.UTF8).Select(Deserialize).Where(static entry => entry is not null).Cast<OperationLogEntry>().ToArray()
                : Array.Empty<OperationLogEntry>();

            using (var writer = new StreamWriter(archivePath, false, Encoding.UTF8))
            {
                writer.WriteLine($"Archive Reason: {reason}");
                writer.WriteLine($"Archive Time: {DateTimeOffset.Now:O}");
                writer.WriteLine($"Entry Count: {entries.Length}");
                writer.WriteLine();
                foreach (var entry in entries.OrderBy(static item => item.OccurredAt))
                {
                    writer.WriteLine($"{entry.OccurredAt:O}\t{entry.Level}\t{entry.Category}\t{entry.Action}\t{entry.Message}");
                }
            }

            File.WriteAllText(CurrentLogPath, string.Empty, Encoding.UTF8);
            return new FileInfo(archivePath);
        }
    }

    public static void ClearCurrent()
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(LogDirectory);
            File.WriteAllText(CurrentLogPath, string.Empty, Encoding.UTF8);
        }
    }

    public static void ExportCurrent(string targetPath)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var entries = ReadRecent(1000).Reverse().ToArray();
            using var writer = new StreamWriter(targetPath, false, Encoding.UTF8);
            foreach (var entry in entries)
            {
                writer.WriteLine($"{entry.OccurredAt:O}\t{entry.Level}\t{entry.Category}\t{entry.Action}\t{entry.Message}");
            }
        }
    }

    private static void Write(string level, string category, string action, string message)
    {
        lock (SyncRoot)
        {
            Directory.CreateDirectory(LogDirectory);
            var entry = new OperationLogEntry(DateTimeOffset.Now, level, category, action, message);
            File.AppendAllText(CurrentLogPath, JsonSerializer.Serialize(entry) + Environment.NewLine, Encoding.UTF8);

            if (File.ReadLines(CurrentLogPath, Encoding.UTF8).Count() >= AutoArchiveThreshold)
            {
                ArchiveCurrent($"auto-threshold-{AutoArchiveThreshold}");
            }
        }
    }

    private static OperationLogEntry? Deserialize(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<OperationLogEntry>(line);
        }
        catch
        {
            return null;
        }
    }
}
