using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace ReceiptStorage.Templates;

public class HouseCommunalTemplate : IPdfTemplate
{
    public async ValueTask<ReceiptDetails> TryExtractAsync(PdfDocument pdfDocument, CancellationToken cancellationToken)
    {
        if (pdfDocument.GetNumberOfPages() != 1) return default;

        var page = pdfDocument.GetPage(1);

        var text = PdfTextExtractor.GetTextFromPage(page);
        if (text is null) return default;
        text = text.Replace('\u00a0', ' ');
        using var stringReader = new StringReader(text);

        var titleBuilder = new StringBuilder();

        //Some properties could be multilne so collect them into string builder
        string? totalAmountLine = null;
        var propertiesBuilder = new StringBuilder();
        bool isTitle = true;
        while (await stringReader.ReadLineAsync(cancellationToken) is {} line)
        {
            if (line.StartsWith("Всего начислено"))
            {
                totalAmountLine = line;
                break;
            }

            if (line.Contains(":"))
            {
                isTitle = false;
            }


            var builder = isTitle ? titleBuilder : propertiesBuilder;
            builder.AppendLine(line);
        }

        if (string.IsNullOrWhiteSpace(totalAmountLine))
        {
            return default;
        }

        var propertiesText = propertiesBuilder.ToString();

        var addressMatch = Regex.Match(propertiesText, @"Адрес помещения : (?<address>.+)Лицевой", RegexOptions.Singleline);
        if (!addressMatch.Success)
        {
            return default;
        }

        var address = ("Адрес помещения", TemplateTools.Normilize(addressMatch.Groups["address"].Value.Replace("» оплат", " ").Replace("района","")));

        var accountMatch = Regex.Match(propertiesText, @"Лицевой счет : (?<account>\d+)");
         

        var  account = ("Лицевой счет", accountMatch.Groups["account"].Value);

        //от 13.05.2024 10:48:35
        var dateTimeMatch = Regex.Match(propertiesText, @"от (?<timestamp>\d\d\.\d\d\.\d\d\d\d \d\d:\d\d:\d\d)");
        if (!dateTimeMatch.Success)
        {
            return default;
        }

        //Всего начислено 212.73 0.00 0.00 212.73
        var amountMatch = Regex.Match(totalAmountLine, @"Всего начислено (?<amount>[\d\.]+)");

        if (!amountMatch.Success)
        {
            return default;
        }

        var title = TemplateTools.Normilize(titleBuilder.ToString());
        if (string.IsNullOrWhiteSpace(title))
        {
            return default;
        }

        //ИЗВЕЩЕНИЕ за апрель 2024 года
        var periodMatch = Regex.Match(title, @"ИЗВЕЩЕНИЕ за (?<month>\w+) (?<year>\d{4})");
        if (!periodMatch.Success)
        {
            return default;
        }



        var monthIndex = TemplateTools.GetMonthNumber(periodMatch.Groups["month"].Value);
        if (monthIndex <= 0)
        {
            return default;
        }

        var period = $"{monthIndex:D2}.{periodMatch.Groups["year"].Value}";

        return new ReceiptDetails()
        {
            Title = "Извещение",
            Type = "ЖКХ",
            Details = [address, account, ("Период", period), ("Сумма", $"{amountMatch.Groups["amount"].Value} BYN")],
            Currency = "BYN",
            Amount = double.Parse(amountMatch.Groups["amount"].Value, CultureInfo.InvariantCulture),
            Timestamp = DateTime.ParseExact(dateTimeMatch.Groups["timestamp"].Value, "dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture)
        };
    }
}