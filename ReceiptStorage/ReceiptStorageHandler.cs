using iText.Kernel.Pdf;

namespace ReceiptStorage;

public class ReceiptStorageHandler : IReceiptStorageHandler
{
    private readonly IPdfTemplate[] _pdfTemplates;
    public ReceiptStorageHandler(IEnumerable<IPdfTemplate> pdfTemplates)
    {
        _pdfTemplates = pdfTemplates.ToArray();
    }

    public async Task<ReceiptHandleResponse> Handle(Stream content, string name, CancellationToken cancellationToken)
    {
        if (string.Equals(Path.GetExtension(name), ".pdf", StringComparison.InvariantCultureIgnoreCase))
        {
            return await HandlePdfAsync(content, name, cancellationToken);
        }

        return ReceiptHandleResponse.UnrecognizedFormat();
    }

    private async Task<ReceiptHandleResponse> HandlePdfAsync(Stream content, string name, CancellationToken cancellationToken)
    {
        var reader = new PdfReader(content);
        var pdfDocument = new PdfDocument(reader);

        var extractedInfo = await _pdfTemplates
            .ToAsyncEnumerable()
            .SelectAwait(async template => await template.TryExtractAsync(pdfDocument, cancellationToken))
            .FirstOrDefaultAsync(r => r != default, cancellationToken);


        if (extractedInfo == default)
        {
            return ReceiptHandleResponse.UnrecognizedFormat();
        }

        //TODO save data
        name = $"{extractedInfo.Title} {extractedInfo.Timestamp}";
        return ReceiptHandleResponse.Ok(name, extractedInfo);
    }
}