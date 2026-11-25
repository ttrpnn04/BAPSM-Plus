using System.Globalization;
using System.Text;
using System.Xml;
using BAPStockManagement.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace BAPStockManagement.Services;

internal static class CustomerExcelReader
{
    internal record Result(
        IReadOnlyCollection<Customer> Customers,
        int InvalidRows,
        IReadOnlyList<int> InvalidRowExamples,
        int DuplicateRows,
        IReadOnlyCollection<int> ConflictingCodes);

    public static Result Read(Stream stream, CancellationToken cancellationToken)
    {
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbook = document.WorkbookPart ?? throw new InvalidDataException("ไม่พบข้อมูลสมุดงาน");
        var sheets = workbook.Workbook.Sheets?.Elements<Sheet>()
            .Where(sheet => sheet.Id?.Value is string id && workbook.GetPartById(id) is WorksheetPart)
            .ToList();
        var sheet = sheets?.FirstOrDefault(sheet =>
            string.Equals(sheet.Name?.Value, "customer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sheet.Name?.Value, "customer_BAP", StringComparison.OrdinalIgnoreCase))
            ?? sheets?.FirstOrDefault()
            ?? throw new InvalidDataException("ไม่พบชีตข้อมูลลูกค้า");
        var worksheet = (WorksheetPart)workbook.GetPartById(sheet.Id!.Value!);
        var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable
            .Elements<SharedStringItem>().Select(item => item.InnerText).ToArray() ?? [];

        using var worksheetStream = worksheet.GetStream();
        using var reader = XmlReader.Create(worksheetStream);
        var entries = new Dictionary<int, (Customer Customer, int RowCount)>();
        var conflicts = new HashSet<int>();
        var invalidRowExamples = new List<int>();
        var invalidRows = 0;
        var rowNumber = 0;
        var values = new string?[12];
        var hasCustomerData = false;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "row")
            {
                cancellationToken.ThrowIfCancellationRequested();
                rowNumber = int.TryParse(reader.GetAttribute("r"), out var index) ? index : rowNumber + 1;
            }
            else if (rowNumber >= 3 && reader.NodeType == XmlNodeType.Element &&
                     reader.LocalName == "c" && !reader.IsEmptyElement)
            {
                var reference = reader.GetAttribute("r");
                if (reference is null || reference.Length < 2 ||
                    !char.IsAsciiDigit(reference[1]) || reference[0] is < 'A' or > 'L' or 'B')
                {
                    continue;
                }

                var value = ReadCellText(reader, sharedStrings, reference[0] == 'A');
                values[reference[0] - 'A'] = value;
                hasCustomerData |= value != null;
            }
            else if (hasCustomerData && reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "row")
            {
                var customer = ReadCustomer(values);
                Array.Clear(values);
                hasCustomerData = false;
                if (customer == null)
                {
                    invalidRows++;
                    if (invalidRowExamples.Count < 12)
                    {
                        invalidRowExamples.Add(rowNumber);
                    }
                    continue;
                }

                if (conflicts.Contains(customer.CustomerCode))
                {
                    continue;
                }

                if (entries.TryGetValue(customer.CustomerCode, out var previous))
                {
                    if (!string.Equals(previous.Customer.Name, customer.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        conflicts.Add(customer.CustomerCode);
                        entries.Remove(customer.CustomerCode);
                        continue;
                    }
                }

                entries[customer.CustomerCode] = (customer, previous.RowCount + 1);
            }
        }

        return new Result(
            entries.Values.Select(entry => entry.Customer).ToList(),
            invalidRows,
            invalidRowExamples,
            entries.Values.Sum(entry => entry.RowCount - 1),
            conflicts);
    }

    private static string? ReadCellText(XmlReader reader, string[] sharedStrings, bool isCustomerCode)
    {
        var type = reader.GetAttribute("t");
        if (type == "e")
        {
            return null;
        }

        using var cell = reader.ReadSubtree();
        if (type == "inlineStr")
        {
            var inlineText = new StringBuilder();
            while (cell.Read())
            {
                if (cell.NodeType == XmlNodeType.Element && cell.LocalName == "t")
                {
                    inlineText.Append(cell.ReadString());
                }
            }

            return TrimToNull(inlineText.ToString());
        }

        if (!cell.ReadToDescendant("v", reader.NamespaceURI))
        {
            return null;
        }

        string? value = cell.ReadString();
        if (type == "s")
        {
            value = int.TryParse(value, out var index) && index >= 0 && index < sharedStrings.Length
                ? sharedStrings[index]
                : null;
        }
        else if (isCustomerCode && type is null or "n" &&
                 decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) &&
                 number is >= int.MinValue and <= int.MaxValue && decimal.Truncate(number) == number)
        {
            return ((int)number).ToString(CultureInfo.InvariantCulture);
        }

        return TrimToNull(value);
    }

    private static Customer? ReadCustomer(string?[] values)
    {
        var name = values[2];
        if (name == null || !int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
        {
            return null;
        }

        decimal.TryParse(values[11], NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out var creditLimit);

        return new Customer
        {
            CustomerCode = code,
            Name = Limit(name, 300)!,
            Address = Limit(values[3], 500),
            District = Limit(values[4], 100),
            Province = Limit(values[5], 100),
            Phone1 = Limit(values[6], 50),
            Phone2 = Limit(values[7], 50),
            SalesZone = Limit(values[8], 50),
            TaxId = Limit(TrimToNull(values[9]?.TrimStart('\'')), 50),
            ShippingInfo = Limit(values[10], 500),
            CreditLimit = creditLimit
        };
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Limit(string? value, int length) =>
        value != null && value.Length > length ? value[..length] : value;
}
