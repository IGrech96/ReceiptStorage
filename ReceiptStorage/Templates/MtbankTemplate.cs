using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace ReceiptStorage.Templates;

public class MtbankTemplate : IPdfTemplate
{
    public async ValueTask<ReceiptDetails> TryExtractAsync(PdfDocument pdfDocument, CancellationToken cancellationToken)
    {
        if (pdfDocument.GetNumberOfPages() != 1) return default;

        var page = pdfDocument.GetPage(1);

        var text = PdfTextExtractor.GetTextFromPage(page);
        if (text is null) return default;
        text = text.Replace('\u00a0', ' ');
        using var stringReader = new StringReader(text);

        var title = await stringReader.ReadLineAsync(cancellationToken);
        var status = await stringReader.ReadLineAsync(cancellationToken);
        var amount = await stringReader.ReadLineAsync(cancellationToken);
        var dateTime = await stringReader.ReadLineAsync(cancellationToken);

        if (title is null ||
            status is null ||
            amount is null ||
            dateTime is null)
        {
            return default;
        }

        var statusMatch = Regex.Match(status, @"Статус  операции: (\w+)");
        var amountMatch = Regex.Match(amount, @"Сумма  операции: (?<amount>[\d\\.\\,]+) (?<ccy>\w+)");
        var dateTimeMatch = Regex.Match(dateTime, @"Дата  и  время  проведения  операции: (.+)");

        if (!statusMatch.Success ||
            !amountMatch.Success ||
            !dateTimeMatch.Success)
        {
            return default;
        }

        while ( await stringReader.ReadLineAsync(cancellationToken) is { } title2 && title2 != title)
        {
            //Skip all lines untile we met title one more time
        }

        //Some properties could be multilne so collect them into string builder
        var propertiesBuilder = new StringBuilder();
        while (await stringReader.ReadLineAsync(cancellationToken) is {} line && !line.StartsWith("N операции в ЕРИП:"))
        {
            propertiesBuilder.AppendLine(line);
        }

        var propertiesMatches = Regex.Matches(propertiesBuilder.ToString(), @"^(?<name>[^:]+):(?<data>[^:]+)$", RegexOptions.Multiline);

        var properties = propertiesMatches
            .Select(p => (p.Groups["name"].Value.Trim(), p.Groups["data"].Value.Trim()))
            .Where(p => !string.IsNullOrWhiteSpace(p.Item1) && !string.IsNullOrWhiteSpace(p.Item2))
            .ToArray();

        return new ReceiptDetails()
        {
            Title = title,
            Type = "MTB",
            Details = properties,
            Currency = amountMatch.Groups["ccy"].Value,
            Amount = double.Parse(amountMatch.Groups["amount"].Value, CultureInfo.InvariantCulture),
            Timestamp = DateTime.ParseExact(dateTimeMatch.Groups[1].Value, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)
        };
    }
}