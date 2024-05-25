using iText.Kernel.Pdf;
using Microsoft.Extensions.Logging;

namespace ReceiptStorage;

public class ReceiptStorageHandler : IReceiptStorageHandler
{
    private readonly IReceiptStorage[] _storages;
    private readonly ILogger<IReceiptStorageHandler> _logger;
    private readonly IPdfTemplate[] _pdfTemplates;
    public ReceiptStorageHandler(
        IEnumerable<IReceiptStorage> storages,
        IEnumerable<IPdfTemplate> pdfTemplates,
        ILogger<IReceiptStorageHandler> logger)
    {
        _storages = storages.ToArray();
        _logger = logger;
        _pdfTemplates = pdfTemplates.ToArray();
    }

    public async Task<ReceiptHandleResponse> Handle(Content content, IUser user,
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

            if (details == null)
            {
                return ReceiptHandleResponse.UnrecognizedFormat();
            }

            var detailsName = $"{details.Value.Title} {details.Value.Timestamp}";
            var fileName = detailsName + extension;

            content = content.WithName(fileName);

            foreach (var receiptStorage in _storages)
            {
                await receiptStorage.SaveAsync(content, details.Value, user, cancellationToken);
            }

            return ReceiptHandleResponse.Ok(detailsName, details.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Can not handle: '{content.Name}'");
            return ReceiptHandleResponse.UnknowError();
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