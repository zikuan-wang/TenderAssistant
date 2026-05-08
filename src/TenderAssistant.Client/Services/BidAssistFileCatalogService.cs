using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using TenderAssistant.Client.Models;

namespace TenderAssistant.Client.Services;

public sealed class BidAssistFileCatalogService
{
    public const string DeferredPreviewText = "已加载文件清单。选择文件后再读取预览，避免刷新文件库时卡顿。";

    private static readonly Regex PdfPageRegex = new(@"/Type\s*/Page\b", RegexOptions.Compiled);
    private static readonly Regex PdfTextRegex = new(@"\((?<text>(?:\\.|[^\\)])*)\)\s*Tj", RegexOptions.Compiled);
    private static readonly Regex RtfControlRegex = new(@"\\[a-z]+\d* ?|[{}]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public IReadOnlyList<BidAssistCategory> GetCategories()
    {
        return
        [
            new("technical", "技术文件", true, "本地技术资料目录。"),
            new("business", "商务文件", true, "本地商务资料目录。"),
            new("qualification", "资质文件", true, "本地资质资料目录。"),
            new("custom", "自定义导入", false, "用户临时选择文件，仅在当前列表使用。")
        ];
    }

    public FileLibraryFolderSyncResult SyncFromFolder(string sourceRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException("源文件库目录不存在。");
        }

        EnsureLibraryFolders();
        var copied = 0;
        var skipped = 0;

        foreach (var mapping in CategoryFolderMappings)
        {
            var sourceDirectory = ResolveCategorySourceDirectory(sourceRoot, mapping.SourceNames);
            if (sourceDirectory is null)
            {
                skipped++;
                continue;
            }

            var targetDirectory = Path.Combine(LibraryRoot, mapping.CategoryCode);
            Directory.CreateDirectory(targetDirectory);
            foreach (var sourceFile in Directory.EnumerateFiles(sourceDirectory, "*.*", SearchOption.AllDirectories)
                         .Where(static path => !path.EndsWith(".ta-meta.json", StringComparison.OrdinalIgnoreCase)))
            {
                var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                var targetFile = Path.Combine(targetDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile) ?? targetDirectory);
                if (File.Exists(targetFile) && HasSameFileState(sourceFile, targetFile))
                {
                    skipped++;
                    continue;
                }

                File.Copy(sourceFile, targetFile, true);
                File.SetLastWriteTimeUtc(targetFile, File.GetLastWriteTimeUtc(sourceFile));
                copied++;
            }
        }

