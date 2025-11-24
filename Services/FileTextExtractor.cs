using System.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.Maui.Storage;
using Syncfusion.Pdf.Parsing;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;

namespace mindvault.Services;

public class FileTextExtractor
{
    // Public entry
    public async Task<string> ExtractAsync(FileResult file)
    {
        if (file == null) return string.Empty;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var stream = await file.OpenReadAsync();
        string raw = ext switch
        {
            ".pptx" => await ExtractPptxAsync(stream),
            ".pdf"  => await ExtractPdfAsync(stream),
            ".docx" => await ExtractDocxAsync(stream),
            ".txt"  => await ExtractTxtAsync(stream),
            _       => await ExtractTxtAsync(stream)
        };
        return Clean(raw);
    }

    // 1. PDF extraction (Syncfusion)
    async Task<string> ExtractPdfAsync(Stream s)
    {
        try
        {
            Stream work = s.CanSeek ? s : await CopyToMemoryAsync(s);
            work.Position = 0;
            using var loaded = new PdfLoadedDocument(work);
            var sb = new StringBuilder();
            for (int i = 0; i < loaded.Pages.Count; i++)
            {
                var pageObj = loaded.Pages[i];
                try
                {
                    dynamic dynPage = pageObj; // late bind for ExtractText
                    string text = dynPage.ExtractText();
                    if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                }
                catch { }
            }
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    // 2. DOCX extraction via OpenXml (more robust than XML substring parsing)
    async Task<string> ExtractDocxAsync(Stream s)
    {
        try
        {
            using var ms = new MemoryStream(); await s.CopyToAsync(ms); ms.Position = 0;
            using var doc = WordprocessingDocument.Open(ms, false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var para in body.Elements<Paragraph>())
            {
                var text = para.InnerText;
                if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
            }
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    // 3. PPTX extraction via zip (unchanged with minor cleanup)
    async Task<string> ExtractPptxAsync(Stream s)
    {
        try
        {
            using var ms = new MemoryStream(); await s.CopyToAsync(ms); ms.Position = 0;
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, true);
            var sb = new StringBuilder();
            foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml")))
            {
                using var es = entry.Open(); using var reader = new StreamReader(es);
                var xml = await reader.ReadToEndAsync();
                int idx = 0;
                while (true)
                {
                    var open = xml.IndexOf("<a:t", idx, StringComparison.OrdinalIgnoreCase);
                    if (open == -1) break;
                    open = xml.IndexOf('>', open);
                    if (open == -1) break;
                    var close = xml.IndexOf("</a:t>", open, StringComparison.OrdinalIgnoreCase);
                    if (close == -1) break;
                    var inner = xml.Substring(open + 1, close - open - 1);
                    sb.Append(inner.Replace("&amp;", "&").Replace("&quot;", "\"")).Append(' ');
                    idx = close + 6;
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch { return string.Empty; }
    }

    // 4. Plain text
    async Task<string> ExtractTxtAsync(Stream s)
    {
        using var reader = new StreamReader(s);
        return await reader.ReadToEndAsync();
    }

    static async Task<MemoryStream> CopyToMemoryAsync(Stream s)
    {
        var ms = new MemoryStream();
        await s.CopyToAsync(ms);
        ms.Position = 0;
        return ms;
    }

    // Text normalization / cleanup
    string Clean(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        // Replace CRLF variants with \n
        text = text.Replace('\r', '\n');
        // Collapse multiple newlines
        while (text.Contains("\n\n\n")) text = text.Replace("\n\n\n", "\n\n");

        var sb = new StringBuilder(text.Length);
        bool lastSpace = false;
        foreach (var ch in text)
        {
            if (char.IsControl(ch) && ch != '\n' && ch != '\t') continue;
            if (char.IsWhiteSpace(ch) && ch != '\n')
            {
                if (!lastSpace) sb.Append(' ');
                lastSpace = true;
            }
            else
            {
                sb.Append(ch);
                lastSpace = false;
            }
        }
        var cleaned = sb.ToString();
        try { cleaned = Regex.Replace(cleaned, "(?<=\\w)-\\n(?=\\w)", string.Empty); } catch { }
        cleaned = cleaned.Replace("\n", " \n ");
        return cleaned.Trim();
    }
}
