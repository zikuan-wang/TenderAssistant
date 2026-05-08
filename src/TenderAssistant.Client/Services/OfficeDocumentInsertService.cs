using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using TenderAssistant.Client.Models;

namespace TenderAssistant.Client.Services;

public sealed class OfficeDocumentInsertService
{
    private const double PointsPerCentimeter = 28.3464567;

    public OfficeInsertResult Insert(
        BidAssistFileItem item,
        PdfQualityOption quality,
        WordInsertModeOption wordMode,
        bool pageBreakBetweenPdfPages,
        double imageWidthCentimeters,
        int pdfFirstPageCount,
        int pdfLastPageCount)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!File.Exists(item.FullPath))
        {
            return OfficeInsertResult.Fail("文件不存在或无权限访问。");
        }

        try
        {
            var context = TryGetOfficeContext();
            if (context is null)
            {
                return OfficeInsertResult.Fail("未检测到已打开的 Word/WPS 文档，请先打开标书并定位光标。");
            }

            return item.FileType switch
            {
                BidAssistFileType.Image => InsertImage(context, item, imageWidthCentimeters),
                BidAssistFileType.Text => InsertText(context, item),
                BidAssistFileType.Word => InsertWord(context, item, wordMode),
                BidAssistFileType.Pdf => InsertPdf(context, item, quality, pageBreakBetweenPdfPages, imageWidthCentimeters, pdfFirstPageCount, pdfLastPageCount),
                _ => OfficeInsertResult.Fail("该文件类型暂不支持插入。")
            };
        }
        catch (COMException ex)
        {
            return OfficeInsertResult.Fail($"Office 自动化失败：{ex.Message}");
        }
        catch (UnauthorizedAccessException)
        {
            return OfficeInsertResult.Fail("没有权限读取文件或访问目标文档。");
        }
        catch (IOException ex)
        {
            return OfficeInsertResult.Fail($"文件读取失败：{ex.Message}");
        }
        catch (Exception ex)
        {
            return OfficeInsertResult.Fail($"插入失败：{ex.Message}");
        }
    }

    private static OfficeInsertResult InsertImage(OfficeContext context, BidAssistFileItem item, double imageWidthCentimeters)
    {
        dynamic selection = context.Application.Selection;
        dynamic shape = AddPicture(selection, context.Document, item.FullPath);

        try
        {
            dynamic pageSetup = context.Document.PageSetup;
            var writableWidth = (float)(pageSetup.PageWidth - pageSetup.LeftMargin - pageSetup.RightMargin);
            var requestedWidth = (float)(Math.Clamp(imageWidthCentimeters, 1, 40) * PointsPerCentimeter);
            var targetWidth = Math.Min(requestedWidth, writableWidth);
            var ratio = targetWidth / shape.Width;
            shape.Width = targetWidth;
            shape.Height = shape.Height * ratio;
        }
        catch
        {
            // Some WPS builds expose a smaller COM surface. Insertion itself is still valid.
        }

        return OfficeInsertResult.Success(context.SoftwareName, $"图片已按 {Math.Clamp(imageWidthCentimeters, 1, 40):0.#}cm 宽度插入。");
    }

    private static dynamic AddPicture(dynamic selection, dynamic document, string path)
    {
        try
        {
            return selection.InlineShapes.AddPicture(path, false, true);
        }
        catch
        {
            try
            {
                return selection.InlineShapes.AddPicture(path);
            }
            catch
            {
                return document.InlineShapes.AddPicture(path, false, true);
            }
        }
    }

    private static OfficeInsertResult InsertText(OfficeContext context, BidAssistFileItem item)
    {
        var text = File.ReadAllText(item.FullPath, Encoding.UTF8);
        dynamic selection = context.Application.Selection;
        selection.TypeText(text);
        return OfficeInsertResult.Success(context.SoftwareName, "纯文本已插入到当前光标位置。");
    }

    private static OfficeInsertResult InsertWord(OfficeContext context, BidAssistFileItem item, WordInsertModeOption wordMode)
    {
        dynamic selection = context.Application.Selection;
        if (wordMode.Code == "plain-text")
        {
            if (Path.GetExtension(item.FullPath).Equals(".docx", StringComparison.OrdinalIgnoreCase)
                && TryExtractDocxText(item.FullPath, out var text))
            {
                selection.TypeText(text);
                return OfficeInsertResult.Success(context.SoftwareName, "Word 文档已抽取正文并按纯文本插入。");
            }

            InsertFile(selection, item.FullPath);
            return OfficeInsertResult.Success(context.SoftwareName, "该 Word 格式无法直接抽取纯文本，已按 Office 兼容方式插入。");
        }

        InsertFile(selection, item.FullPath);
        return OfficeInsertResult.Success(context.SoftwareName, "Word 文档已按保留格式方式插入。");
    }

    private static void InsertFile(dynamic selection, string path)
    {
        try
        {
            selection.InsertFile(path);
        }
        catch
        {
            selection.InsertFile(path, Type.Missing, false, false, false);
        }
    }

    private static OfficeInsertResult InsertPdf(
        OfficeContext context,
        BidAssistFileItem item,
        PdfQualityOption quality,
        bool pageBreakBetweenPdfPages,
        double imageWidthCentimeters,
        int pdfFirstPageCount,
        int pdfLastPageCount)
    {
        dynamic selection = context.Application.Selection;
        try
        {
            var pageImages = PdfPageRenderService.RenderSelectedPages(item.FullPath, quality.Dpi, pdfFirstPageCount, pdfLastPageCount);
            if (pageImages.Count == 0)
            {
                return OfficeInsertResult.Fail("PDF 未找到可插入页面。");
            }

            for (var index = 0; index < pageImages.Count; index++)
            {
                if (index > 0 && pageBreakBetweenPdfPages)
                {
                    selection.InsertBreak(7);
                }

                InsertImage(context, CreateImageInsertItem(item, pageImages[index], index + 1), imageWidthCentimeters);
            }

            var pageRangeText = pdfFirstPageCount <= 0 && pdfLastPageCount <= 0
                ? "全部页面"
                : $"前 {Math.Max(0, pdfFirstPageCount)} 页 + 后 {Math.Max(0, pdfLastPageCount)} 页";
            return OfficeInsertResult.Success(context.SoftwareName, $"PDF 已按 {pageRangeText} 转换为 {pageImages.Count} 张图片，并按 {Math.Clamp(imageWidthCentimeters, 1, 40):0.#}cm 宽度逐页插入。");
        }
        catch (Exception renderException)
        {
            try
            {
                AddOleObject(selection, item.FullPath, item.FileName);
                return OfficeInsertResult.Success(context.SoftwareName, "PDF 无法转换为正文，已作为嵌入对象插入。");
            }
            catch (Exception ex)
            {
                return OfficeInsertResult.Fail($"PDF 插入失败：转换图片失败：{renderException.Message}；嵌入对象失败：{ex.Message}");
            }
        }
    }

    private static void AddOleObject(dynamic selection, string path, string label)
    {
        try
        {
            selection.InlineShapes.AddOLEObject(
                ClassType: Type.Missing,
                FileName: path,
                LinkToFile: false,
                DisplayAsIcon: true,
                IconLabel: label);
        }
        catch
        {
            selection.InlineShapes.AddOLEObject(Type.Missing, path, false, true, Type.Missing, Type.Missing, label);
        }
    }

    private static BidAssistFileItem CreateImageInsertItem(BidAssistFileItem source, string imagePath, int pageNumber)
    {
        return new BidAssistFileItem
        {
            Id = $"{source.Id}:pdf-page:{pageNumber}",
            CategoryCode = source.CategoryCode,
            CategoryName = source.CategoryName,
            FileName = $"{Path.GetFileNameWithoutExtension(source.FileName)}-第{pageNumber}页.png",
            FullPath = imagePath,
            FileType = BidAssistFileType.Image,
            InsertMode = "PDF 页面图片插入",
            SourceLabel = source.SourceLabel,
            SizeText = source.SizeText,
            PageCount = null,
            SyncToLocal = source.SyncToLocal,
            LastModified = source.LastModified,
            ExpiresAtUtc = source.ExpiresAtUtc,
            PreviewText = source.PreviewText,
            PreviewImagePath = imagePath
        };
    }

    private static bool TryExtractDocxText(string path, out string text)
    {
        text = string.Empty;
        try
        {
            using var archive = ZipFile.OpenRead(path);
            var documentEntry = archive.GetEntry("word/document.xml");
            if (documentEntry is null)
            {
                return false;
            }

            using var stream = documentEntry.Open();
            var document = XDocument.Load(stream);
            XNamespace wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var paragraphs = document.Descendants(wordNamespace + "p")
                .Select(paragraph => string.Concat(paragraph.Descendants(wordNamespace + "t").Select(static node => node.Value)).Trim())
                .Where(static paragraphText => !string.IsNullOrWhiteSpace(paragraphText));

            text = string.Join(Environment.NewLine, paragraphs);
            return !string.IsNullOrWhiteSpace(text);
        }
        catch
        {
            return false;
        }
    }

    private static OfficeContext? TryGetOfficeContext()
    {
        foreach (var candidate in new[]
        {
            ("Word.Application", "Microsoft Word"),
            ("KWPS.Application", "WPS 文字"),
            ("WPS.Application", "WPS 文字"),
            ("kwps.Application", "WPS 文字"),
            ("wps.Application", "WPS 文字")
        })
        {
            try
            {
                var type = Type.GetTypeFromProgID(candidate.Item1);
                if (type is null)
                {
                    continue;
                }

                dynamic application = GetActiveComObject(candidate.Item1);
                dynamic document = GetActiveDocument(application);
                _ = document.Name;
                return new OfficeContext(application, document, candidate.Item2);
            }
            catch
            {
                // Try the next compatible automation ProgID.
            }
        }

        return null;
    }

    private static dynamic GetActiveDocument(dynamic application)
    {
        try
        {
            return application.ActiveDocument;
        }
        catch
        {
            if (application.Documents.Count > 0)
            {
                return application.Documents[1];
            }

            throw;
        }
    }

    private static object GetActiveComObject(string progId)
    {
        CLSIDFromProgID(progId, out var clsid);
        GetActiveObject(ref clsid, IntPtr.Zero, out var instance);
        return instance;
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll", PreserveSig = false)]
    private static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

    private sealed record OfficeContext(dynamic Application, dynamic Document, string SoftwareName);
}

public sealed record OfficeInsertResult(bool IsSuccess, string TargetSoftware, string Message)
{
    public static OfficeInsertResult Success(string targetSoftware, string message)
    {
        return new OfficeInsertResult(true, targetSoftware, message);
    }

    public static OfficeInsertResult Fail(string message)
    {
        return new OfficeInsertResult(false, "-", message);
    }
}
