using iText.Kernel.Pdf;
using Microsoft.Extensions.Logging;
using ReceiptStorage.Tags;
using ReceiptStorage.Templates;

namespace ReceiptStorage;

public class ReceiptParser : IReceiptParser
{
    private readonly ITagResolver _tagResolver;
    private readonly ILogger<IReceiptParser> _logger;
    private readonly IPdfTemplate[] _pdfTemplates;
    public ReceiptParser(
        IEnumerable<IPdfTemplate> pdfTemplates,
        ITagResolver tagResolver,
        ILogger<IReceiptParser> logger)
    {
        _tagResolver = tagResolver;
        _logger = logger;
        _pdfTemplates = pdfTemplates.ToArray();
    }

    public async Task<ReceiptParserResponse> Parse(Content content,
        CancellationToken cancellationToken)
    {
        try
        {
            var extension = Path.GetExtension(content.Name);
            ReceiptDetails? details = null;
            if (string.Equals(extension, ".pdf", StringComparison.InvariantCultureIgnoreCase))
            {
                details = await ExtractPdfAsync(content, cancellationToken);
            }

            if (details == null || details.Value == default)
            {
                return ReceiptParserResponse.UnrecognizedFormat();
            }

            var tags = await _tagResolver.ResolveTagsAsync(details.Value, cancellationToken);

            details = details.Value with
            {
                Tags = tags
            };

            var detailsName = $"{details.Value.Title} {details.Value.Timestamp}";
            var fileName = detailsName + extension;

            content = content with{ Name = fileName};

            return ReceiptParserResponse.Ok(detailsName, details.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Can not handle: '{content.Name}'");
            return ReceiptParserResponse.UnknowError();
        }
    }

    private async Task<ReceiptDetails> ExtractPdfAsync(Content content, CancellationToken cancellationToken)
    {
        var reader = new PdfReader(content.GetStream());
        var pdfDocument = new PdfDocument(reader);

        var extractedInfo = await _pdfTemplates
            .ToAsyncEnumerable()
            .SelectAwait(async template => await template.TryExtractAsync(pdfDocument, cancellationToken))
            .FirstOrDefaultAsync(r => r != default, cancellationToken);

        return extractedInfo;
    }
}