        return new FileLibraryFolderSyncResult(copied, skipped, LibraryRoot);
    }

    public IReadOnlyList<BidAssistFileItem> LoadLibraryFiles()
    {
        EnsureLibraryFolders();
        var items = new List<BidAssistFileItem>();

        foreach (var category in GetCategories().Where(static item => item.SyncToLocal))
        {
            var directory = Path.Combine(LibraryRoot, category.Code);
            foreach (var file in Directory.EnumerateFiles(directory).Where(static path => !path.EndsWith(".ta-meta.json", StringComparison.OrdinalIgnoreCase)))
            {
                var item = CreateItem(file, category);
                if (item is not null)
                {
                    items.Add(item);
                }
            }
        }

        return items
            .OrderBy(static item => item.CategoryCode)
            .ThenBy(static item => item.FileName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public BidAssistFileItem? CreateCustomFile(string path)
    {
        var custom = GetCategories().Single(static item => item.Code == "custom");
        return CreateItem(path, custom);
    }

    public BidAssistFileItem? LoadPreview(BidAssistFileItem item)
    {
        var category = GetCategories().FirstOrDefault(category => string.Equals(category.Code, item.CategoryCode, StringComparison.OrdinalIgnoreCase))
            ?? new BidAssistCategory(item.CategoryCode, item.CategoryName, item.SyncToLocal, string.Empty);
        return CreateItem(item.FullPath, category, includePreview: true);
    }

    public BidAssistFileItem LoadPageCount(BidAssistFileItem item)
    {
        if (item.FileType != BidAssistFileType.Pdf || item.PageCount is not null || !File.Exists(item.FullPath))
        {
            return item;
        }

        return new BidAssistFileItem
        {
            Id = item.Id,
            CategoryCode = item.CategoryCode,
            CategoryName = item.CategoryName,
            FileName = item.FileName,
            FullPath = item.FullPath,
            FileType = item.FileType,
            InsertMode = item.InsertMode,
            SourceLabel = item.SourceLabel,
            SizeText = item.SizeText,
            PageCount = TryCountPdfPages(item.FullPath),
            SyncToLocal = item.SyncToLocal,
            LastModified = item.LastModified,
            ExpiresAtUtc = item.ExpiresAtUtc,
            PreviewText = item.PreviewText,
            PreviewImagePath = item.PreviewImagePath,
            IsPreviewLoaded = item.IsPreviewLoaded
        };
    }

    private static BidAssistFileItem? CreateItem(string path, BidAssistCategory category, bool includePreview = false)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var file = new FileInfo(path);
        var type = DetectType(file.Extension);
        var pageCount = includePreview && type == BidAssistFileType.Pdf ? TryCountPdfPages(path) : null;
        var preview = includePreview ? CreatePreview(file, type, pageCount) : DeferredPreviewText;
        var metadata = ReadMetadata(file.FullName);

        return new BidAssistFileItem
        {
            Id = $"{category.Code}:{file.FullName}",
            CategoryCode = category.Code,
            CategoryName = category.Name,
            FileName = file.Name,
            FullPath = file.FullName,
            FileType = type,
            InsertMode = GetDefaultInsertMode(type),
            SourceLabel = category.SyncToLocal ? "本地文件库" : "用户本地选择",
            SizeText = FormatSize(file.Length),
            PageCount = pageCount,
            SyncToLocal = category.SyncToLocal,
            LastModified = file.LastWriteTime,
            ExpiresAtUtc = metadata?.ExpiresAtUtc,
            PreviewText = preview,
            PreviewImagePath = includePreview
                ? type == BidAssistFileType.Image
                    ? file.FullName
                    : type == BidAssistFileType.Pdf
                        ? PdfPageRenderService.RenderFirstPagePreview(file.FullName)
                        : null
                : null,
            IsPreviewLoaded = includePreview
        };
    }

    private static void EnsureLibraryFolders()
    {
        foreach (var category in new[] { "technical", "business", "qualification", "custom" })
        {
            Directory.CreateDirectory(Path.Combine(LibraryRoot, category));
        }

        SeedTextFile(
            Path.Combine(LibraryRoot, "technical", "技术方案示例.txt"),
            "技术方案示例\r\n\r\n这里用于放置本地技术资料，可将 PDF、图片、Word 或 TXT 文件放入该目录。");
        SeedTextFile(
            Path.Combine(LibraryRoot, "business", "商务条款示例.txt"),
            "商务条款示例\r\n\r\n这里用于放置本地商务资料，插入时默认按纯文本处理。");
        SeedTextFile(
            Path.Combine(LibraryRoot, "qualification", "资质说明示例.txt"),
            "资质说明示例\r\n\r\n证书、扫描件和盖章文件建议使用 PDF 或图片格式。");
    }

    private static string LibraryRoot => ClientAppSettingsService.FileLibraryCachePath;

    private static readonly CategoryFolderMapping[] CategoryFolderMappings =
    [
        new("technical", ["technical", "技术文件", "技术"]),
        new("business", ["business", "商务文件", "商务"]),
        new("qualification", ["qualification", "资质文件", "资质"])
    ];

    public bool DeleteLocalCache(BidAssistFileItem item)
    {
        if (!item.SyncToLocal || !File.Exists(item.FullPath))
        {
            return false;
        }

        File.Delete(item.FullPath);
        var metadataPath = GetMetadataPath(item.FullPath);
        if (File.Exists(metadataPath))
        {
            File.Delete(metadataPath);
        }

        return true;
    }

    private static void SeedTextFile(string path, string content)
    {
        if (!File.Exists(path))
        {
            File.WriteAllText(path, content, Encoding.UTF8);
        }
    }

    private static BidAssistFileType DetectType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => BidAssistFileType.Pdf,
            ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif" or ".tif" or ".tiff" => BidAssistFileType.Image,
            ".doc" or ".docx" or ".rtf" => BidAssistFileType.Word,
            ".txt" => BidAssistFileType.Text,
            _ => BidAssistFileType.Unsupported
        };
    }

    private static string GetDefaultInsertMode(BidAssistFileType type)
    {
        return type switch
        {
            BidAssistFileType.Pdf => "PDF 逐页图片插入",
            BidAssistFileType.Image => "图片适配页面宽度",
            BidAssistFileType.Word => "Word 保留格式插入",
            BidAssistFileType.Text => "纯文本插入",
            _ => "暂不支持"
        };
    }

    private static string CreatePreview(FileInfo file, BidAssistFileType type, int? pageCount)
    {
        return type switch
        {
            BidAssistFileType.Text => ReadTextPreview(file.FullName),
            BidAssistFileType.Image => CreateImagePreview(file.FullName),
            BidAssistFileType.Pdf => CreatePdfPreview(file.FullName, pageCount),
            BidAssistFileType.Word => CreateWordPreview(file),
            _ => $"暂不支持的文件类型：{file.Extension}"
        };
    }

    private static string ReadTextPreview(string path)
    {
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, true);
            var buffer = new char[3000];
            var read = reader.Read(buffer, 0, buffer.Length);
            return new string(buffer, 0, read);
        }
        catch (Exception ex)
        {
            return $"文本预览失败：{ex.Message}";
        }
    }

    private static string CreateImagePreview(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var frame = decoder.Frames[0];
            return $"图片文件\r\n尺寸：{frame.PixelWidth} x {frame.PixelHeight}\r\n插入方式：自动适配页面可打印宽度。";
        }
        catch (Exception ex)
        {
            return $"图片预览失败：{ex.Message}";
        }
    }

    private static string CreatePdfPreview(string path, int? pageCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("PDF 文件");
        builder.AppendLine($"页数：{(pageCount is null ? "未知" : pageCount.Value.ToString(CultureInfo.CurrentCulture))}");
        builder.AppendLine("插入方式：按质量选项渲染为图片后依次插入。");

        var text = ExtractPdfText(path);
        if (!string.IsNullOrWhiteSpace(text))
        {
            builder.AppendLine();
            builder.AppendLine("文本预览：");
            builder.AppendLine(text);
        }

        return builder.ToString();
    }

    private static string CreateWordPreview(FileInfo file)
    {
        return file.Extension.Equals(".docx", StringComparison.OrdinalIgnoreCase)
            ? ReadDocxPreview(file.FullName)
            : file.Extension.Equals(".rtf", StringComparison.OrdinalIgnoreCase)
                ? ReadRtfPreview(file.FullName)
                : $"Word/WPS 文档\r\n文件：{file.Name}\r\n旧版 .doc 文件需要使用 Word/WPS 打开预览；插入时可选择保留格式。";
    }

    private static string ReadDocxPreview(string path)
    {
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var entry = archive.GetEntry("word/document.xml");
            if (entry is null)
            {
                return "DOCX 预览失败：未找到正文内容。";
            }

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            var text = string.Join(Environment.NewLine, document.Descendants()
                .Where(element => element.Name.LocalName == "p")
                .Select(paragraph => string.Concat(paragraph.Descendants().Where(run => run.Name.LocalName == "t").Select(run => run.Value)).Trim())
                .Where(static line => !string.IsNullOrWhiteSpace(line))
                .Take(80));

            return string.IsNullOrWhiteSpace(text) ? "DOCX 文件未提取到可预览文本。" : text;
        }
        catch (Exception ex)
        {
            return $"DOCX 预览失败：{ex.Message}";
        }
    }

    private static string ReadRtfPreview(string path)
    {
        try
        {
            var content = File.ReadAllText(path, Encoding.UTF8);
            var text = RtfControlRegex.Replace(content, string.Empty).Trim();
            return string.IsNullOrWhiteSpace(text) ? "RTF 文件未提取到可预览文本。" : text[..Math.Min(text.Length, 3000)];
        }
        catch (Exception ex)
        {
            return $"RTF 预览失败：{ex.Message}";
        }
    }

    private static string ExtractPdfText(string path)
    {
        try
        {
            var content = File.ReadAllText(path, Encoding.Latin1);
            var lines = PdfTextRegex.Matches(content)
                .Select(match => match.Groups["text"].Value.Replace(@"\(", "(", StringComparison.Ordinal).Replace(@"\)", ")", StringComparison.Ordinal))
                .Where(static text => !string.IsNullOrWhiteSpace(text))
                .Take(80);
            var text = string.Join(Environment.NewLine, lines);
            return text.Length > 3000 ? text[..3000] : text;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static int? TryCountPdfPages(string path)
    {
        try
        {
            var content = File.ReadAllText(path, Encoding.Latin1);
            var count = PdfPageRegex.Matches(content).Count;
            return count > 0 ? count : null;
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? $"{Guid.NewGuid():N}.bin" : sanitized;
    }

    private static string? ResolveCategorySourceDirectory(string sourceRoot, IReadOnlyList<string> candidateNames)
    {
        foreach (var name in candidateNames)
        {
            var path = Path.Combine(sourceRoot, name);
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static bool HasSameFileState(string sourceFile, string targetFile)
    {
        var source = new FileInfo(sourceFile);
        var target = new FileInfo(targetFile);
        return source.Length == target.Length && source.LastWriteTimeUtc <= target.LastWriteTimeUtc;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        var value = (double)bytes;
        var unitIndex = 0;
        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return $"{value:0.##} {units[unitIndex]}";
    }

    private sealed record LocalFileMetadata(DateTimeOffset? ExpiresAtUtc);

    private static string GetMetadataPath(string path)
    {
        return $"{path}.ta-meta.json";
    }

    private static void WriteMetadata(string path, DateTimeOffset? expiresAtUtc)
    {
        File.WriteAllText(GetMetadataPath(path), System.Text.Json.JsonSerializer.Serialize(new LocalFileMetadata(expiresAtUtc)));
    }

    private static LocalFileMetadata? ReadMetadata(string path)
    {
        try
        {
            var metadataPath = GetMetadataPath(path);
            return File.Exists(metadataPath)
                ? System.Text.Json.JsonSerializer.Deserialize<LocalFileMetadata>(File.ReadAllText(metadataPath))
                : null;
        }
        catch
        {
            return null;
        }
    }
}

public sealed record FileLibraryFolderSyncResult(int Copied, int Skipped, string TargetRoot);

public sealed record CategoryFolderMapping(string CategoryCode, IReadOnlyList<string> SourceNames);

