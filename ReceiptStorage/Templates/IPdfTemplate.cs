using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using iText.Kernel.Pdf;

namespace ReceiptStorage.Templates;

public interface IPdfTemplate
{
    ValueTask<ReceiptDetails> TryExtractAsync(PdfDocument pdfDocument, CancellationToken cancellationToken);
}