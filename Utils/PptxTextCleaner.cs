using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace mindvault.Utils
{
    /// <summary>
    /// Extracts clean plain text from PPTX slides by reading OOXML and keeping only a:t text runs.
    /// Avoids importing DrawingML styling, backgrounds, gradients, effects, etc.
    /// </summary>
    public static class PptxTextCleaner
    {
        // Regex to capture text inside <a:t>...</a:t> (handles namespaces and entities)
        private static readonly Regex TextRunRegex = new Regex(
            "<a:t>(.*?)</a:t>", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

        /// <summary>
        /// Extracts clean text from all slides in a PPTX file.
        /// </summary>
        /// <param name="pptxPath">Path to .pptx file</param>
        /// <returns>Plain text with paragraphs separated by blank lines</returns>
        public static string ExtractTextClean(string pptxPath)
        {
            if (string.IsNullOrWhiteSpace(pptxPath) || !File.Exists(pptxPath))
                throw new FileNotFoundException("PPTX file not found", pptxPath);

            using var fs = File.OpenRead(pptxPath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read, leaveOpen: false);

            // Collect slide XML entries (ppt/slides/slideN.xml), ordered by N
            var slides = zip.Entries
                .Where(e => e.FullName.StartsWith("ppt/slides/slide", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => ParseSlideNumber(e.FullName))
                .ToList();

            var sb = new StringBuilder(4096);
            for (int i = 0; i < slides.Count; i++)
            {
                var slide = slides[i];
                using var sr = new StreamReader(slide.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var xml = sr.ReadToEnd();

                // Extract text runs only
                var matches = TextRunRegex.Matches(xml);
                bool wroteLine = false;
                foreach (Match m in matches)
                {
                    var raw = m.Groups[1].Value;
                    var text = DecodeXmlEntities(raw);
                    text = NormalizeWhitespace(text);
                    if (text.Length == 0) continue;

                    // Append with spaces within a paragraph
                    if (wroteLine) sb.Append(' ');
                    sb.Append(text);
                    wroteLine = true;
                }

                // Separate slides with blank line
                if (wroteLine)
                    sb.AppendLine().AppendLine();
            }

            return sb.ToString().Trim();
        }

        private static int ParseSlideNumber(string fullName)
        {
            // e.g., ppt/slides/slide12.xml -> 12
            var name = Path.GetFileNameWithoutExtension(fullName);
            var digits = new string(name.Where(char.IsDigit).ToArray());
            return int.TryParse(digits, out var n) ? n : int.MaxValue;
        }

        private static string DecodeXmlEntities(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            // Basic entity decoding
            return s.Replace("&lt;", "<")
                    .Replace("&gt;", ">")
                    .Replace("&amp;", "&")
                    .Replace("&quot;", "\"")
                    .Replace("&apos;", "'");
        }

        private static string NormalizeWhitespace(string s)
        {
            // Collapse multiple spaces/newlines, trim
            var normalized = Regex.Replace(s, @"\s+", " ");
            return normalized.Trim();
        }
    }
}
