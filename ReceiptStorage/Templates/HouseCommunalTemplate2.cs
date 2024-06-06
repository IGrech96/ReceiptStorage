using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;

namespace ReceiptStorage.Templates;

public class HouseCommunalTemplate2 : IPdfTemplate
{
    public async ValueTask<ReceiptDetails> TryExtractAsync(PdfDocument pdfDocument, CancellationToken cancellationToken)
    {
        if (pdfDocument.GetNumberOfPages() != 1) return default;

        var page = pdfDocument.GetPage(1);

        var text = PdfTextExtractor.GetTextFromPage(page);
        if (text is null) return default;
        text = text.Replace('\u00a0', ' ');

        var title = "empty";
        var payerMatch = Regex.Match(text, @"Плательщик (?<name>[\w\s]+)Лицевой");
        var accountMatch = Regex.Match(text, @"Лицевой счет  (?<account>\d+)");

        var addressMatch = Regex.Match(text, @"Адрес помещения: (?<address>.+)");
        var periodMatch = Regex.Match(text, @"Отчетный месяц (?<month>\w+) (?<year>\d{4})");
        var amountMatch = Regex.Match(text, @"К ОПЛАТЕ на \(\d\d.\d\d.\d\d\d\d\) (?<amount>[\d\.]+)");

        if (!payerMatch.Success ||
            !accountMatch.Success ||
            !addressMatch.Success ||
            !periodMatch.Success ||
            !amountMatch.Success)
        {
            return default;
        }

        var monthIndex = TemplateTools.GetMonthNumber(periodMatch.Groups["month"].Value);
        if (monthIndex <= 0)
        {
            return default;
        }

        var address = ("Адрес помещения", TemplateTools.Normilize(addressMatch.Groups["address"].Value));
        var account = ("Лицевой счет", TemplateTools.Normilize(accountMatch.Groups["account"].Value));
        var payer = ("Плательщик", TemplateTools.Normilize(payerMatch.Groups["name"].Value));
        var period = ("Период", TemplateTools.Normilize($"{monthIndex:D2}.{periodMatch.Groups["year"].Value}"));
        var amount = ("Сумма", TemplateTools.Normilize($"{amountMatch.Groups["amount"].Value} BYN"));

        (string, string)[] details =
        [
            address,
            account,
            payer,
            period,
            amount
        ];

        return new ReceiptDetails()
        {
            Title = "Извещение",
            Type = "ЖКХ",
            Details = details,
            Currency = "BYN",
            Amount = double.Parse(amountMatch.Groups["amount"].Value, CultureInfo.InvariantCulture),
            Timestamp = DateTime.Now 
        };
    }
}