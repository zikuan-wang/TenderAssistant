using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Docnet.Core;
using Docnet.Core.Models;

namespace TenderAssistant.Client.Services;

public static class PdfPageRenderService
{
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TenderAssistant",
        "PdfPageCache");

    public static IReadOnlyList<string> RenderAllPages(string pdfPath, int dpi)
    {
        return RenderSelectedPages(pdfPath, dpi, 0, 0);
    }

    public static IReadOnlyList<string> RenderSelectedPages(string pdfPath, int dpi, int firstPageCount, int lastPageCount)
    {
        using var reader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(dpi / 72.0));
        var pageCount = reader.GetPageCount();
        var pageIndexes = GetSelectedPageIndexes(pageCount, firstPageCount, lastPageCount);
        var result = new List<string>(pageIndexes.Count);
        foreach (var index in pageIndexes)
        {
            result.Add(RenderPage(reader, pdfPath, index, dpi));
        }

        return result;
    }

    public static string? RenderFirstPagePreview(string pdfPath)
    {
        try
        {
            using var reader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(1.5));
            return reader.GetPageCount() == 0 ? null : RenderPage(reader, pdfPath, 0, 108);
        }
        catch
        {
            return null;
        }
    }

    private static string RenderPage(Docnet.Core.Readers.IDocReader reader, string pdfPath, int pageIndex, int dpi)
    {
        Directory.CreateDirectory(CacheRoot);
        var cacheKey = CreateCacheKey(pdfPath, pageIndex, dpi);
        var outputPath = Path.Combine(CacheRoot, $"{cacheKey}.png");
        if (File.Exists(outputPath))
        {
            return outputPath;
        }

        using var pageReader = reader.GetPageReader(pageIndex);
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();
        var bytes = pageReader.GetImage();
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, bytes, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
        return outputPath;
    }

    private static IReadOnlyList<int> GetSelectedPageIndexes(int pageCount, int firstPageCount, int lastPageCount)
    {
        if (pageCount <= 0)
        {
            return [];
        }

        var first = Math.Clamp(firstPageCount, 0, pageCount);
        var last = Math.Clamp(lastPageCount, 0, pageCount);
        if (first == 0 && last == 0)
        {
            return Enumerable.Range(0, pageCount).ToArray();
        }

        var selected = new SortedSet<int>();
        for (var index = 0; index < first; index++)
        {
            selected.Add(index);
        }

        for (var index = Math.Max(0, pageCount - last); index < pageCount; index++)
        {
            selected.Add(index);
        }

        return selected.ToArray();
    }

    private static string CreateCacheKey(string path, int pageIndex, int dpi)
    {
        var file = new FileInfo(path);
        var raw = $"{file.FullName}|{file.Length}|{file.LastWriteTimeUtc.Ticks}|{pageIndex}|{dpi}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }
}
