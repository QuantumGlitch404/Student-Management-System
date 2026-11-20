using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace StudentManagementSystem.Reporting
{
    /// <summary>
    /// Minimal, dependency-free PDF 1.4 writer (Helvetica + Helvetica-Bold).
    /// Enough for receipts and tabular exports: text lines, rules and pagination.
    /// Coordinates are given top-left (like a web page) and converted internally.
    /// Non-ASCII characters are replaced with '?' because the base-14 fonts use
    /// WinAnsiEncoding and the file is written as ASCII.
    /// </summary>
    public class SimplePdf
    {
        public const float PageWidth = 595f;   // A4 portrait, points
        public const float PageHeight = 842f;
        public const float MarginLeft = 40f;

        /// <summary>Instance page size; a landscape report uses 842 x 595.</summary>
        public float Width = PageWidth;
        public float Height = PageHeight;

        private readonly List<StringBuilder> _pages = new List<StringBuilder>();
        private StringBuilder _current;

        public SimplePdf() { NewPage(); }

        public SimplePdf(bool landscape)
        {
            if (landscape) { Width = PageHeight; Height = PageWidth; }
            NewPage();
        }

        public int PageCount { get { return _pages.Count; } }
        public int CurrentPage { get; private set; }

        public void NewPage()
        {
            _current = new StringBuilder();
            _pages.Add(_current);
            CurrentPage = _pages.Count;
        }

        /// <summary>Filled or stroked rectangle, coordinates top-left.</summary>
        public void Rect(float x, float y, float w, float h, bool fill, float gray)
        {
            _current.Append(N(gray)).Append(fill ? " g\n" : " G\n");
            _current.Append(N(x)).Append(' ').Append(N(Height - y - h)).Append(' ')
                    .Append(N(w)).Append(' ').Append(N(h)).Append(" re ")
                    .Append(fill ? "f" : "S").Append('\n');
        }

        public void LineWidth(float width)
        {
            _current.Append(N(width)).Append(" w\n");
        }

        public void Gray(float gray)
        {
            _current.Append(N(gray)).Append(" g ").Append(N(gray)).Append(" G\n");
        }

        /// <summary>
        /// Stamps a rule and footer on every page after the body has been built,
        /// so "Page n of N" is correct. Called once by PdfReport before WriteTo.
        /// </summary>
        public void AppendFooters(string left, string rightFormat, float marginLeft, float size)
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                var target = _pages[i];
                var previous = _current;
                _current = target;
                Rule(marginLeft, Height - 34, Width - marginLeft, Height - 34);
                Text(marginLeft, Height - 20, size, false, left);
                string right = string.Format(CultureInfo.InvariantCulture, rightFormat, i + 1, _pages.Count);
                float x = Width - marginLeft - right.Length * size * 0.5f;
                Text(x, Height - 20, size, false, right);
                _current = previous;
            }
        }

        public void Text(float x, float y, float size, bool bold, string text)
        {
            _current.Append("BT /").Append(bold ? "F2" : "F1").Append(' ')
                    .Append(N(size)).Append(" Tf ")
                    .Append(N(x)).Append(' ').Append(N(Height - y)).Append(" Td (")
                    .Append(Escape(text)).Append(") Tj ET\n");
        }

        public void Rule(float x1, float y1, float x2, float y2)
        {
            _current.Append(N(x1)).Append(' ').Append(N(Height - y1)).Append(" m ")
                    .Append(N(x2)).Append(' ').Append(N(Height - y2)).Append(" l S\n");
        }

        public static string Fit(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Length <= maxChars) return text;
            return maxChars <= 3 ? text.Substring(0, maxChars) : text.Substring(0, maxChars - 3) + "...";
        }

        public void WriteTo(Stream output)
        {
            var body = new StringBuilder();
            var offsets = new List<int>();  // offsets[i] = byte offset of object (i + 1)

            body.Append("%PDF-1.4\n");

            float w = Width, h = Height;
            AppendObject(body, offsets, "<< /Type /Catalog /Pages 2 0 R >>");
            int[] kids = new int[_pages.Count];
            for (int i = 0; i < _pages.Count; i++) kids[i] = 5 + i * 2;
            var kidList = new StringBuilder();
            foreach (int k in kids) kidList.Append(k).Append(" 0 R ");
            AppendObject(body, offsets, "<< /Type /Pages /Kids [" + kidList.ToString().Trim() + "] /Count " + _pages.Count + " >>");
            AppendObject(body, offsets, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
            AppendObject(body, offsets, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

            for (int i = 0; i < _pages.Count; i++)
            {
                // Every object gets its number from the running count, so page and
                // content objects stay in step regardless of how many pages there are.
                int pageObj = offsets.Count + 1;
                int contentObj = pageObj + 1;
                AppendObject(body, offsets,
                    "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + N(w) + " " + N(h) + "] " +
                    "/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents " + contentObj + " 0 R >>");

                // Content streams are objects too; they must appear in the xref table.
                AppendStreamObject(body, offsets, _pages[i].ToString());
            }

            int xrefPos = Encoding.ASCII.GetByteCount(body.ToString());
            body.Append("xref\n0 ").Append(offsets.Count + 1).Append('\n');
            body.Append("0000000000 65535 f \n");
            foreach (int off in offsets) body.Append(off.ToString("D10")).Append(" 00000 n \n");
            body.Append("trailer\n<< /Size ").Append(offsets.Count + 1).Append(" /Root 1 0 R >>\nstartxref\n")
                .Append(xrefPos).Append("\n%%EOF\n");

            byte[] bytes = Encoding.ASCII.GetBytes(body.ToString());
            output.Write(bytes, 0, bytes.Length);
        }

        private static void AppendObject(StringBuilder body, List<int> offsets, string content)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(body.ToString()));
            body.Append(offsets.Count).Append(" 0 obj\n").Append(content).Append("\nendobj\n");
        }

        private static void AppendStreamObject(StringBuilder body, List<int> offsets, string content)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(body.ToString()));
            body.Append(offsets.Count).Append(" 0 obj\n<< /Length ")
                .Append(Encoding.ASCII.GetByteCount(content)).Append(" >>\nstream\n");
            body.Append(content);
            body.Append("endstream\nendobj\n");
        }

        private static string N(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Escape(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("\u20b9", "INR").Replace("\u00a0", " ").Replace("\u2026", "...");
            text = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(text.Length + 8);
            foreach (char c in text)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                char ch = c;
                // WinAnsiEncoding supports Latin-1; map common typographic marks.
                if (ch == '\u2018' || ch == '\u2019') ch = '\'';
                else if (ch == '\u201c' || ch == '\u201d') ch = '"';
                else if (ch == '\u2013' || ch == '\u2014') ch = '-';
                else if (ch == '\u20b9') ch = 'R'; // INR sign is not in WinAnsi
                if (ch < 32) ch = ' ';
                if (ch == '(' || ch == ')' || ch == '\\') sb.Append('\\').Append(ch);
                else if (ch <= 126) sb.Append(ch);
                else if (ch <= 255) sb.Append('\\').Append(Convert.ToString(ch, 8).PadLeft(3, '0'));
                else sb.Append('?');
            }
            return sb.ToString();
        }
    }
}
