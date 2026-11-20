using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace StudentManagementSystem.Reporting
{
    /// <summary>
    /// Writes a single-sheet .xlsx (SpreadsheetML inside an Open Packaging zip)
    /// using only System.IO.Compression - no third-party library, so it works on
    /// the mandated .NET Framework stack.
    /// </summary>
    public static class XlsxWriter
    {
        public static void Write(DataTable table, string sheetName, Stream output)
        {
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                AddEntry(zip, "[Content_Types].xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                    "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                    "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                    "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                    "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                    "</Types>");

                AddEntry(zip, "_rels/.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                    "</Relationships>");

                AddEntry(zip, "xl/workbook.xml",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
                    "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                    "<sheets><sheet name=\"" + Xml(SafeSheet(sheetName)) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");

                AddEntry(zip, "xl/_rels/workbook.xml.rels",
                    "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                    "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                    "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                    "</Relationships>");

                AddEntry(zip, "xl/worksheets/sheet1.xml", SheetXml(table));
            }
        }

        private static string SheetXml(DataTable table)
        {
            var sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");

            sb.Append("<row r=\"1\">");
            for (int c = 0; c < table.Columns.Count; c++)
                sb.Append(Cell(1, c, table.Columns[c].ColumnName, false));
            sb.Append("</row>");

            for (int r = 0; r < table.Rows.Count; r++)
            {
                int rowNumber = r + 2;
                sb.Append("<row r=\"").Append(rowNumber).Append("\">");
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    object value = table.Rows[r][c];
                    if (value == null || value == DBNull.Value) continue;
                    bool numeric = value is int || value is long || value is decimal || value is double || value is float;
                    sb.Append(Cell(rowNumber, c, value, numeric));
                }
                sb.Append("</row>");
            }

            sb.Append("</sheetData></worksheet>");
            return sb.ToString();
        }

        private static string Cell(int row, int column, string text, bool numeric)
        {
            return Cell(row, column, (object)text, numeric);
        }

        private static string Cell(int row, int column, object value, bool numeric)
        {
            string reference = ColumnName(column) + row.ToString(CultureInfo.InvariantCulture);
            if (numeric)
            {
                string number = Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                return "<c r=\"" + reference + "\"><v>" + number + "</v></c>";
            }
            return "<c r=\"" + reference + "\" t=\"inlineStr\"><is><t xml:space=\"preserve\">" +
                   Xml(Convert.ToString(value, CultureInfo.InvariantCulture)) + "</t></is></c>";
        }

        private static string ColumnName(int index)
        {
            string name = "";
            int n = index;
            do
            {
                name = (char)('A' + (n % 26)) + name;
                n = n / 26 - 1;
            } while (n >= 0);
            return name;
        }

        private static string SafeSheet(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Data";
            var sb = new StringBuilder();
            foreach (char c in name)
                sb.Append(":\\/?*[]".IndexOf(c) >= 0 || c < 32 ? '_' : c);
            string result = sb.ToString();
            return result.Length > 31 ? result.Substring(0, 31) : result;
        }

        private static string Xml(string value)
        {
            if (value == null) return "";
            return value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                        .Replace("\"", "&quot;").Replace("'", "&apos;")
                        .Replace("\r", "").Replace("\n", " ");
        }

        private static void AddEntry(ZipArchive zip, string path, string content)
        {
            var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write(content);
        }
    }
}
