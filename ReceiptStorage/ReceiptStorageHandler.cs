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

    public async Task<ReceiptHandleResponse> Handle(Stream content, string name, CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(Path.GetExtension(name), ".pdf", StringComparison.InvariantCultureIgnoreCase))
            {
                return await HandlePdfAsync(content, name, cancellationToken);
            }

            return ReceiptHandleResponse.UnrecognizedFormat();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Can not handle: '{name}'");
            return ReceiptHandleResponse.UnknowError();
        }
    }

    private async Task<ReceiptHandleResponse> HandlePdfAsync(Stream content, string name, CancellationToken cancellationToken)
    {
        var position = content.Position;
        var reader = new PdfReader(content);
        var pdfDocument = new PdfDocument(reader);

        var extractedInfo = await _pdfTemplates
            .ToAsyncEnumerable()
            .SelectAwait(async template => await template.TryExtractAsync(pdfDocument, cancellationToken))
            .FirstOrDefaultAsync(r => r != default, cancellationToken);

        if (content.Position != position && content.CanSeek)
        {
            content.Position = position;
        }

        if (extractedInfo == default)
        {
            return ReceiptHandleResponse.UnrecognizedFormat();
        }

        name = $"{extractedInfo.Title} {extractedInfo.Timestamp}";

        foreach (var receiptStorage in _storages)
        {
            if (content.Position != position && content.CanSeek)
            {
                content.Position = position;
            }
            await receiptStorage.SaveAsync(content, extractedInfo, name + ".pdf", cancellationToken);
        }

        return ReceiptHandleResponse.Ok(name, extractedInfo);
    }
}