using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

var path = args.Length > 0 ? args[0] : throw new Exception("Usage: PdfExtract <pdf>");
var outPath = args.Length > 1 ? args[1] : null;
var sb = new StringBuilder();
using (var doc = PdfDocument.Open(path))
{
    foreach (var page in doc.GetPages())
    {
        var t = ContentOrderTextExtractor.GetText(page);
        if (!string.IsNullOrWhiteSpace(t)) { sb.AppendLine(t); sb.AppendLine(); }
    }
}
var text = sb.ToString();
if (outPath != null)
    File.WriteAllText(outPath, text, Encoding.UTF8);
else
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.WriteLine(text);
}
