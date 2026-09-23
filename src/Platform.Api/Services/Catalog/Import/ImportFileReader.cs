using ClosedXML.Excel;
using Microsoft.VisualBasic.FileIO;
using Platform.Api.Common.Exceptions;
using Platform.Shared.Constants;

namespace Platform.Api.Services.Catalog.Import;

/// <summary>
/// One data row of an import file: its row number and each cell by column heading.
/// </summary>
/// <param name="RowNumber">Row number as the user sees it (header is row 1).</param>
/// <param name="Cells">Trimmed cell text by column heading (case-insensitive).</param>
public sealed record ImportFileRow(int RowNumber, IReadOnlyDictionary<string, string> Cells)
{
    /// <summary>
    /// Returns a cell's text, or an empty string when the column is absent.
    /// </summary>
    /// <param name="column">Column heading.</param>
    /// <returns>Trimmed text.</returns>
    public string this[string column] => Cells.TryGetValue(column, out var value) ? value : string.Empty;
}

/// <summary>
/// Reads an uploaded catalogue file — Excel (.xlsx) or CSV — into rows keyed by
/// column heading. Knows nothing about what the columns mean.
/// </summary>
public static class ImportFileReader
{
    /// <summary>
    /// Reads a catalogue file (columns from <see cref="CatalogImportColumns"/>).
    /// </summary>
    /// <param name="content">File content.</param>
    /// <param name="fileName">Original file name; its extension picks the format.</param>
    /// <returns>Non-empty data rows.</returns>
    /// <exception cref="FieldValidationException">Unsupported type, unreadable file, missing columns or too many rows.</exception>
    public static IReadOnlyList<ImportFileRow> Read(Stream content, string fileName) =>
        Read(content, fileName, CatalogImportColumns.All.Where(c => c.Required).Select(c => c.Name).ToList(), CatalogImportColumns.MaxRows);

    /// <summary>
    /// Reads the first worksheet (Excel) or the whole file (CSV).
    /// </summary>
    /// <param name="content">File content.</param>
    /// <param name="fileName">Original file name; its extension picks the format.</param>
    /// <param name="requiredColumns">Headings that must be present.</param>
    /// <param name="maxRows">Most data rows accepted.</param>
    /// <returns>Non-empty data rows.</returns>
    /// <exception cref="FieldValidationException">Unsupported type, unreadable file, missing columns or too many rows.</exception>
    public static IReadOnlyList<ImportFileRow> Read(Stream content, string fileName, IReadOnlyList<string> requiredColumns, int maxRows)
    {
        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        (List<string> headers, List<(int Row, List<string> Values)> rows) = extension switch
        {
            ".xlsx" => ReadExcel(content),
            ".csv" => ReadCsv(content),
            _ => throw new FieldValidationException("File", "Upload an Excel (.xlsx) or CSV (.csv) file."),
        };

        string[] missing = requiredColumns
            .Where(c => !headers.Contains(c, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new FieldValidationException("File", $"Missing column(s): {string.Join(", ", missing)}. Download the template to see the layout.");
        }

        var result = rows
            .Where(r => r.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            .Select(r => new ImportFileRow(
                r.Row,
                headers
                    .Select((header, i) => (header, value: i < r.Values.Count ? r.Values[i].Trim() : string.Empty))
                    .Where(x => !string.IsNullOrWhiteSpace(x.header))
                    .GroupBy(x => x.header, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().value, StringComparer.OrdinalIgnoreCase)))
            .ToList();

        if (result.Count == 0)
        {
            throw new FieldValidationException("File", "The file has no data rows.");
        }

        if (result.Count > maxRows)
        {
            throw new FieldValidationException("File", $"At most {maxRows} rows per file; split it and import in parts.");
        }

        return result;
    }

    /// <summary>
    /// Reads the first worksheet's used range; row 1 is the header.
    /// </summary>
    /// <param name="content">Workbook content.</param>
    /// <returns>Headers and data rows as displayed text.</returns>
    /// <exception cref="FieldValidationException">The workbook cannot be opened.</exception>
    private static (List<string>, List<(int, List<string>)>) ReadExcel(Stream content)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            IXLWorksheet sheet = workbook.Worksheets.First();
            IXLRange? used = sheet.RangeUsed();
            if (used is null)
            {
                return (new List<string>(), new List<(int, List<string>)>());
            }

            int firstColumn = used.FirstColumn().ColumnNumber();
            int lastColumn = used.LastColumn().ColumnNumber();
            int headerRow = used.FirstRow().RowNumber();

            List<string> Values(int row) => Enumerable.Range(firstColumn, lastColumn - firstColumn + 1)
                .Select(col => sheet.Cell(row, col).GetFormattedString().Trim())
                .ToList();

            var rows = Enumerable.Range(headerRow + 1, used.LastRow().RowNumber() - headerRow)
                .Select(row => (row, Values(row)))
                .ToList();
            return (Values(headerRow), rows);
        }
        catch (Exception ex) when (ex is not FieldValidationException)
        {
            throw new FieldValidationException("File", "The Excel file could not be read. Save it as .xlsx and try again.");
        }
    }

    /// <summary>
    /// Reads a comma-separated file with optional quoted fields; row 1 is the header.
    /// </summary>
    /// <param name="content">CSV content (UTF-8).</param>
    /// <returns>Headers and data rows.</returns>
    private static (List<string>, List<(int, List<string>)>) ReadCsv(Stream content)
    {
        using var parser = new TextFieldParser(content, System.Text.Encoding.UTF8)
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = true,
        };
        parser.SetDelimiters(",");

        var headers = parser.EndOfData ? new List<string>() : (parser.ReadFields() ?? Array.Empty<string>()).ToList();
        var rows = new List<(int, List<string>)>();
        int rowNumber = 1;
        while (!parser.EndOfData)
        {
            rowNumber++;
            rows.Add((rowNumber, (parser.ReadFields() ?? Array.Empty<string>()).ToList()));
        }

        return (headers, rows);
    }
}
