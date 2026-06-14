using System.IO;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ExamCoachDesktop;

public static class AssignmentDocumentReader
{
    public static string ReadFile(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".pdf" => ReadPdf(path),
            ".txt" or ".md" or ".json" => File.ReadAllText(path, Encoding.UTF8),
            _ => throw new NotSupportedException($"Формат {ext} не поддерживается. Используйте PDF или TXT.")
        };
    }

    public static string ReadPdf(string path)
    {
        try
        {
            return ExtractPdfText(path);
        }
        catch (IOException)
        {
            // Telegram и другие программы иногда держат файл открытым — читаем копию из памяти.
            var bytes = File.ReadAllBytes(path);
            return ExtractPdfText(bytes);
        }
    }

    private static string ExtractPdfText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return ExtractPdfText(stream);
    }

    private static string ExtractPdfText(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        return ExtractPdfText(stream);
    }

    private static string ExtractPdfText(Stream stream)
    {
        var sb = new StringBuilder();
        using var document = PdfDocument.Open(stream);

        foreach (var page in document.GetPages())
        {
            var text = ContentOrderTextExtractor.GetText(page);
            if (!string.IsNullOrWhiteSpace(text))
            {
                sb.AppendLine(text);
                sb.AppendLine();
            }
        }

        var result = sb.ToString().Trim();
        if (string.IsNullOrWhiteSpace(result))
            throw new InvalidOperationException(
                "PDF не содержит извлекаемого текста (возможно, это скан без OCR). " +
                "Скопируйте текст вручную или используйте PDF с текстовым слоем.");

        return result;
    }
}
